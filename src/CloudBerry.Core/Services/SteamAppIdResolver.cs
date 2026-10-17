using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CloudBerry.Core.Services;

public sealed class SteamAppIdResolver
{
    private readonly HttpClient _httpClient;

    public SteamAppIdResolver(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Cirra/2.0 (achievement metadata resolver; no commercial use)");
    }

    public async Task<string> ResolveByTitleAsync(
        string title,
        string platform = "",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var url = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(title)}&l=english&cc=US";
        SteamStoreSearchResponse? response;
        try
        {
            response = await _httpClient.GetFromJsonAsync<SteamStoreSearchResponse>(url, cancellationToken);
        }
        catch
        {
            return string.Empty;
        }

        var normalizedTitle = Normalize(title);
        var best = (response?.Items ?? [])
            .Select(item => new
            {
                Item = item,
                Score = ScoreMatch(normalizedTitle, Normalize(item.Name ?? string.Empty), platform, item.Platforms)
            })
            .Where(match => match.Score >= 0.72)
            .OrderByDescending(match => match.Score)
            .FirstOrDefault();

        return best?.Item.Id > 0 ? best.Item.Id.ToString() : string.Empty;
    }

    public static string TryExtractAppIdFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var marker = "/app/";
        var markerIndex = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return string.Empty;
        }

        var start = markerIndex + marker.Length;
        var digits = new string(url[start..].TakeWhile(char.IsDigit).ToArray());
        return digits;
    }

    private static double ScoreMatch(
        string normalizedQuery,
        string normalizedCandidate,
        string platform,
        SteamStorePlatforms? platforms)
    {
        if (string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return 0;
        }

        var score = normalizedCandidate == normalizedQuery
            ? 1.0
            : normalizedCandidate.Contains(normalizedQuery, StringComparison.Ordinal) ||
              normalizedQuery.Contains(normalizedCandidate, StringComparison.Ordinal)
                ? 0.86
                : TokenOverlapScore(normalizedQuery, normalizedCandidate);

        if (platform.Contains("PC", StringComparison.OrdinalIgnoreCase) &&
            platforms?.Windows == true)
        {
            score += 0.05;
        }

        return Math.Min(1, score);
    }

    private static double TokenOverlapScore(string left, string right)
    {
        var leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        if (leftTokens.Count == 0 || rightTokens.Count == 0)
        {
            return 0;
        }

        var overlap = leftTokens.Count(token => rightTokens.Contains(token));
        var union = leftTokens.Union(rightTokens).Count();
        return union == 0 ? 0 : overlap / (double)union;
    }

    private static string Normalize(string value)
    {
        var cleaned = new string(value
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray());

        return string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed class SteamStoreSearchResponse
    {
        [JsonPropertyName("items")]
        public List<SteamStoreSearchItem> Items { get; set; } = [];
    }

    private sealed class SteamStoreSearchItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("platforms")]
        public SteamStorePlatforms? Platforms { get; set; }
    }

    private sealed class SteamStorePlatforms
    {
        [JsonPropertyName("windows")]
        public bool Windows { get; set; }
    }
}
