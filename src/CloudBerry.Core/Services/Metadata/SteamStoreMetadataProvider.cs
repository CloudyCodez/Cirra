using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services.Metadata;

/// <summary>
/// Pulls metadata, artwork, screenshots, and trailers from the public Steam Store API.
/// Zero credentials required — just HTTP.
/// </summary>
public sealed class SteamStoreMetadataProvider : IGameMetadataProvider
{
    private readonly HttpClient _httpClient;

    public SteamStoreMetadataProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Cirra/2.0 (universal game library; no commercial use)");
    }

    public MetadataProviderDescriptor Descriptor { get; } = new()
    {
        Id = "steam-store",
        DisplayName = "Steam Store",
        Description = "Pulls metadata, cover art, background images, screenshots, and trailers from the public Steam Store API. No API key required.",
        RequiresApiKey = false,
        ApiKeyLabel = string.Empty,
        ApiKeyHelpText = "Steam Store is completely free — no setup needed."
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

        var url = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(request.Query)}&l=english&cc=US";
        var response = await _httpClient.GetFromJsonAsync<SteamSearchResponse>(url, cancellationToken)
                       ?? new SteamSearchResponse();

        return response.Items
            .Select(item => new
            {
                Item = item,
                Score = ScoreSearchResult(request.Query, item.Name ?? string.Empty)
            })
            .Where(entry => entry.Score >= 0.32)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => Math.Abs((entry.Item.Name ?? string.Empty).Length - request.Query.Length))
            .Take(Math.Clamp(request.MaxResults, 1, 20))
            .Select(entry => entry.Item)
            .Select(MapSearchResult)
            .Where(r => !string.IsNullOrWhiteSpace(r.Title))
            .ToArray();
    }

    public async Task<GameMetadataRecord?> GetDetailsAsync(
        GameMetadataRecord searchResult,
        CloudBerrySettings settings,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchResult.RemoteId))
        {
            return searchResult;
        }

        var url = $"https://store.steampowered.com/api/appdetails?appids={Uri.EscapeDataString(searchResult.RemoteId)}&l=english&cc=US";

        Dictionary<string, SteamAppDetailsWrapper>? raw;
        try
        {
            raw = await _httpClient.GetFromJsonAsync<Dictionary<string, SteamAppDetailsWrapper>>(url, cancellationToken);
        }
        catch
        {
            return searchResult;
        }

        if (raw is null ||
            !raw.TryGetValue(searchResult.RemoteId, out var wrapper) ||
            !wrapper.Success ||
            wrapper.Data is null)
        {
            return searchResult;
        }

        return MapDetails(searchResult.RemoteId, wrapper.Data);
    }

    // ── Mapping ─────────────────────────────────────────────────────────────

    private GameMetadataRecord MapSearchResult(SteamSearchItem item)
    {
        var appId = item.Id.ToString();
        return new GameMetadataRecord
        {
            ProviderId = Descriptor.Id,
            ProviderName = Descriptor.DisplayName,
            RemoteId = appId,
            RemoteSlug = appId,
            Title = item.Name ?? string.Empty,
            // Library portrait art (600×900) — best "box art" equivalent
            CoverImageUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg",
            BackgroundImageUrl = FirstNonEmpty(
                item.TinyImage,
                $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_hero.jpg",
                $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/page_bg_generated_v6b.jpg"),
            BannerImageUrl = FirstNonEmpty(
                item.TinyImage,
                $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg"),
            IconImageUrl = FirstNonEmpty(
                item.TinyImage,
                $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/capsule_184x69.jpg"),
            ProviderItemUrl = $"https://store.steampowered.com/app/{appId}",
            Platforms = ["PC"]
        };
    }

    private GameMetadataRecord MapDetails(string appId, SteamAppData data)
    {
        var screenshots = data.Screenshots
            .Select(s => s.PathFull ?? s.PathThumbnail ?? string.Empty)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Take(8)
            .ToList();

        var trailerUrl = data.Movies
            .Select(m => m.Mp4?.Max ?? m.Mp4?.Q480 ?? string.Empty)
            .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u)) ?? string.Empty;

        // Prefer the library portrait art; fall back to header image
        var coverUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg";
        var bannerUrl = FirstNonEmpty(
            data.HeaderImage,
            $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg",
            screenshots.FirstOrDefault());
        var iconUrl = FirstNonEmpty(
            BuildSteamIconUrl(appId, data.ClientIcon),
            data.HeaderImage,
            $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/capsule_184x69.jpg");
        var backgroundUrl = FirstNonEmpty(
            data.BackgroundRaw,
            data.Background,
            $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_hero.jpg",
            screenshots.FirstOrDefault(),
            data.HeaderImage,
            $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/page_bg_generated_v6b.jpg");

        var summary = FirstNonEmpty(
            data.ShortDescription,
            StripHtml(data.AboutTheGame),
            StripHtml(data.DetailedDescription));

        double? rating = null;
        if (data.Metacritic?.Score is double score && score > 0)
        {
            // Metacritic 0–100 → our 0–5 scale
            rating = Math.Round(score / 20.0, 2);
        }

        return new GameMetadataRecord
        {
            ProviderId = Descriptor.Id,
            ProviderName = Descriptor.DisplayName,
            RemoteId = appId,
            RemoteSlug = appId,
            Title = data.Name ?? string.Empty,
            Summary = summary,
            ReleaseDate = TryParseDate(data.ReleaseDate?.Date),
            CoverImageUrl = coverUrl,
            BackgroundImageUrl = backgroundUrl,
            BannerImageUrl = bannerUrl,
            IconImageUrl = iconUrl,
            WebsiteUrl = data.Website ?? string.Empty,
            ProviderItemUrl = $"https://store.steampowered.com/app/{appId}",
            CommunityRating = rating,
            Platforms = ["PC"],
            Genres = data.Genres
                .Select(g => g.Description ?? string.Empty)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .ToList(),
            Developers = data.Developers ?? [],
            Publishers = data.Publishers ?? [],
            Screenshots = screenshots,
            TrailerUrl = trailerUrl
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string BuildSteamIconUrl(string appId, string? clientIcon)
    {
        return string.IsNullOrWhiteSpace(clientIcon)
            ? string.Empty
            : $"https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/{appId}/{clientIcon}.jpg";
    }

    private static double ScoreSearchResult(string query, string title)
    {
        var normalizedQuery = NormalizeTitle(query);
        var normalizedTitle = NormalizeTitle(title);
        if (string.IsNullOrWhiteSpace(normalizedQuery) || string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return 0;
        }

        var queryTokens = Tokenize(normalizedQuery);
        var titleTokens = Tokenize(normalizedTitle);
        if (queryTokens.Count == 0 || titleTokens.Count == 0)
        {
            return 0;
        }

        var score = 0d;
        if (normalizedTitle.Equals(normalizedQuery, StringComparison.Ordinal))
        {
            score += 1.4;
        }
        else
        {
            if (normalizedTitle.StartsWith(normalizedQuery, StringComparison.Ordinal))
            {
                score += 0.5;
            }

            if (normalizedTitle.Contains(normalizedQuery, StringComparison.Ordinal))
            {
                score += 0.25;
            }
        }

        var intersection = queryTokens.Intersect(titleTokens, StringComparer.Ordinal).Count();
        var union = queryTokens.Union(titleTokens, StringComparer.Ordinal).Count();
        score += union == 0 ? 0 : intersection / (double)union;
        score += intersection / (double)queryTokens.Count * 0.45;
        score -= CalculateSearchPenalty(normalizedQuery, normalizedTitle);
        return score;
    }

    private static double CalculateSearchPenalty(string query, string title)
    {
        if (query.Equals(title, StringComparison.Ordinal))
        {
            return 0;
        }

        var titleTokens = Tokenize(title);
        var queryTokens = Tokenize(query);
        var penaltyTerms = new[]
        {
            "demo",
            "dlc",
            "pack",
            "bundle",
            "soundtrack",
            "upgrade",
            "season",
            "pass",
            "artbook",
            "cosmetic",
            "skin"
        };

        var penalty = penaltyTerms
            .Where(term => titleTokens.Contains(term) && !queryTokens.Contains(term))
            .Sum(_ => 0.24);

        if (title.Contains(" x ", StringComparison.Ordinal) &&
            !query.Contains(" x ", StringComparison.Ordinal))
        {
            penalty += 0.28;
        }

        if (title.Contains(" deluxe ", StringComparison.Ordinal) &&
            !query.Contains(" deluxe ", StringComparison.Ordinal))
        {
            penalty += 0.2;
        }

        return Math.Min(penalty, 0.8);
    }

    private static HashSet<string> Tokenize(string value) =>
        value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 1)
            .ToHashSet(StringComparer.Ordinal);

    private static string NormalizeTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return string.Join(
            ' ',
            builder.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
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

    private static DateTime? TryParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Contains("soon", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }

    // ── Steam API DTOs ───────────────────────────────────────────────────────

    private sealed class SteamSearchResponse
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("items")]
        public List<SteamSearchItem> Items { get; set; } = [];
    }

    private sealed class SteamSearchItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("tiny_image")]
        public string? TinyImage { get; set; }
    }

    private sealed class SteamAppDetailsWrapper
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public SteamAppData? Data { get; set; }
    }

    private sealed class SteamAppData
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("short_description")]
        public string? ShortDescription { get; set; }

        [JsonPropertyName("detailed_description")]
        public string? DetailedDescription { get; set; }

        [JsonPropertyName("about_the_game")]
        public string? AboutTheGame { get; set; }

        [JsonPropertyName("header_image")]
        public string? HeaderImage { get; set; }

        [JsonPropertyName("clienticon")]
        public string? ClientIcon { get; set; }

        [JsonPropertyName("background")]
        public string? Background { get; set; }

        [JsonPropertyName("background_raw")]
        public string? BackgroundRaw { get; set; }

        [JsonPropertyName("website")]
        public string? Website { get; set; }

        [JsonPropertyName("release_date")]
        public SteamReleaseDate? ReleaseDate { get; set; }

        [JsonPropertyName("metacritic")]
        public SteamMetacritic? Metacritic { get; set; }

        [JsonPropertyName("genres")]
        public List<SteamGenre> Genres { get; set; } = [];

        [JsonPropertyName("developers")]
        public List<string> Developers { get; set; } = [];

        [JsonPropertyName("publishers")]
        public List<string> Publishers { get; set; } = [];

        [JsonPropertyName("screenshots")]
        public List<SteamScreenshot> Screenshots { get; set; } = [];

        [JsonPropertyName("movies")]
        public List<SteamMovie> Movies { get; set; } = [];
    }

    private sealed class SteamReleaseDate
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }
    }

    private sealed class SteamMetacritic
    {
        [JsonPropertyName("score")]
        public double? Score { get; set; }
    }

    private sealed class SteamGenre
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    private sealed class SteamScreenshot
    {
        [JsonPropertyName("path_thumbnail")]
        public string? PathThumbnail { get; set; }

        [JsonPropertyName("path_full")]
        public string? PathFull { get; set; }
    }

    private sealed class SteamMovie
    {
        [JsonPropertyName("mp4")]
        public SteamMp4? Mp4 { get; set; }
    }

    private sealed class SteamMp4
    {
        [JsonPropertyName("480")]
        public string? Q480 { get; set; }

        [JsonPropertyName("max")]
        public string? Max { get; set; }
    }
}
