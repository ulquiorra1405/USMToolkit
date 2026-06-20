using System.IO;
using System.IO.Compression;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text.Json;

namespace Toolkit.Services;

public class SystemInfoService
{
    public string GetOsVersion()
    {
        try
        {
            using var mos = new ManagementObjectSearcher("SELECT Caption,Version,BuildNumber FROM Win32_OperatingSystem");
            var mo = mos.Get().Cast<ManagementObject>().FirstOrDefault();
            if (mo == null) return "Desconocido";
            var caption = mo["Caption"]?.ToString()?.Trim() ?? "";
            var ver = mo["Version"]?.ToString() ?? "";
            var build = mo["BuildNumber"]?.ToString() ?? "";
            return $"{caption}\nVersión {ver} (Build {build})";
        }
        catch { return Environment.OSVersion.ToString(); }
    }

    public string GetOsInstallDate()
    {
        try
        {
            using var mos = new ManagementObjectSearcher("SELECT InstallDate FROM Win32_OperatingSystem");
            var mo = mos.Get().Cast<ManagementObject>().FirstOrDefault();
            var dateStr = mo?["InstallDate"]?.ToString();
            if (dateStr != null && dateStr.Length >= 14
                && DateTime.TryParse($"{dateStr[..4]}-{dateStr[4..6]}-{dateStr[6..8]} {dateStr[8..10]}:{dateStr[10..12]}:{dateStr[12..14]}", out var dt))
                return $"Instalado: {dt:dd/MM/yyyy}";
            return "N/A";
        }
        catch { return "N/A"; }
    }

    public string GetUptime()
    {
        try
        {
            using var mos = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
            var boot = mos.Get().Cast<ManagementObject>().FirstOrDefault()?["LastBootUpTime"]?.ToString();
            if (boot != null && boot.Length >= 14
                && DateTime.TryParse($"{boot[..4]}-{boot[4..6]}-{boot[6..8]} {boot[8..10]}:{boot[10..12]}:{boot[12..14]}", out var bootTime))
            {
                var span = DateTime.Now - bootTime;
                return $"{span.Days}d {span.Hours}h {span.Minutes}m";
            }
        }
        catch { }
        var t = TimeSpan.FromMilliseconds(Environment.TickCount64);
        return $"{(int)t.TotalDays}d {t.Hours}h {t.Minutes}m";
    }

    public int GetLogicalProcessorCount() => Environment.ProcessorCount;

    public (string Label, double UsedPercent) GetMemoryUsage()
    {
        try
        {
            using var mos = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem");
            var mo = mos.Get().Cast<ManagementObject>().FirstOrDefault();
            if (mo != null)
            {
                var total = Convert.ToUInt64(mo["TotalVisibleMemorySize"]);
                var free = Convert.ToUInt64(mo["FreePhysicalMemory"]);
                var used = total - free;
                var usedPct = (double)used / total * 100;
                var totalGb = total * 1024.0 / 1_073_741_824;
                var usedGb = used * 1024.0 / 1_073_741_824;
                return ($"{usedPct:F1}% ({usedGb:F1} / {totalGb:F1} GB)", usedPct);
            }
        }
        catch { }
        return ("N/A", 0);
    }

