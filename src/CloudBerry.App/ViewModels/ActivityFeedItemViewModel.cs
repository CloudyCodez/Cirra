namespace CloudBerry.App.ViewModels;

/// <summary>
/// Display-ready wrapper around a <see cref="CloudBerry.Core.Models.DashboardActivityEntry"/>
/// for the Home "Recent activity" feed. Adds an icon glyph per activity kind and a
/// human-friendly relative-time label, so the XAML template can stay simple.
/// </summary>
public sealed class ActivityFeedItemViewModel
{
    public required string Kind { get; init; }

    public required string Detail { get; init; }

    public required string GameTitle { get; init; }

    public required DateTime TimestampUtc { get; init; }

    public bool HasGameTitle => !string.IsNullOrWhiteSpace(GameTitle);

    /// <summary>Segoe MDL2/Fluent icon glyph representing this activity's category.</summary>
    public string Glyph => Kind switch
    {
        _ when Kind.StartsWith("import.", StringComparison.Ordinal) => "\uE710", // add
        _ when Kind.StartsWith("metadata.", StringComparison.Ordinal) => "\uE8B9", // picture/info
        _ when Kind.StartsWith("achievements.", StringComparison.Ordinal) => "\uE9D9", // trophy-ish
        "launch" => "\u25B6", // play triangle
        "favorite.add" => "\uE735", // star filled
        "favorite.remove" => "\uE734", // star outline
        "library.remove" => "\uE74D", // trash
        "library.install-state" => "\uE73E", // checkmark
        "library.repair" => "\uE90F", // repair/tool
        "library.recover" => "\uE7A6", // recover
        "library.link-duplicate" => "\uE71B", // link/chain
        "session.external-detect" => "\uE946", // info/radar-ish — Cirra noticed this on its own
        _ => "\uE81C" // generic activity dot
    };

    /// <summary>Short, human-friendly "time ago" label, recomputed each time the feed refreshes.</summary>
    public string TimeAgoLabel
    {
        get
        {
            var elapsed = DateTime.UtcNow - TimestampUtc;
            if (elapsed < TimeSpan.FromSeconds(45)) return "Just now";
            if (elapsed < TimeSpan.FromMinutes(60)) return $"{(int)elapsed.TotalMinutes}m ago";
            if (elapsed < TimeSpan.FromHours(24)) return $"{(int)elapsed.TotalHours}h ago";
            if (elapsed < TimeSpan.FromDays(2)) return "Yesterday";
            return $"{(int)elapsed.TotalDays}d ago";
        }
    }
}
