using System.Collections.ObjectModel;

namespace Toolkit.Models;

public class ApplicationItem
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Shell { get; set; } = "cmd";
    public ObservableCollection<string> Pasos { get; set; } = [];
    public ObservableCollection<CommandVariable> Variables { get; set; } = [];
    public bool IsInstalled { get; set; }
    public bool IsSelected { get; set; }
    public bool IsClickToRun { get; set; }
    public bool RequireConfirm { get; set; }
    public string Icono { get; set; } = "PlayArrow";
    public string Status => IsInstalled ? "Completado" : "Disponible";
}
