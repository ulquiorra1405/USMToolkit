using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

public partial class DeploymentsViewModel : ObservableObject
{
    private readonly CommandService _cmd = new();
    private readonly CommandsConfigService _config = new();
    private CancellationTokenSource? _cts;
    private List<ApplicationItem> _loadedApps = [];
    private readonly Stopwatch _sw = new();
    private readonly DispatcherTimer _progressTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    public ObservableCollection<ApplicationItem> Applications { get; } = [];

    [ObservableProperty]
    private string _searchQuery = "";

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter(value?.Trim() ?? "");
    }

    private void ApplyFilter(string query)
    {
        Applications.Clear();
        if (string.IsNullOrEmpty(query))
        {
            foreach (var app in _loadedApps)
                Applications.Add(app);
            return;
        }
        var q = query.AsSpan();
        foreach (var app in _loadedApps)
        {
            if (app.Name.AsSpan().Contains(q, StringComparison.OrdinalIgnoreCase) ||
                app.Description.AsSpan().Contains(q, StringComparison.OrdinalIgnoreCase))
                Applications.Add(app);
        }
    }

    [ObservableProperty]
    private string _logOutput = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _progressMessage = "";

    [ObservableProperty]
    private bool _isSnackbarActive;

    [ObservableProperty]
    private string _actionVerb = "Instalando";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private double _progressMax = 100;

    [ObservableProperty]
    private double _progressStep;

    [ObservableProperty]
    private double _progressStepMax = 1;

    public double ProgressPercent => ProgressStepMax > 0 ? ProgressStep / ProgressStepMax * 100 : 0;

    private string ElapsedDisplay => _sw.Elapsed.TotalSeconds < 60
        ? $"{_sw.Elapsed.Seconds}s"
        : $"{_sw.Elapsed.Minutes}m {_sw.Elapsed.Seconds}s";

    private string _currentAppName = "";

    [ObservableProperty]
    private CategoriaItem? _selectedCategory;

    partial void OnSelectedCategoryChanged(CategoriaItem? value)
    {
        LoadFromConfig();
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
    }

    public string Title => SelectedCategory?.Nombre ?? "Despliegues";
    public string Description => SelectedCategory?.Descripcion ?? "Aplicaciones disponibles";

    public DeploymentsViewModel()
    {
        _progressTimer.Tick += (_, _) =>
        {
            if (!_sw.IsRunning) return;
            var pct = ProgressStepMax > 0 ? (int)(ProgressStep / ProgressStepMax * 100) : 0;
            ProgressMessage = $"{ActionVerb} {_currentAppName} — {pct}% {ElapsedDisplay}";
        };
        LoadFromConfig();
    }

    public void LoadFromConfig()
    {
        _loadedApps.Clear();
        var cfg = _config.Load();
        var source = SelectedCategory != null
            ? cfg.Categorias.Where(c => c.Nombre == SelectedCategory.Nombre)
            : cfg.Categorias.Where(c => c.Tipo == "Despliegue");

        foreach (var cat in source)
        {
            foreach (var item in cat.Items)
            {
                var app = new ApplicationItem
                {
                    Name = item.Nombre,
                    Description = item.Descripcion,
                    RequireConfirm = item.RequireConfirm,
                    Shell = string.IsNullOrEmpty(item.Shell) ? "cmd" : item.Shell
                };
                foreach (var paso in item.Pasos)
                    app.Pasos.Add(paso);
                foreach (var v in item.Variables)
                    app.Variables.Add(new CommandVariable { Name = v.Name, Label = v.Label, DefaultValue = v.DefaultValue, Sensitive = v.Sensitive, Required = v.Required });
                _loadedApps.Add(app);
            }
        }
        ApplyFilter(SearchQuery);
    }

    [RelayCommand]
    private async Task InstallSelectedAsync()
    {
        var selected = Applications.Where(a => a.IsSelected && !a.IsInstalled).ToList();
        if (selected.Count == 0) return;

        var totalSteps = selected.Sum(a => a.Pasos.Count);
        IsBusy = true;
        IsSnackbarActive = true;
        ProgressMax = selected.Count;
        ProgressValue = 0;
        ProgressStepMax = totalSteps;
        ProgressStep = 0;
        _sw.Restart();
        _progressTimer.Start();
        _currentAppName = selected.FirstOrDefault()?.Name ?? "";
        ProgressMessage = $"{ActionVerb} aplicaciones... 0% 0s";
        LogOutput = "Iniciando...\n";
        _cts = new CancellationTokenSource();

        var dispatcher = Application.Current.Dispatcher;
        var completedApps = 0;

        try
        {
            foreach (var app in selected)
            {
                _cts.Token.ThrowIfCancellationRequested();
                _currentAppName = app.Name;

                // Collect variables for this app
                var appVariables = new Dictionary<string, string>();
                if (app.Variables.Count > 0)
                {
                    var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
                    stack.Children.Add(new TextBlock
                    {
                        Text = $"Variables para \"{app.Name}\"",
                        FontSize = 16,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8)),
                        Margin = new Thickness(0, 0, 0, 16)
                    });
                    var inputs = new List<UIElement>();
                    foreach (var v in app.Variables)
                    {
                        stack.Children.Add(new TextBlock
                        {
                            Text = string.IsNullOrEmpty(v.Label) ? v.Name : v.Label,
                            FontSize = 11,
                            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                            Margin = new Thickness(0, 0, 0, 4)
                        });
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
                    var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
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
                    var vResult = await DialogHost.Show(new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(24),
                        MinWidth = 380,
                        Child = stack
                    }, "RootDialog");
                    if (vResult is not true) return;
                    for (int i = 0; i < app.Variables.Count; i++)
                    {
                        var input = inputs[i];
                        if (input is System.Windows.Controls.PasswordBox pw)
                            appVariables[app.Variables[i].Name] = pw.Password;
                        else if (input is TextBox tb)
                            appVariables[app.Variables[i].Name] = tb.Text;
                    }
                }

                LogOutput += $"\n▶ {ActionVerb} {app.Name} ({app.Pasos.Count} pasos)...\n";

                foreach (var paso in app.Pasos)
                {
                    var resolvedPaso = VariableHelper.ReplaceVariables(paso, appVariables);
                    _cts.Token.ThrowIfCancellationRequested();
                    ProgressStep++;
                    OnPropertyChanged(nameof(ProgressPercent));
                    var pct = (int)(ProgressStep / ProgressStepMax * 100);
                    ProgressMessage = $"{ActionVerb} {app.Name} — {pct}% {ElapsedDisplay}";

                    try
                    {
                        await _cmd.RunCommandStreamingAsync(
                            resolvedPaso,
                            onAppendLine: line => _ = dispatcher.BeginInvoke(() =>
                            {
                                LogOutput += line + "\n";
                                OnPropertyChanged(nameof(LogOutput));
                            }),
                            onReplaceLine: line => _ = dispatcher.BeginInvoke(() =>
                            {
                                int lastNl = LogOutput.LastIndexOf('\n', LogOutput.Length - 2);
                                LogOutput = lastNl >= 0
                                    ? LogOutput[..(lastNl + 1)] + line
                                    : line;
                                OnPropertyChanged(nameof(LogOutput));
                            }),
                        ct: _cts.Token,
                        shell: app.Shell
                    );
                    }
                    catch (OperationCanceledException) { throw; }
                    catch
                    {
                        _ = dispatcher.BeginInvoke(() =>
                        {
                            LogOutput += "⛔ Error en paso.\n";
                            OnPropertyChanged(nameof(LogOutput));
                        });
                    }
                }

                app.IsInstalled = true;
                completedApps++;
                ProgressValue = completedApps;
            }

            if (!_cts.Token.IsCancellationRequested)
            {
                _sw.Stop();
                _progressTimer.Stop();
                ProgressMessage = $"✅ Instalación completada ({ElapsedDisplay})";
                _ = dispatcher.BeginInvoke(() =>
                {
                    LogOutput += "\n✅ Proceso completado.";
                    OnPropertyChanged(nameof(LogOutput));
                });
            }
        }
        catch (OperationCanceledException)
        {
            ProgressMessage = "⛔ Proceso interrumpido";
            _ = dispatcher.BeginInvoke(() =>
            {
                LogOutput += "\n⛔ Proceso interrumpido.";
                OnPropertyChanged(nameof(LogOutput));
            });
        }
        finally
        {
            IsBusy = false;
            _progressTimer.Stop();
            _cts?.Dispose();
            _cts = null;
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                await dispatcher.BeginInvoke(() => IsSnackbarActive = false);
            });
        }
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogOutput = "";
    }

    [RelayCommand]
    private void Interrupt()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private void SelectAll()
    {
        bool allSelected = Applications.All(a => a.IsSelected);
        foreach (var app in Applications)
            app.IsSelected = !allSelected;
    }
}
