using System.Net;
using System.Net.Http;
using System.Text;
using CloudBerry.Core.Services;

namespace CloudBerry.Core.Tests;

public sealed class MediaCacheServiceTests : IDisposable
{
    private readonly string _mediaRootPath = Path.Combine(Path.GetTempPath(), "CloudBerryMediaTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CacheImageAsync_DownloadsAndPersistsRemoteAsset()
    {
        var handler = new StubHttpMessageHandler("image-bytes", "image/png");
        var service = new MediaCacheService(
            new HttpClient(handler),
            _mediaRootPath);

        var cachedPath = await service.CacheImageAsync(
            "https://images.example/covers/hades",
            "hades",
            "cover");

        Assert.True(File.Exists(cachedPath));
        Assert.EndsWith(".png", cachedPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("image-bytes", await File.ReadAllTextAsync(cachedPath));
    }

    [Fact]
    public async Task CacheImageAsync_LocalPath_ReturnsAbsolutePathWithoutDownloading()
    {
        Directory.CreateDirectory(_mediaRootPath);
        var localFile = Path.Combine(_mediaRootPath, "existing.jpg");
        await File.WriteAllTextAsync(localFile, "local");

        var service = new MediaCacheService(new HttpClient(new StubHttpMessageHandler("", "image/jpeg")), _mediaRootPath);
        var cachedPath = await service.CacheImageAsync(localFile, "ignored", "cover");

        Assert.Equal(Path.GetFullPath(localFile), cachedPath);
    }

    [Fact]
    public async Task CacheImageAsync_ReusesCachedAssetWithoutASecondRequest()
    {
        var handler = new CountingHttpMessageHandler("first response", "image/png");
        var service = new MediaCacheService(new HttpClient(handler), _mediaRootPath);

        var first = await service.CacheImageAsync("https://images.example/covers/hades", "hades", "cover");
        var second = await service.CacheImageAsync("https://images.example/covers/hades", "hades", "cover");

        Assert.Equal(first, second);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CacheImageAsync_TrimsOldAssetsWhenCacheExceedsBudget()
    {
        var handler = new CountingHttpMessageHandler("1234567890", "image/png");
        var service = new MediaCacheService(new HttpClient(handler), _mediaRootPath, cacheBudgetBytes: 12, maximumAssetBytes: 32);

        await service.CacheImageAsync("https://images.example/covers/one", "one", "cover");
        await Task.Delay(20);
        var newest = await service.CacheImageAsync("https://images.example/covers/two", "two", "cover");

        Assert.True(File.Exists(newest));
        Assert.False(File.Exists(Path.Combine(_mediaRootPath, "one", "cover.png")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRootPath))
        {
            Directory.Delete(_mediaRootPath, recursive: true);
        }
    }

    private sealed class StubHttpMessageHandler(string content, string contentType) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, contentType)
            });
        }
    }

    private sealed class CountingHttpMessageHandler(string content, string contentType) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, contentType)
            });
        }
    }
}
