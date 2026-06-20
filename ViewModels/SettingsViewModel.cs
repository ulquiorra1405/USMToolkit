using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Toolkit.Models;
using Toolkit.Services;

namespace Toolkit.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly RegistryService _reg = new();
    private readonly CommandService _cmd = new();
    private readonly CommandsConfigService _config = new();

    public ObservableCollection<SettingItem> Settings { get; } = new()
    {
        new()
        {
            Name = "Mostrar extensiones de archivos",
            Description = "Muestra las extensiones (.txt, .pdf, etc.) en el Explorador",
            Category = "Explorador",
            IsRegistry = true,
            RegistryPath = "HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
            RegistryValue = "HideFileExt",
            RegistryDataOn = 0,
            RegistryDataOff = 1
        },
        new()
        {
            Name = "Mostrar archivos ocultos",
            Description = "Revela archivos y carpetas ocultas en el Explorador",
            Category = "Explorador",
            IsRegistry = true,
            RegistryPath = "HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
            RegistryValue = "Hidden",
            RegistryDataOn = 1,
            RegistryDataOff = 2
        },
        new()
        {
            Name = "Acelerar menú contextual",
            Description = "Reduce el tiempo de retardo del menú contextual",
            Category = "Rendimiento",
            IsRegistry = true,
            RegistryPath = "HKEY_CURRENT_USER\\Control Panel\\Desktop",
            RegistryValue = "MenuShowDelay",
            RegistryDataOn = 100,
            RegistryDataOff = 400
        },
        new()
        {
            Name = "Deshabilitar animaciones",
            Description = "Desactiva animaciones de la interfaz para mayor rendimiento",
            Category = "Rendimiento",
            IsRegistry = true,
            RegistryPath = "HKEY_CURRENT_USER\\Control Panel\\Desktop\\WindowMetrics",
            RegistryValue = "MinAnimate",
            RegistryDataOn = 0,
            RegistryDataOff = 1
        },
    };

    [ObservableProperty]
    private string _cmdOutput = "";

    [ObservableProperty]
    private string _toolkitNetworkPath = "";

    [ObservableProperty]
    private bool _toolkitAdvancedMode;

    [ObservableProperty]
    private bool _toolkitUseLightTheme;

    public SettingsViewModel()
    {
        LoadToolkitConfig();
    }

    public void LoadToolkitConfig()
    {
        var cfg = _config.Load();
        if (cfg.Configuracion != null)
        {
            ToolkitNetworkPath = cfg.Configuracion.NetworkPath ?? "";
            ToolkitAdvancedMode = cfg.Configuracion.AdvancedMode;
            ToolkitUseLightTheme = cfg.Configuracion.UseLightTheme;
        }
    }

    partial void OnToolkitNetworkPathChanged(string value) => SaveToolkitConfig();
    partial void OnToolkitAdvancedModeChanged(bool value) => SaveToolkitConfig();
    partial void OnToolkitUseLightThemeChanged(bool value)
    {
        SaveToolkitConfig();
        ApplyTheme(value);
    }

    private void SaveToolkitConfig()
    {
        var cfg = _config.Load();
        cfg.Configuracion = new ToolkitSettings
        {
            NetworkPath = ToolkitNetworkPath,
            AdvancedMode = ToolkitAdvancedMode,
            UseLightTheme = ToolkitUseLightTheme
        };
        _config.Save(cfg);

        if (MainViewModel.Shared != null)
        {
            MainViewModel.Shared.NotifyConfigChanged();
        }
    }

    private static void ApplyTheme(bool light)
    {
        ThemeService.ApplyTheme(light);
        try
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(light ? BaseTheme.Light : BaseTheme.Dark);
            paletteHelper.SetTheme(theme);
        }
        catch
        {
            App.Log($"Theme change failed: {light}");
        }
    }

    [RelayCommand]
    private void ReadRegistrySettings()
    {
        foreach (var s in Settings.Where(x => x.IsRegistry))
        {
            if (_reg.TryReadValue(s.RegistryPath, s.RegistryValue, out var value))
                s.IsActive = value?.ToString() == s.RegistryDataOn?.ToString();
        }
    }

    [RelayCommand]
    private void ToggleSetting(SettingItem? item)
    {
        if (item == null || !item.IsRegistry) return;
        var newValue = item.IsActive ? item.RegistryDataOn : item.RegistryDataOff;
        var success = _reg.TryWriteValue(item.RegistryPath, item.RegistryValue, newValue!);
        if (success)
            item.IsActive = !item.IsActive;
    }

    [RelayCommand]
    private void ToggleRegistry(SettingItem? item)
    {
        if (item == null || !item.IsRegistry) return;
        var newValue = item.IsActive ? item.RegistryDataOff : item.RegistryDataOn;
        if (_reg.TryWriteValue(item.RegistryPath, item.RegistryValue, newValue!))
            item.IsActive = !item.IsActive;
    }

    [RelayCommand]
    private async Task RestartExplorerAsync()
    {
        CmdOutput = "Reiniciando Explorer...\n";
        var (output, _) = await _cmd.RunCommandWithStatusAsync("taskkill /f /im explorer.exe && start explorer.exe");
        CmdOutput += output;
    }
}