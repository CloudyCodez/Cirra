using System.Text.Json.Serialization;

namespace CloudBerry.Core.Models;

/// <summary>
/// A preserved PDF manual, strategy guide, or handbook attached to a library game.
/// The original PDF remains untouched so page order, spreads, typography, and artwork stay authentic.
/// </summary>
public sealed class GameGuidebook
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    /// <summary>A local path or an HTTPS PDF URL.</summary>
    public string PdfLocation { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;

    public string SourcePageUrl { get; set; } = string.Empty;

    /// <summary>Original Case Manual, Digital Manual, Strategy Guide, Guidebook, or Quick Reference.</summary>
    public string DocumentKind { get; set; } = "Game Guide";

    public string Platform { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public DateTime AddedOn { get; set; } = DateTime.Now;

    [JsonIgnore]
    public bool IsRemote => Uri.TryCreate(PdfLocation, UriKind.Absolute, out var uri) &&
                            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    [JsonIgnore]
    public string SourceLabel => string.IsNullOrWhiteSpace(SourceName)
        ? (IsRemote ? "Online archive" : "Local PDF")
        : SourceName;

    [JsonIgnore]
    public string DiscoveryLabel => string.Join(" / ", new[] { DocumentKind, Platform, SourceLabel }
        .Where(value => !string.IsNullOrWhiteSpace(value)));

    public GameGuidebook Clone() => new()
    {
        Id = Id,
        Title = Title,
        PdfLocation = PdfLocation,
        SourceName = SourceName,
        SourcePageUrl = SourcePageUrl,
        DocumentKind = DocumentKind,
        Platform = Platform,
        Language = Language,
        AddedOn = AddedOn
    };
}
