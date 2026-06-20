using System.Windows;
using System.Windows.Media;

namespace Toolkit.Services;

public static class ThemeService
{
    public static readonly string[] ResourceKeys =
    [
        "ContentBg", "SidebarBg", "CardBg", "DarkCardBg",
        "BorderColor", "TextPrimary", "TextSecondary", "TextMuted", "TextDim", "HintText",
        "ContentBgBrush", "SidebarBgBrush", "CardBgBrush", "DarkCardBgBrush",
        "BorderBrush", "TextPrimaryBrush", "TextSecondaryBrush", "TextMutedBrush", "TextDimBrush", "HintTextBrush",
    ];

    private static readonly Dictionary<string, Color> DarkColors = new()
    {
        ["ContentBg"] = Color.FromRgb(0x12, 0x12, 0x12),
        ["SidebarBg"] = Color.FromRgb(0x1A, 0x1A, 0x1A),
        ["CardBg"] = Color.FromRgb(0x1E, 0x1E, 0x1E),
        ["DarkCardBg"] = Color.FromRgb(0x0D, 0x0D, 0x0D),
        ["BorderColor"] = Color.FromRgb(0x2A, 0x2A, 0x2A),
        ["TextPrimary"] = Color.FromRgb(0xE8, 0xE8, 0xE8),
        ["TextSecondary"] = Color.FromRgb(0xCC, 0xCC, 0xCC),
        ["TextMuted"] = Color.FromRgb(0x88, 0x88, 0x88),
        ["TextDim"] = Color.FromRgb(0x55, 0x55, 0x55),
        ["HintText"] = Color.FromRgb(0x77, 0x77, 0x77),
    };

    private static readonly Dictionary<string, Color> LightColors = new()
    {
        ["ContentBg"] = Color.FromRgb(0xF5, 0xF5, 0xF5),
        ["SidebarBg"] = Color.FromRgb(0xFF, 0xFF, 0xFF),
        ["CardBg"] = Color.FromRgb(0xE8, 0xE8, 0xE8),
        ["DarkCardBg"] = Color.FromRgb(0xFA, 0xFA, 0xFA),
        ["BorderColor"] = Color.FromRgb(0xD0, 0xD0, 0xD0),
        ["TextPrimary"] = Color.FromRgb(0x1A, 0x1A, 0x1A),
        ["TextSecondary"] = Color.FromRgb(0x44, 0x44, 0x44),
        ["TextMuted"] = Color.FromRgb(0x77, 0x77, 0x77),
        ["TextDim"] = Color.FromRgb(0x99, 0x99, 0x99),
        ["HintText"] = Color.FromRgb(0x55, 0x55, 0x55),
    };

    public static void ApplyTheme(bool isLight)
    {
        var colors = isLight ? LightColors : DarkColors;

        foreach (var (key, color) in colors)
        {
            Application.Current.Resources[key] = color;
            Application.Current.Resources[key + "Brush"] = new SolidColorBrush(color);
        }
    }

    public static void InitializeDark()
    {
        ApplyTheme(false);
    }
}