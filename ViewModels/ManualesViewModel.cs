using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Toolkit.Models;
using Toolkit.Services;

namespace Toolkit.ViewModels;

public partial class ManualesViewModel : ObservableObject
{
    private readonly ManualesService _service = new();
    private readonly MarkdownRenderer _renderer = new();
    private List<ManualInfo> _allManuals = [];

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private bool _isEditingEnabled;

    [ObservableProperty]
    private string _emptyMessage = "No hay manuales aún.\nCrea el primero para empezar.";

    [ObservableProperty]
    private string _selectedCategoryFilter = "";

    public ObservableCollection<string> AvailableCategories { get; } = ["Todas", "Instalación", "Resolución"];

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private bool _isViewerVisible;

    [ObservableProperty]
    private ManualInfo? _selectedManual;

    [ObservableProperty]
    private FlowDocument? _renderedContent;

    [ObservableProperty]
    private string _viewerTitle = "";

    [ObservableProperty]
    private string _viewerMeta = "";

    public ObservableCollection<ManualInfo> Manuals { get; } = [];

    public ManualesService Service => _service;

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter(value?.Trim() ?? "");
    }

    partial void OnSelectedCategoryFilterChanged(string value)
    {
        ApplyFilter(SearchQuery?.Trim() ?? "");
    }

    public void ReRenderCurrent()
    {
        if (SelectedManual != null && IsViewerVisible)
        {
            var content = _service.LoadContent(SelectedManual);
            SelectedManual.ContenidoMd = content;
            var doc = _renderer.Render(content, SelectedManual.RutaIndex);
            RenderedContent = doc;
        }
    }

    public void SetBasePath(string path)
    {
        _service.SetBasePath(path);
        Reload();
    }

    public void Reload()
    {
        _allManuals = _service.ScanAll();
        _allManuals.Sort((a, b) => b.UltimaRevision.CompareTo(a.UltimaRevision));
        ApplyFilter(SearchQuery?.Trim() ?? "");
    }

    private void ApplyFilter(string query)
    {
        Manuals.Clear();

        var filtered = _allManuals.AsEnumerable();

        // Filtrar por categoría
        var catFilter = SelectedCategoryFilter;
        if (!string.IsNullOrEmpty(catFilter) && catFilter != "Todas")
        {
            var catKey = catFilter switch
            {
                "Instalación" => "instalacion",
                "Resolución" => "resolucion",
                _ => null
            };
            if (catKey != null)
                filtered = filtered.Where(m =>
                    string.Equals(m.Categoria, catKey, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(query))
        {
            var q = query;
            filtered = filtered.Where(m =>
                m.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                m.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var manual in filtered)
            Manuals.Add(manual);

        EmptyMessage = "No hay manuales aún.\nCrea el primero para empezar.";
        IsEmpty = Manuals.Count == 0;
    }

    [RelayCommand]
    private void OpenManual(ManualInfo? manual)
    {
        if (manual == null) return;

        var content = _service.LoadContent(manual);
        manual.ContenidoMd = content;
        SelectedManual = manual;

        var doc = _renderer.Render(content, manual.RutaIndex);
        RenderedContent = doc;

        ViewerTitle = manual.Title;
        ViewerMeta = $"Última revisión: {manual.UltimaRevisionDisplay}";
        if (!string.IsNullOrEmpty(manual.Autor))
            ViewerMeta += $" | Autor: {manual.Autor}";

        IsViewerVisible = true;
    }

    [RelayCommand]
    private void BackToList()
    {
        IsViewerVisible = false;
        SelectedManual = null;
        RenderedContent = null;
    }

    [RelayCommand]
    private void EditManual(ManualInfo? manual)
    {
        if (manual == null || !IsEditingEnabled) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "code",
                Arguments = $"\"{manual.RutaCarpeta}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            // Fallback a bloc de notas
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad",
                    Arguments = $"\"{manual.RutaIndex}\"",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    [RelayCommand]
    private void OpenFolder(ManualInfo? manual)
    {
        if (manual == null) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer",
                Arguments = $"\"{manual.RutaCarpeta}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void DeleteManual(ManualInfo? manual)
    {
        if (manual == null) return;

        var result = MessageBox.Show(
            $"¿Eliminar \"{manual.Title}\"?\nEsta acción no se puede deshacer.",
            "Eliminar manual",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        if (_service.Delete(manual))
        {
            if (SelectedManual == manual)
                BackToList();
            Reload();
        }
    }

    [RelayCommand]
    private void NewManual()
    {
        // Crear diálogo simple
        var dialog = new Views.NewManualDialog();
        if (dialog.ShowDialog() != true) return;

        var title = dialog.ManualTitle;
        var categoria = dialog.ManualCategoria;
        var tags = dialog.ManualTags?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList() ?? [];

        var autor = Environment.UserName;

        var created = _service.Create(title, categoria, tags, autor);
        if (created == null)
        {
            MessageBox.Show(
                "No se pudo crear el manual. Verifica que la ruta esté configurada y tengas permisos de escritura.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        // Agregar a la lista y abrir
        _allManuals.Add(created);
        _allManuals.Sort((a, b) => b.UltimaRevision.CompareTo(a.UltimaRevision));
        ApplyFilter(SearchQuery?.Trim() ?? "");

        // Abrir en VS Code
        OpenManualCommand.Execute(created);
        EditManualCommand.Execute(created);
    }
}
