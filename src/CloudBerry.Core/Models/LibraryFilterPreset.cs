namespace CloudBerry.Core.Models;

/// <summary>
/// A named snapshot of the Library page's filter bar — search text plus every filter dropdown —
/// so a person can jump straight back to a combination like "PS3, Needs metadata" without
/// re-picking each dropdown by hand every time.
/// </summary>
public sealed class LibraryFilterPreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string SearchText { get; set; } = string.Empty;

    public string CollectionKey { get; set; } = "all";

    public string StatusFilterKey { get; set; } = "all";

    public string PlatformFilterKey { get; set; } = "all";

    public string InstallFilterKey { get; set; } = "all";

    public string AchievementFilterKey { get; set; } = "all";

    /// <summary>
    /// Human-readable description of what this preset filters on, computed once at save time
    /// (e.g. "PS3, Needs metadata") and persisted so the preset chip's tooltip can show it
    /// without needing live access to the filter-option lists that generated it.
    /// </summary>
    public string Summary { get; set; } = string.Empty;
}
