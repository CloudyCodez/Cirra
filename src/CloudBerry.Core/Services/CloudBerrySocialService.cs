using System.Net.Http.Json;
using System.Text.Json;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services;

public sealed class CloudBerrySocialService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly HttpClient _directHttpClient;

    public CloudBerrySocialService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateClient(useProxy: true);
        _directHttpClient = CreateClient(useProxy: false);
    }

    public async Task<CloudBerryFriendPresenceResult?> LookupAsync(string endpoint, string cloudCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(cloudCode))
        {
            return null;
        }

        var url = $"{endpoint.TrimEnd('/')}/friend/{Uri.EscapeDataString(cloudCode.Trim())}";
        return await SendWithFallbackAsync(
            (HttpClient client, CancellationToken token) => client.GetFromJsonAsync<CloudBerryFriendPresenceResult>(url, JsonOptions, token),
            cancellationToken);
    }

    public async Task<bool> PublishAsync(string endpoint, CloudBerryPresencePublishRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(request.CloudCode))
        {
            return false;
        }

        using var response = await SendWithFallbackAsync(
            (HttpClient client, CancellationToken token) => client.PostAsJsonAsync($"{endpoint.TrimEnd('/')}/publish", request, JsonOptions, token),
            cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> HeartbeatAsync(string endpoint, string cloudCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(cloudCode))
        {
            return false;
        }

        using var response = await SendWithFallbackAsync(
            (HttpClient client, CancellationToken token) => client.PostAsJsonAsync(
                $"{endpoint.TrimEnd('/')}/heartbeat",
                new CloudBerryPresenceHeartbeatRequest { CloudCode = cloudCode.Trim() },
                JsonOptions,
                token),
            cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private static HttpClient CreateClient(bool useProxy)
    {
        var handler = new SocketsHttpHandler
        {
            UseProxy = useProxy,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CloudBerry/1.1");
        return client;
    }

    private async Task<T> SendWithFallbackAsync<T>(Func<HttpClient, CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        try
        {
            return await operation(_httpClient, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return await operation(_directHttpClient, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await operation(_directHttpClient, cancellationToken);
        }
    }
}

public sealed class CloudBerryPresencePublishRequest
{
    public string CloudCode { get; set; } = string.Empty;

    public string IconKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Motto { get; set; } = string.Empty;

    public string PresenceLine { get; set; } = string.Empty;

    public string HighlightTone { get; set; } = string.Empty;

    public string ShellStyle { get; set; } = string.Empty;

    public string PreferredTheme { get; set; } = string.Empty;

    public string FavoritePlatform { get; set; } = string.Empty;

    public int LibraryCount { get; set; }

    public int AchievementCount { get; set; }

    public int GamerScore { get; set; }
}

public sealed class CloudBerryPresenceHeartbeatRequest
{
    public string CloudCode { get; set; } = string.Empty;
}

public sealed class CloudBerryFriendPresenceResult
{
    public string CloudCode { get; set; } = string.Empty;

    public string IconKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Motto { get; set; } = string.Empty;

    public string PresenceLine { get; set; } = string.Empty;

    public string HighlightTone { get; set; } = string.Empty;

    public string ShellStyle { get; set; } = string.Empty;

    public string PreferredTheme { get; set; } = string.Empty;

    public string FavoritePlatform { get; set; } = string.Empty;

    public int LibraryCount { get; set; }

    public int AchievementCount { get; set; }

    public int GamerScore { get; set; }

    public bool IsOnline { get; set; }

    public DateTime LastSeenAtUtc { get; set; }
}
