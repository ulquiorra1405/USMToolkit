<div align="center">
  <img src="Ico/favicon.ico" width="64" height="64" alt="Toolkit Logo"/>
  <h1>Toolkit</h1>
  <p>Suite de administración IT para Windows</p>
  <p>
    <strong>.NET 9 · WPF · Material Design · MVVM</strong>
  </p>
</div>

---

## 📝 Descripción

Toolkit es una herramienta todo-en-uno para técnicos de sistemas y administradores IT. Permite ejecutar despliegues de software, scripts categorizados, monitorear hardware en vivo, consultar garantías de equipos y administrar configuraciones desde una interfaz moderna.

---

## ✨ Funcionalidades

### 📊 Dashboard
- Monitoreo en vivo de CPU, GPU, temperaturas y almacenamiento
- Indicador de red local vs remota
- Acceso rápido a comandos favoritos

### 📦 Deployments
- Instalación automatizada de software mediante `winget`
- Categorías configurables desde `commands.json`
- Log en tiempo real con salida formateada

### ⚡ Ejecuciones
- Scripts categorizados con variables personalizables (`{{variable}}`)
- Soporte para CMD y PowerShell
- Confirmación obligatoria para comandos sensibles
- Ejecución como administrador

### ✏️ Editor de comandos
- GUI para crear, editar y organizar scripts
- Variables con valores por defecto, sensibles y obligatorias
- Categorías arrastrables

### 🖥️ Diagnósticos
- Información del sistema vía WMI
- Consulta de garantía para Lenovo, HP y Dell mediante web scraping

### ⚙️ Configuración
- Ruta de red compartida (`commands.json` centralizado para equipos)
- Tema claro/oscuro

---

## 🛠️ Stack

| Capa | Tecnología |
|---|---|
| **Lenguaje** | C# 13 |
| **Framework** | .NET 9.0, WPF |
| **MVVM** | CommunityToolkit.Mvvm 8.4.2 |
| **UI** | MaterialDesignThemes 5.3.2 |
| **Hardware** | LibreHardwareMonitorLib 0.9.6 |
| **WMI** | System.Management |
| **Web scraping** | HttpClient + WebView2 |
| **Config** | JSON local / red (`commands.json`) |
| **Tests** | Playwright |

---

## 🚀 Cómo compilar

### Requisitos
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Windows (WPF)

### Compilar y ejecutar
```bash
git clone https://github.com/<tu-usuario>/Toolkit.git
cd Toolkit
dotnet run -c Release
```

### Publicar como ejecutable independiente
```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

---

## 📁 Estructura del proyecto

```
Toolkit/
├── App.xaml / App.xaml.cs          # Entry point
├── MainWindow.xaml / .cs           # Ventana principal con sidebar + contenido dinámico
├── Views/                          # Vistas de cada módulo
├── ViewModels/                     # ViewModels (Dashboard, Deployments, Ejecuciones, etc.)
├── Models/                         # Modelos (ComandoItem, CategoriaItem, etc.)
├── Services/                       # Servicios (CommandService, WarrantyService, HardwareMonitor...)
├── Controls/                       # Controles personalizados
├── Converters/                     # Value converters
├── Styles/                         # Estilos y recursos XAML
├── Ico/                            # Iconos
├── playwright-test/                # Tests de integración con Playwright
└── Toolkit.csproj                  # Proyecto .NET 9
```

---

## ⚙️ Configuración

Los comandos y categorías se definen en `commands.json`. Puedes:

- **Local**: el archivo se carga desde el directorio del ejecutable
- **Red**: configura una ruta de red compartida en Settings para que todo el equipo use el mismo `commands.json`
- **Migración automática**: si tienes datos en `%APPDATA%\Toolkit\commands.json`, se migran solos al abrir la app

---

## 📄 Licencia

Uso personal / interno.
