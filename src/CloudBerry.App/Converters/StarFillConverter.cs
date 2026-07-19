using System.Globalization;
using System.Windows.Data;

namespace CloudBerry.App.Converters;

/// <summary>
/// Renders one glyph in a five-star rating row. Value is the current rating (0-5, e.g.
/// GameEntryViewModel.UserRatingStars); ConverterParameter is this glyph's 1-based position.
/// Returns a filled star once the rating reaches that position, a hollow star otherwise \u2014
/// so an unrated game reads as five hollow stars instead of five gold ones.
/// </summary>
[ValueConversion(typeof(double), typeof(string))]
public sealed class StarFillConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var rating = value switch
        {
            double d => d,
            int i => i,
            null => 0d,
            _ => 0d
        };

        var position = parameter is string text && int.TryParse(text, out var parsed) ? parsed : 0;
        return rating >= position ? "\u2605" : "\u2606";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
