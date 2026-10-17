namespace CloudBerry.Core.Models;

public sealed class GameMetadataRecord
{
    public string ProviderId { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public string RemoteId { get; set; } = string.Empty;

    public string RemoteSlug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public DateTime? ReleaseDate { get; set; }

    public string CoverImageUrl { get; set; } = string.Empty;

    public string BackgroundImageUrl { get; set; } = string.Empty;

    public string BannerImageUrl { get; set; } = string.Empty;

    public string IconImageUrl { get; set; } = string.Empty;

    public string WebsiteUrl { get; set; } = string.Empty;

    public string ProviderItemUrl { get; set; } = string.Empty;

    public double? CommunityRating { get; set; }

    public List<string> Platforms { get; set; } = [];

    public List<string> Genres { get; set; } = [];

    public List<string> Developers { get; set; } = [];

    public List<string> Publishers { get; set; } = [];

    public List<string> Tags { get; set; } = [];

    /// <summary>Full-resolution screenshot URLs (up to 8).</summary>
    public List<string> Screenshots { get; set; } = [];

    /// <summary>Direct MP4 URL for the primary trailer, if available.</summary>
    public string TrailerUrl { get; set; } = string.Empty;

    /// <summary>Provider-discovered manuals and guides that can be attached without reformatting.</summary>
    public List<GameGuidebook> Guidebooks { get; set; } = [];
}
