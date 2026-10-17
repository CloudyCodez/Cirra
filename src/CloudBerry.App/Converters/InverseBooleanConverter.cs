using System.Globalization;
using System.Windows.Data;

namespace CloudBerry.App.Converters;

/// <summary>
/// Inverts a bool. Used for IsEnabled (and similar) bindings that need the opposite of a
/// bool property — e.g. disabling a control while IsMetadataBusy is true — as distinct from
/// InverseBooleanToVisibilityConverter, which targets Visibility rather than a raw bool.
/// </summary>
[ValueConversion(typeof(bool), typeof(bool))]
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => !(value is true);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => !(value is true);
}
