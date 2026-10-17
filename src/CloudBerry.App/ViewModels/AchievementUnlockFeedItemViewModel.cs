namespace CloudBerry.App.ViewModels;

public sealed class AchievementUnlockFeedItemViewModel
{
    /// <summary>
    /// When the current CloudBerry session started. Any unlock timestamped at or after
    /// this moment is a genuinely new unlock the user just earned this session, as
    /// opposed to older unlocks the feed is simply showing for history. Set once at
    /// app startup by MainViewModel.
    /// </summary>
    public static DateTime SessionStartUtc { get; set; } = DateTime.UtcNow;

    public required string GameTitle { get; init; }

    public required string AchievementTitle { get; init; }

    public required string Description { get; init; }

    public required string IconUrl { get; init; }

    public required DateTime UnlockedAtUtc { get; init; }

    public string UnlockedLabel => $"Unlocked {UnlockedAtUtc.ToLocalTime():MMM d, yyyy h:mm tt}";

    /// <summary>True if this unlock happened during the current CloudBerry session (used to group/badge "new" unlocks in the feed).</summary>
    public bool IsNewThisSession => UnlockedAtUtc >= SessionStartUtc;
}
