using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services.Metadata;

/// <summary>
/// Keyless broad-coverage metadata from Wikipedia/Wikimedia.
/// Excellent fallback for console games, live-service games, and titles not sold on Steam.
/// </summary>
public sealed class WikipediaMetadataProvider : IGameMetadataProvider
{
    private readonly HttpClient _httpClient;

    public WikipediaMetadataProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Cirra/2.0 (universal game library; metadata research)");
    }

    public MetadataProviderDescriptor Descriptor { get; } = new()
    {
        Id = "wikipedia",
        DisplayName = "Wikipedia",
        Description = "Keyless fallback for game descriptions, release context, official links, and Wikimedia-hosted media.",
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

        var merged = new List<GameMetadataRecord>();
        foreach (var query in BuildSearchQueries(request.Query))
        {
            var url = "https://en.wikipedia.org/w/api.php" +
                      "?action=query&list=search&format=json&srlimit=" + Math.Clamp(request.MaxResults, 1, 10) +
                      "&srsearch=" + Uri.EscapeDataString(query);

            var response = await _httpClient.GetFromJsonAsync<WikiSearchResponse>(url, cancellationToken);
            var records = response?.Query?.Search?
                .Select(result => new GameMetadataRecord
                {
                    ProviderId = Descriptor.Id,
                    ProviderName = Descriptor.DisplayName,
                    RemoteId = result.PageId.ToString(),
                    RemoteSlug = result.Title ?? string.Empty,
                    Title = result.Title ?? string.Empty,
                    Summary = StripHtml(result.Snippet),
                    ProviderItemUrl = BuildPageUrl(result.Title),
                    Platforms = string.IsNullOrWhiteSpace(request.Platform) ? [] : [request.Platform]
                })
                .Where(record => !string.IsNullOrWhiteSpace(record.Title))
                .ToList() ?? [];

            foreach (var record in records)
            {
                if (merged.Any(existing => existing.Title.Equals(record.Title, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                merged.Add(record);
            }

            if (merged.Count >= Math.Clamp(request.MaxResults, 1, 10))
            {
                break;
            }
        }

        return merged.Take(Math.Clamp(request.MaxResults, 1, 10)).ToArray();
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
            var url = "https://en.wikipedia.org/api/rest_v1/page/summary/" + Uri.EscapeDataString(title);
            var summary = await _httpClient.GetFromJsonAsync<WikiSummaryResponse>(url, cancellationToken);
            if (summary is null)
            {
                return searchResult;
            }

            var imageUrl = summary.OriginalImage?.Source ?? summary.Thumbnail?.Source ?? string.Empty;
            return new GameMetadataRecord
            {
                ProviderId = Descriptor.Id,
                ProviderName = Descriptor.DisplayName,
                RemoteId = summary.PageId?.ToString() ?? searchResult.RemoteId,
                RemoteSlug = summary.Titles?.Normalized ?? title,
                Title = summary.Title ?? searchResult.Title,
                Summary = summary.Extract ?? searchResult.Summary,
                CoverImageUrl = imageUrl,
                BackgroundImageUrl = imageUrl,
                BannerImageUrl = imageUrl,
                IconImageUrl = summary.Thumbnail?.Source ?? imageUrl,
                ProviderItemUrl = summary.ContentUrls?.Desktop?.Page ?? BuildPageUrl(title),
                Platforms = searchResult.Platforms
            };
        }
        catch
        {
            return searchResult;
        }
    }

    private static string BuildPageUrl(string? title) =>
        string.IsNullOrWhiteSpace(title)
            ? string.Empty
            : "https://en.wikipedia.org/wiki/" + Uri.EscapeDataString(title.Replace(' ', '_'));

    private static IReadOnlyList<string> BuildSearchQueries(string query)
    {
        var normalized = Regex.Replace(query ?? string.Empty, @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        var queries = new List<string>();
        AddQuery(queries, normalized);
        AddQuery(queries, $"{normalized} video game");
        AddQuery(queries, $"{normalized} game");

        var noEdition = Regex.Replace(normalized, @"\b(deluxe|definitive|remastered?|director'?s cut|game of the year|goty|ultimate|complete|edition)\b", " ", RegexOptions.IgnoreCase);
        noEdition = Regex.Replace(noEdition, @"\s+", " ").Trim();
        AddQuery(queries, noEdition);
        AddQuery(queries, string.IsNullOrWhiteSpace(noEdition) ? string.Empty : $"{noEdition} video game");

        return queries;
    }

    private static void AddQuery(ICollection<string> queries, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        if (queries.Any(existing => existing.Equals(query, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        queries.Add(query);
    }

    private static string StripHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var withoutTags = System.Text.RegularExpressions.Regex.Replace(value, "<.*?>", " ");
        return WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(withoutTags, "\\s+", " ")).Trim();
    }

    private sealed class WikiSearchResponse
    {
        [JsonPropertyName("query")]
        public WikiQuery? Query { get; set; }
    }

    private sealed class WikiQuery
    {
        [JsonPropertyName("search")]
        public List<WikiSearchResult>? Search { get; set; }
    }

    private sealed class WikiSearchResult
    {
        [JsonPropertyName("pageid")]
        public int PageId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("snippet")]
        public string? Snippet { get; set; }
    }

    private sealed class WikiSummaryResponse
    {
        [JsonPropertyName("pageid")]
        public int? PageId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("titles")]
        public WikiTitles? Titles { get; set; }

        [JsonPropertyName("extract")]
        public string? Extract { get; set; }

        [JsonPropertyName("thumbnail")]
        public WikiImage? Thumbnail { get; set; }

        [JsonPropertyName("originalimage")]
        public WikiImage? OriginalImage { get; set; }

        [JsonPropertyName("content_urls")]
        public WikiContentUrls? ContentUrls { get; set; }
    }

    private sealed class WikiTitles
    {
        [JsonPropertyName("normalized")]
        public string? Normalized { get; set; }
    }

    private sealed class WikiImage
    {
        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }

    private sealed class WikiContentUrls
    {
        [JsonPropertyName("desktop")]
        public WikiDesktopUrls? Desktop { get; set; }
    }

    private sealed class WikiDesktopUrls
    {
        [JsonPropertyName("page")]
        public string? Page { get; set; }
    }
}
