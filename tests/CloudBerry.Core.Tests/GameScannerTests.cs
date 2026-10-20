using CloudBerry.Core.Services;

namespace CloudBerry.Core.Tests;

public sealed class GameScannerTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "CloudBerryTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ScanFolderAsync_FindsLikelyWindowsGame()
    {
        Directory.CreateDirectory(Path.Combine(_rootPath, "Games", "Celeste"));
        var gamePath = Path.Combine(_rootPath, "Games", "Celeste", "Celeste.exe");
        await File.WriteAllTextAsync(gamePath, "test");

        var scanner = new GameScanner();
        var report = await scanner.ScanFolderAsync(_rootPath);

        var candidate = Assert.Single(report.Candidates);
        Assert.Equal("Celeste", candidate.Title);
        Assert.Equal("Windows PC", candidate.Platform);
        Assert.True(candidate.Confidence >= 0.65);
    }

    [Fact]
    public async Task ScanFolderAsync_SkipsInstallersAndRedistributables()
    {
        Directory.CreateDirectory(Path.Combine(_rootPath, "Game", "_CommonRedist"));
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "Game", "setup.exe"), "test");
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "Game", "_CommonRedist", "vc_redist.x64.exe"), "test");

        var scanner = new GameScanner();
        var report = await scanner.ScanFolderAsync(_rootPath);

        Assert.Empty(report.Candidates);
    }

    [Fact]
    public async Task ScanFolderAsync_IgnoresGenericLauncherAndPrefersShippingExecutable()
    {
        var root = Path.Combine(_rootPath, "Games", "Solar Ash");
        Directory.CreateDirectory(Path.Combine(root, "Binaries", "Win64"));
        await File.WriteAllTextAsync(Path.Combine(root, "Launcher.exe"), "test");
        var shippingExe = Path.Combine(root, "Binaries", "Win64", "SolarAsh-Win64-Shipping.exe");
        await File.WriteAllTextAsync(shippingExe, "test");

        var scanner = new GameScanner();
        var report = await scanner.ScanFolderAsync(_rootPath);

        var candidate = Assert.Single(report.Candidates);
        Assert.Equal("Solar Ash", candidate.Title);
        Assert.Equal(shippingExe, candidate.InstallPath);
        Assert.DoesNotContain(candidate.Signals, signal => signal.Contains("helper", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ScanFolderAsync_SkipsDedicatedServerAndPicksMainExecutable()
    {
        var root = Path.Combine(_rootPath, "Games", "Risk of Rain Returns");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "RiskOfRainReturns.exe"), "test");
        await File.WriteAllTextAsync(Path.Combine(root, "RiskOfRainReturnsServer.exe"), "test");
        await File.WriteAllTextAsync(Path.Combine(root, "RiskOfRainReturnsBenchmark.exe"), "test");

        var scanner = new GameScanner();
        var report = await scanner.ScanFolderAsync(_rootPath);

        var candidate = Assert.Single(report.Candidates);
        Assert.Equal("Risk of Rain Returns", candidate.Title);
        Assert.EndsWith("RiskOfRainReturns.exe", candidate.InstallPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScanFolderAsync_IgnoresGenericLauncherWithoutGameSpecificExecutable()
    {
        var root = Path.Combine(_rootPath, "Games", "Mystery Game");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "Launcher.exe"), "test");
        await File.WriteAllTextAsync(Path.Combine(root, "Settings.exe"), "test");

        var scanner = new GameScanner();
        var report = await scanner.ScanFolderAsync(_rootPath);

        Assert.Empty(report.Candidates);
    }

    [Fact]
    public async Task ScanFolderAsync_MissingFolder_ReturnsWarningInsteadOfThrowing()
    {
        var scanner = new GameScanner();
        var report = await scanner.ScanFolderAsync(Path.Combine(_rootPath, "Missing"));

        Assert.Empty(report.Candidates);
        Assert.Contains(report.Warnings, warning => warning.Contains("no longer exists", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("Dreamcast", "Skies of Arcadia.gdi", "Dreamcast")]
    [InlineData("Switch", "Metroid Dread.xci", "Nintendo Switch")]
    [InlineData("Wii U", "The Wind Waker HD.wua", "Wii U")]
    [InlineData("Genesis", "Sonic The Hedgehog.md", "Sega Genesis")]
    [InlineData("3DS", "Dragon Ball Fusions.cxi", "Nintendo 3DS")]
    public async Task ScanFolderAsync_RecognizesCommonEmulationPlatforms(string folder, string fileName, string platform)
    {
        var root = Path.Combine(_rootPath, "ROMs", folder);
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, fileName), "test");

        var scanner = new GameScanner();
        var report = await scanner.ScanFolderAsync(_rootPath);

        var candidate = Assert.Single(report.Candidates);
        Assert.Equal(platform, candidate.Platform);
        Assert.True(candidate.Confidence >= 0.45);
    }

    [Fact]
    public async Task ScanFolderAsync_UsesCueInsteadOfSameDiscBinPayload()
    {
        var root = Path.Combine(_rootPath, "ROMs", "PlayStation");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "Castlevania Symphony of the Night.cue"), "FILE \"Castlevania Symphony of the Night.bin\" BINARY");
        await File.WriteAllTextAsync(Path.Combine(root, "Castlevania Symphony of the Night.bin"), "payload");

        var scanner = new GameScanner();
        var report = await scanner.ScanFolderAsync(_rootPath);

        var candidate = Assert.Single(report.Candidates);
        Assert.EndsWith(".cue", candidate.InstallPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, report.CompanionFilesCollapsed);
    }

    [Fact]
    public async Task ScanFolderAsync_UsesPlaylistForMultiDiscSetWithoutDroppingOtherGames()
    {
        var root = Path.Combine(_rootPath, "ROMs", "PlayStation");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "Final Fantasy IX.m3u"), "Final Fantasy IX (Disc 1).cue\nFinal Fantasy IX (Disc 2).cue");
        await File.WriteAllTextAsync(Path.Combine(root, "Final Fantasy IX (Disc 1).cue"), "disc one");
        await File.WriteAllTextAsync(Path.Combine(root, "Final Fantasy IX (Disc 2).cue"), "disc two");
        await File.WriteAllTextAsync(Path.Combine(root, "Chrono Cross.cue"), "another game");

        var scanner = new GameScanner();
        var report = await scanner.ScanFolderAsync(_rootPath);

        Assert.Equal(2, report.Candidates.Count);
        Assert.Contains(report.Candidates, candidate => candidate.InstallPath.EndsWith("Final Fantasy IX.m3u", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Candidates, candidate => candidate.InstallPath.EndsWith("Chrono Cross.cue", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, report.CompanionFilesCollapsed);
    }

    [Fact]
    public async Task ScanFolderAsync_PreservesSeparateDiscsWhenNoPlaylistExists()
    {
        var root = Path.Combine(_rootPath, "ROMs", "PlayStation");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "Final Fantasy VII (Disc 1).cue"), "disc one");
        await File.WriteAllTextAsync(Path.Combine(root, "Final Fantasy VII (Disc 2).cue"), "disc two");

        var scanner = new GameScanner();
        var report = await scanner.ScanFolderAsync(_rootPath);

        Assert.Equal(2, report.Candidates.Count);
        Assert.Equal(0, report.CompanionFilesCollapsed);
    }

    [Fact]
    public void NormalizeScanRoots_RemovesNestedRootsAndKeepsSeparateLibraries()
    {
        var parent = Path.Combine(_rootPath, "Libraries");
        var nested = Path.Combine(parent, "Steam", "steamapps", "common");
        var separate = Path.Combine(_rootPath, "ROMs");
        Directory.CreateDirectory(nested);
        Directory.CreateDirectory(separate);

        var roots = new GameScanner().NormalizeScanRoots([nested, separate, parent, parent]);

        Assert.Equal(2, roots.Count);
        Assert.Contains(Path.GetFullPath(parent), roots, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.GetFullPath(separate), roots, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeTitleFromPath_RemovesNoiseTokensAndBrackets()
    {
        var title = GameNameNormalizer.NormalizeTitleFromPath(@"C:\ROMs\SNES\Chrono_Trigger_(USA)_[v1.0].sfc");

        Assert.Equal("Chrono Trigger", title);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
