namespace CloudBerry.App.ViewModels;

public sealed class AchievementUnlockFeedItemViewModel
{
    public required string GameTitle { get; init; }

    public required string AchievementTitle { get; init; }

    public required string Description { get; init; }

    public required string IconUrl { get; init; }

    public required DateTime UnlockedAtUtc { get; init; }

    public string UnlockedLabel => $"Unlocked {UnlockedAtUtc.ToLocalTime():MMM d, yyyy h:mm tt}";
}
