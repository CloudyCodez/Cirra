using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CloudBerry.Core.Services;

namespace CloudBerry.Core.Tests;

public sealed class CloudBerryUpdateServiceTests
{
    [Fact]
    public async Task CheckAsync_NormalizesRelativeInstallerAndFindsNewerVersion()
    {
        var payload = JsonSerializer.Serialize(new CloudBerryUpdateManifest(
            "0.4.2", "Cirra-Setup-v0.4.2.exe", "Polish", DateTime.UtcNow,
            Sha256: new string('a', 64), PackageSizeBytes: 42));
        var service = CreateService(_ => Json(payload));

        var result = await service.CheckAsync("https://updates.example/latest.json", new Version(0, 4, 1));

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("https://updates.example/Cirra-Setup-v0.4.2.exe", result.Manifest!.InstallerUrl);
    }

    [Fact]
    public async Task DownloadInstallerAsync_VerifiesHashAndReusesValidPackage()
    {
        byte[] package = Encoding.UTF8.GetBytes("verified Cirra installer");
        int downloads = 0;
        var service = CreateService(_ =>
        {
            downloads++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(package) };
        });
        var manifest = ManifestFor(package);
        string directory = CreateTempDirectory();

        try
        {
            string first = await service.DownloadInstallerAsync(manifest, directory);
            string second = await service.DownloadInstallerAsync(manifest, directory);

            Assert.Equal(first, second);
            Assert.Equal(1, downloads);
            Assert.Equal(package, await File.ReadAllBytesAsync(first));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadInstallerAsync_RejectsCorruptPackageWithoutStagingIt()
    {
        byte[] package = Encoding.UTF8.GetBytes("tampered installer");
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(package)
        });
        var manifest = ManifestFor(package) with { Sha256 = new string('0', 64) };
        string directory = CreateTempDirectory();

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadInstallerAsync(manifest, directory));
            Assert.Empty(Directory.EnumerateFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadInstallerAsync_RejectsNonExecutablePackage()
    {
        byte[] package = Encoding.UTF8.GetBytes("zip");
        var service = CreateService(_ => throw new InvalidOperationException("Network should not be used."));
        var manifest = ManifestFor(package) with { InstallerUrl = "https://updates.example/Cirra.zip" };
        string directory = CreateTempDirectory();

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DownloadInstallerAsync(manifest, directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static CloudBerryUpdateManifest ManifestFor(byte[] package) => new(
        "0.4.2",
        "https://updates.example/Cirra-Setup-v0.4.2.exe",
        "Update",
        DateTime.UtcNow,
        Sha256: Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant(),
        PackageSizeBytes: package.Length);

    private static CloudBerryUpdateService CreateService(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        return new CloudBerryUpdateService(new HttpClient(new StubHandler(responseFactory)));
    }

    private static HttpResponseMessage Json(string payload) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(payload, Encoding.UTF8, "application/json")
    };

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "cirra-update-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
