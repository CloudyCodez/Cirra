using System.Globalization;

namespace CloudBerry.Core.Services;

public static class CloudBerryPalette
{
    public static (string Start, string End) CreateGradient(string seed)
    {
        var hash = ComputeHash(seed);
        var startHue = hash % 360;
        var endHue = (startHue + 58) % 360;

        var start = HslToHex(startHue, 0.67, 0.56);
        var end = HslToHex(endHue, 0.60, 0.46);
        return (start, end);
    }

    private static int ComputeHash(string value)
    {
        unchecked
        {
            var hash = 23;
            foreach (var character in value)
            {
                hash = (hash * 31) + character;
            }

            return Math.Abs(hash);
        }
    }

    private static string HslToHex(double hue, double saturation, double lightness)
    {
        var chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var huePrime = hue / 60d;
        var x = chroma * (1 - Math.Abs((huePrime % 2) - 1));

        var (red, green, blue) = huePrime switch
        {
            >= 0 and < 1 => (chroma, x, 0d),
            >= 1 and < 2 => (x, chroma, 0d),
            >= 2 and < 3 => (0d, chroma, x),
            >= 3 and < 4 => (0d, x, chroma),
            >= 4 and < 5 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };

        var match = lightness - (chroma / 2);
        var r = (byte)Math.Round((red + match) * 255);
        var g = (byte)Math.Round((green + match) * 255);
        var b = (byte)Math.Round((blue + match) * 255);

        return string.Create(
            7,
            (r, g, b),
            static (span, state) =>
            {
                span[0] = '#';
                state.r.TryFormat(span[1..3], out _, "X2", CultureInfo.InvariantCulture);
                state.g.TryFormat(span[3..5], out _, "X2", CultureInfo.InvariantCulture);
                state.b.TryFormat(span[5..7], out _, "X2", CultureInfo.InvariantCulture);
            });
    }
}
