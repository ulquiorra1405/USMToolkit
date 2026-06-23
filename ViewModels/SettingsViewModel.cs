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

    [ObservableProperty]
    private string _manualesPath = "";

    [ObservableProperty]
    private bool _manualesEditEnabled;

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
            ManualesPath = cfg.Configuracion.ManualesPath ?? "";
            ManualesEditEnabled = cfg.Configuracion.ManualesEditEnabled;
        }
    }

    partial void OnToolkitNetworkPathChanged(string value) => SaveToolkitConfig();
    partial void OnToolkitAdvancedModeChanged(bool value) => SaveToolkitConfig();
    partial void OnManualesPathChanged(string value) => SaveToolkitConfig();
    partial void OnManualesEditEnabledChanged(bool value) => SaveToolkitConfig();
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
            UseLightTheme = ToolkitUseLightTheme,
            ManualesPath = ManualesPath,
            ManualesEditEnabled = ManualesEditEnabled
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

            // Actualizar brushes custom para el tema actual
            var res = System.Windows.Application.Current.Resources;
            if (light)
            {
                res["TextPrimaryBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x1A));
                res["TextSecondaryBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44));
                res["TextMutedBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x77, 0x77, 0x77));
                res["TextDimBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA));
                res["ContentBgBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0xF5, 0xF5));
                res["SidebarBgBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xED, 0xED, 0xED));
                res["CardBgBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
                res["CardHoverBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFA, 0xFA, 0xFA));
                res["BorderBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDD, 0xDD, 0xDD));
                res["TagChipBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF0, 0xE8, 0xD8));
                res["TagChipForegroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x7A, 0x6B, 0x4F));
                res["CategoryBadgeBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xE8, 0xF0));
                res["CodeBlockBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF0, 0xF0, 0xF0));
                res["CodeBlockBorderBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD0, 0xD0, 0xD0));
                res["InlineCodeBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xE8, 0xE8));
                res["HorizontalRuleBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD0, 0xD0, 0xD0));
                res["QuoteBorderBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xBB, 0xBB, 0xBB));
            }
            else
            {
                res["TextPrimaryBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xE8, 0xE8));
                res["TextSecondaryBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0xCC, 0xCC));
                res["TextMutedBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88));
                res["TextDimBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55));
                res["ContentBgBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x12, 0x12, 0x12));
                res["SidebarBgBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x1A));
                res["CardBgBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
                res["CardHoverBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x28, 0x28, 0x28));
                res["BorderBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x2A, 0x2A));
                res["TagChipBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3A, 0x32, 0x26));
                res["TagChipForegroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC4, 0xA8, 0x7A));
                res["CategoryBadgeBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x2A, 0x30));
                res["CodeBlockBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x18, 0x18, 0x18));
                res["CodeBlockBorderBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3C, 0x3C, 0x3C));
                res["InlineCodeBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x28, 0x28, 0x28));
                res["HorizontalRuleBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3C, 0x3C, 0x3C));
                res["QuoteBorderBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x64, 0x64, 0x64));
            }
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