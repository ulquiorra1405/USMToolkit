using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Toolkit.Converters;

public class BoolToGridLengthConverter : IValueConverter
{
    public double ExpandedWidth { get; set; } = 180;
    public double CollapsedWidth { get; set; } = 48;
    public bool UseStar { get; set; } = false;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool expanded && expanded)
            return UseStar ? new GridLength(1, GridUnitType.Star) : new GridLength(ExpandedWidth);
        return new GridLength(CollapsedWidth);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