    public string GetDiskInfo()
    {
        try
        {
            var parts = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d =>
                {
                    var pct = (double)(d.TotalSize - d.TotalFreeSpace) / d.TotalSize * 100;
                    var totalGb = d.TotalSize / 1_073_741_824.0;
                    var freeGb = d.TotalFreeSpace / 1_073_741_824.0;
                    return $"{d.Name.TrimEnd('\\')} {pct:F0}% ({freeGb:F0}/{totalGb:F0} GB)";
                });
            return string.Join("\n", parts);
        }
        catch { return "N/A"; }
    }

    public (string HostName, string IpAddress) GetNetworkInfo()
    {
        try
        {
            var host = Dns.GetHostName();
            var ip = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address))
                ?.Address.ToString();
            return (host, ip ?? "N/A");
        }
        catch { return (Environment.MachineName, "N/A"); }
    }

    private (string Label, double LatencyMs, double PacketLossPct) PingHost(string host, string displayName)
    {
        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var rtts = new List<long>();
            int lost = 0;

            for (int i = 0; i < 4; i++)
            {
                var reply = ping.Send(host, 2000);
                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                    rtts.Add(reply.RoundtripTime);
                else
                    lost++;
            }

            var avg = rtts.Count > 0 ? rtts.Average() : 0;
            var loss = (double)lost / 4 * 100;
            return ($"{displayName}: {avg:F0} ms ({loss:F0}% pérdida)", avg, loss);
        }
        catch { return ($"{displayName}: error", 0, 100); }
    }

    public (string Label, double LatencyMs, double PacketLossPct) GetPingToGateway()
    {
        try
        {
            var gateway = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(n => n.GetIPProperties().GatewayAddresses.FirstOrDefault()?.Address)
                .FirstOrDefault(a => a != null && !IPAddress.IsLoopback(a));

            if (gateway == null) return ("Sin gateway", 0, 100);
            return PingHost(gateway.ToString(), "Gateway");
        }
        catch { return ("Error de ping", 0, 100); }
    }

    public (string Label, double LatencyMs, double PacketLossPct) GetPingToGoogle()
        => PingHost("8.8.8.8", "Google");

    public string GetCpuDetail()
    {
        try
        {
            using var mos = new ManagementObjectSearcher("SELECT Name,MaxClockSpeed,NumberOfCores,NumberOfLogicalProcessors FROM Win32_Processor");
            var cpu = mos.Get().Cast<ManagementObject>().FirstOrDefault();
            if (cpu == null) return "N/A";
            var name = cpu["Name"]?.ToString()?.Trim() ?? "";
            var speed = Convert.ToUInt32(cpu["MaxClockSpeed"]);
            var cores = Convert.ToInt32(cpu["NumberOfCores"]);
            var threads = Convert.ToInt32(cpu["NumberOfLogicalProcessors"]);
            var ghz = speed / 1000.0;
            return $"{name}\n{ghz:F1} GHz • {cores} núcleos / {threads} hilos";
        }
        catch { return Environment.ProcessorCount > 0 ? $"{Environment.ProcessorCount} procesadores lógicos" : "N/A"; }
    }

    public double GetCpuUsage()
    {
        try
        {
            using var mos = new ManagementObjectSearcher("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name = '_Total'");
            var mo = mos.Get().Cast<ManagementObject>().FirstOrDefault();
            return mo != null ? Convert.ToDouble(mo["PercentProcessorTime"]) : 0;
        }
        catch { return 0; }
    }

    public double GetGpuUsage()
    {
        try
        {
            using var mos = new ManagementObjectSearcher(@"root\CIMv2", "SELECT PercentUtilization FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine WHERE Name LIKE '%3D%'");
            var engines = mos.Get().Cast<ManagementObject>().ToList();
            if (engines.Count == 0) return 0;
            var total = engines.Sum(e => Convert.ToDouble(e["PercentUtilization"]));
            return Math.Min(100, Math.Round(total, 1));
        }
        catch { return 0; }
    }

    public string GetGpuInfo()
    {
        try
        {
            using var mos = new ManagementObjectSearcher("SELECT Name,AdapterRAM FROM Win32_VideoController");
            var cards = mos.Get().Cast<ManagementObject>().ToList();
            if (cards.Count == 0) return "No disponible";
            var parts = cards.Select(gpu =>
            {
                var name = gpu["Name"]?.ToString()?.Trim() ?? "";
                var vram = gpu["AdapterRAM"] as ulong?;
                var vramGb = vram.HasValue ? vram.Value / 1_073_741_824.0 : 0;
                return vramGb > 0 ? $"{name} ({vramGb:F1} GB)" : name;
            });
            return string.Join("\n", parts);
        }
        catch { return "N/A"; }
    }

    public string GetRamSpecs()
    {
        try
        {
            using var mos = new ManagementObjectSearcher("SELECT Capacity,Speed,SMBIOSMemoryType FROM Win32_PhysicalMemory");
            var sticks = mos.Get().Cast<ManagementObject>().ToList();
            if (sticks.Count == 0) return "N/A";

            var total = sticks.Sum(s => (decimal)Convert.ToUInt64(s["Capacity"]));
            var speed = sticks.Select(s => Convert.ToUInt32(s["Speed"])).FirstOrDefault();
            var memType = sticks.Select(s => { try { return Convert.ToUInt16(s["SMBIOSMemoryType"]); } catch { return (ushort)0; } }).FirstOrDefault();

            var ddr = memType switch { 20 => "DDR", 21 => "DDR2", 24 => "DDR3", 26 => "DDR4", 34 => "DDR5", _ => null };
            var totalGb = (double)total / 1_073_741_824.0;
            var label = ddr != null ? $"{ddr}-{speed}" : $"{speed} MHz";
            return $"{totalGb:F0} GB • {label}\n{sticks.Count} módulo(s)";
        }
        catch { return "N/A"; }
    }

    public string GetMotherboardInfo()
    {
        try
        {
            using var mos = new ManagementObjectSearcher("SELECT Manufacturer,Product,SerialNumber FROM Win32_BaseBoard");
            var mb = mos.Get().Cast<ManagementObject>().FirstOrDefault();
            if (mb == null) return "N/A";
            var mfr = mb["Manufacturer"]?.ToString()?.Trim() ?? "";
            var prod = mb["Product"]?.ToString()?.Trim() ?? "";
            var sn = mb["SerialNumber"]?.ToString()?.Trim() ?? "";
            return $"{mfr}\n{prod}\nSN: {sn}";
        }
        catch { return "N/A"; }
    }

    public string GetBiosInfo()
    {
        try
        {
            using var mos = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion,ReleaseDate FROM Win32_BIOS");
            var bios = mos.Get().Cast<ManagementObject>().FirstOrDefault();
            if (bios == null) return "N/A";
            var ver = bios["SMBIOSBIOSVersion"]?.ToString()?.Trim() ?? "";
            var dateStr = bios["ReleaseDate"]?.ToString();
            var date = "";
            if (dateStr != null && dateStr.Length >= 8)
            {
                var y = dateStr[..4];
                var m = dateStr[4..6];
                var d = dateStr[6..8];
                date = $"{d}/{m}/{y}";
            }
            return $"{ver}\n{date}";
        }
        catch { return "N/A"; }
    }

    public (double? HealthPercent, string Detail) GetBatteryHealth()
    {
        try
        {
            using var staticMos = new ManagementObjectSearcher(@"root\wmi", "SELECT DesignedCapacity FROM BatteryStaticData");
            var designed = staticMos.Get().Cast<ManagementObject>().FirstOrDefault()?["DesignedCapacity"] as uint?;
            if (designed == null || designed == 0) return (null, "No disponible");

            using var fullMos = new ManagementObjectSearcher(@"root\wmi", "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity");
            var full = fullMos.Get().Cast<ManagementObject>().FirstOrDefault()?["FullChargedCapacity"] as uint?;
            if (full == null || full == 0) return (null, "No disponible");

            var pct = (double)full.Value / designed.Value * 100;
            return (pct, $"{pct:F1}% ({full.Value} / {designed.Value} mWh)");
        }
        catch { return (null, "No disponible"); }
    }

    public string GetDiskSmartInfo()
    {
        try
        {
            using var mos = new ManagementObjectSearcher("SELECT Model,Status,InterfaceType FROM Win32_DiskDrive");
            var drives = mos.Get().Cast<ManagementObject>().ToList();
            if (drives.Count == 0) return "N/A";

            var parts = drives.Select(d =>
            {
                var model = d["Model"]?.ToString()?.Trim() ?? "?";
                var status = d["Status"]?.ToString() ?? "?";
                var iface = d["InterfaceType"]?.ToString() ?? "";
                var type = iface.Contains("NVMe") ? "NVMe" : iface.Contains("RAID") ? "RAID" : iface.Contains("SCSI") || model.Contains("SSD") ? "SSD" : "HDD";
                return $"{model}\n{type} · {status}";
            });

            return string.Join("\n\n", parts);
        }
        catch { return "N/A"; }
    }

    public string GetPowerPlan()
    {
        try
        {
            using var mos = new ManagementObjectSearcher(@"root\cimv2\power", "SELECT ElementName,InstanceID FROM Win32_PowerPlan WHERE IsActive = true");
            var mo = mos.Get().Cast<ManagementObject>().FirstOrDefault();
            if (mo != null)
            {
                var name = mo["ElementName"]?.ToString();
                if (!string.IsNullOrEmpty(name)) return name;
            }
        }
        catch { }

        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes");
            var active = key?.GetValue("ActivePowerScheme")?.ToString();
            if (active != null)
            {
                using var descKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Control\Power\PowerSchemes\{active}");
                var name = descKey?.GetValue("FriendlyName")?.ToString();
                if (!string.IsNullOrEmpty(name)) return name;
            }
        }
        catch { }

        return "N/A";
    }

    public string GetPendingUpdates()
    {
        try
        {
            var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
            if (sessionType == null) return "N/A";
            dynamic session = Activator.CreateInstance(sessionType)!;
            dynamic searcher = session.CreateUpdateSearcher();
            dynamic result = searcher.Search("IsInstalled=0 and IsHidden=0 and Type='Software'");
            int count = result.Updates.Count;
            return count > 0 ? $"{count} pendiente(s)" : "Al día";
        }
        catch
        {
            return "No disponible";
        }
    }

    public (string Errors, string Criticals) GetRecentErrorCount()
    {
        int errors = 0;
        int criticals = 0;

        try
        {
            var cutoff = DateTime.Now.AddHours(-24).ToString("yyyyMMddHHmmss");
            using var errMos = new ManagementObjectSearcher($"SELECT TimeGenerated FROM Win32_NTLogEvent WHERE LogFile='System' AND Type='Error' AND TimeGenerated >= '{cutoff}'");
            errors = errMos.Get().Cast<ManagementObject>().Count();
        }
        catch
        {
            try
            {
                using var log = new EventLog("System");
                errors = log.Entries.Cast<EventLogEntry>()
                    .Count(e => e.EntryType == EventLogEntryType.Error
                             && e.TimeGenerated > DateTime.Now.AddHours(-24));
            }
            catch { errors = -1; }
        }

        try
        {
            var critQuery = new System.Diagnostics.Eventing.Reader.EventLogQuery("System", System.Diagnostics.Eventing.Reader.PathType.LogName, "*[System[Level=1 and TimeCreated[timediff(@SystemTime) <= 86400000]]]");
            using var reader = new System.Diagnostics.Eventing.Reader.EventLogReader(critQuery);
            while (reader.ReadEvent() != null) criticals++;
        }
        catch { criticals = -1; }

        var errStr = errors < 0 ? "N/A" : $"{errors} error(es) (24h)";
        var critStr = criticals < 0 ? "N/A" : $"{criticals} crítico(s) (24h)";
        return (errStr, critStr);
    }

    public string GetDefenderStatus()
    {
        try
        {
            using var mos = new ManagementObjectSearcher(@"root\Microsoft\Windows\Defender", "SELECT AntivirusEnabled,RealTimeProtectionEnabled,AntispywareEnabled,QuickScanSignatureVersion FROM MSFT_MpComputerStatus");
            var mo = mos.Get().Cast<ManagementObject>().FirstOrDefault();
            if (mo == null) return "No disponible";

            var av = (bool)(mo["AntivirusEnabled"] ?? false);
            var rtp = (bool)(mo["RealTimeProtectionEnabled"] ?? false);
            var sig = mo["QuickScanSignatureVersion"]?.ToString() ?? "?";

            if (!av) return "Desactivado";
            return $"{(rtp ? "Protección activa" : "Prot. deshabilitada")}\n{sig}";
        }
        catch { return "No disponible"; }
    }

    public string GetNetworkSpeed()
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .FirstOrDefault();
            if (nic == null) return "N/A";

            var speed = nic.Speed;
            if (speed >= 1_000_000_000)
                return $"{speed / 1_000_000_000.0:F0} Gbps";
            if (speed >= 1_000_000)
                return $"{speed / 1_000_000.0:F0} Mbps";
            if (speed > 0)
                return $"{speed / 1_000.0:F0} Kbps";
            return "N/A";
        }
        catch { return "N/A"; }
    }

    public (double DownloadMbps, double UploadMbps, string Error) RunSpeedTest()
    {
        try
        {
            var exePath = GetOrDownloadSpeedtestCli();
            if (exePath == null)
                return (0, 0, "No se pudo descargar speedtest CLI");

            var psi = new ProcessStartInfo(exePath, "--accept-license -f json")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi) ?? throw new Exception("No se pudo iniciar speedtest");
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(120000);

            if (!string.IsNullOrEmpty(stderr))
            {
                // --accept-license prints license acceptance to stderr, ignore that
                if (proc.ExitCode != 0) return (0, 0, stderr.Trim());
            }

            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;
            var download = root.GetProperty("download").GetProperty("bandwidth").GetInt64();
            var upload = root.GetProperty("upload").GetProperty("bandwidth").GetInt64();

            // bandwidth is in bytes/sec → convert to Mbps
            var downMbps = download * 8.0 / 1_000_000;
            var upMbps = upload * 8.0 / 1_000_000;

            return (Math.Round(downMbps, 1), Math.Round(upMbps, 1), "");
        }
        catch (Exception ex) { return (0, 0, ex.Message); }
    }

    private static string? GetOrDownloadSpeedtestCli()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ToolkitSpeedtest");
        var exe = Path.Combine(dir, "speedtest.exe");
        if (File.Exists(exe)) return exe;

        Directory.CreateDirectory(dir);
        // Clean any leftover files from previous extraction
        foreach (var f in Directory.GetFiles(dir)) try { File.Delete(f); } catch { }
        var zipPath = Path.Combine(dir, "speedtest.zip");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var bytes = http.GetByteArrayAsync("https://install.speedtest.net/app/cli/ookla-speedtest-1.2.0-win64.zip").Result;
        File.WriteAllBytes(zipPath, bytes);
        ZipFile.ExtractToDirectory(zipPath, dir);
        File.Delete(zipPath);

        return File.Exists(exe) ? exe : null;
    }
}