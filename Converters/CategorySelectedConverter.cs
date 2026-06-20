using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Toolkit.Models;

namespace Toolkit.Converters;

public class CategorySelectedConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush Accent = new(Color.FromRgb(0xF5, 0xA6, 0x23));
    private static readonly SolidColorBrush Dim = new(Color.FromRgb(0x88, 0x88, 0x88));

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is CategoriaItem selected && values[1] is CategoriaItem item)
            return selected == item ? Accent : Dim;
        return Dim;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
