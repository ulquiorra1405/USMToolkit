using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Toolkit.Converters;

public class NavColorConverter : IValueConverter
{
    private static readonly Color Accent = Color.FromRgb(0xF5, 0xA6, 0x23);
    private static readonly Color Dim = Color.FromRgb(0x55, 0x55, 0x55);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index && parameter is string param && int.TryParse(param, out var target))
            return new SolidColorBrush(index == target ? Accent : Dim);
        return new SolidColorBrush(Dim);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
