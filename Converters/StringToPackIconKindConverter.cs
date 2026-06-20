using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace Toolkit.Converters;

public class StringToPackIconKindConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && Enum.TryParse<PackIconKind>(s, out var kind))
            return kind;
        return PackIconKind.FolderOutline;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PackIconKind kind)
            return kind.ToString();
        return "FolderOutline";
    }
}
