namespace CloudBerry.Core.Models;

public sealed class PlatformDashboardSummary
{
    public string Platform { get; set; } = "Unknown";

    public int TitleCount { get; set; }

    public int InstalledCount { get; set; }

    public int PlaytimeMinutes { get; set; }

    public int MetadataReadyCount { get; set; }
}
