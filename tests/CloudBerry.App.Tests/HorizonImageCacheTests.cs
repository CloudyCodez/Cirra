using CloudBerry.App.Services;
using System.IO;

namespace CloudBerry.App.Tests;

public sealed class HorizonImageCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"cirra-image-cache-{Guid.NewGuid():N}");

    [Fact]
    public async Task LoadAsync_ReusesFrozenDecodedImage()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "cirra_app_icon_256.png");
        var cache = new HorizonImageCache(32 * 1024 * 1024);

        var first = await cache.LoadAsync(path, 960);
        var second = await cache.LoadAsync(path, 960);

        Assert.NotNull(first);
        Assert.True(first.IsFrozen);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task LoadAsync_HonorsCancellationBeforeDecode()
    {
        var cache = new HorizonImageCache();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cache.LoadAsync(Path.Combine(_root, "missing.png"), 960, cancellation.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
