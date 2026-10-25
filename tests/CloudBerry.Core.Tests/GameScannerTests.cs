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
    public async Task ScanFolderAsync_WhenInstallFolderIsTheScanRoot_RetainsInstallRootIdentity()
    {
        var installRoot = Path.Combine(_rootPath, "Castlevania Dominus Collection");
        Directory.CreateDirectory(installRoot);
        var executable = Path.Combine(installRoot, "M2Game.exe");
        await File.WriteAllTextAsync(executable, "test");

        var scanner = new GameScanner();
        var report = await scanner.ScanFolderAsync(installRoot);

        var candidate = Assert.Single(report.Candidates);
        Assert.Equal(executable, candidate.InstallPath);
        Assert.Contains("castlevania dominus collection", candidate.DeduplicationKey, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadInstalledXboxGames_UsesPackageManifestForUnusualLaunchProxy()
    {
        var libraryRoot = Path.Combine(_rootPath, "XboxGames");
        var installRoot = Path.Combine(libraryRoot, "Castlevania", "Content");
        Directory.CreateDirectory(installRoot);
        var executable = Path.Combine(installRoot, "GameLaunchHelper.exe");
        await File.WriteAllTextAsync(executable, "test");
        await File.WriteAllTextAsync(Path.Combine(installRoot, "MicrosoftGame.Config"), """
            <Game>
              <Identity Name="Konami.Castlevania" />
              <ExecutableList><Executable Name="GameLaunchHelper.exe" /></ExecutableList>
            </Game>
            """);

        var game = Assert.Single(GameScanner.ReadInstalledXboxGames([libraryRoot]));
        var candidate = new GameScanner().CreateLauncherCandidate(game);

        Assert.Equal(executable, candidate.InstallPath);
        Assert.Equal("source:xbox:konami.castlevania", candidate.DeduplicationKey);
        Assert.Equal("Castlevania", candidate.Title);
        Assert.Equal(1, candidate.Confidence);
    }

    [Fact]
    public void ReadInstalledSteamGames_FindsBelmontsCurseInCustomSteamInstall()
    {
        var steamRoot = Path.Combine(_rootPath, "Custom Steam");
        var steamApps = Path.Combine(steamRoot, "steamapps");
        var installRoot = Path.Combine(steamApps, "common", "Castlevania Belmont's Curse");
        Directory.CreateDirectory(installRoot);
        File.WriteAllText(Path.Combine(steamApps, "appmanifest_4231820.acf"), """
            "AppState"
            {
                "appid"             "4231820"
                "name"              "Castlevania: Belmont's Curse"
                "installdir"        "Castlevania Belmont's Curse"
                "playtime_forever"  "37"
            }
            """);

        var game = Assert.Single(new GameScanner().ReadInstalledSteamGames([steamRoot]));

        Assert.Equal("4231820", game.AppId);
        Assert.Equal("Castlevania: Belmont's Curse", game.Title);
        Assert.Equal(installRoot, game.InstallPath);
        Assert.Equal(37, game.PlaytimeMinutes);
    }

    [Fact]
    public void CreateLauncherCandidate_SteamUsesStableProtocolAndBothProviderAndRootIdentities()
    {
        var installRoot = Path.Combine(_rootPath, "SteamLibrary", "steamapps", "common", "Castlevania Dominus Collection");
        Directory.CreateDirectory(installRoot);

        var candidate = new GameScanner().CreateLauncherCandidate(new GameScanner.LauncherGameInfo(
            "Steam",
            "2369900",
            "Castlevania Dominus Collection",
            installRoot,
            "steam://rungameid/2369900"));

        Assert.Equal("Castlevania Dominus Collection", candidate.Title);
        Assert.Equal("steam://rungameid/2369900", candidate.InstallPath);
        Assert.Equal("source:steam:2369900", candidate.DeduplicationKey);
        Assert.Contains("source:steam:2369900", candidate.IdentityKeys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(candidate.IdentityKeys, key => key.StartsWith("desktop:", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, candidate.Confidence);
    }

    [Fact]
    public async Task LauncherAndFolderDiscovery_ShareInstallRootIdentity()
    {
        var libraryRoot = Path.Combine(_rootPath, "SteamLibrary", "steamapps", "common");
        var installRoot = Path.Combine(libraryRoot, "Castlevania Dominus Collection");
        Directory.CreateDirectory(installRoot);
        await File.WriteAllTextAsync(Path.Combine(installRoot, "M2Game.exe"), "test");
        var scanner = new GameScanner();

        var folderCandidate = Assert.Single((await scanner.ScanFolderAsync(libraryRoot)).Candidates);
        var manifestCandidate = scanner.CreateLauncherCandidate(new GameScanner.LauncherGameInfo(
            "Steam",
            "2369900",
            "Castlevania Dominus Collection",
            installRoot,
            "steam://rungameid/2369900"));

        Assert.Contains(folderCandidate.DeduplicationKey, manifestCandidate.IdentityKeys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateLauncherCandidate_EpicResolvesDeclaredRelativeExecutable()
    {
        var installRoot = Path.Combine(_rootPath, "Epic Games", "Castlevania");
        var executable = Path.Combine(installRoot, "Binaries", "Win64", "ProjectC.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        File.WriteAllText(executable, "test");

        var candidate = new GameScanner().CreateLauncherCandidate(new GameScanner.LauncherGameInfo(
            "Epic",
            "catalog-castlevania",
            "Castlevania",
            installRoot,
            @"Binaries\Win64\ProjectC.exe"));

        Assert.Equal(executable, candidate.InstallPath);
        Assert.Equal("source:epic:catalog-castlevania", candidate.DeduplicationKey);
        Assert.Contains(candidate.Signals, signal => signal.Contains("manifest", StringComparison.OrdinalIgnoreCase));
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
