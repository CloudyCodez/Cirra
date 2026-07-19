namespace CloudBerry.App.Models;

public sealed class FilterOption
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public int Count { get; init; }
}
