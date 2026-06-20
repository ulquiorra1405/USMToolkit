using System.IO;
using System.Text.Json;

namespace Toolkit.Services;

public class LayoutSettings
{
    public double DeploymentsCol0 { get; set; }
    public double DeploymentsCol2 { get; set; }
    public double SettingsCol0 { get; set; }
    public double SettingsCol2 { get; set; }
    public double DiagnosticsCol0 { get; set; }
    public double DiagnosticsCol2 { get; set; }
}

public static class SettingsService
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Toolkit", "layout.json");

    public static void Save(LayoutSettings settings)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static LayoutSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<LayoutSettings>(json) ?? new LayoutSettings();
            }
        }
        catch { }
        return new LayoutSettings();
    }
}
