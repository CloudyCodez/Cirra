using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services.Metadata;

/// <summary>
/// Pulls game metadata and critic scores from the public OpenCritic API.
/// Zero credentials required.
/// </summary>
public sealed class OpenCriticMetadataProvider : IGameMetadataProvider
{
    private readonly HttpClient _httpClient;

    public OpenCriticMetadataProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Cirra/2.0 (universal game library; no commercial use)");
    }

    public MetadataProviderDescriptor Descriptor { get; } = new()
    {
        Id = "opencritic",
        DisplayName = "OpenCritic",
        Description = "Pulls game info, critic scores, screenshots, and genre data from OpenCritic. No API key required.",
        RequiresApiKey = false,
        ApiKeyLabel = string.Empty,
        ApiKeyHelpText = "OpenCritic is completely free — no setup needed."
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

        var url = $"https://api.opencritic.com/api/game/search?criteria={Uri.EscapeDataString(request.Query)}";

        List<OpenCriticSearchResult>? results;
        try
        {
            results = await _httpClient.GetFromJsonAsync<List<OpenCriticSearchResult>>(url, cancellationToken);
        }
        catch
        {
            return [];
        }

        return (results ?? [])
            .Take(Math.Clamp(request.MaxResults, 1, 20))
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

        var url = $"https://api.opencritic.com/api/game/{Uri.EscapeDataString(searchResult.RemoteId)}";

        OpenCriticGameDetail? detail;
        try
        {
            detail = await _httpClient.GetFromJsonAsync<OpenCriticGameDetail>(url, cancellationToken);
        }
        catch
        {
            return searchResult;
        }

        return detail is null ? searchResult : MapDetails(detail);
    }

    // ── Mapping ─────────────────────────────────────────────────────────────

    private GameMetadataRecord MapSearchResult(OpenCriticSearchResult item)
    {
        return new GameMetadataRecord
        {
            ProviderId = Descriptor.Id,
            ProviderName = Descriptor.DisplayName,
            RemoteId = item.Id.ToString(),
            Title = item.Name ?? string.Empty,
            ProviderItemUrl = $"https://opencritic.com/game/{item.Id}"
        };
    }

    private GameMetadataRecord MapDetails(OpenCriticGameDetail detail)
    {
        var screenshots = detail.Screenshots
            .Select(s => s.FullSize ?? s.Thumbnail ?? string.Empty)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Take(8)
            .ToList();

        double? rating = null;
        if (detail.TopCriticScore is double score && score > 0)
        {
            rating = Math.Round(score / 20.0, 2);
        }

        var developers = detail.Companies
            .Where(c => c.Type?.Contains("DEVELOPER", StringComparison.OrdinalIgnoreCase) == true)
            .Select(c => c.Name ?? string.Empty)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        var publishers = detail.Companies
            .Where(c => c.Type?.Contains("PUBLISHER", StringComparison.OrdinalIgnoreCase) == true)
            .Select(c => c.Name ?? string.Empty)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        return new GameMetadataRecord
        {
            ProviderId = Descriptor.Id,
            ProviderName = Descriptor.DisplayName,
            RemoteId = detail.Id.ToString(),
            Title = detail.Name ?? string.Empty,
            Summary = detail.Description ?? string.Empty,
            ReleaseDate = detail.FirstReleaseDate,
            CoverImageUrl = detail.CoverImageUrl ?? string.Empty,
            BackgroundImageUrl = screenshots.FirstOrDefault() ?? detail.CoverImageUrl ?? string.Empty,
            ProviderItemUrl = $"https://opencritic.com/game/{detail.Id}",
            CommunityRating = rating,
            Platforms = detail.Platforms
                .Select(p => p.Name ?? string.Empty)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList(),
            Genres = detail.Genres
                .Select(g => g.Name ?? string.Empty)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .ToList(),
            Developers = developers,
            Publishers = publishers,
            Screenshots = screenshots
        };
    }

    // ── OpenCritic API DTOs ──────────────────────────────────────────────────

    private sealed class OpenCriticSearchResult
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class OpenCriticGameDetail
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("firstReleaseDate")]
        public DateTime? FirstReleaseDate { get; set; }

        [JsonPropertyName("coverImageUrl")]
        public string? CoverImageUrl { get; set; }

        [JsonPropertyName("topCriticScore")]
        public double? TopCriticScore { get; set; }

        [JsonPropertyName("percentRecommended")]
        public double? PercentRecommended { get; set; }

        [JsonPropertyName("Platforms")]
        public List<OpenCriticPlatform> Platforms { get; set; } = [];

        [JsonPropertyName("Genres")]
        public List<OpenCriticGenre> Genres { get; set; } = [];

        [JsonPropertyName("Companies")]
        public List<OpenCriticCompany> Companies { get; set; } = [];

        [JsonPropertyName("Screenshots")]
        public List<OpenCriticScreenshot> Screenshots { get; set; } = [];
    }

    private sealed class OpenCriticPlatform
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class OpenCriticGenre
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class OpenCriticCompany
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    private sealed class OpenCriticScreenshot
    {
        [JsonPropertyName("thumbnail")]
        public string? Thumbnail { get; set; }

        [JsonPropertyName("fullSize")]
        public string? FullSize { get; set; }
    }
}
