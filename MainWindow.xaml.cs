using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using MaterialDesignThemes.Wpf;
using Toolkit.ViewModels;

namespace Toolkit;

public partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    private enum WindowMode { Normal, Maximized, Fullscreen }
    private WindowMode _windowMode = WindowMode.Fullscreen;
    private Rect _normalRect;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        Loaded += OnLoaded;
        Closing += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                vm.Dashboard.StopRefresh();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _normalRect = new Rect(Left, Top, Width, Height);
        _windowMode = WindowMode.Fullscreen;
        ApplyCurrentMode();

        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            _ = vm.Dashboard.LoadAsync();
            vm.Deployments.PropertyChanged += OnProgressPropertyChanged;
            vm.Ejecuciones.PropertyChanged += OnProgressPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsSidebarExpanded) && DataContext is MainViewModel vm)
        {
            var target = vm.IsSidebarExpanded ? 180.0 : 48.0;
            var anim = new DoubleAnimation(target, TimeSpan.FromSeconds(0.25))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            SidebarPanel.BeginAnimation(FrameworkElement.WidthProperty, anim);
        }
    }

    private void OnProgressPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DeploymentsViewModel.LogOutput))
        {
            Dispatcher.BeginInvoke(() =>
                LogScrollViewer?.ScrollToEnd());
        }
        else if (e.PropertyName is nameof(DeploymentsViewModel.IsSnackbarActive) or nameof(EjecucionesViewModel.IsSnackbarActive) && DataContext is MainViewModel vm)
        {
            bool active = (sender is DeploymentsViewModel d && d.IsSnackbarActive)
                       || (sender is EjecucionesViewModel ej && ej.IsSnackbarActive);
            Dispatcher.BeginInvoke(() =>
            {
                var target = active ? 1.0 : 0.0;
                var anim = new DoubleAnimation(target, TimeSpan.FromSeconds(0.3))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };
                ProgressSnackbar.BeginAnimation(UIElement.OpacityProperty, anim);
            });
        }
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            CycleWindowMode();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e) => CycleWindowMode();

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void CycleWindowMode()
    {
        if (WindowState == WindowState.Normal && _windowMode != WindowMode.Fullscreen)
            _normalRect = new Rect(Left, Top, Width, Height);

        _windowMode = _windowMode switch
        {
            WindowMode.Normal => WindowMode.Maximized,
            WindowMode.Maximized => WindowMode.Fullscreen,
            WindowMode.Fullscreen => WindowMode.Normal,
            _ => WindowMode.Normal
        };
        ApplyCurrentMode();
    }

    private void ApplyCurrentMode()
    {
        switch (_windowMode)
        {
            case WindowMode.Normal:
                WindowState = WindowState.Normal;
                ResizeMode = ResizeMode.CanResize;
                Topmost = false;
                if (_normalRect.Width > 0)
                {
                    Left = _normalRect.Left;
                    Top = _normalRect.Top;
                    Width = _normalRect.Width;
                    Height = _normalRect.Height;
                }
                RootGrid.Margin = new Thickness(0);
                if (LogBorder != null) LogBorder.Margin = new Thickness(0);
                break;
            case WindowMode.Maximized:
                WindowState = WindowState.Maximized;
                ResizeMode = ResizeMode.CanResize;
                Topmost = false;
                RootGrid.Margin = new Thickness(7);
                if (LogBorder != null) LogBorder.Margin = new Thickness(0, 0, 0, 35);
                break;
            case WindowMode.Fullscreen:
                var hwnd = new WindowInteropHelper(this).Handle;
                var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
                GetMonitorInfo(monitor, ref mi);
                WindowState = WindowState.Normal;
                Left = mi.rcMonitor.Left;
                Top = mi.rcMonitor.Top;
                Width = mi.rcMonitor.Right - mi.rcMonitor.Left;
                Height = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
                ResizeMode = ResizeMode.NoResize;
                Topmost = true;
                RootGrid.Margin = new Thickness(0);
                if (LogBorder != null) LogBorder.Margin = new Thickness(0);
                break;
        }

        if (MaxIcon != null)
        {
            MaxIcon.Kind = _windowMode switch
            {
                WindowMode.Normal => PackIconKind.WindowMaximize,
                WindowMode.Maximized => PackIconKind.WindowRestore,
                WindowMode.Fullscreen => PackIconKind.FullscreenExit,
                _ => PackIconKind.WindowMaximize
            };
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_windowMode == WindowMode.Fullscreen) return;

        if (WindowState == WindowState.Normal && _windowMode != WindowMode.Normal)
        {
            _windowMode = WindowMode.Normal;
            ResizeMode = ResizeMode.CanResize;
            Topmost = false;
            if (_normalRect.Width <= 0)
                _normalRect = new Rect(Left, Top, Width, Height);
            RootGrid.Margin = new Thickness(0);
            if (LogBorder != null) LogBorder.Margin = new Thickness(0);
            if (MaxIcon != null) MaxIcon.Kind = PackIconKind.WindowMaximize;
        }
        else if (WindowState == WindowState.Maximized && _windowMode == WindowMode.Normal)
        {
            _windowMode = WindowMode.Maximized;
            RootGrid.Margin = new Thickness(7);
            if (LogBorder != null) LogBorder.Margin = new Thickness(0, 0, 0, 35);
            if (MaxIcon != null) MaxIcon.Kind = PackIconKind.WindowRestore;
        }
    }
}
