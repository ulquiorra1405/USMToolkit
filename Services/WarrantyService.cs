using System.Globalization;
using System.Management;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

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
                return "Dell: haz clic para validar en la web";
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

    [GeneratedRegex(@"var ds_warranties = window\.ds_warranties \|\| (.+?);")]
    private static partial Regex DsWarrantiesRegex();

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
