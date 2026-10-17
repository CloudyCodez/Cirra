using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services.Metadata;

public sealed class RawgMetadataProvider : IGameMetadataProvider
{
    private readonly HttpClient _httpClient;

    public RawgMetadataProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri("https://api.rawg.io/api/");
        }
    }

    public MetadataProviderDescriptor Descriptor { get; } = new()
    {
        Id = "rawg",
        DisplayName = "RAWG",
        Description = "Search online game metadata, artwork, genres, release dates, and store links from RAWG.",
        RequiresApiKey = true,
        ApiKeyLabel = "RAWG API key",
        ApiKeyHelpText = "Create a RAWG API key and paste it here to enable online metadata lookups."
    };

    public async Task<IReadOnlyList<GameMetadataRecord>> SearchAsync(
        GameMetadataSearchRequest request,
        CloudBerrySettings settings,
        CancellationToken cancellationToken = default)
    {
        var apiKey = RequireApiKey(settings);
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return [];
        }

        var query = $"games?key={Uri.EscapeDataString(apiKey)}&search={Uri.EscapeDataString(request.Query)}&search_precise=true&page_size={Math.Clamp(request.MaxResults, 1, 20)}";
        var response = await _httpClient.GetFromJsonAsync<RawgSearchResponse>(query, cancellationToken)
                       ?? new RawgSearchResponse();

        return response.Results
            .Select(MapSearchResult)
            .Where(result => !string.IsNullOrWhiteSpace(result.Title))
            .ToArray();
    }

    public async Task<GameMetadataRecord?> GetDetailsAsync(
        GameMetadataRecord searchResult,
        CloudBerrySettings settings,
        CancellationToken cancellationToken = default)
    {
        var apiKey = RequireApiKey(settings);
        if (string.IsNullOrWhiteSpace(searchResult.RemoteId))
        {
            return searchResult;
        }

        var query = $"games/{Uri.EscapeDataString(searchResult.RemoteId)}?key={Uri.EscapeDataString(apiKey)}";
        var response = await _httpClient.GetFromJsonAsync<RawgGameDetailsResponse>(query, cancellationToken);
        return response is null ? searchResult : MapDetails(response);
    }

    private static string RequireApiKey(CloudBerrySettings settings)
    {
        var apiKey = settings.Rawg.ApiKey.Trim();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return apiKey;
        }

        throw new InvalidOperationException("Add a RAWG API key in Cirra before searching for online metadata.");
    }

    private GameMetadataRecord MapSearchResult(RawgGameSummaryResponse source)
    {
        return new GameMetadataRecord
        {
            ProviderId = Descriptor.Id,
            ProviderName = Descriptor.DisplayName,
            RemoteId = source.Id.ToString(),
            RemoteSlug = source.Slug ?? string.Empty,
            Title = source.Name ?? string.Empty,
            Summary = string.Empty,
            ReleaseDate = TryParseDate(source.Released),
            CoverImageUrl = source.BackgroundImage ?? string.Empty,
            BackgroundImageUrl = source.BackgroundImage ?? string.Empty,
            WebsiteUrl = string.Empty,
            ProviderItemUrl = string.IsNullOrWhiteSpace(source.Slug) ? string.Empty : $"https://rawg.io/games/{source.Slug}",
            CommunityRating = source.Rating,
            Platforms = source.Platforms.Select(entry => entry.Platform?.Name ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Genres = source.Genres.Select(entry => entry.Name ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Tags = source.Tags.Select(entry => entry.Name ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private GameMetadataRecord MapDetails(RawgGameDetailsResponse source)
    {
        return new GameMetadataRecord
        {
            ProviderId = Descriptor.Id,
            ProviderName = Descriptor.DisplayName,
            RemoteId = source.Id.ToString(),
            RemoteSlug = source.Slug ?? string.Empty,
            Title = source.Name ?? string.Empty,
            Summary = source.DescriptionRaw ?? string.Empty,
            ReleaseDate = TryParseDate(source.Released),
            CoverImageUrl = source.BackgroundImage ?? string.Empty,
            BackgroundImageUrl = source.BackgroundImageAdditional ?? source.BackgroundImage ?? string.Empty,
            WebsiteUrl = source.Website ?? string.Empty,
            ProviderItemUrl = string.IsNullOrWhiteSpace(source.Slug) ? string.Empty : $"https://rawg.io/games/{source.Slug}",
            CommunityRating = source.Rating,
            Platforms = source.Platforms.Select(entry => entry.Platform?.Name ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Genres = source.Genres.Select(entry => entry.Name ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Developers = source.Developers.Select(entry => entry.Name ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Publishers = source.Publishers.Select(entry => entry.Name ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Tags = source.Tags.Select(entry => entry.Name ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static DateTime? TryParseDate(string? value)
    {
        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }

    private sealed class RawgSearchResponse
    {
        [JsonPropertyName("results")]
        public List<RawgGameSummaryResponse> Results { get; set; } = [];
    }

    private sealed class RawgGameDetailsResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("slug")]
        public string? Slug { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description_raw")]
        public string? DescriptionRaw { get; set; }

        [JsonPropertyName("released")]
        public string? Released { get; set; }

        [JsonPropertyName("background_image")]
        public string? BackgroundImage { get; set; }

        [JsonPropertyName("background_image_additional")]
        public string? BackgroundImageAdditional { get; set; }

        [JsonPropertyName("website")]
        public string? Website { get; set; }

        [JsonPropertyName("rating")]
        public double? Rating { get; set; }

        [JsonPropertyName("platforms")]
        public List<RawgPlatformEntry> Platforms { get; set; } = [];

        [JsonPropertyName("genres")]
        public List<RawgNamedItem> Genres { get; set; } = [];

        [JsonPropertyName("developers")]
        public List<RawgNamedItem> Developers { get; set; } = [];

        [JsonPropertyName("publishers")]
        public List<RawgNamedItem> Publishers { get; set; } = [];

        [JsonPropertyName("tags")]
        public List<RawgNamedItem> Tags { get; set; } = [];
    }

    private sealed class RawgGameSummaryResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("slug")]
        public string? Slug { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("released")]
        public string? Released { get; set; }

        [JsonPropertyName("background_image")]
        public string? BackgroundImage { get; set; }

        [JsonPropertyName("rating")]
        public double? Rating { get; set; }

        [JsonPropertyName("platforms")]
        public List<RawgPlatformEntry> Platforms { get; set; } = [];

        [JsonPropertyName("genres")]
        public List<RawgNamedItem> Genres { get; set; } = [];

        [JsonPropertyName("tags")]
        public List<RawgNamedItem> Tags { get; set; } = [];
    }

    private sealed class RawgPlatformEntry
    {
        [JsonPropertyName("platform")]
        public RawgNamedItem? Platform { get; set; }
    }

    private sealed class RawgNamedItem
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
