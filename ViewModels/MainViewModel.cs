using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Toolkit.Models;
using Toolkit.Services;
using Toolkit.Views;

namespace Toolkit.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly CommandsConfigService _config = new();

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private bool _isSidebarExpanded;

    [ObservableProperty]
    private int _selectedIndex = 0;

    [ObservableProperty]
    private string _searchQuery = "";

    partial void OnSearchQueryChanged(string value)
    {
        var q = value?.Trim() ?? "";
        if (CurrentPage is DeploymentsViewModel dv)
            dv.SearchQuery = q;
        else if (CurrentPage is EjecucionesViewModel ev)
            ev.SearchQuery = q;
        else if (CurrentPage is ManualesViewModel mv)
            mv.SearchQuery = q;
    }

    [ObservableProperty]
    private CategoriaItem? _selectedCategory;

    partial void OnSelectedCategoryChanged(CategoriaItem? value)
    {
        if (value?.Tipo == "Ejecucion" || value?.Tipo == "External")
            Ejecuciones.SelectedCategory = value;
        else
            Deployments.SelectedCategory = value;
    }

    public double SidebarWidth => IsSidebarExpanded ? 180 : 48;
    public string ToggleTooltip => IsSidebarExpanded ? "Colapsar" : "Expandir";
    public bool IsDeploymentsView => CurrentPage is DeploymentsViewModel;
    public bool IsEjecucionesView => CurrentPage is EjecucionesViewModel;
    public bool IsManualesView => CurrentPage is ManualesViewModel;
    public bool HasLogPanel => IsDeploymentsView || IsEjecucionesView;

    public DashboardViewModel Dashboard { get; }
    public DeploymentsViewModel Deployments { get; }
    public EjecucionesViewModel Ejecuciones { get; }
    public SettingsViewModel Settings { get; }
    public DiagnosticsViewModel Diagnostics { get; }
    public CommandsEditorViewModel CommandsEditor { get; }
    public ManualesViewModel Manuales { get; }

    public ObservableCollection<CategoriaItem> SidebarCategories { get; } = [];
    public bool HasSidebarCategories => SidebarCategories.Count > 0;

    private CommandsEditorView? _editorView;

    public static MainViewModel? Shared { get; private set; }

    public MainViewModel()
    {
        Shared = this;
        Dashboard = new DashboardViewModel();
        Deployments = new DeploymentsViewModel();
        Ejecuciones = new EjecucionesViewModel();
        CommandsEditor = new CommandsEditorViewModel();
        CommandsEditor.Saved += OnEditorSaved;
        CommandsEditor.ContentChanged += OnEditorContentChanged;
        Manuales = new ManualesViewModel();
        Diagnostics = new DiagnosticsViewModel();
        Settings = new SettingsViewModel();
        CurrentPage = Dashboard;
        LoadSidebarCategories();
        RefreshToolkitRoot();
        RefreshManualesConfig();
    }

    public void NotifyConfigChanged()
    {
        RefreshToolkitRoot();
        LoadSidebarCategories();
        Deployments.LoadFromConfig();
        Ejecuciones.LoadFromConfig();
        CommandsEditor?.ReloadConfig();
        RefreshManualesConfig();
        Manuales?.ReRenderCurrent();
    }

    private void OnEditorContentChanged()
    {
        LoadSidebarCategories();
        Deployments.LoadFromConfig();
        Ejecuciones.LoadFromConfig();
    }

    private void OnEditorSaved()
    {
        LoadSidebarCategories();
        Deployments.LoadFromConfig();
        Ejecuciones.LoadFromConfig();
    }

    private void LoadSidebarCategories()
    {
        SidebarCategories.Clear();
        var cfg = _config.Load();
        foreach (var cat in cfg.Categorias.Where(c => !c.Fija))
            SidebarCategories.Add(cat);
        OnPropertyChanged(nameof(HasSidebarCategories));
    }

    [RelayCommand]
    private void Navigate(object? parameter)
    {
        var index = parameter is int i ? i : int.TryParse(parameter?.ToString(), out var p) ? p : 0;
        SelectedCategory = null;
        SelectedIndex = index;
        CurrentPage = index switch
        {
            0 => Dashboard,
            1 => Deployments,
            2 => _editorView ??= new CommandsEditorView { DataContext = CommandsEditor },
            3 => new Views.SettingsView { DataContext = Settings },
            4 => GetManualesPage(),
            _ => Dashboard
        };
        OnPropertyChanged(nameof(IsDeploymentsView));
        OnPropertyChanged(nameof(IsEjecucionesView));
        OnPropertyChanged(nameof(IsManualesView));
        OnPropertyChanged(nameof(HasLogPanel));
    }

    [RelayCommand]
    private void ShowAllDeployments()
    {
        SelectedCategory = null;
        SelectedIndex = 1;
        CurrentPage = Deployments;
        OnPropertyChanged(nameof(IsDeploymentsView));
        OnPropertyChanged(nameof(IsEjecucionesView));
        OnPropertyChanged(nameof(IsManualesView));
        OnPropertyChanged(nameof(HasLogPanel));
    }

    [RelayCommand]
    private void NavigateToCategory(CategoriaItem? category)
    {
        if (category == null) return;
        SelectedCategory = category;
        SelectedIndex = -1;

        if (category.Tipo == "Ejecucion" || category.Tipo == "External")
            CurrentPage = Ejecuciones;
        else
            CurrentPage = Deployments;

        OnPropertyChanged(nameof(IsDeploymentsView));
        OnPropertyChanged(nameof(IsEjecucionesView));
        OnPropertyChanged(nameof(IsManualesView));
        OnPropertyChanged(nameof(HasLogPanel));
    }

    private object GetManualesPage()
    {
        Manuales.Reload();
        return Manuales;
    }

    public void RefreshManualesConfig()
    {
        var cfg = _config.Load();
        var manualesPath = cfg.Configuracion?.ManualesPath ?? "";
        var editEnabled = cfg.Configuracion?.ManualesEditEnabled ?? false;

        if (string.IsNullOrEmpty(manualesPath))
        {
            // Default path: AppData\USMToolkit\manuales
            manualesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "USMToolkit", "manuales");
        }

        Manuales.SetBasePath(manualesPath);
        Manuales.IsEditingEnabled = editEnabled;
    }

    partial void OnIsSidebarExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(SidebarWidth));
        OnPropertyChanged(nameof(ToggleTooltip));
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
    }

    // --- Network mode indicator ---

    [ObservableProperty]
    private string _toolkitRootDisplay = "💻 LOCAL";

    public void RefreshToolkitRoot()
    {
        var cfg = _config.Load();
        var local = AppContext.BaseDirectory;
        var network = cfg.Configuracion?.NetworkPath ?? "";

        if (!string.IsNullOrEmpty(network) && Directory.Exists(network))
        {
            var localOk = CheckLocalResources(local);
            var netOk = CheckLocalResources(network);
            if (localOk && netOk)
                ToolkitRootDisplay = "💻 LOCAL";
            else if (netOk)
                ToolkitRootDisplay = "🌐 RED";
            else if (localOk)
                ToolkitRootDisplay = "💻 LOCAL";
            else
                ToolkitRootDisplay = "💻 LOCAL";
        }
        else
        {
            ToolkitRootDisplay = "💻 LOCAL";
        }
    }

    [RelayCommand]
    private void ToggleToolkitRoot()
    {
        var cfg = _config.Load();
        var network = cfg.Configuracion?.NetworkPath ?? "";
        if (string.IsNullOrEmpty(network) || !Directory.Exists(network)) return;

        var local = AppContext.BaseDirectory;
        var isLocal = ToolkitRootDisplay.Contains("LOCAL");

        if (isLocal && CheckLocalResources(network))
            ToolkitRootDisplay = "🌐 RED";
        else if (!isLocal && CheckLocalResources(local))
            ToolkitRootDisplay = "💻 LOCAL";
    }

    private static bool CheckLocalResources(string root)
    {
        try
        {
            var testFile = Path.Combine(root, "commands.json");
            return File.Exists(testFile);
        }
        catch
        {
            return false;
        }
    }

    public string ResolvedToolkitRoot()
    {
        if (ToolkitRootDisplay.Contains("RED"))
        {
            var cfg = _config.Load();
            return cfg.Configuracion?.NetworkPath ?? AppContext.BaseDirectory;
        }
        return AppContext.BaseDirectory;
    }
}