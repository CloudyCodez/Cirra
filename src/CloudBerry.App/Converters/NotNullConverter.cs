using System.Globalization;
using System.Windows.Data;

namespace CloudBerry.App.Converters;

/// <summary>Returns true (enabled) when the bound value is not null.</summary>
[ValueConversion(typeof(object), typeof(bool))]
public sealed class NotNullConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
