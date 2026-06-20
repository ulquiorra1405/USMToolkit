using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Toolkit.Models;
using Toolkit.Services;

namespace Toolkit.ViewModels;

public partial class DiagnosticsViewModel : ObservableObject
{
    private readonly CommandService _cmd = new();

    public ObservableCollection<DiagnosticTest> Tests { get; } = new()
    {
        new() { Name = "Ping a Google", Description = "Verifica conectividad a Internet", Command = "ping -n 4 8.8.8.8" },
        new() { Name = "Información del sistema", Description = "Muestra detalles del hardware y SO", Command = "systeminfo" },
        new() { Name = "Configuración de red", Description = "Muestra adaptadores y direcciones IP", Command = "ipconfig /all" },
        new() { Name = "Tabla de rutas", Description = "Muestra la tabla de enrutamiento IPv4", Command = "route print -4" },
        new() { Name = "Conexiones activas", Description = "Puertos y conexiones TCP activas", Command = "netstat -an" },
        new() { Name = "Espacio en disco", Description = "Muestra el uso de los discos", Command = "wmic logicaldisk get size,freespace,caption" },
        new() { Name = "Procesos en ejecución", Description = "Lista los procesos con más consumo de CPU", Command = "tasklist /SVC /FO LIST" },
        new() { Name = "DNS Cache", Description = "Muestra el caché DNS del sistema", Command = "ipconfig /displaydns" },
    };

    [ObservableProperty]
    private DiagnosticTest? _selectedTest;

    [ObservableProperty]
    private string _globalOutput = "";

    [ObservableProperty]
    private bool _isRunning;

    [RelayCommand]
    private async Task RunTestAsync(DiagnosticTest? test)
    {
        if (test == null) return;

        test.IsRunning = true;
        test.HasResult = false;
        SelectedTest = test;

        var (output, success) = await _cmd.RunCommandWithStatusAsync(test.Command);

        test.RawOutput = output;
        test.IsSuccess = success;
        test.ParsedSummary = ParseOutput(test.Name, output);
        test.HasResult = true;
        test.IsRunning = false;
    }

    [RelayCommand]
    private async Task RunAllTestsAsync()
    {
        IsRunning = true;
        GlobalOutput = "Ejecutando todos los diagnósticos...\n";

        foreach (var test in Tests)
        {
            GlobalOutput += $"\n═══════ {test.Name} ═══════\n";
            var (output, _) = await _cmd.RunCommandWithStatusAsync(test.Command);
            GlobalOutput += output + "\n";
        }

        GlobalOutput += "\n✅ Diagnóstico completo.";
        IsRunning = false;
    }

    private static string ParseOutput(string testName, string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        return testName switch
        {
            "Ping a Google" => ParsePing(lines),
            "Información del sistema" => ParseSystemInfo(lines),
            "Configuración de red" => ParseIpConfig(lines),
            "Espacio en disco" => ParseDiskSpace(lines),
            "Procesos en ejecución" => $"{lines.Length} procesos listados",
            _ => $"Comando ejecutado. {lines.Length} líneas de salida."
        };
    }

    private static string ParsePing(string[] lines)
    {
        foreach (var line in lines)
            if (line.Contains("media", StringComparison.OrdinalIgnoreCase) && line.Contains("="))
                return $"⚠️  {line.Trim()}";
        foreach (var line in lines)
            if (line.Contains("perdidos", StringComparison.OrdinalIgnoreCase))
                return line.Trim();
        return "No se pudo analizar el ping.";
    }

    private static string ParseSystemInfo(string[] lines)
    {
        var summary = new System.Text.StringBuilder();
        foreach (var line in lines)
        {
            if (line.Contains("OS Name", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("OS Version", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("System Manufacturer", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("System Model", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Total Physical Memory", StringComparison.OrdinalIgnoreCase))
            {
                summary.AppendLine(line.Trim());
            }
        }
        return summary.Length > 0 ? summary.ToString() : "No se pudieron extraer datos clave.";
    }

    private static string ParseIpConfig(string[] lines)
    {
        var adapters = new System.Text.StringBuilder();
        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("IPv4", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Descripción", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("DNS", StringComparison.OrdinalIgnoreCase))
            {
                adapters.AppendLine(line.Trim());
            }
        }
        return adapters.Length > 0 ? adapters.ToString() : "Sin adaptadores IPv4.";
    }

    private static string ParseDiskSpace(string[] lines)
    {
        var diskInfo = new System.Text.StringBuilder();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("Caption", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && parts[0].Length == 2 && parts[0][1] == ':')
            {
                if (long.TryParse(parts[1], out var free) && long.TryParse(parts[2], out var total))
                {
                    var usedPct = total > 0 ? (double)(total - free) / total * 100 : 0;
                    diskInfo.AppendLine($"{parts[0]}  {usedPct:F0}% usado  ({free / 1_000_000_000:F1} GB libre de {total / 1_000_000_000:F1} GB)");
                }
            }
        }
        return diskInfo.Length > 0 ? diskInfo.ToString() : "No se encontraron discos.";
    }
}
