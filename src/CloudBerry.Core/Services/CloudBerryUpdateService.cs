using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace CloudBerry.Core.Services;

public sealed class CloudBerryUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly HttpClient _directHttpClient;

    public CloudBerryUpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateClient(useProxy: true);
        _directHttpClient = CreateClient(useProxy: false);
    }

    public async Task<CloudBerryUpdateCheckResult> CheckAsync(string endpoint, Version currentVersion, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return CloudBerryUpdateCheckResult.NoUpdate("No update feed is configured.");
        }

        var manifest = await SendWithFallbackAsync(
            (client, token) => client.GetFromJsonAsync<CloudBerryUpdateManifest>(endpoint, JsonOptions, token),
            cancellationToken);

        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
        {
            return CloudBerryUpdateCheckResult.NoUpdate("The update feed did not return a valid manifest.");
        }

        if (!Version.TryParse(manifest.Version, out var availableVersion))
        {
            return CloudBerryUpdateCheckResult.NoUpdate("The update feed returned an unreadable version number.");
        }

        var manifestUri = new Uri(endpoint, UriKind.Absolute);
        var installerUrl = string.IsNullOrWhiteSpace(manifest.InstallerUrl)
            ? string.Empty
            : new Uri(manifestUri, manifest.InstallerUrl).ToString();
        var chunkUrls = manifest.ChunkUrls?
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => new Uri(manifestUri, url).ToString())
            .ToArray() ?? [];

        var normalized = manifest with
        {
            InstallerUrl = installerUrl,
            ChunkUrls = chunkUrls
        };

        return availableVersion > currentVersion
            ? CloudBerryUpdateCheckResult.UpdateAvailable(normalized, availableVersion)
            : CloudBerryUpdateCheckResult.NoUpdate($"Cirra {currentVersion} is current.");
    }

    public async Task<string> DownloadInstallerAsync(CloudBerryUpdateManifest manifest, string destinationDirectory, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(manifest.InstallerUrl) && (manifest.ChunkUrls == null || manifest.ChunkUrls.Count == 0))
        {
            throw new InvalidOperationException("The update manifest does not include a downloadable package.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Sha256) || manifest.Sha256.Length != 64 ||
            !manifest.Sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidOperationException("The update manifest does not include a valid SHA-256 checksum.");
        }

        if (!string.IsNullOrWhiteSpace(manifest.InstallerUrl) &&
            !string.Equals(Path.GetExtension(new Uri(manifest.InstallerUrl).AbsolutePath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cirra updates must be Windows installer executables.");
        }

        Directory.CreateDirectory(destinationDirectory);
        var targetPath = Path.Combine(destinationDirectory, BuildInstallerFileName(manifest));
        var tempPath = targetPath + ".download";

        if (File.Exists(targetPath) && await ValidatePackageAsync(targetPath, manifest, cancellationToken))
        {
            progress?.Report(1.0);
            return targetPath;
        }

        TryDelete(targetPath);
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        await using (var output = File.Create(tempPath))
        {
            var packageParts = manifest.ChunkUrls != null && manifest.ChunkUrls.Count > 0
                ? manifest.ChunkUrls.ToArray()
                : [manifest.InstallerUrl];

            long bytesWritten = 0;
            long? totalBytes = null;

            foreach (var packagePart in packageParts)
            {
                using var response = await SendWithFallbackAsync(
                    (client, token) => client.GetAsync(packagePart, HttpCompletionOption.ResponseHeadersRead, token),
                    cancellationToken);

                response.EnsureSuccessStatusCode();
                totalBytes = (totalBytes ?? 0) + response.Content.Headers.ContentLength.GetValueOrDefault();
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);

                var buffer = new byte[81920];
                int bytesRead;
                while ((bytesRead = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    bytesWritten += bytesRead;
                    if (totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        progress?.Report((double)bytesWritten / totalBytes.Value);
                    }
                }
            }

            progress?.Report(1.0);
            await output.FlushAsync(cancellationToken);
        }

        if (!await ValidatePackageAsync(tempPath, manifest, cancellationToken))
        {
            TryDelete(tempPath);
            throw new InvalidDataException("The downloaded update did not match its release manifest.");
        }

        File.Move(tempPath, targetPath);
        return targetPath;
    }

    public static string BuildInstallerFileName(CloudBerryUpdateManifest manifest)
    {
        var version = string.IsNullOrWhiteSpace(manifest.Version) ? "latest" : manifest.Version.Trim();
        var safeVersion = string.Concat(version.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
        return $"cirra-update-{safeVersion}.exe";
    }

    private static async Task<bool> ValidatePackageAsync(string path, CloudBerryUpdateManifest manifest, CancellationToken cancellationToken)
    {
        var file = new FileInfo(path);
        if (!file.Exists || (manifest.PackageSizeBytes.HasValue && file.Length != manifest.PackageSizeBytes.Value))
        {
            return false;
        }

        await using var stream = File.OpenRead(path);
        var checksum = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        return checksum.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // A locked old package is ignored; a new target uses a versioned name.
        }
    }

    private static HttpClient CreateClient(bool useProxy)
    {
        var handler = new HttpClientHandler
        {
            Proxy = useProxy ? WebRequest.DefaultWebProxy : null,
            UseProxy = useProxy
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(45)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("Cirra-Updater/1.2");
        return client;
    }

    private async Task<T> SendWithFallbackAsync<T>(Func<HttpClient, CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        try
        {
            return await operation(_httpClient, cancellationToken);
        }
        catch (Exception exception) when (ShouldFallback(exception))
        {
            return await operation(_directHttpClient, cancellationToken);
        }
    }

    private static bool ShouldFallback(Exception exception) =>
        exception is HttpRequestException ||
        exception is TaskCanceledException;
}

public sealed record CloudBerryUpdateManifest(
    string Version,
    string InstallerUrl,
    string Notes,
    DateTime PublishedAtUtc,
    IReadOnlyList<string>? ChunkUrls = null,
    string Sha256 = "",
    long? PackageSizeBytes = null);

public sealed record CloudBerryUpdateCheckResult(
    bool IsUpdateAvailable,
    CloudBerryUpdateManifest? Manifest,
    Version? AvailableVersion,
    string StatusMessage)
{
    public static CloudBerryUpdateCheckResult NoUpdate(string statusMessage) =>
        new(false, null, null, statusMessage);

    public static CloudBerryUpdateCheckResult UpdateAvailable(CloudBerryUpdateManifest manifest, Version availableVersion) =>
        new(true, manifest, availableVersion, $"Cirra {availableVersion} is ready to download.");
}
