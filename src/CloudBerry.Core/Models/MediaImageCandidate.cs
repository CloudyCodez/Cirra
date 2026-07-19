namespace CloudBerry.Core.Models;

public sealed class MediaImageCandidate
{
    public string Title { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public string ThumbnailUrl { get; set; } = string.Empty;

    public string SourceUrl { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    public string SourceName { get; set; } = string.Empty;

    public string MediaKind { get; set; } = string.Empty;

    public double Score { get; set; }
}
