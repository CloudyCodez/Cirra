namespace CloudBerry.Core.Models;

public sealed class LibrarySnapshot
{
    public List<GameRecord> Games { get; set; } = [];

    public CloudBerrySettings Settings { get; set; } = new();

    /// <summary>A3: Dashboard staging + recent activity continuity state.</summary>
    public DashboardSnapshotState Dashboard { get; set; } = new();

    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
