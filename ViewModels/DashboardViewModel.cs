using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Toolkit.Models;
using Toolkit.Services;

namespace Toolkit.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(76, 175, 80));
    private static readonly Brush Amber = new SolidColorBrush(Color.FromRgb(245, 166, 35));
    private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(244, 67, 54));
    private static readonly Brush Grey = new SolidColorBrush(Color.FromRgb(102, 102, 102));

    private readonly SystemInfoService _sys = new();
    private readonly HardwareMonitorService _hw = new();
    private readonly WarrantyService _warranty = new();
    private readonly CommandsConfigService _cmdConfig = new();
    private readonly CommandService _cmdRunner = new();
    private DispatcherTimer? _refreshTimer;

    public ObservableCollection<ComandoItem> QuickActions { get; } = [];

    [ObservableProperty]
    private string _osVersion = "Cargando...";

    [ObservableProperty]
    private string _osInstallDate = "Cargando...";

    [ObservableProperty]
    private string _uptime = "Cargando...";

    [ObservableProperty]
    private string _cpuInfo = "Cargando...";

    [ObservableProperty]
    private string _memoryUsage = "Cargando...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MemoryUsageColor))]
    private double _memoryPercent;

    [ObservableProperty]
    private string _diskInfo = "Cargando...";

    [ObservableProperty]
    private string _networkInfo = "Cargando...";

    [ObservableProperty]
    private string _networkTraffic = "Haz clic para probar";

    [ObservableProperty]
    private bool _isSpeedTesting;

    [ObservableProperty]
    private string _batteryInfo = "Cargando...";

    [ObservableProperty]
    private double _batteryPercent;

    [ObservableProperty]
    private bool _hasBattery;

    [ObservableProperty]
    private string _gpuInfo = "Cargando...";

    [ObservableProperty]
    private string _ramSpecs = "Cargando...";

    [ObservableProperty]
    private string _motherboardInfo = "Cargando...";

    [ObservableProperty]
    private string _biosInfo = "Cargando...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CpuTempColor))]
    private string _cpuTemp = "Cargando...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CpuTempColor))]
    private double _cpuTempValue = -1;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GpuTempColor))]
    private string _gpuTemp = "Cargando...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GpuTempColor))]
    private double _gpuTempValue = -1;

    [ObservableProperty]
    private string _warrantyInfo = "Consultando...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CpuUsageColor))]
    private double _cpuUsagePercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GpuUsageColor))]
    private double _gpuUsagePercent;

    [ObservableProperty]
    private string _diskSmartInfo = "Cargando...";

    [ObservableProperty]
    private string _diskTemp = "";

    [ObservableProperty]
    private string _powerPlan = "Cargando...";

    [ObservableProperty]
    private string _pendingUpdates = "Cargando...";

    [ObservableProperty]
    private string _recentErrorCount = "Cargando...";

    [ObservableProperty]
    private string _recentCriticalCount = "";

    [ObservableProperty]
    private string _fanSpeeds = "Cargando...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PingLatencyColor))]
    [NotifyPropertyChangedFor(nameof(PingLossColor))]
    private string _pingGateway = "Cargando...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PingLatencyColor))]
    private double _pingLatency;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PingLossColor))]
    private double _pingPacketLoss;

    [ObservableProperty]
    private string _pingGoogle = "Cargando...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PingGoogleLatencyColor))]
    private double _pingGoogleLatency;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PingGoogleLossColor))]
    private double _pingGooglePacketLoss;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HealthScoreColor))]
    private int _healthScore;

    [ObservableProperty]
    private string _healthStatus = "Calculando...";

    [ObservableProperty]
    private string _defenderStatus = "Cargando...";

    [ObservableProperty]
    private string _healthBreakdown = "";

    public Brush MemoryUsageColor => MemoryPercent switch
    {
        >= 85 => Red,
        >= 60 => Amber,
        _ => Green
    };

    public Brush CpuTempColor => CpuTempValue switch
    {
        >= 80 => Red,
        >= 60 => Amber,
        >= 0 => Green,
        _ => Grey
    };

    public Brush GpuTempColor => GpuTempValue switch
    {
        >= 80 => Red,
        >= 60 => Amber,
        >= 0 => Green,
        _ => Grey
    };

    public Brush CpuUsageColor => CpuUsagePercent switch
    {
        >= 80 => Red,
        >= 50 => Amber,
        _ => Green
    };

    public Brush GpuUsageColor => GpuUsagePercent switch
    {
        >= 80 => Red,
        >= 50 => Amber,
        _ => Green
    };

    public Brush PingLatencyColor => PingLatency switch
    {
        >= 100 => Red,
        >= 30 => Amber,
        > 0 => Green,
        _ => Grey
    };

    public Brush PingLossColor => PingPacketLoss switch
    {
        >= 10 => Red,
        > 0 => Amber,
        _ => Green
    };

    public Brush PingGoogleLatencyColor => PingGoogleLatency switch
    {
        >= 100 => Red,
        >= 30 => Amber,
        > 0 => Green,
        _ => Grey
    };

    public Brush PingGoogleLossColor => PingGooglePacketLoss switch
    {
        >= 10 => Red,
        > 0 => Amber,
        _ => Green
    };

    public Brush HealthScoreColor => HealthScore switch
    {
        >= 80 => Green,
        >= 50 => Amber,
        _ => Red
    };

    public async Task LoadAsync()
    {
        await Task.Run(() =>
        {
            OsVersion = _sys.GetOsVersion();
            OsInstallDate = _sys.GetOsInstallDate();
            CpuInfo = _sys.GetCpuDetail();
            DiskInfo = _sys.GetDiskInfo();
            GpuInfo = _sys.GetGpuInfo();
            RamSpecs = _sys.GetRamSpecs();
            MotherboardInfo = _sys.GetMotherboardInfo();
            BiosInfo = _sys.GetBiosInfo();
            PowerPlan = _sys.GetPowerPlan();
            PendingUpdates = _sys.GetPendingUpdates();
            var (ipHost, ipAddr) = _sys.GetNetworkInfo();
            NetworkInfo = $"{ipHost} • {ipAddr}";
            var (batPct, batDetail) = _sys.GetBatteryHealth();
            BatteryInfo = batDetail;
            BatteryPercent = batPct ?? 0;
            HasBattery = batPct.HasValue;

            DefenderStatus = _sys.GetDefenderStatus();

            RefreshDynamic();
        });

        WarrantyInfo = await _warranty.CheckWarrantyAsync();
        _ = RunSpeedTest();
        LoadQuickActions();

        _refreshTimer = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Normal, async (_, _) =>
        {
            await RefreshDynamicAsync();
        }, Application.Current.Dispatcher);
        _refreshTimer.Start();
    }

    private async Task RefreshDynamicAsync()
    {
        var data = await Task.Run(() =>
        {
            var (memLabel, memPct) = _sys.GetMemoryUsage();
            var uptime = _sys.GetUptime();
            var cpuTemp = _hw.GetCpuTemperatures();
            var cpuTempVal = ExtractTempValue(cpuTemp);
            var gpuTemp = _hw.GetGpuTemperatures();
            var gpuTempVal = ExtractTempValue(gpuTemp);
            var cpuUsage = _sys.GetCpuUsage();
            var gpuUsage = _sys.GetGpuUsage();
            var smartInfo = _sys.GetDiskSmartInfo();
            var (errStr, critStr) = _sys.GetRecentErrorCount();
            var fans = _hw.GetFanSpeeds();
            var diskTemp = _hw.GetDiskTemperatures();
            var (pingLabel, pingLat, pingLoss) = _sys.GetPingToGateway();
            var (googleLabel, googleLat, googleLoss) = _sys.GetPingToGoogle();
            return (memLabel, memPct, uptime, cpuTemp, cpuTempVal, gpuTemp, gpuTempVal, cpuUsage, gpuUsage, smartInfo, errStr, critStr, fans, diskTemp, pingLabel, pingLat, pingLoss, googleLabel, googleLat, googleLoss);
        });

        MemoryUsage = data.memLabel;
        MemoryPercent = data.memPct;
        Uptime = data.uptime;
        CpuTemp = data.cpuTemp;
        CpuTempValue = data.cpuTempVal;
        GpuTemp = data.gpuTemp;
        GpuTempValue = data.gpuTempVal;
        CpuUsagePercent = data.cpuUsage;
        GpuUsagePercent = data.gpuUsage;
        DiskSmartInfo = data.smartInfo;
        RecentErrorCount = data.errStr;
        RecentCriticalCount = data.critStr;
        FanSpeeds = data.fans;
        DiskTemp = data.diskTemp;
        PingGateway = data.pingLabel;
        PingLatency = data.pingLat;
        PingPacketLoss = data.pingLoss;
        PingGoogle = data.googleLabel;
        PingGoogleLatency = data.googleLat;
        PingGooglePacketLoss = data.googleLoss;
        CalculateHealthScore();
    }

    private void RefreshDynamic()
    {
        var (memLabel, memPct) = _sys.GetMemoryUsage();
        MemoryUsage = memLabel;
        MemoryPercent = memPct;
        Uptime = _sys.GetUptime();
        CpuTemp = _hw.GetCpuTemperatures();
        CpuTempValue = ExtractTempValue(CpuTemp);
        GpuTemp = _hw.GetGpuTemperatures();
        GpuTempValue = ExtractTempValue(GpuTemp);
        CpuUsagePercent = _sys.GetCpuUsage();
        GpuUsagePercent = _sys.GetGpuUsage();
        DiskSmartInfo = _sys.GetDiskSmartInfo();
        DiskTemp = _hw.GetDiskTemperatures();
        var (errStr, critStr) = _sys.GetRecentErrorCount();
        RecentErrorCount = errStr;
        RecentCriticalCount = critStr;
        FanSpeeds = _hw.GetFanSpeeds();
        var (pingLabel, pingLat, pingLoss) = _sys.GetPingToGateway();
        PingGateway = pingLabel;
        PingLatency = pingLat;
        PingPacketLoss = pingLoss;
        var (googleLabel, googleLat, googleLoss) = _sys.GetPingToGoogle();
        PingGoogle = googleLabel;
        PingGoogleLatency = googleLat;
        PingGooglePacketLoss = googleLoss;
        CalculateHealthScore();
    }

    private void CalculateHealthScore()
    {
        int score = 100;
        var penalties = new System.Collections.Generic.List<string>();

        if (CpuTempValue > 80) { score -= 20; penalties.Add("CPU >80°C"); }
        else if (CpuTempValue > 60) { score -= 10; penalties.Add("CPU >60°C"); }

        if (GpuTempValue > 80) { score -= 20; penalties.Add("GPU >80°C"); }
        else if (GpuTempValue > 60) { score -= 10; penalties.Add("GPU >60°C"); }

        if (MemoryPercent > 85) { score -= 20; penalties.Add("RAM >85%"); }
        else if (MemoryPercent > 60) { score -= 10; penalties.Add("RAM >60%"); }

        if (CpuUsagePercent > 80) { score -= 15; penalties.Add("CPU >80%"); }
        else if (CpuUsagePercent > 50) { score -= 5; penalties.Add("CPU >50%"); }

        if (GpuUsagePercent > 80) { score -= 15; penalties.Add("GPU >80%"); }
        else if (GpuUsagePercent > 50) { score -= 5; penalties.Add("GPU >50%"); }

        if (PingPacketLoss > 10) { score -= 30; penalties.Add("Pérdida >10%"); }
        else if (PingPacketLoss > 0) { score -= 15; penalties.Add("Pérdida >0%"); }

        HealthScore = Math.Max(0, Math.Min(100, score));
        HealthStatus = HealthScore >= 80 ? "✅ Bueno" : HealthScore >= 50 ? "⚠ Atención" : "❌ Crítico";
        HealthBreakdown = penalties.Count > 0
            ? string.Join("\n", penalties.Select(p => $"• {p}"))
            : "Sin penalizaciones";
    }

    private static double ExtractTempValue(string tempStr)
    {
        if (tempStr is "N/A" or null or "Cargando...") return -1;
        var m = Regex.Match(tempStr, @"(\d+)°C");
        return m.Success && double.TryParse(m.Groups[1].Value, out var v) ? v : -1;
    }

    [RelayCommand]
    private async Task RunSpeedTest()
    {
        if (IsSpeedTesting) return;
        IsSpeedTesting = true;
        NetworkTraffic = "Probando velocidad...";
        await Task.Run(() =>
        {
            var (down, up, error) = _sys.RunSpeedTest();
            if (!string.IsNullOrEmpty(error))
                NetworkTraffic = $"Error: {error}";
            else
                NetworkTraffic = $"⬇ {down:F1} Mbps | ⬆ {up:F1} Mbps";
        });
        IsSpeedTesting = false;
    }

    private void LoadQuickActions()
    {
        var cfg = _cmdConfig.Load();
        foreach (var cat in cfg.Categorias.Where(c => c.Tipo == "Accion"))
        {
            foreach (var item in cat.Items)
                QuickActions.Add(item);
        }
    }

    [RelayCommand]
    private async Task RunQuickAction(ComandoItem? cmd)
    {
        if (cmd == null || cmd.Pasos.Count == 0) return;

        var dispatcher = Application.Current.Dispatcher;
        var log = $"▶ Ejecutando {cmd.Nombre}...\n";

        foreach (var paso in cmd.Pasos)
        {
            log += $"\n$ {paso}\n";
            try
            {
                var result = await _cmdRunner.RunCommandAsync(paso);
                log += result + "\n";
            }
            catch (Exception ex)
            {
                log += $"⛔ Error: {ex.Message}\n";
            }
        }

        log += $"✅ {cmd.Nombre} completado.\n";

        _ = dispatcher.BeginInvoke(() =>
        {
            // Show result in deployments log
            if (Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
                mainVm.Deployments.LogOutput += log;
        });
    }

    [RelayCommand]
    private void OpenWarrantyUrl()
    {
        var (mfr, _) = _warranty.GetSystemInfo();
        var upper = mfr.ToUpperInvariant();
        var url = upper switch
        {
            string s when s.Contains("DELL") => "https://www.dell.com/support",
            string s when s.Contains("LENOVO") => "https://pcsupport.lenovo.com",
            string s when s.Contains("HP") || s.Contains("HEWLETT") => "https://support.hp.com",
            _ => "https://www.dell.com/support"
        };
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }

    public void StopRefresh()
    {
        _refreshTimer?.Stop();
        _hw.Dispose();
        _warranty.Dispose();
    }
}