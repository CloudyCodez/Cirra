namespace CloudBerry.App.Models;

public sealed class CollectionOption
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public required int Count { get; init; }

    public bool IsNested { get; init; }

    public bool IsMenuHeader { get; init; }

    public string GenreName { get; init; } = string.Empty;

    /// <summary>Set for user-created Collections (nested items under the "My Collections" header), backed by each game's free-text Tags.</summary>
    public string TagName { get; init; } = string.Empty;

    /// <summary>Set for manually-created collections (nested items under "My Collections"). Points at CloudBerryCollection.Id.</summary>
    public string CollectionId { get; init; } = string.Empty;

    public double Indent => IsNested ? 16 : 0;

    public string MenuGlyph => IsMenuHeader ? ">" : IsNested ? "-" : string.Empty;
}
