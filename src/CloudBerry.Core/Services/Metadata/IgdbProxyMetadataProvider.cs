using CloudBerry.Core.Models;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CloudBerry.Core.Services.Metadata;

public sealed class IgdbProxyMetadataProvider : IGameMetadataProvider
{
    private readonly HttpClient _httpClient;

    public IgdbProxyMetadataProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public MetadataProviderDescriptor Descriptor { get; } = new()
    {
        Id = "igdb-proxy",
        DisplayName = "IGDB Proxy",
        Description = "Queries a Cirra-compatible IGDB proxy so users do not manage Twitch tokens locally.",
        RequiresApiKey = false,
        ApiKeyLabel = string.Empty,
        ApiKeyHelpText = "Point Cirra at a trusted proxy that holds IGDB credentials server-side."
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

        var endpoint = string.IsNullOrWhiteSpace(settings.Igdb.ProxyEndpoint)
            ? CloudBerrySettings.BuiltInIgdbProxyEndpoint
            : settings.Igdb.ProxyEndpoint.Trim();
        endpoint = endpoint.TrimEnd('/');
        var getUrl = endpoint + "/search?query=" + Uri.EscapeDataString(request.Query) +
                     "&platform=" + Uri.EscapeDataString(request.Platform ?? string.Empty) +
                     "&limit=" + Math.Clamp(request.MaxResults, 1, 20);

        try
        {
            var getRecords = await _httpClient.GetFromJsonAsync<List<ProxyGame>>(getUrl, cancellationToken);
            if (getRecords is { Count: > 0 })
            {
                return getRecords.Select(MapGame).Where(game => !string.IsNullOrWhiteSpace(game.Title)).ToList();
            }
        }
        catch
        {
        }

        try
        {
            var body = JsonSerializer.Serialize(new
            {
                query = request.Query,
                platform = request.Platform ?? string.Empty,
                limit = Math.Clamp(request.MaxResults, 1, 20)
            });
            using var response = await _httpClient.PostAsync(
                endpoint.TrimEnd('/') + "/metadata/search",
                new StringContent(body, Encoding.UTF8, "application/json"),
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var records = await response.Content.ReadFromJsonAsync<List<ProxyGame>>(cancellationToken: cancellationToken) ?? [];
            return records.Select(MapGame).Where(game => !string.IsNullOrWhiteSpace(game.Title)).ToList();
        }
        catch
        {
            return [];
        }
    }

    public Task<GameMetadataRecord?> GetDetailsAsync(
        GameMetadataRecord searchResult,
        CloudBerrySettings settings,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<GameMetadataRecord?>(searchResult);
    }

    private GameMetadataRecord MapGame(ProxyGame game)
    {
        return new GameMetadataRecord
        {
            ProviderId = Descriptor.Id,
            ProviderName = Descriptor.DisplayName,
            RemoteId = FirstNonEmpty(game.Id, game.RemoteId),
            RemoteSlug = FirstNonEmpty(game.Slug, game.RemoteSlug, game.Id, game.RemoteId),
            Title = FirstNonEmpty(game.Title, game.Name),
            Summary = FirstNonEmpty(game.Summary, game.Description),
            ReleaseDate = ParseDate(game.ReleaseDate),
            CoverImageUrl = NormalizeImage(game.CoverImageUrl),
            BackgroundImageUrl = NormalizeImage(FirstNonEmpty(game.BackgroundImageUrl, game.ArtworkUrl, game.Screenshots.FirstOrDefault())),
            BannerImageUrl = NormalizeImage(FirstNonEmpty(game.BannerImageUrl, game.Screenshots.FirstOrDefault(), game.CoverImageUrl)),
            IconImageUrl = NormalizeImage(FirstNonEmpty(game.IconImageUrl, game.CoverImageUrl)),
            WebsiteUrl = FirstNonEmpty(game.WebsiteUrl, game.Url),
            ProviderItemUrl = FirstNonEmpty(game.ProviderItemUrl, game.Url, game.WebsiteUrl),
            CommunityRating = NormalizeRating(game.CommunityRating ?? game.Rating),
            Platforms = game.Platforms ?? [],
            Genres = game.Genres ?? [],
            Developers = game.Developers ?? [],
            Publishers = game.Publishers ?? [],
            Tags = game.Tags ?? [],
            Screenshots = (game.Screenshots ?? []).Select(NormalizeImage).Where(value => !string.IsNullOrWhiteSpace(value)).Take(8).ToList(),
            TrailerUrl = game.TrailerUrl ?? string.Empty,
            Guidebooks = (game.Guidebooks ?? []).Where(guide => !string.IsNullOrWhiteSpace(guide.PdfLocation)).Select(guide => guide.Clone()).ToList()
        };
    }

    private static string NormalizeImage(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var normalized = url.StartsWith("//", StringComparison.Ordinal) ? "https:" + url : url;
        return normalized.Replace("t_thumb", "t_1080p", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime? ParseDate(string? value)
    {
        return DateTime.TryParse(value, out var date) ? date : null;
    }

    private static double? NormalizeRating(double? rating)
    {
        if (!rating.HasValue)
        {
            return null;
        }

        return rating.Value > 5 ? Math.Round(rating.Value / 20d, 2) : rating.Value;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private sealed class ProxyGame
    {
        public string? Id { get; set; }
        public string? RemoteId { get; set; }
        public string? Slug { get; set; }
        public string? RemoteSlug { get; set; }
        public string? Title { get; set; }
        public string? Name { get; set; }
        public string? Summary { get; set; }
        public string? Description { get; set; }
        public string? ReleaseDate { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? BackgroundImageUrl { get; set; }
        public string? BannerImageUrl { get; set; }
        public string? IconImageUrl { get; set; }
        public string? ArtworkUrl { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? ProviderItemUrl { get; set; }
        public string? Url { get; set; }
        public double? CommunityRating { get; set; }
        public double? Rating { get; set; }
        public List<string> Platforms { get; set; } = [];
        public List<string> Genres { get; set; } = [];
        public List<string> Developers { get; set; } = [];
        public List<string> Publishers { get; set; } = [];
        public List<string> Tags { get; set; } = [];
        public List<string> Screenshots { get; set; } = [];
        public string? TrailerUrl { get; set; }
        public List<GameGuidebook> Guidebooks { get; set; } = [];
    }
}
