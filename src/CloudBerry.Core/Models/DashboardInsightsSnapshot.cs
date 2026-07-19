namespace CloudBerry.Core.Models;

public sealed class DashboardInsightsSnapshot
{
    public int TotalGames { get; set; }

    public int InstalledGames { get; set; }

    public int RunningGames { get; set; }

    public int MetadataReadyGames { get; set; }

    public int MissingMetadataGames { get; set; }

    public int TotalPlaytimeMinutes { get; set; }

    public int GamerScoreEarned { get; set; }

    public int GamerScoreTotal { get; set; }

    public int SyncedAchievementGames { get; set; }

    public int RecentActivityCount { get; set; }

    public string DominantPlatform { get; set; } = "Unknown";

    public int DominantPlatformCount { get; set; }

    public string MostPlayedPlatform { get; set; } = "Unknown";

    public int MostPlayedPlatformMinutes { get; set; }

    public List<DashboardActivityMixItem> ActivityMix { get; set; } = [];
}

public sealed class DashboardActivityMixItem
{
    public string Kind { get; set; } = string.Empty;

    public int Count { get; set; }
}
