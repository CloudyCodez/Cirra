namespace CloudBerry.App.Models;

public static class CloudQuoteCatalog
{
    private static readonly CloudQuoteEntry[] Quotes =
    [
        new("Every cloud in the room is quietly rooting for your backlog.", "soft launch weather"),
        new("A good library drifts together the way soft clouds fill a sky.", "cirrus calm"),
        new("Some games arrive like thunder. The best ones linger like clouds.", "evening storm glow"),
        new("Even a storm cloud can carry the silver lining of a new favorite game.", "silver lining pick"),
        new("Clouds know something about collections: a little shape, a little wonder, and room to roam.", "open-sky browse"),
        new("The sky never rushes a cloud, and a great library never rushes discovery.", "slow discovery drift"),
        new("Today's forecast: light scanning, scattered cover art, and a strong chance of favorites.", "berry forecast"),
        new("A shelf should feel like a skyline: familiar at a glance, surprising when you wander.", "skyline shelf"),
        new("Some saves are tiny weather reports from older versions of ourselves.", "save-state weather"),
        new("Good launchers feel weightless. Great ones still know exactly where everything lives.", "weightless library"),
        new("There is an art to a calm dashboard: less clutter, more horizon.", "horizon focus"),
        new("The nicest kind of backlog is the one that still feels full of weather and wonder.", "backlog breeze"),
        new("Clouds travel lightly. A sharp library should too.", "stratus efficiency"),
        new("A favorite game should never feel more than one clear sky away.", "favorite skies")
    ];

    public static CloudQuoteEntry PickQuoteOfTheDay(DateTime? today = null)
    {
        var target = DateOnly.FromDateTime((today ?? DateTime.Now).Date);
        var index = Math.Abs((target.DayNumber * 17) + target.Year) % Quotes.Length;
        return Quotes[index];
    }

    public static CloudQuoteEntry PickRandomQuoteEntry()
    {
        return Quotes[Random.Shared.Next(Quotes.Length)];
    }

    public static string PickRandomQuote()
    {
        return PickRandomQuoteEntry().Text;
    }
}

public sealed record CloudQuoteEntry(string Text, string BadgeText);
