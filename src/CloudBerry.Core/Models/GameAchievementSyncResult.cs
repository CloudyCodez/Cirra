namespace CloudBerry.Core.Models;

public sealed class GameAchievementSyncResult
{
    public GameAchievementSnapshot Snapshot { get; set; } = new();

    public List<string> TrackingFilePaths { get; set; } = [];

    public bool SupportsLiveTracking { get; set; }

    public string SourceDescription { get; set; } = string.Empty;
}
