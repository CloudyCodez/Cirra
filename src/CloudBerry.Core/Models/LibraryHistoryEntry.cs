namespace CloudBerry.Core.Models;

public sealed class LibraryHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public string Kind { get; set; } = "library.change";

    public string Title { get; set; } = "Library changed";

    public string Detail { get; set; } = string.Empty;

    public int AddedCount { get; set; }

    public int RemovedCount { get; set; }

    public int ModifiedCount { get; set; }

    public string BeforeSnapshotFile { get; set; } = string.Empty;

    public string AfterSnapshotFile { get; set; } = string.Empty;

    public string TimestampLabel => TimestampUtc.ToLocalTime().ToString("g");

    public string Summary => BuildSummary(AddedCount, RemovedCount, ModifiedCount);

    private static string BuildSummary(int added, int removed, int modified)
    {
        List<string> parts = [];
        if (added > 0) parts.Add($"{added} added");
        if (removed > 0) parts.Add($"{removed} removed");
        if (modified > 0) parts.Add($"{modified} changed");
        return parts.Count == 0 ? "Library state captured" : string.Join(", ", parts);
    }
}

public sealed record LibraryHistoryContext(string Kind, string Title, string Detail = "");
