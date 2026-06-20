using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Toolkit.Services;

public partial class WarrantyService : IDisposable
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public WarrantyService()
    {
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public (string Manufacturer, string SerialNumber) GetSystemInfo()
    {
        try
        {
            using var cs = new ManagementObjectSearcher("SELECT Manufacturer FROM Win32_ComputerSystem");
            var mfr = cs.Get().Cast<ManagementObject>().FirstOrDefault()?["Manufacturer"]?.ToString()?.Trim() ?? "";
            using var bios = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS");
            var sn = bios.Get().Cast<ManagementObject>().FirstOrDefault()?["SerialNumber"]?.ToString()?.Trim() ?? "";
            return (mfr, sn);
        }
        catch { return ("", ""); }
    }

    public async Task<string> CheckWarrantyAsync()
    {
        var (mfr, serial) = GetSystemInfo();
        if (string.IsNullOrEmpty(serial))
            return "Serial no disponible";

        var upper = mfr.ToUpperInvariant();
        try
        {
            if (upper.Contains("LENOVO"))
                return await CheckLenovoAsync(serial);
            if (upper.Contains("HP") || upper.Contains("HEWLETT"))
                return await CheckHpAsync(serial);
            if (upper.Contains("DELL"))
                return "Haz clic para consultar garantía";
            return $"'{mfr}' no soportada";
        }
        catch (HttpRequestException ex)
        {
            return $"Error de red: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            return "Tiempo de espera agotado";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> CheckDellWarrantyAsync()
    {
        var (_, serial) = GetSystemInfo();
        if (string.IsNullOrEmpty(serial))
            return "Serial no disponible";

        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                return await RunOnUiAsync(() => CheckDellWithWebView2Async(serial));
            }
            catch (Exception ex) when (
                ex.Message.Contains("runtime", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("webview2", StringComparison.OrdinalIgnoreCase))
            {
                if (attempt == 0)
                {
                    var ok = await TryInstallWebView2RuntimeAsync();
                    if (!ok)
                        return "WebView2 Runtime necesario.\nDescarga: https://developer.microsoft.com/microsoft-edge/webview2/";
                    continue;
                }
                return "WebView2 Runtime no disponible incluso tras instalar.\nDescarga manual: https://developer.microsoft.com/microsoft-edge/webview2/";
            }
            catch (Exception ex)
            {
                return $"Error Dell: {ex.Message}";
            }
        }

        return "Error inesperado al consultar garantía Dell";
    }

    private static async Task<bool> TryInstallWebView2RuntimeAsync()
    {
        try
        {
            var url = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
            var installerPath = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
            {
                var data = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(installerPath, data);
            }

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/silent /install",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process == null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> CheckDellWithWebView2Async(string tag)
    {
        using var hidden = new HiddenWebViewHost();
        await hidden.InitializeAsync();

        hidden.WebView.CoreWebView2.CookieManager.DeleteAllCookies();
        await NavigateAsync(hidden.WebView, "https://www.dell.com/support/home/en-us", 30);
        var abck = await WaitForCookieAsync(hidden.WebView, "_abck", "dell.com", 10);
        if (string.IsNullOrEmpty(abck))
            return "No se pudo obtener cookie _abck de Akamai.\nIntenta de nuevo o revisa la conexión";

        var encJs = $@"
            (async () => {{
                try {{
                    const r = await fetch('https://www.dell.com/support/apis/detectproduct/encvalue/{tag}?appname=warranty');
                    return await r.text();
                }} catch(e) {{ return 'FETCH_ERROR: ' + e.message; }}
            }})();
        ";
        var encResult = await hidden.WebView.CoreWebView2.ExecuteScriptAsync(encJs);
        var encText = JsonSerializer.Deserialize<string>(encResult) ?? encResult.Trim('"');

        if (encText.StartsWith("FETCH_ERROR"))
            return $"Dell {tag}\nError de red al consultar API: {encText}";

        if (!TryParseAssetId(encText, out var assetId) || assetId == null)
            return $"Dell {tag}\nRespuesta inesperada de API:\n{Truncate(encText, 200)}";

        var postJs = $@"
            (async () => {{
                try {{
                    const r = await fetch('https://www.dell.com/support/apis/entitlement/details', {{
                        method: 'POST',
                        headers: {{ 'Content-Type': 'application/json' }},
                        body: JSON.stringify([{{assetId: '{assetId.Replace("'", "\\'")}'}}])
                    }});
                    return await r.text();
                }} catch(e) {{ return 'FETCH_ERROR: ' + e.message; }}
            }})();
        ";
        var postResult = await hidden.WebView.CoreWebView2.ExecuteScriptAsync(postJs);
        var html = JsonSerializer.Deserialize<string>(postResult) ?? postResult.Trim('"');

        if (html.StartsWith("FETCH_ERROR"))
            return $"Dell {tag}\nError de red al consultar detalles: {html}";

        var dates = DellWarrantyTableDateRegex().Matches(html);
        if (dates.Count == 0)
            return $"Dell {tag}\nNo se encontraron fechas en la respuesta";

        var entries = new List<string>();
        foreach (Match m in dates)
        {
            var endDate = FormatDate(m.Groups[1].Value);
            if (!string.IsNullOrEmpty(endDate))
                entries.Add($"{endDate} · Vigente");
        }

        return entries.Count > 0
            ? $"Dell\n{string.Join("\n", entries)}"
            : $"Dell {tag}\nNo se pudieron parsear las fechas";
    }

    private static async Task NavigateAsync(Microsoft.Web.WebView2.Wpf.WebView2 wv, string url, int timeoutSec)
    {
        var tcs = new TaskCompletionSource();
        void onNav(object? s, CoreWebView2NavigationCompletedEventArgs? e)
        {
            tcs.TrySetResult();
            wv.NavigationCompleted -= onNav;
        }
        wv.NavigationCompleted += onNav;
        wv.CoreWebView2.Navigate(url);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(timeoutSec));
    }

    private static async Task<string?> WaitForCookieAsync(Microsoft.Web.WebView2.Wpf.WebView2 wv, string name, string domain, int timeoutSec)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSec);
        while (DateTime.UtcNow < deadline)
        {
            var cookies = await wv.CoreWebView2.CookieManager.GetCookiesAsync($"https://{domain}");
            var val = cookies.FirstOrDefault(c => c.Name == name)?.Value;
            if (!string.IsNullOrEmpty(val))
                return val;
            await Task.Delay(500);
        }
        return null;
    }

    private static bool TryParseAssetId(string json, out string? assetId)
    {
        assetId = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("assetId", out var p))
            {
                assetId = p.GetString();
                return !string.IsNullOrEmpty(assetId);
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";

    private static async Task<T> RunOnUiAsync<T>(Func<Task<T>> func)
    {
        if (!Application.Current.Dispatcher.CheckAccess())
            await Application.Current.Dispatcher.InvokeAsync(() => { });
        return await func();
    }

    private sealed class HiddenWebViewHost : IDisposable
    {
        public Microsoft.Web.WebView2.Wpf.WebView2 WebView { get; }
        private readonly Window _window;

        public HiddenWebViewHost()
        {
            _window = new Window
            {
                Width = 1,
                Height = 1,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false,
                Left = -3000,
                Top = -3000
            };

            WebView = new Microsoft.Web.WebView2.Wpf.WebView2();
            _window.Content = WebView;
        }

        public async Task InitializeAsync()
        {
            _window.Show();
            try
            {
                await WebView.EnsureCoreWebView2Async().WaitAsync(TimeSpan.FromSeconds(15));
            }
            catch (TimeoutException)
            {
                _window.Close();
                throw new Exception("Timeout al inicializar WebView2");
            }
            catch (Exception ex) when (
                ex.Message.Contains("runtime", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("webview2", StringComparison.OrdinalIgnoreCase))
            {
                _window.Close();
                throw new Exception($"No se encontró WebView2 Runtime compatible: {ex.Message}");
            }
            catch (Exception ex)
            {
                _window.Close();
                throw new Exception($"Error al inicializar WebView2: {ex.Message}");
            }
        }

        public void Dispose()
        {
            WebView.Dispose();
            _window.Close();
        }
    }

    [GeneratedRegex(@"(?:End Date|Fecha de fin|Fecha fin)\s*:\s*(\d{4}-\d{2}-\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex DellWarrantyTableDateRegex();

    private async Task<string> CheckLenovoAsync(string serial)
    {
        var resolveUrl = $"https://pcsupport.lenovo.com/us/en/api/v4/mse/getproducts?productId={serial}";
        using var resolveResp = await _http.GetAsync(resolveUrl);
        resolveResp.EnsureSuccessStatusCode();

        using var resolveDoc = await JsonDocument.ParseAsync(await resolveResp.Content.ReadAsStreamAsync());
        var entries = resolveDoc.RootElement.EnumerateArray().ToList();
        var productEntry = entries.FirstOrDefault(e => e.TryGetProperty("Type", out var t) && t.GetString() == "Product.Serial");
        if (productEntry.ValueKind == JsonValueKind.Undefined)
            productEntry = entries.FirstOrDefault();

        string? productId = null;
        if (productEntry.ValueKind != JsonValueKind.Undefined && productEntry.TryGetProperty("Id", out var id))
            productId = id.GetString();
        if (string.IsNullOrEmpty(productId))
            return $"Lenovo · {serial}\nNo se pudo identificar el producto";

        var warrantyUrl = $"https://pcsupport.lenovo.com/us/en/products/{productId}/warranty";
        using var warrantyResp = await _http.GetAsync(warrantyUrl);
        warrantyResp.EnsureSuccessStatusCode();

        var html = await warrantyResp.Content.ReadAsStringAsync();
        var match = DsWarrantiesRegex().Match(html);
        if (!match.Success)
            return $"Lenovo · {serial}\nNo se pudo obtener información de garantía";

        using var warrantiesDoc = JsonDocument.Parse(match.Groups[1].Value);
        var root = warrantiesDoc.RootElement;

        var productName = root.TryGetProperty("ProductName", out var p) ? p.GetString() ?? "" : "";
        var baseWarranties = root.TryGetProperty("BaseWarranties", out var bw)
            ? bw.EnumerateArray().ToList()
            : [];

        if (baseWarranties.Count == 0)
            return $"Lenovo\nSin garantía activa";

        var results = baseWarranties.Select(w =>
        {
            var end = w.TryGetProperty("End", out var en) ? en.GetString() ?? "" : "";
            var status = w.TryGetProperty("StatusV2", out var sv) ? sv.GetString() ?? "" : "";
            var label = status switch
            {
                "InWarranty" => "Vigente",
                "OutOfWarranty" => "Expirada",
                _ => status
            };
            var date = FormatDate(end);
            return string.IsNullOrEmpty(date) ? label : $"{date} · {label}";
        });

        return $"Lenovo\n{string.Join("\n", results)}";
    }

    [GeneratedRegex(@"var ds_warranties = window\.ds_warranties \|\| (.+?);")]
    private static partial Regex DsWarrantiesRegex();

    private async Task<string> CheckHpAsync(string serial)
    {
        var url = $"https://support.hp.com/us-en/checkwarranty/{serial}";
        using var resp = await _http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        return $"HP · {serial}\nConsulta manual:\n{url}";
    }

    private static string FormatDate(string? raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length < 10) return raw ?? "";
        if (DateTime.TryParseExact(raw[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            || DateTime.TryParseExact(raw[..10], "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            return dt.ToString("dd/MM/yyyy");
        return raw[..10];
    }

    public void Dispose() => _http.Dispose();
}