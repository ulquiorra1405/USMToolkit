using System.IO;
using System.Windows;
using Toolkit.Services;

namespace Toolkit;

public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Toolkit", "crash.log");

    public App()
    {
        ThemeService.InitializeDark();
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        File.AppendAllText(LogPath, $"\n=== App started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");

        DispatcherUnhandledException += (s, e) =>
        {
            File.AppendAllText(LogPath, $"[Dispatcher] {e.Exception}\n");
            e.Handled = false;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            File.AppendAllText(LogPath, $"[AppDomain] {(e.ExceptionObject as Exception)?.ToString() ?? e.ExceptionObject?.ToString()}\n");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            File.AppendAllText(LogPath, $"[TaskScheduler] {e.Exception}\n");
            e.SetObserved();
        };
    }

    public static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
        }
        catch { }
    }
}
