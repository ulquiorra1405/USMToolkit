using System.IO;
using System.Text.Json;
using Toolkit.Models;

namespace Toolkit.Services;

public class ManualesService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private string _basePath = "";

    public void SetBasePath(string path)
    {
        _basePath = path;
    }

    public string GetBasePath() => _basePath;

    public bool IsPathValid()
    {
        if (string.IsNullOrEmpty(_basePath)) return false;
        try
        {
            return Directory.Exists(_basePath) || HasWritePermission(_basePath);
        }
        catch
        {
            return false;
        }
    }

    public bool HasWritePermission(string path)
    {
        try
        {
            var testDir = Path.Combine(path, ".permtest");
            Directory.CreateDirectory(testDir);
            Directory.Delete(testDir);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Escanea la carpeta base y devuelve todos los manuales encontrados.
    /// </summary>
    public List<ManualInfo> ScanAll()
    {
        var result = new List<ManualInfo>();

        if (string.IsNullOrEmpty(_basePath) || !Directory.Exists(_basePath))
            return result;

        foreach (var categoriaDir in Directory.GetDirectories(_basePath))
        {
            var categoria = Path.GetFileName(categoriaDir).ToLowerInvariant();
            foreach (var manualDir in Directory.GetDirectories(categoriaDir))
            {
                var manual = LoadManual(manualDir, categoria);
                if (manual != null)
                    result.Add(manual);
            }
        }

        return result;
    }

    /// <summary>
    /// Escanea una categoría específica.
    /// </summary>
    public List<ManualInfo> ScanCategoria(string categoria)
    {
        var result = new List<ManualInfo>();
        if (string.IsNullOrEmpty(_basePath) || !Directory.Exists(_basePath))
            return result;

        var catPath = Path.Combine(_basePath, categoria);
        if (!Directory.Exists(catPath))
        {
            Directory.CreateDirectory(catPath);
            return result;
        }

        foreach (var manualDir in Directory.GetDirectories(catPath))
        {
            var manual = LoadManual(manualDir, categoria);
            if (manual != null)
                result.Add(manual);
        }

        return result;
    }

    /// <summary>
    /// Carga un manual individual desde su carpeta.
    /// </summary>
    public ManualInfo? LoadManual(string manualDir, string categoria)
    {
        var indexMd = Path.Combine(manualDir, "index.md");
        var metaJson = Path.Combine(manualDir, "meta.json");

        if (!File.Exists(indexMd)) return null;

        var info = new ManualInfo
        {
            RutaCarpeta = manualDir,
            RutaIndex = indexMd,
            Categoria = categoria
        };

        // Intentar cargar meta.json
        if (File.Exists(metaJson))
        {
            try
            {
                var json = File.ReadAllText(metaJson);
                var meta = JsonSerializer.Deserialize<ManualMeta>(json, JsonOptions);
                if (meta != null)
                {
                    info.Title = meta.Title ?? "";
                    info.Tags = meta.Tags ?? [];
                    info.Autor = meta.Autor ?? "";
                    info.HasMeta = true;
                }
            }
            catch { /* Si falla el JSON, usamos fallback */ }
        }

        // Fallback: título desde primer heading
        if (string.IsNullOrEmpty(info.Title))
        {
            try
            {
                var content = File.ReadAllText(indexMd);
                var lines = content.Split('\n');
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("# ") && !trimmed.StartsWith("## "))
                    {
                        info.Title = trimmed[2..].Trim();
                        break;
                    }
                }
            }
            catch { info.Title = Path.GetFileName(manualDir); }
        }

        if (string.IsNullOrEmpty(info.Title))
            info.Title = Path.GetFileName(manualDir);

        // Fechas del filesystem
        try
        {
            var fileInfo = new FileInfo(indexMd);
            info.Creado = fileInfo.CreationTime;
            info.UltimaRevision = fileInfo.LastWriteTime;
        }
        catch
        {
            info.Creado = DateTime.MinValue;
            info.UltimaRevision = DateTime.MinValue;
        }

        return info;
    }

    /// <summary>
    /// Carga el contenido Markdown de un manual.
    /// </summary>
    public string LoadContent(ManualInfo manual)
    {
        try
        {
            if (File.Exists(manual.RutaIndex))
                return File.ReadAllText(manual.RutaIndex);
        }
        catch { }
        return "";
    }

    /// <summary>
    /// Crea un nuevo manual: carpeta + index.md + meta.json + images/.
    /// </summary>
    public ManualInfo? Create(string title, string categoria, List<string>? tags, string autor)
    {
        if (string.IsNullOrEmpty(_basePath)) return null;

        var slug = Slugify(title);
        var catPath = Path.Combine(_basePath, categoria);
        var manualPath = Path.Combine(catPath, slug);
        var imagesPath = Path.Combine(manualPath, "images");
        var indexPath = Path.Combine(manualPath, "index.md");
        var metaPath = Path.Combine(manualPath, "meta.json");

        try
        {
            Directory.CreateDirectory(imagesPath);

            var now = DateTime.Now;

            // Plantilla index.md
            var template = $"# {title}\n\n## Descripción\n\nBreve descripción del problema o procedimiento.\n\n## Pasos\n\n1. \n2. \n3. \n\n## Notas\n\n";
            File.WriteAllText(indexPath, template);

            // meta.json
            var meta = new ManualMeta
            {
                Title = title,
                Tags = tags ?? [],
                Autor = autor
            };
            var json = JsonSerializer.Serialize(meta, JsonOptions);
            File.WriteAllText(metaPath, json);

            // Crear settings de VS Code para paste de imágenes
            EnsureVscodeSettings();

            return new ManualInfo
            {
                Title = title,
                Categoria = categoria,
                Tags = tags ?? [],
                Autor = autor,
                Creado = now,
                UltimaRevision = now,
                RutaCarpeta = manualPath,
                RutaIndex = indexPath,
                HasMeta = true
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Elimina un manual completo.
    /// </summary>
    public bool Delete(ManualInfo manual)
    {
        try
        {
            if (Directory.Exists(manual.RutaCarpeta))
                Directory.Delete(manual.RutaCarpeta, recursive: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Crea el .vscode/settings.json para paste automático de imágenes.
    /// </summary>
    private void EnsureVscodeSettings()
    {
        if (string.IsNullOrEmpty(_basePath)) return;
        var vscodeDir = Path.Combine(_basePath, ".vscode");
        var settingsFile = Path.Combine(vscodeDir, "settings.json");
        if (File.Exists(settingsFile)) return;

        try
        {
            Directory.CreateDirectory(vscodeDir);
            var settings = new
            {
                markdown = new
                {
                    copyFiles = new
                    {
                        destination = new Dictionary<string, string>
                        {
                            ["*"] = "images/"
                        }
                    }
                }
            };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsFile, json);
        }
        catch { }
    }

    private static string Slugify(string text)
    {
        var slug = text.ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('ñ', 'n')
            .Replace('á', 'a').Replace('é', 'e').Replace('í', 'i').Replace('ó', 'o').Replace('ú', 'u')
            .Replace('ü', 'u');
        // Eliminar caracteres no válidos
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-{2,}", "-");
        slug = slug.Trim('-');
        return string.IsNullOrEmpty(slug) ? "manual" : slug;
    }
}

public class ManualMeta
{
    public string? Title { get; set; }
    public List<string>? Tags { get; set; }
    public string? Autor { get; set; }
}
