namespace CloudBerry.Core.Models;

/// <summary>
/// Describes a single achievement unlock "pop" — enough data for the
/// animated toast overlay that fires when an achievement is detected in real time.
/// </summary>
public sealed class AchievementUnlockEvent
{
    public required string GameTitle { get; init; }
    public required string AchievementTitle { get; init; }
    public required string Description { get; init; }
    public required string IconUrl { get; init; }
    public required int GamerScore { get; init; }
    public required string ProviderName { get; init; }
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Short display string: "+25G" or "Trophy Unlocked".</summary>
    public string ScoreLabel => GamerScore > 0 ? $"+{GamerScore}G" : "Achievement Unlocked";
}
