using CloudBerry.Core.Models;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CloudBerry.Core.Services.Metadata;

public sealed class IgdbMetadataProvider : IGameMetadataProvider
{
    private readonly HttpClient _httpClient;
    private string _accessToken = string.Empty;
    private DateTimeOffset _accessTokenExpiresAt = DateTimeOffset.MinValue;

    public IgdbMetadataProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public MetadataProviderDescriptor Descriptor { get; } = new()
    {
        Id = "igdb-direct",
        DisplayName = "IGDB",
        Description = "Uses the official IGDB API when Twitch client credentials are configured.",
        RequiresApiKey = true,
        ApiKeyLabel = "Twitch Client ID + Secret",
        ApiKeyHelpText = "IGDB requires a Twitch application client ID and secret. CloudBerry keeps those local."
    };

    public async Task<IReadOnlyList<GameMetadataRecord>> SearchAsync(
        GameMetadataSearchRequest request,
        CloudBerrySettings settings,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query) || !IsConfigured(settings))
        {
            return [];
        }

        var token = await GetAccessTokenAsync(settings, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return [];
        }

        var query = BuildGameQuery(request.Query, request.Platform, request.MaxResults);
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games")
        {
            Content = new StringContent(query, Encoding.UTF8, "text/plain")
        };
        message.Headers.TryAddWithoutValidation("Client-ID", settings.Igdb.ClientId.Trim());
        message.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var games = await response.Content.ReadFromJsonAsync<List<IgdbGame>>(cancellationToken: cancellationToken) ?? [];
        return games.Select(MapGame).Where(game => !string.IsNullOrWhiteSpace(game.Title)).ToList();
    }

    public Task<GameMetadataRecord?> GetDetailsAsync(
        GameMetadataRecord searchResult,
        CloudBerrySettings settings,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<GameMetadataRecord?>(searchResult);
    }

    private static bool IsConfigured(CloudBerrySettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.Igdb.ClientId) &&
               !string.IsNullOrWhiteSpace(settings.Igdb.ClientSecret);
    }

    private async Task<string> GetAccessTokenAsync(CloudBerrySettings settings, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) &&
            _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return _accessToken;
        }

        var url = "https://id.twitch.tv/oauth2/token" +
                  "?client_id=" + Uri.EscapeDataString(settings.Igdb.ClientId.Trim()) +
                  "&client_secret=" + Uri.EscapeDataString(settings.Igdb.ClientSecret.Trim()) +
                  "&grant_type=client_credentials";

        var token = await _httpClient.PostAsync(url, content: null, cancellationToken);
        if (token == null || !token.IsSuccessStatusCode)
        {
            return string.Empty;
        }

        var payload = await token.Content.ReadFromJsonAsync<IgdbTokenResponse>(cancellationToken: cancellationToken);
        _accessToken = payload?.AccessToken ?? string.Empty;
        _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, payload?.ExpiresIn ?? 3600));
        return _accessToken;
    }

    private static string BuildGameQuery(string query, string? platform, int maxResults)
    {
        var escaped = query.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var limit = Math.Clamp(maxResults, 1, 20);
        var platformFilter = BuildPlatformFilter(platform);
        var where = string.IsNullOrWhiteSpace(platformFilter)
            ? "where version_parent = null; "
            : $"where version_parent = null & {platformFilter}; ";
        return
            $"search \"{escaped}\"; " +
            "fields id,name,slug,summary,first_release_date,total_rating,url," +
            "cover.url,artworks.url,screenshots.url,genres.name,platforms.name," +
            "alternative_names.name,websites.url,involved_companies.company.name,involved_companies.developer,involved_companies.publisher,videos.video_id; " +
            where +
            $"limit {limit};";
    }

    private static string BuildPlatformFilter(string? platform)
    {
        if (string.IsNullOrWhiteSpace(platform))
        {
            return string.Empty;
        }

        var normalized = platform.Trim().ToLowerInvariant();
        var id = normalized switch
        {
            "pc" or "windows" or "steam" => 6,
            "nes" or "nintendo entertainment system" => 18,
            "snes" or "super nintendo" => 19,
            "nintendo 64" or "n64" => 4,
            "gamecube" or "game cube" => 21,
            "wii" => 5,
            "wii u" => 41,
            "switch" or "nintendo switch" => 130,
            "game boy" => 33,
            "game boy color" => 22,
            "game boy advance" or "gba" => 24,
            "nintendo ds" or "nds" => 20,
            "nintendo 3ds" or "3ds" => 37,
            "playstation" or "ps1" => 7,
            "playstation 2" or "ps2" => 8,
            "playstation 3" or "ps3" => 9,
            "playstation 4" or "ps4" => 48,
            "playstation 5" or "ps5" => 167,
            "psp" or "playstation portable" => 38,
            "dreamcast" or "sega dreamcast" => 23,
            "saturn" or "sega saturn" => 32,
            "genesis" or "mega drive" or "sega genesis" => 29,
            "xbox" => 11,
            "xbox 360" => 12,
            "xbox one" => 49,
            "xbox series" => 169,
            _ => 0
        };

        return id == 0 ? string.Empty : $"platforms = ({id})";
    }

    private GameMetadataRecord MapGame(IgdbGame game)
    {
        var developers = game.InvolvedCompanies
            .Where(company => company.Developer)
            .Select(company => company.Company?.Name ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var publishers = game.InvolvedCompanies
            .Where(company => company.Publisher)
            .Select(company => company.Company?.Name ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var screenshots = game.Screenshots
            .Select(image => NormalizeIgdbImageUrl(image.Url, "t_screenshot_big"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Take(8)
            .ToList();

        var artwork = game.Artworks
            .Select(image => NormalizeIgdbImageUrl(image.Url, "t_1080p"))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

        var cover = NormalizeIgdbImageUrl(game.Cover?.Url, "t_cover_big");
        var trailerId = game.Videos.FirstOrDefault(video => !string.IsNullOrWhiteSpace(video.VideoId))?.VideoId ?? string.Empty;

        return new GameMetadataRecord
        {
            ProviderId = Descriptor.Id,
            ProviderName = Descriptor.DisplayName,
            RemoteId = game.Id.ToString(),
            RemoteSlug = game.Slug ?? game.Id.ToString(),
            Title = game.Name ?? string.Empty,
            Summary = game.Summary ?? string.Empty,
            ReleaseDate = game.FirstReleaseDate.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(game.FirstReleaseDate.Value).DateTime
                : null,
            CoverImageUrl = cover,
            BackgroundImageUrl = FirstNonEmpty(artwork, screenshots.FirstOrDefault(), cover),
            BannerImageUrl = FirstNonEmpty(screenshots.FirstOrDefault(), artwork, cover),
            IconImageUrl = cover,
            WebsiteUrl = game.Url ?? string.Empty,
            ProviderItemUrl = game.Url ?? string.Empty,
            CommunityRating = game.TotalRating.HasValue ? Math.Round(game.TotalRating.Value / 20d, 2) : null,
            Platforms = game.Platforms.Select(platform => platform.Name ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).ToList(),
            Genres = game.Genres.Select(genre => genre.Name ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).ToList(),
            Developers = developers,
            Publishers = publishers,
            Screenshots = screenshots,
            TrailerUrl = string.IsNullOrWhiteSpace(trailerId) ? string.Empty : "https://www.youtube.com/watch?v=" + trailerId
        };
    }

    private static string NormalizeIgdbImageUrl(string? url, string size)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var normalized = url.StartsWith("//", StringComparison.Ordinal) ? "https:" + url : url;
        return normalized.Replace("t_thumb", size, StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private sealed class IgdbTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class IgdbGame
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public string? Slug { get; set; }
        public string? Summary { get; set; }
        public string? Url { get; set; }

        [JsonPropertyName("first_release_date")]
        public long? FirstReleaseDate { get; set; }

        [JsonPropertyName("total_rating")]
        public double? TotalRating { get; set; }

        public IgdbImage? Cover { get; set; }
        public List<IgdbImage> Artworks { get; set; } = [];
        public List<IgdbImage> Screenshots { get; set; } = [];
        public List<IgdbNamedItem> Genres { get; set; } = [];
        public List<IgdbNamedItem> Platforms { get; set; } = [];

        [JsonPropertyName("involved_companies")]
        public List<IgdbInvolvedCompany> InvolvedCompanies { get; set; } = [];

        public List<IgdbVideo> Videos { get; set; } = [];
    }

    private sealed class IgdbImage
    {
        public string? Url { get; set; }
    }

    private sealed class IgdbNamedItem
    {
        public string? Name { get; set; }
    }

    private sealed class IgdbInvolvedCompany
    {
        public bool Developer { get; set; }
        public bool Publisher { get; set; }
        public IgdbNamedItem? Company { get; set; }
    }

    private sealed class IgdbVideo
    {
        [JsonPropertyName("video_id")]
        public string? VideoId { get; set; }
    }
}
