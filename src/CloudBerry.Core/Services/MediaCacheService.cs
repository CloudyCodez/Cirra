using System.Collections.Concurrent;
using System.Net.Http.Headers;

namespace CloudBerry.Core.Services;

public sealed class MediaCacheService
{
    private readonly HttpClient _httpClient;
    // Deduplicates concurrent downloads of the same URL — only one HTTP request flies at a time per URL.
    private readonly ConcurrentDictionary<string, Task<string>> _inFlight = new(StringComparer.Ordinal);

    public MediaCacheService(HttpClient? httpClient = null, string? mediaRootPath = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestHeaders = { { "User-Agent", "Cirra/1.0" } }
        };
        MediaRootPath = mediaRootPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CloudBerry",
            "media");
    }

    public string MediaRootPath { get; }

    public Task<string> CacheImageAsync(
        string source,
        string libraryItemKey,
        string assetName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
            return Task.FromResult(string.Empty);

        // Local file — resolve immediately without touching the network.
        if (File.Exists(source))
            return Task.FromResult(Path.GetFullPath(source));

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return Task.FromResult(string.Empty);

        // Reuse any in-flight download for the same URL to avoid duplicate HTTP requests.
        return _inFlight.GetOrAdd(uri.AbsoluteUri, _ => FetchAndCacheAsync(uri, libraryItemKey, assetName, cancellationToken))
            .ContinueWith(t => { _inFlight.TryRemove(uri.AbsoluteUri, out _); return t.Result; },
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private async Task<string> FetchAndCacheAsync(
        Uri uri,
        string libraryItemKey,
        string assetName,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(MediaRootPath);

        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var extension = ResolveExtension(response.Content.Headers.ContentType, uri.AbsolutePath);
        var folderPath = Path.Combine(MediaRootPath, SanitizePathSegment(libraryItemKey));
        Directory.CreateDirectory(folderPath);

        var filePath = Path.Combine(folderPath, $"{SanitizePathSegment(assetName)}{extension}");
        if (File.Exists(filePath) && new FileInfo(filePath).Length > 0)
            return filePath;

        // Write to a temp path then rename to avoid partially-written files being served.
        var tmpPath = filePath + ".tmp";
        await using (var destination = File.Create(tmpPath))
        {
            await response.Content.CopyToAsync(destination, cancellationToken);
        }
        File.Move(tmpPath, filePath, overwrite: true);
        return filePath;
    }

    private static string ResolveExtension(MediaTypeHeaderValue? contentType, string absolutePath)
    {
        var extension = Path.GetExtension(absolutePath);
        if (!string.IsNullOrWhiteSpace(extension) && extension.Length <= 5)
        {
            return extension.ToLowerInvariant();
        }

        return contentType?.MediaType?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/gif" => ".gif",
            _ => ".img"
        };
    }

    private static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "item";

        var invalid = Path.GetInvalidFileNameChars();
        // Use string.Create to avoid intermediate char[] allocation.
        var result = string.Create(value.Length, (value, invalid), static (span, state) =>
        {
            var (src, inv) = state;
            for (var i = 0; i < src.Length; i++)
            {
                var c = src[i];
                span[i] = Array.IndexOf(inv, c) >= 0 ? '_' : c;
            }
        });

        return string.IsNullOrWhiteSpace(result) ? "item" : result;
    }
}
