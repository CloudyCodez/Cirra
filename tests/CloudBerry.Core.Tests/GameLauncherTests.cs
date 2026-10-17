using CloudBerry.Core.Services;

namespace CloudBerry.Core.Tests;

public sealed class GameLauncherTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CloudBerryLauncherTests", Guid.NewGuid().ToString("N"));

    public GameLauncherTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Theory]
    [InlineData("game.3ds")]
    [InlineData("game.3dsx")]
    [InlineData("game.cci")]
    [InlineData("game.cxi")]
    [InlineData("game.axf")]
    [InlineData("game.cia")]
    public void IsRomFile_RecognizesNintendo3dsContent(string fileName)
    {
        Assert.True(GameLauncher.IsRomFile(fileName));
    }

    [Fact]
    public void TryResolveLaunchTarget_KeepsStorefrontUrls()
    {
        var resolved = GameLauncher.TryResolveLaunchTarget("steam://rungameid/12345", out var target, out var workingDirectory);

        Assert.True(resolved);
        Assert.Equal("steam://rungameid/12345", target);
        Assert.Equal(string.Empty, workingDirectory);
    }

    [Fact]
    public async Task TryResolveLaunchTarget_ResolvesInstallFolderToBestExecutable()
    {
        var gameRoot = Path.Combine(_root, "Cloud Quest");
        var redistRoot = Path.Combine(gameRoot, "_CommonRedist");
        var shippingRoot = Path.Combine(gameRoot, "CloudQuest", "Binaries", "Win64");
        Directory.CreateDirectory(redistRoot);
        Directory.CreateDirectory(shippingRoot);

        await File.WriteAllTextAsync(Path.Combine(gameRoot, "Launcher.exe"), "tiny");
        await File.WriteAllTextAsync(Path.Combine(redistRoot, "setup.exe"), "ignore");
        var shippingExe = Path.Combine(shippingRoot, "CloudQuest-Win64-Shipping.exe");
        await File.WriteAllBytesAsync(shippingExe, new byte[1024 * 1024 * 3]);

        var resolved = GameLauncher.TryResolveLaunchTarget(gameRoot, out var target, out var workingDirectory);

        Assert.True(resolved);
        Assert.Equal(shippingExe, target);
        Assert.Equal(shippingRoot, workingDirectory);
    }

    [Fact]
    public async Task TryResolveLaunchTarget_ParsesQuotedExecutableCommand()
    {
        var gameRoot = Path.Combine(_root, "Command Game");
        Directory.CreateDirectory(gameRoot);
        var executable = Path.Combine(gameRoot, "CommandGame.exe");
        await File.WriteAllTextAsync(executable, "stub");

        var resolved = GameLauncher.TryResolveLaunchTarget($"\"{executable}\" -fullscreen -profile cloud", out var target, out var workingDirectory);

        Assert.True(resolved);
        Assert.Equal(executable, target);
        Assert.Equal(gameRoot, workingDirectory);
    }

    [Fact]
    public async Task TryResolveLaunchTarget_IgnoresNonGamePayloads()
    {
        var gameRoot = Path.Combine(_root, "Only Tools");
        Directory.CreateDirectory(gameRoot);
        await File.WriteAllTextAsync(Path.Combine(gameRoot, "uninstall.exe"), "ignore");
        await File.WriteAllTextAsync(Path.Combine(gameRoot, "crashreporter.exe"), "ignore");

        var resolved = GameLauncher.TryResolveLaunchTarget(gameRoot, out var target, out _);

        Assert.False(resolved);
        Assert.Equal(string.Empty, target);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Best-effort temp cleanup.
        }
    }
}
