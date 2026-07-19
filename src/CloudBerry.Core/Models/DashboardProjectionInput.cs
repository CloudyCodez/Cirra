namespace CloudBerry.Core.Models;

public sealed class DashboardProjectionInput
{
    public string Platform { get; set; } = "Unknown";

    public bool IsInstalled { get; set; }

    public bool IsRunning { get; set; }

    public bool NeedsMetadataRefresh { get; set; }

    public int PlaytimeMinutes { get; set; }

    public int GamerScoreEarned { get; set; }

    public int GamerScoreTotal { get; set; }

    public bool HasAchievements { get; set; }
}
