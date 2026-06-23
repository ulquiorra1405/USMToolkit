# Módulo de Manuales — Diseño

> Fecha: 23-jun-2026
> Estado: Diseño aprobado, pendiente de implementación

## Sidebar

```
🏠 Dashboard
📖 Manuales                  ← BookOpenOutline
📦 Instalación              ← existente
✏️ Editor
⚙️ Ajustes
```

Un solo item que carga todos los manuales. La categoría (Instalación / Resolución) se muestra como badge en cada card.

## Estructura de archivos

```
manuales/
├── instalacion/titulo/
│   ├── index.md
│   ├── meta.json
│   └── images/
├── resolucion/titulo/
│   ├── index.md
│   ├── meta.json
│   └── images/
└── .vscode/
    └── settings.json
```

### meta.json

```json
{
  "title": "VPN - Error de autenticación",
  "categoria": "resolucion",
  "tags": ["vpn", "fortinet"],
  "autor": "Bryan"
}
```

- `ultima_revision` y `creado` → `File.GetLastWriteTime` / `File.GetCreationTime`
- Fallback: si falta `meta.json`, título se extrae del primer `# heading`
- Fallback: si falta categoría, se infiere de la carpeta padre

## Visor

- **Renderizado:** Markdown.Xaml (NuGet) — texto + imágenes inline
- **Categorías:** filtro por Instalación / Resolución
- **Orden:** fecha de modificación (más reciente primero)
- **Buscador:** global reutilizado, hint dinámico ("Buscar manuales...")
- **Editar:** botón condicional (según check en Settings)
- **Log:** contador de aperturas
- **Estado vacío:** mensaje "No hay manuales aún. Crea el primero" + botón

## Creación de manuales

Diálogo: Título, Categoría (dropdown), Tags (opcional).
Acción: genera carpeta + `index.md` (plantilla) + `meta.json` + `images/`, abre VS Code.

## Settings

- Ruta de manuales (configurable)
- Check "Permitir edición de manuales"

## Edición externa

`Process.Start("code", rutaCarpeta)` → abre VS Code en la carpeta del manual.

## Dependencias

- `Markdown.Xaml` (NuGet) — ~350 KB en Release
