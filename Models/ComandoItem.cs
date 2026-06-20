using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Toolkit.Models;

public class CommandVariable : INotifyPropertyChanged
{
    private string _name = "";
    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
    }

    private string _label = "";
    public string Label
    {
        get => _label;
        set { if (_label != value) { _label = value; PropertyChanged?.Invoke(this, new(nameof(Label))); } }
    }

    private string _defaultValue = "";
    public string DefaultValue
    {
        get => _defaultValue;
        set { if (_defaultValue != value) { _defaultValue = value; PropertyChanged?.Invoke(this, new(nameof(DefaultValue))); } }
    }

    private bool _sensitive;
    public bool Sensitive
    {
        get => _sensitive;
        set { if (_sensitive != value) { _sensitive = value; PropertyChanged?.Invoke(this, new(nameof(Sensitive))); } }
    }

    private bool _required;
    public bool Required
    {
        get => _required;
        set { if (_required != value) { _required = value; PropertyChanged?.Invoke(this, new(nameof(Required))); } }
    }

    [field: NonSerialized]
    public event PropertyChangedEventHandler? PropertyChanged;
}



public class ComandoItem : INotifyPropertyChanged
{
    private string _nombre = "";
    public string Nombre
    {
        get => _nombre;
        set { if (_nombre != value) { _nombre = value; PropertyChanged?.Invoke(this, new(nameof(Nombre))); } }
    }

    private string _descripcion = "";
    public string Descripcion
    {
        get => _descripcion;
        set { if (_descripcion != value) { _descripcion = value; PropertyChanged?.Invoke(this, new(nameof(Descripcion))); } }
    }

    public bool Admin { get; set; }

    private string _shell = "cmd";
    public string Shell
    {
        get => _shell;
        set { if (_shell != value) { _shell = value; PropertyChanged?.Invoke(this, new(nameof(Shell))); } }
    }

    private bool _requireConfirm;
    public bool RequireConfirm
    {
        get => _requireConfirm;
        set { if (_requireConfirm != value) { _requireConfirm = value; PropertyChanged?.Invoke(this, new(nameof(RequireConfirm))); } }
    }

    public ObservableCollection<string> Pasos { get; set; } = [];

    public ObservableCollection<CommandVariable> Variables { get; set; } = [];

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set { if (_isVisible != value) { _isVisible = value; PropertyChanged?.Invoke(this, new(nameof(IsVisible))); } }
    }

    [field: NonSerialized]
    public event PropertyChangedEventHandler? PropertyChanged;
}

public class CategoriaItem : INotifyPropertyChanged
{
    private string _nombre = "";
    public string Nombre
    {
        get => _nombre;
        set { if (_nombre != value) { _nombre = value; PropertyChanged?.Invoke(this, new(nameof(Nombre))); } }
    }

    private string _descripcion = "";
    public string Descripcion
    {
        get => _descripcion;
        set { if (_descripcion != value) { _descripcion = value; PropertyChanged?.Invoke(this, new(nameof(Descripcion))); } }
    }

    private string _tipo = "Despliegue";
    public string Tipo
    {
        get => _tipo;
        set { if (_tipo != value) { _tipo = value; PropertyChanged?.Invoke(this, new(nameof(Tipo))); } }
    }

    private bool _fija;
    public bool Fija
    {
        get => _fija;
        set { if (_fija != value) { _fija = value; PropertyChanged?.Invoke(this, new(nameof(Fija))); } }
    }

    private string _icono = "FolderOutline";
    public string Icono
    {
        get => _icono;
        set { if (_icono != value) { _icono = value; PropertyChanged?.Invoke(this, new(nameof(Icono))); } }
    }

    public ObservableCollection<ComandoItem> Items { get; set; } = [];

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set { if (_isVisible != value) { _isVisible = value; PropertyChanged?.Invoke(this, new(nameof(IsVisible))); } }
    }

    [field: NonSerialized]
    public event PropertyChangedEventHandler? PropertyChanged;
}
