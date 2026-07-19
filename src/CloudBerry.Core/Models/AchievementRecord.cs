namespace CloudBerry.Core.Models;

public sealed class AchievementRecord
{
    public string ApiName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string IconUrl { get; set; } = string.Empty;

    public string LockedIconUrl { get; set; } = string.Empty;

    public bool IsHidden { get; set; }

    public bool IsUnlocked { get; set; }

    public DateTime? UnlockedAtUtc { get; set; }

    public double? GlobalUnlockPercent { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>
    /// Gamer Score points awarded when this achievement is unlocked.
    /// Rare achievements should be worth more (e.g. 5, 10, 25, 50, 100).
    /// Defaults to 10.
    /// </summary>
    public int GamerScoreValue { get; set; } = 10;
}
