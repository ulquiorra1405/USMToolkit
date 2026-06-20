using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Toolkit.Models;

namespace Toolkit.Services;

public class CommandsConfigService
{
    private static readonly string ConfigPath = Path.Combine(
        AppContext.BaseDirectory, "commands.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CommandsConfig Load()
    {
        MigrateFromAppData();

        try
        {
            if (!File.Exists(ConfigPath))
            {
                var defaults = GetDefaults();
                Save(defaults);
                return defaults;
            }
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<CommandsConfig>(json, JsonOptions) ?? GetDefaults();
        }
        catch
        {
            return GetDefaults();
        }
    }

    private static void MigrateFromAppData()
    {
        if (File.Exists(ConfigPath)) return;
        var oldPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Toolkit", "commands.json");
        if (!File.Exists(oldPath)) return;
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.Copy(oldPath, ConfigPath, overwrite: false);
        }
        catch { }
    }

    public void Save(CommandsConfig config)
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }

    private static CommandsConfig GetDefaults()
    {
        return new CommandsConfig
        {
            Categorias =
            [
                new()
                {
                    Nombre = "Acciones Rápidas",
                    Tipo = "Accion",
                    Fija = true,
                    Icono = "Flash",
                    Items =
                    [
                        new() { Nombre = "Limpiar DNS", Admin = true, Pasos = ["ipconfig /flushdns"] },
                        new() { Nombre = "Liberar memoria", Pasos = ["ipconfig /flushdns"] },
                        new() { Nombre = "Vaciar Papelera", Pasos = ["rd /s /q %systemdrive%\\$Recycle.Bin"] },
                        new() { Nombre = "Reparar red", Admin = true, Pasos =
                        [
                            "ipconfig /release",
                            "ipconfig /renew",
                            "ipconfig /flushdns",
                            "netsh winsock reset",
                            "netsh int ip reset"
                        ]},
                    ]
                },
                new()
                {
                    Nombre = "Despliegue",
                    Tipo = "Despliegue",
                    Fija = true,
                    Icono = "PackageVariantClosed",
                    Items =
                    [
                        new() { Nombre = "7-Zip", Descripcion = "Compresor de archivos ligero y potente", Pasos = ["winget install 7zip.7zip"] },
                        new() { Nombre = "Google Chrome", Descripcion = "Navegador web rápido y seguro", Pasos = ["winget install Google.Chrome"] },
                        new() { Nombre = "Mozilla Firefox", Descripcion = "Navegador open-source centrado en privacidad", Pasos = ["winget install Mozilla.Firefox"] },
                        new() { Nombre = "VLC Media Player", Descripcion = "Reproductor multimedia universal", Pasos = ["winget install VideoLAN.VLC"] },
                        new() { Nombre = "PowerToys", Descripcion = "Utilidades avanzadas para Windows de Microsoft", Pasos = ["winget install Microsoft.PowerToys"] },
                        new() { Nombre = "Notepad++", Descripcion = "Editor de texto y código fuente", Pasos = ["winget install Notepad++.Notepad++"] },
                        new() { Nombre = "Git", Descripcion = "Sistema de control de versiones distribuido", Pasos = ["winget install Git.Git"] },
                        new() { Nombre = "Node.js", Descripcion = "Entorno de ejecución para JavaScript", Pasos = ["winget install OpenJS.NodeJS.LTS"] },
                    ]
                },
            ]
        };
    }
}

public class CommandsConfig
{
    public List<CategoriaItem> Categorias { get; set; } = [];
    public ToolkitSettings? Configuracion { get; set; }
}

public class ToolkitSettings
{
    public string NetworkPath { get; set; } = "";
    public bool AdvancedMode { get; set; }
    public bool UseLightTheme { get; set; }
}
