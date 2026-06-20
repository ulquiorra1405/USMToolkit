using System.Management;
using LibreHardwareMonitor.Hardware;

namespace Toolkit.Services;

public class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer;

    public HardwareMonitorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = false,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsNetworkEnabled = false,
            IsStorageEnabled = true,
            IsPsuEnabled = false
        };
        _computer.Open();
    }

    public string GetCpuTemperatures()
    {
        var libres = GetLibreCpuTemps();
        if (libres != null) return libres;

        try
        {
            using var mos = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            var vals = mos.Get().Cast<ManagementObject>()
                .Select(m => (uint)m["CurrentTemperature"])
                .Where(v => v < 0x80000000)
                .Select(v => (v / 10.0) - 273.15)
                .ToList();
            if (vals.Count > 0)
                return string.Join("\n", vals.Select(v => $"{v:F0}°C"));
        }
        catch { }

        return "N/A";
    }

    public string GetGpuTemperatures()
    {
        var libres = GetLibreGpuTemps();
        if (libres != null) return libres;

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=temperature.gpu,name --format=csv,noheader",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0)
                    return string.Join("\n", lines.Select(l =>
                    {
                        var parts = l.Split(',');
                        return parts.Length == 2 ? $"{parts[1].Trim()}: {parts[0].Trim()}°C" : l;
                    }));
            }
        }
        catch { }

        return "N/A";
    }

    public string GetFanSpeeds()
    {
        try
        {
            var parts = new List<string>();

            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                var fans = hw.Sensors
                    .Where(s => s.SensorType == SensorType.Fan && s.Value.HasValue)
                    .Select(s => $"{s.Name}: {s.Value:F0} RPM")
                    .ToList();

                if (fans.Count > 0)
                {
                    var label = hw.Name.Length > 25 ? hw.Name[..25] + "…" : hw.Name;
                    parts.Add($"{label}\n{string.Join("  ", fans)}");
                }
            }

            if (parts.Count > 0) return string.Join("\n\n", parts);
        }
        catch { }

        // Fallback: WMI Win32_Fan
        try
        {
            using var mos = new ManagementObjectSearcher("SELECT * FROM Win32_Fan");
            var fans = mos.Get().Cast<ManagementObject>().ToList();
            if (fans.Count > 0)
            {
                var lines = fans.Select(f =>
                {
                    var name = f["Name"]?.ToString() ?? "";
                    var speed = f["DesiredSpeed"]?.ToString();
                    return string.IsNullOrEmpty(speed) ? $"{name}: N/A" : $"{name}: {speed} RPM";
                });
                return string.Join("\n", lines);
            }
        }
        catch { }

        return "N/A";
    }

    public string GetDiskTemperatures()
    {
        try
        {
            var parts = new List<string>();

            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                if (hw.HardwareType != HardwareType.Storage) continue;

                var temps = hw.Sensors
                    .Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)
                    .Select(s => $"{s.Value:F0}°C")
                    .ToList();

                if (temps.Count > 0)
                    parts.Add(string.Join("  ", temps));
            }

            if (parts.Count > 0) return string.Join("\n\n", parts);
        }
        catch { }
        return "N/A";
    }

    public List<IHardware> GetStorageHardware()
    {
        try
        {
            foreach (var hw in _computer.Hardware)
                hw.Update();
            return _computer.Hardware.Where(h => h.HardwareType == HardwareType.Storage).ToList();
        }
        catch { return []; }
    }

    private string? GetLibreCpuTemps()
    {
        try
        {
            var cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
            if (cpu == null) return null;

            cpu.Update();
            var temps = cpu.Sensors
                .Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)
                .Select(s => $"{s.Name}: {s.Value:F0}°C")
                .ToList();

            if (temps.Count > 0) return string.Join("\n", temps);
        }
        catch { }
        return null;
    }

    private string? GetLibreGpuTemps()
    {
        try
        {
            var gpus = _computer.Hardware
                .Where(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
                .ToList();

            if (gpus.Count == 0) return null;

            var parts = gpus.Select(gpu =>
            {
                gpu.Update();
                var temps = gpu.Sensors
                    .Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)
                    .Select(s => $"{s.Name}: {s.Value:F0}°C");
                var label = gpu.Name.Length > 30 ? gpu.Name[..30] + "…" : gpu.Name;
                return $"{label}\n{string.Join("  ", temps)}";
            });

            var result = string.Join("\n\n", parts);
            if (!string.IsNullOrEmpty(result)) return result;
        }
        catch { }
        return null;
    }

    public void Dispose()
    {
        _computer.Close();
    }
}