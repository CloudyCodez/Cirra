using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services.Metadata;

/// <summary>
/// Keyless PC-focused metadata from PCGamingWiki. Particularly useful for launcher games,
/// live-service games, and Windows releases that are not available on Steam.
/// </summary>
public sealed class PcgamingWikiMetadataProvider : IGameMetadataProvider
{
    private readonly HttpClient _httpClient;

    public PcgamingWikiMetadataProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Cirra/2.0 (universal game library; metadata research)");
    }

    public MetadataProviderDescriptor Descriptor { get; } = new()
    {
        Id = "pcgamingwiki",
        DisplayName = "PCGamingWiki",
        Description = "Keyless PC metadata for launcher, live-service, storefront, and Windows games.",
        RequiresApiKey = false,
        ApiKeyLabel = string.Empty,
        ApiKeyHelpText = "No setup required."
    };

    public async Task<IReadOnlyList<GameMetadataRecord>> SearchAsync(
        GameMetadataSearchRequest request,
        CloudBerrySettings settings,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return [];
        }

        var url = "https://www.pcgamingwiki.com/w/api.php" +
                  "?action=query&list=search&format=json&srlimit=" + Math.Clamp(request.MaxResults, 1, 10) +
                  "&srsearch=" + Uri.EscapeDataString(request.Query);

        var response = await _httpClient.GetFromJsonAsync<PcgwSearchResponse>(url, cancellationToken);
        return response?.Query?.Search?
            .Select(result => new GameMetadataRecord
            {
                ProviderId = Descriptor.Id,
                ProviderName = Descriptor.DisplayName,
                RemoteId = result.PageId.ToString(),
                RemoteSlug = result.Title ?? string.Empty,
                Title = result.Title ?? string.Empty,
                Summary = StripHtml(result.Snippet),
                ProviderItemUrl = BuildPageUrl(result.Title),
                Platforms = ["PC"]
            })
            .Where(record => !string.IsNullOrWhiteSpace(record.Title))
            .ToArray() ?? [];
    }

    public async Task<GameMetadataRecord?> GetDetailsAsync(
        GameMetadataRecord searchResult,
        CloudBerrySettings settings,
        CancellationToken cancellationToken = default)
    {
        var title = string.IsNullOrWhiteSpace(searchResult.RemoteSlug)
            ? searchResult.Title
            : searchResult.RemoteSlug;
        if (string.IsNullOrWhiteSpace(title))
        {
            return searchResult;
        }

        try
        {
            var url = "https://www.pcgamingwiki.com/w/api.php" +
                      "?action=parse&prop=wikitext%7Cimages&format=json&page=" + Uri.EscapeDataString(title);
            var parse = await _httpClient.GetFromJsonAsync<PcgwParseResponse>(url, cancellationToken);
            var wikitext = parse?.Parse?.Wikitext?.Text ?? string.Empty;

            var coverFile = ExtractInfoboxValue(wikitext, "cover");
            var coverUrl = await ResolveImageUrlAsync(coverFile, cancellationToken);

            var screenshotFile = parse?.Parse?.Images?
                .FirstOrDefault(image => IsLikelyScreenshot(image, title, coverFile)) ?? string.Empty;
            var screenshotUrl = await ResolveImageUrlAsync(screenshotFile, cancellationToken);

            var officialSite = ExtractInfoboxValue(wikitext, "official site");
            var developers = ExtractTemplateValues(wikitext, "developer");
            var publishers = ExtractTemplateValues(wikitext, "publisher");
            var releaseDate = TryParseDate(ExtractFirstReleaseDate(wikitext));

            return new GameMetadataRecord
            {
                ProviderId = Descriptor.Id,
                ProviderName = Descriptor.DisplayName,
                RemoteId = searchResult.RemoteId,
                RemoteSlug = title,
                Title = parse?.Parse?.Title ?? searchResult.Title,
                Summary = searchResult.Summary,
                ReleaseDate = releaseDate,
                CoverImageUrl = coverUrl,
                BackgroundImageUrl = screenshotUrl,
                BannerImageUrl = screenshotUrl,
                IconImageUrl = coverUrl,
                WebsiteUrl = officialSite,
                ProviderItemUrl = BuildPageUrl(title),
                Platforms = ["PC"],
                Developers = developers,
                Publishers = publishers,
                Screenshots = string.IsNullOrWhiteSpace(screenshotUrl) ? [] : [screenshotUrl]
            };
        }
        catch
        {
            return searchResult;
        }
    }

    private async Task<string> ResolveImageUrlAsync(string fileName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        fileName = fileName.Trim();
        if (!fileName.StartsWith("File:", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "File:" + fileName;
        }

        try
        {
            var url = "https://www.pcgamingwiki.com/w/api.php" +
                      "?action=query&prop=imageinfo&iiprop=url%7Csize%7Cmime&format=json&titles=" +
                      Uri.EscapeDataString(fileName);
            var response = await _httpClient.GetFromJsonAsync<PcgwImageInfoResponse>(url, cancellationToken);
            return response?.Query?.Pages?.Values
                .SelectMany(page => page.ImageInfo ?? [])
                .Select(info => info.Url ?? string.Empty)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsLikelyScreenshot(string image, string title, string coverFile)
    {
        if (string.IsNullOrWhiteSpace(image) ||
            image.Equals(coverFile, StringComparison.OrdinalIgnoreCase) ||
            image.Contains("icon", StringComparison.OrdinalIgnoreCase) ||
            image.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalizedTitle = Normalize(title);
        var normalizedImage = Normalize(Path.GetFileNameWithoutExtension(image));
        return string.IsNullOrWhiteSpace(normalizedTitle) ||
               normalizedImage.Contains(normalizedTitle, StringComparison.Ordinal) ||
               normalizedImage.Contains("screenshot", StringComparison.Ordinal) ||
               normalizedImage.Contains("settings", StringComparison.Ordinal);
    }

    private static string ExtractInfoboxValue(string wikitext, string field)
    {
        var match = Regex.Match(
            wikitext,
            "^\\s*\\|" + Regex.Escape(field) + "\\s*=\\s*(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? CleanWikiValue(match.Groups[1].Value) : string.Empty;
    }

    private static List<string> ExtractTemplateValues(string wikitext, string templateName)
    {
        return Regex.Matches(
                wikitext,
                "\\{\\{Infobox game/row/" + Regex.Escape(templateName) + "\\|([^}|]+)",
                RegexOptions.IgnoreCase)
            .Select(match => CleanWikiValue(match.Groups[1].Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ExtractFirstReleaseDate(string wikitext)
    {
        var match = Regex.Match(
            wikitext,
            "\\{\\{Infobox game/row/date\\|[^|}]+\\|([^}|]+)",
            RegexOptions.IgnoreCase);
        return match.Success ? CleanWikiValue(match.Groups[1].Value) : string.Empty;
    }

    private static string CleanWikiValue(string value)
    {
        var cleaned = value.Trim();
        cleaned = Regex.Replace(cleaned, "<.*?>", " ");
        cleaned = Regex.Replace(cleaned, "\\[\\[(?:[^|\\]]+\\|)?([^\\]]+)\\]\\]", "$1");
        cleaned = Regex.Replace(cleaned, "\\{\\{.*?\\}\\}", " ");
        return WebUtility.HtmlDecode(Regex.Replace(cleaned, "\\s+", " ")).Trim();
    }

    private static string StripHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var withoutTags = Regex.Replace(value, "<.*?>", " ");
        return WebUtility.HtmlDecode(Regex.Replace(withoutTags, "\\s+", " ")).Trim();
    }

    private static DateTime? TryParseDate(string value)
    {
        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string Normalize(string value)
    {
        return string.Join(
            ' ',
            value.ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
                .ToArray())
            .Trim();
    }

    private static string BuildPageUrl(string? title) =>
        string.IsNullOrWhiteSpace(title)
            ? string.Empty
            : "https://www.pcgamingwiki.com/wiki/" + Uri.EscapeDataString(title.Replace(' ', '_'));

    private sealed class PcgwSearchResponse
    {
        [JsonPropertyName("query")]
        public PcgwQuery? Query { get; set; }
    }

    private sealed class PcgwQuery
    {
        [JsonPropertyName("search")]
        public List<PcgwSearchResult>? Search { get; set; }

        [JsonPropertyName("pages")]
        public Dictionary<string, PcgwImagePage>? Pages { get; set; }
    }

    private sealed class PcgwSearchResult
    {
        [JsonPropertyName("pageid")]
        public int PageId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("snippet")]
        public string? Snippet { get; set; }
    }

    private sealed class PcgwParseResponse
    {
        [JsonPropertyName("parse")]
        public PcgwParse? Parse { get; set; }
    }

    private sealed class PcgwParse
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("wikitext")]
        public PcgwText? Wikitext { get; set; }

        [JsonPropertyName("images")]
        public List<string>? Images { get; set; }
    }

    private sealed class PcgwText
    {
        [JsonPropertyName("*")]
        public string? Text { get; set; }
    }

    private sealed class PcgwImageInfoResponse
    {
        [JsonPropertyName("query")]
        public PcgwQuery? Query { get; set; }
    }

    private sealed class PcgwImagePage
    {
        [JsonPropertyName("imageinfo")]
        public List<PcgwImageInfo>? ImageInfo { get; set; }
    }

    private sealed class PcgwImageInfo
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
