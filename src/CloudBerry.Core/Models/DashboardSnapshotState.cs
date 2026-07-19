namespace CloudBerry.Core.Models;

public sealed class DashboardSnapshotState
{
    /// <summary>
    /// A3: Persisted selected-game staging so Home/Cloudscape can rehydrate
    /// the same focal game after restart.
    /// </summary>
    public Guid? SelectedGameId { get; set; }

    /// <summary>A3: Explicit selected-game stage context for surface transitions.</summary>
    public SelectedGameStageSnapshot SelectedGameStage { get; set; } = new();

    /// <summary>A3: Persisted shell staging for dashboard continuity.</summary>
    public int SelectedPrimaryPage { get; set; }

    /// <summary>A3: Persisted workspace staging for Browse/Import continuity.</summary>
    public int SelectedWorkspaceTab { get; set; }

    /// <summary>A1: Persisted shell focus mode so the sidebar stays tucked away when desired.</summary>
    public bool IsSidebarVisible { get; set; } = true;

    /// <summary>A3: Keeps "random backlog" stable across restarts until reshuffled.</summary>
    public Guid? RandomBacklogGameId { get; set; }

    /// <summary>A3: Most recent activity feed entries shown in Home/Cloudscape.</summary>
    public List<DashboardActivityEntry> RecentActivity { get; set; } = [];

    /// <summary>A3: latest feed kind marker for quick dashboard state banners.</summary>
    public string LastActivityKind { get; set; } = string.Empty;

    /// <summary>A3: timestamp for latest dashboard activity marker.</summary>
    public DateTime? LastActivityAtUtc { get; set; }

    /// <summary>
    /// A3: Persisted platform-level rollups so dashboard surfaces can render
    /// stable identity/context cards without recomputing historical state.
    /// </summary>
    public List<PlatformDashboardSummary> PlatformSummaries { get; set; } = [];

    /// <summary>
    /// A3: Persisted dashboard KPI context for Home/Cloudscape rendering.
    /// </summary>
    public DashboardInsightsSnapshot Insights { get; set; } = new();
}

public sealed class DashboardActivityEntry
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public string Kind { get; set; } = string.Empty;

    public Guid? GameId { get; set; }

    public string GameTitle { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;
}
