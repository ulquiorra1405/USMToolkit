using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Toolkit.Converters;

public class SidebarAlignConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => HorizontalAlignment.Center;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
