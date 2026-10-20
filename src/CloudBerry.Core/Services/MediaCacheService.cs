using System.Collections.Concurrent;
using System.Net.Http.Headers;

namespace CloudBerry.Core.Services;

public sealed class MediaCacheService
{
    private const long DefaultCacheBudgetBytes = 1_500L * 1024 * 1024;
    private const long DefaultMaximumAssetBytes = 32L * 1024 * 1024;
    private readonly HttpClient _httpClient;
    // Deduplicates concurrent downloads of the same URL — only one HTTP request flies at a time per URL.
    private readonly ConcurrentDictionary<string, Task<string>> _inFlight = new(StringComparer.Ordinal);

    public MediaCacheService(
        HttpClient? httpClient = null,
        string? mediaRootPath = null,
        long cacheBudgetBytes = DefaultCacheBudgetBytes,
        long maximumAssetBytes = DefaultMaximumAssetBytes)
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
        CacheBudgetBytes = Math.Max(1, cacheBudgetBytes);
        MaximumAssetBytes = Math.Max(1, maximumAssetBytes);
    }

    public string MediaRootPath { get; }

    public long CacheBudgetBytes { get; }

    public long MaximumAssetBytes { get; }

    public async Task<string> CacheImageAsync(
        string source,
        string libraryItemKey,
        string assetName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
            return string.Empty;

        // Local file — resolve immediately without touching the network.
        if (File.Exists(source))
            return Path.GetFullPath(source);

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return string.Empty;

        var cachedPath = TryFindCachedAsset(libraryItemKey, assetName);
        if (!string.IsNullOrWhiteSpace(cachedPath))
        {
            TouchCachedAsset(cachedPath);
            return cachedPath;
        }

        // The shared fetch intentionally has no caller cancellation token. Closing one details
        // view must not cancel a download another active surface is awaiting.
        var fetchTask = _inFlight.GetOrAdd(uri.AbsoluteUri,
            _ => FetchAndCacheAsync(uri, libraryItemKey, assetName));
        _ = RemoveCompletedFetchAsync(uri.AbsoluteUri, fetchTask);

        return cancellationToken.CanBeCanceled
            ? await fetchTask.WaitAsync(cancellationToken)
            : await fetchTask;
    }

    private async Task RemoveCompletedFetchAsync(string key, Task<string> fetchTask)
    {
        try
        {
            await fetchTask.ConfigureAwait(false);
        }
        catch
        {
            // The caller receives the original failure. This continuation only owns cleanup.
        }
        finally
        {
            if (_inFlight.TryGetValue(key, out var current) && ReferenceEquals(current, fetchTask))
            {
                _inFlight.TryRemove(key, out _);
            }
        }
    }

    private async Task<string> FetchAndCacheAsync(
        Uri uri,
        string libraryItemKey,
        string assetName)
    {
        Directory.CreateDirectory(MediaRootPath);

        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength > MaximumAssetBytes)
        {
            throw new InvalidOperationException("The artwork source is larger than Cirra's per-asset cache limit.");
        }

        var extension = ResolveExtension(response.Content.Headers.ContentType, uri.AbsolutePath);
        var folderPath = Path.Combine(MediaRootPath, SanitizePathSegment(libraryItemKey));
        Directory.CreateDirectory(folderPath);

        var filePath = Path.Combine(folderPath, $"{SanitizePathSegment(assetName)}{extension}");
        if (File.Exists(filePath) && new FileInfo(filePath).Length > 0)
        {
            TouchCachedAsset(filePath);
            return filePath;
        }

        // Write to a temp path then rename to avoid partially-written files being served.
        var tmpPath = filePath + "." + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            {
                await using var source = await response.Content.ReadAsStreamAsync(CancellationToken.None);
                await using var destination = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);
                var buffer = new byte[64 * 1024];
                long bytesWritten = 0;
                int read;
                while ((read = await source.ReadAsync(buffer)) > 0)
                {
                    bytesWritten += read;
                    if (bytesWritten > MaximumAssetBytes)
                    {
                        throw new InvalidOperationException("The artwork source exceeded Cirra's per-asset cache limit.");
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read));
                }

                await destination.FlushAsync();
            }
            File.Move(tmpPath, filePath, overwrite: true);
            TouchCachedAsset(filePath);
            TrimCacheToBudget();
            return filePath;
        }
        finally
        {
            TryDeleteFile(tmpPath);
        }
    }

    private string TryFindCachedAsset(string libraryItemKey, string assetName)
    {
        var folderPath = Path.Combine(MediaRootPath, SanitizePathSegment(libraryItemKey));
        if (!Directory.Exists(folderPath))
        {
            return string.Empty;
        }

        try
        {
            return Directory.EnumerateFiles(folderPath, SanitizePathSegment(assetName) + ".*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(path => new FileInfo(path).Length > 0) ?? string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private void TrimCacheToBudget()
    {
        try
        {
            var files = Directory.EnumerateFiles(MediaRootPath, "*", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
                .Select(path => new FileInfo(path))
                .OrderBy(file => file.LastWriteTimeUtc)
                .ToList();
            var totalBytes = files.Sum(file => file.Length);
            foreach (var file in files)
            {
                if (totalBytes <= CacheBudgetBytes)
                {
                    break;
                }

                totalBytes -= file.Length;
                TryDeleteFile(file.FullName);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A full cache is recoverable. A later successful write will attempt cleanup again.
        }
    }

    private static void TouchCachedAsset(string path)
    {
        try
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Last-write time is only an eviction hint, never a reason to fail artwork loading.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort only; the next cache maintenance pass can retry.
        }
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
