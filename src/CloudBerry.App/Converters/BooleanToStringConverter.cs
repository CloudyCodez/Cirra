using System.Globalization;
using System.Windows.Data;

namespace CloudBerry.App.Converters;

/// <summary>
/// Converts a bool to one of two pipe-separated strings supplied in ConverterParameter.
/// Usage: ConverterParameter='TrueText|FalseText'
/// true  -> first segment
/// false -> second segment
/// </summary>
[ValueConversion(typeof(bool), typeof(string))]
public sealed class BooleanToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var parts = (parameter as string ?? "|").Split('|');
        bool flag = value is bool b && b;
        return flag
            ? (parts.Length > 0 ? parts[0] : string.Empty)
            : (parts.Length > 1 ? parts[1] : string.Empty);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
