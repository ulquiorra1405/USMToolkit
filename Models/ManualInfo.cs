namespace Toolkit.Models;

public class ManualInfo
{
    public string Title { get; set; } = "";
    public string Categoria { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public string Autor { get; set; } = "";
    public DateTime Creado { get; set; }
    public DateTime UltimaRevision { get; set; }
    public string RutaCarpeta { get; set; } = "";
    public string RutaIndex { get; set; } = "";
    public string ContenidoMd { get; set; } = "";
    public bool HasMeta { get; set; }

    /// <summary>
    /// Categoría amigable para mostrar: "instalacion" → "Instalación", "resolucion" → "Resolución"
    /// </summary>
    public string CategoriaDisplay => Categoria switch
    {
        "instalacion" => "Instalación",
        "resolucion" => "Resolución",
        _ => Categoria
    };

    public string UltimaRevisionDisplay => UltimaRevision.ToString("dd MMM yyyy");
    public string TagsDisplay => Tags.Count > 0 ? string.Join(", ", Tags) : "";
}
