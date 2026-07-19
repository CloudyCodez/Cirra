using System.Globalization;
using System.Windows.Data;

namespace CloudBerry.App.Converters;

/// <summary>Returns true (enabled) when the integer value is greater than zero.</summary>
[ValueConversion(typeof(int), typeof(bool))]
public sealed class IsGreaterThanZeroConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int count && count > 0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
