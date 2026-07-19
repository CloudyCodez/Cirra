using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CloudBerry.App.Converters;

/// <summary>
/// Converts a bool to a <see cref="GridLength"/> — true maps to Star (1*, or a custom
/// star value supplied in ConverterParameter), false collapses to 0 width.
/// Used to fully close the gap left behind when a Home stat card is hidden, instead of
/// leaving an empty column.
/// Usage: Width="{Binding ShowLibraryStat, Converter={StaticResource BooleanToGridLengthConverter}}"
/// </summary>
[ValueConversion(typeof(bool), typeof(GridLength))]
public sealed class BooleanToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool visible = value is bool b && b;
        if (!visible)
        {
            return new GridLength(0);
        }

        double star = 1.0;
        if (parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            star = parsed;
        }

        return new GridLength(star, GridUnitType.Star);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
