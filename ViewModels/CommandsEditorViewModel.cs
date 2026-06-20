using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Toolkit.Models;
using Toolkit.Services;

namespace Toolkit.ViewModels;

public partial class CommandsEditorViewModel : ObservableObject
{
    private readonly CommandsConfigService _config = new();
    private readonly CommandService _cmd = new();
    private readonly DispatcherTimer _saveTimer;

    public event Action? Saved;
    public event Action? ContentChanged;
    public event Action? IconSelected;

    public ObservableCollection<CategoriaItem> Categorias { get; } = [];

    public List<string> TiposDisponibles { get; } = ["Despliegue", "Accion", "Ejecucion", "External"];

    public List<string> ShellsDisponibles { get; } = ["cmd", "powershell"];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveItemUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveItemDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddItemCommand))]
    private CategoriaItem? _selectedCategoria;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveItemUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveItemDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExecuteSelectedCommand))]
    private ComandoItem? _selectedCommand;

    [ObservableProperty]
    private string _editNombre = "";

    [ObservableProperty]
    private string _editDescripcion = "";

    [ObservableProperty]
    private string _editShell = "cmd";

    [ObservableProperty]
    private bool _editAdmin;

    [ObservableProperty]
    private bool _editRequireConfirm;

    [ObservableProperty]
    private string _editCategoriaNombre = "";

    [ObservableProperty]
    private string _editCategoriaTipo = "Despliegue";

    [ObservableProperty]
    private string _editCategoriaIcono = "FolderOutline";

    [ObservableProperty]
    private string _editCategoriaDescripcion = "";

    [ObservableProperty]
    private string _iconSearchText = "";

    partial void OnIconSearchTextChanged(string value)
    {
        FilteredIconos.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            foreach (var icon in IconosDisponibles)
                FilteredIconos.Add(icon);
        }
        else
        {
            var lower = value.ToLowerInvariant();
            foreach (var icon in IconosDisponibles)
            {
                if (icon.ToLowerInvariant().Contains(lower))
                    FilteredIconos.Add(icon);
            }
        }
    }

    public List<string> IconosDisponibles { get; } =
    [
        "FolderOutline", "FolderOpenOutline", "CogOutline", "Wrench", "Hammer",
        "Tools", "SettingsOutline", "Tune", "FilterOutline",
        "NetworkOutline", "Server", "Security", "ShieldOutline",
        "DatabaseOutline", "CloudOutline", "DownloadOutline", "UploadOutline",
        "CodeBraces", "Console", "MonitorOutline",
        "PackageVariantClosed", "BoxOutline",
        "StarOutline", "HeartOutline", "FlagOutline", "BookmarkOutline",
        "TagOutline", "Pin", "Link",
        "AlertOutline", "InformationOutline", "CheckCircleOutline",
        "HelpCircleOutline", "LightbulbOutline", "ClockOutline",
        "CalendarOutline", "ChartBar", "ChartPie",
        "AccountGroupOutline", "BriefcaseOutline",
        "FileOutline", "FileDocumentOutline", "ContentSaveOutline",
        "Restore", "Update", "CloudSyncOutline",
        "Refresh", "Autorenew",
        "Web", "Earth", "Globe",
        "PrinterOutline", "ImageOutline", "Palette",
        "MapOutline", "CompassOutline", "Navigation",
        "BatteryOutline", "PowerPlugOutline", "HomeOutline",
        "Apps", "ViewDashboard", "GridView",
        "FormatListBulleted", "FormatListChecks",
        "KeyOutline", "LockOutline", "LockOpenOutline",
    ];

    public ObservableCollection<string> FilteredIconos { get; } = [];

    [ObservableProperty]
    private string _editPasosText = "";

    public ObservableCollection<CommandVariable> EditVariables { get; } = [];

    [RelayCommand]
    private void AddVariable()
    {
        EditVariables.Add(new CommandVariable { Name = "", Label = "", DefaultValue = "" });
    }

    [RelayCommand]
    private void RemoveVariable(CommandVariable? variable)
    {
        if (variable != null)
            EditVariables.Remove(variable);
    }

    [ObservableProperty]
    private string _runOutput = "";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _hasRunOutput;

    [ObservableProperty]
    private bool _isAdvancedMode;

    partial void OnIsAdvancedModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEditorEnabled));
        OnPropertyChanged(nameof(IsCategoriaEditable));
    }

    public bool IsEditorEnabled => IsAdvancedMode;

    [ObservableProperty]
    private string _searchText = "";

    partial void OnSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            foreach (var cat in Categorias)
            {
                cat.IsVisible = true;
                foreach (var item in cat.Items)
                    item.IsVisible = true;
            }
        }
        else
        {
            var lower = value.ToLowerInvariant();
            foreach (var cat in Categorias)
            {
                bool catMatch = cat.Nombre.ToLowerInvariant().Contains(lower);
                bool anyItemMatch = false;
                foreach (var item in cat.Items)
                {
                    bool itemMatch = item.Nombre.ToLowerInvariant().Contains(lower);
                    item.IsVisible = itemMatch;
                    anyItemMatch = anyItemMatch || itemMatch;
                }
                cat.IsVisible = catMatch || anyItemMatch;
            }
        }
    }

    public bool IsCategoriaEditable => SelectedCategoria is { Fija: false } && IsAdvancedMode;
    public bool IsCategoryPanelVisible => SelectedCategoria != null && SelectedCommand == null;
    public bool IsCommandPanelVisible => SelectedCommand != null;

    public CommandsEditorViewModel()
    {
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            Persist();
        };
        var cfg = _config.Load();
        foreach (var c in cfg.Categorias)
            Categorias.Add(c);
        foreach (var icon in IconosDisponibles)
            FilteredIconos.Add(icon);
        IsAdvancedMode = cfg.Configuracion?.AdvancedMode ?? false;
    }

    public void ReloadConfig()
    {
        var cfg = _config.Load();
        IsAdvancedMode = cfg.Configuracion?.AdvancedMode ?? false;
    }

    partial void OnSelectedCategoriaChanged(CategoriaItem? value)
    {
        if (value != SelectedCategoria)
            SelectedCommand = null;
        if (value != null)
        {
            EditCategoriaNombre = value.Nombre;
            EditCategoriaTipo = string.IsNullOrEmpty(value.Tipo) ? "Despliegue" : value.Tipo;
            EditCategoriaIcono = string.IsNullOrEmpty(value.Icono) ? "FolderOutline" : value.Icono;
            EditCategoriaDescripcion = value.Descripcion;
        }
        OnPropertyChanged(nameof(IsCategoriaEditable));
        OnPropertyChanged(nameof(IsCategoryPanelVisible));
        OnPropertyChanged(nameof(IsCommandPanelVisible));
    }

    partial void OnSelectedCommandChanged(ComandoItem? value)
    {
        HasRunOutput = false;
        if (value != null)
        {
            EditNombre = value.Nombre;
            EditDescripcion = value.Descripcion;
            EditShell = string.IsNullOrEmpty(value.Shell) ? "cmd" : value.Shell;
            EditAdmin = value.Admin;
            EditRequireConfirm = value.RequireConfirm;
            EditPasosText = string.Join("\n", value.Pasos);
            EditVariables.Clear();
            foreach (var v in value.Variables)
                EditVariables.Add(new CommandVariable { Name = v.Name, Label = v.Label, DefaultValue = v.DefaultValue, Sensitive = v.Sensitive, Required = v.Required });
        }
        OnPropertyChanged(nameof(IsCategoryPanelVisible));
        OnPropertyChanged(nameof(IsCommandPanelVisible));
    }

    [RelayCommand]
    private void AddCategoria()
    {
        var cat = new CategoriaItem { Nombre = "Nueva categoría", Tipo = "Despliegue", Icono = "FolderOutline", Descripcion = "" };
        Categorias.Add(cat);
        SelectedCategoria = cat;
    }

    [RelayCommand]
    private void DeleteCategoria()
    {
        if (SelectedCategoria == null || SelectedCategoria.Fija) return;
        Categorias.Remove(SelectedCategoria);
        SelectedCategoria = null;
    }

    [RelayCommand(CanExecute = nameof(CanAddItem))]
    private void AddItem()
    {
        var item = new ComandoItem { Nombre = "Nuevo comando" };
        if (SelectedCommand != null)
        {
            int idx = SelectedCategoria!.Items.IndexOf(SelectedCommand);
            SelectedCategoria.Items.Insert(idx + 1, item);
        }
        else
        {
            SelectedCategoria!.Items.Insert(0, item);
        }
        SelectedCommand = item;
    }

    private bool CanAddItem => SelectedCategoria != null;

    partial void OnEditCategoriaNombreChanged(string value)
    {
        if (SelectedCategoria != null)
            SelectedCategoria.Nombre = value;
        ScheduleCategorySave();
    }

    partial void OnEditCategoriaDescripcionChanged(string value)
    {
        if (SelectedCategoria != null)
            SelectedCategoria.Descripcion = value;
        ScheduleCategorySave();
    }

    partial void OnEditCategoriaTipoChanged(string value)
    {
        if (SelectedCategoria != null)
            SelectedCategoria.Tipo = value;
        AutoSaveCategory();
    }

    partial void OnEditCategoriaIconoChanged(string value)
    {
        if (SelectedCategoria != null)
            SelectedCategoria.Icono = value;
        AutoSaveCategory();
    }

    private void ScheduleCategorySave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void AutoSaveCategory()
    {
        _saveTimer.Stop();
        Persist();
    }

    private void Persist()
    {
        _config.Save(new CommandsConfig { Categorias = [.. Categorias] });
        ContentChanged?.Invoke();
    }

    [RelayCommand]
    private void SaveAll()
    {
        if (SelectedCategoria != null)
        {
            SelectedCategoria.Nombre = EditCategoriaNombre;
            SelectedCategoria.Tipo = EditCategoriaTipo;
            SelectedCategoria.Icono = EditCategoriaIcono;
            SelectedCategoria.Descripcion = EditCategoriaDescripcion;
        }
        if (SelectedCommand != null)
        {
            SelectedCommand.Nombre = EditNombre;
            SelectedCommand.Descripcion = EditDescripcion;
            SelectedCommand.Shell = EditShell;
            SelectedCommand.Admin = EditAdmin;
            SelectedCommand.RequireConfirm = EditRequireConfirm;
            SelectedCommand.Variables.Clear();
            foreach (var v in EditVariables)
                SelectedCommand.Variables.Add(new CommandVariable { Name = v.Name, Label = v.Label, DefaultValue = v.DefaultValue, Sensitive = v.Sensitive, Required = v.Required });
            SelectedCommand.Pasos.Clear();
            foreach (var line in EditPasosText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                SelectedCommand.Pasos.Add(line.TrimEnd('\r'));
        }
        _config.Save(new CommandsConfig { Categorias = [.. Categorias] });
        Saved?.Invoke();
    }

    [RelayCommand]
    private void DeleteItem()
    {
        if (SelectedCategoria == null || SelectedCommand == null) return;
        SelectedCategoria.Items.Remove(SelectedCommand);
        SelectedCommand = null;
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedCommand != null)
            DeleteItem();
        else if (SelectedCategoria is { Fija: false })
            DeleteCategoria();
    }

    [RelayCommand]
    private void SelectIcon(string? iconName)
    {
        if (iconName != null)
            EditCategoriaIcono = iconName;
        IconSelected?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(CanMoveItemUp))]
    private void MoveItemUp()
    {
        int idx = SelectedCategoria!.Items.IndexOf(SelectedCommand!);
        SelectedCategoria.Items.Move(idx, idx - 1);
    }

    [RelayCommand(CanExecute = nameof(CanMoveItemDown))]
    private void MoveItemDown()
    {
        int idx = SelectedCategoria!.Items.IndexOf(SelectedCommand!);
        SelectedCategoria.Items.Move(idx, idx + 1);
    }

    private bool CanMoveItemUp => SelectedCommand != null && SelectedCategoria != null &&
                                  SelectedCategoria.Items.IndexOf(SelectedCommand) > 0;

    private bool CanMoveItemDown => SelectedCommand != null && SelectedCategoria != null &&
                                    SelectedCategoria.Items.IndexOf(SelectedCommand) < SelectedCategoria.Items.Count - 1;


    [RelayCommand(CanExecute = nameof(CanExecuteSelected))]
    private async Task ExecuteSelected()
    {
        if (SelectedCommand == null) return;

        var variables = new Dictionary<string, string>();
        if (SelectedCommand.Variables.Count > 0)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            stack.Children.Add(new TextBlock
            {
                Text = "Variables del comando",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            var inputs = new List<UIElement>();
            foreach (var v in SelectedCommand.Variables)
            {
                var label = new TextBlock
                {
                    Text = string.IsNullOrEmpty(v.Label) ? v.Name : v.Label,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                    Margin = new Thickness(0, 0, 0, 4)
                };
                stack.Children.Add(label);

                if (v.Sensitive)
                {
                    var pwBox = new System.Windows.Controls.PasswordBox
                    {
                        FontSize = 12,
                        Margin = new Thickness(0, 0, 0, 12),
                        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                        Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
                        BorderThickness = new Thickness(1),
                        MinWidth = 300
                    };
                    stack.Children.Add(pwBox);
                    inputs.Add(pwBox);
                }
                else
                {
                    var tb = new TextBox
                    {
                        Text = v.DefaultValue,
                        FontSize = 12,
                        Margin = new Thickness(0, 0, 0, 12),
                        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                        Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
                        BorderThickness = new Thickness(1),
                        MinWidth = 300
                    };
                    stack.Children.Add(tb);
                    inputs.Add(tb);
                }
            }

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            btnPanel.Children.Add(new Button
            {
                Content = "Cancelar",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 7, 14, 7),
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand,
                Command = DialogHost.CloseDialogCommand,
                CommandParameter = false
            });
            btnPanel.Children.Add(new Button
            {
                Content = "Ejecutar",
                Style = (Style)Application.Current.FindResource("OutlinedAccentButton"),
                Padding = new Thickness(14, 7, 14, 7),
                FontSize = 12,
                Margin = new Thickness(8, 0, 0, 0),
                Command = DialogHost.CloseDialogCommand,
                CommandParameter = true
            });
            stack.Children.Add(btnPanel);

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(24),
                MinWidth = 380,
                Child = stack
            };

            var result = await DialogHost.Show(border, "RootDialog");
            if (result is not true) return;

            for (int i = 0; i < SelectedCommand.Variables.Count; i++)
            {
                var input = inputs[i];
                if (input is System.Windows.Controls.PasswordBox pw)
                    variables[SelectedCommand.Variables[i].Name] = pw.Password;
                else if (input is TextBox tb)
                    variables[SelectedCommand.Variables[i].Name] = tb.Text;
            }
        }

        IsRunning = true;
        HasRunOutput = true;
        RunOutput = "";
        var dispatcher = Application.Current.Dispatcher;

        foreach (var paso in SelectedCommand.Pasos)
        {
            var resolvedPaso = VariableHelper.ReplaceVariables(paso, variables);
            RunOutput += $"> {resolvedPaso}\n";
            try
            {
                await _cmd.RunCommandStreamingAsync(
                    resolvedPaso,
                    onAppendLine: line => _ = dispatcher.BeginInvoke(() =>
                    {
                        RunOutput += line + "\n";
                        OnPropertyChanged(nameof(RunOutput));
                    }),
                    onReplaceLine: line => _ = dispatcher.BeginInvoke(() =>
                    {
                        int lastNl = RunOutput.LastIndexOf('\n', RunOutput.Length - 2);
                        RunOutput = lastNl >= 0
                            ? RunOutput[..(lastNl + 1)] + line
                            : line;
                        OnPropertyChanged(nameof(RunOutput));
                    }),
                    shell: SelectedCommand.Shell
                );
            }
            catch (Exception ex)
            {
                RunOutput += $"⛔ {ex.Message}\n";
            }
        }

        IsRunning = false;
        OnPropertyChanged(nameof(RunOutput));
    }

    private bool CanExecuteSelected => SelectedCommand != null;

    public void SelectTreeItem(object item)
    {
        if (item is CategoriaItem cat)
        {
            SelectedCategoria = cat;
            SelectedCommand = null;
        }
        else if (item is ComandoItem cmd)
        {
            // find parent category
            foreach (var c in Categorias)
            {
                if (c.Items.Contains(cmd))
                {
                    SelectedCategoria = c;
                    SelectedCommand = cmd;
                    return;
                }
            }
            SelectedCommand = cmd;
        }
    }
}
