using System;
using System.Collections.Generic;
using System.IO;
using CloudBerry.App.ViewModels;
using CloudBerry.Core.Models;
using Xunit;

namespace CloudBerry.App.Tests;

/// <summary>
/// Regression coverage for the duplicate-import detection and missing-path cleanup logic that
/// lives in <see cref="MainViewModel"/>. These helpers were previously untested because
/// MainViewModel itself has a 17-dependency constructor that makes full instantiation
/// impractical in a unit test; the relevant static helpers were made `internal` (via
/// InternalsVisibleTo) specifically so this logic could be locked down without needing to stand
/// up the whole view model. See CloudBerry_Aurora_Roadmap.md, Phase 1 / section 2.
/// </summary>
public sealed class DuplicateAndMissingPathCleanupTests : IDisposable
{
    private readonly string _tempRoot;

    public DuplicateAndMissingPathCleanupTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "CloudBerryAppTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; leaving a stray temp dir behind isn't worth failing the test run over.
        }
    }

    private string CreateTempExecutable(string fileName = "Game.exe")
    {
        var path = Path.Combine(_tempRoot, fileName);
        File.WriteAllText(path, "not a real exe, just needs to exist");
        return path;
    }

    // ── NormalizeLaunchTargetKey ────────────────────────────────────────────

    [Fact]
    public void NormalizeLaunchTargetKey_ReturnsEmpty_ForNullOrWhitespace()
    {
        Assert.Equal(string.Empty, MainViewModel.NormalizeLaunchTargetKey(""));
        Assert.Equal(string.Empty, MainViewModel.NormalizeLaunchTargetKey("   "));
    }

    [Fact]
    public void NormalizeLaunchTargetKey_TrimsTrailingSeparators_ForDirectoryStylePaths()
    {
        var withSlash = _tempRoot + Path.DirectorySeparatorChar;
        var withoutSlash = _tempRoot;

        var keyWithSlash = MainViewModel.NormalizeLaunchTargetKey(withSlash);
        var keyWithoutSlash = MainViewModel.NormalizeLaunchTargetKey(withoutSlash);

        Assert.Equal(keyWithoutSlash, keyWithSlash);
    }

    [Fact]
    public void NormalizeLaunchTargetKey_ProducesSameKey_ForSameExecutable_RegardlessOfQuoting()
    {
        var exePath = CreateTempExecutable();

        var plainKey = MainViewModel.NormalizeLaunchTargetKey(exePath);
        var quotedKey = MainViewModel.NormalizeLaunchTargetKey($"\"{exePath}\"");

        Assert.Equal(plainKey, quotedKey);
        Assert.False(string.IsNullOrWhiteSpace(plainKey));
    }

    // ── IsKnownLaunchTarget (duplicate-import "already in library" detection) ──

    [Fact]
    public void IsKnownLaunchTarget_ReturnsTrue_WhenNormalizedKeyIsInExistingSet()
    {
        var exePath = CreateTempExecutable();
        var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            MainViewModel.NormalizeLaunchTargetKey(exePath)
        };

        // A candidate pointing at the exact same file, just quoted differently, must still
        // resolve to a duplicate — this is exactly the scan-import scenario the flag drives.
        Assert.True(MainViewModel.IsKnownLaunchTarget($"\"{exePath}\"", existingKeys));
    }

    [Fact]
    public void IsKnownLaunchTarget_ReturnsFalse_ForUnrelatedPath()
    {
        var exePath = CreateTempExecutable();
        var otherPath = Path.Combine(_tempRoot, "SomethingElse.exe");
        var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            MainViewModel.NormalizeLaunchTargetKey(exePath)
        };

        Assert.False(MainViewModel.IsKnownLaunchTarget(otherPath, existingKeys));
    }

    [Fact]
    public void IsKnownLaunchTarget_ReturnsFalse_ForEmptyPath()
    {
        var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "somekey" };
        Assert.False(MainViewModel.IsKnownLaunchTarget("", existingKeys));
    }

    // ── ShouldAutoSelectDesktopCandidate (scan-preview auto-check logic) ────

    [Theory]
    [InlineData(0.90, "install folder | exe present", false, true)]   // confident, clean signals -> auto-select
    [InlineData(0.90, "install folder | exe present", true, false)]   // already in library -> never auto-select
    [InlineData(0.30, "install folder | exe present", false, false)]  // low confidence -> don't auto-select
    [InlineData(0.90, "launcher detected", false, false)]             // launcher signal -> excluded
    [InlineData(0.90, "updater helper", false, false)]                // updater/helper signal -> excluded
    public void ShouldAutoSelectDesktopCandidate_HonoursConfidenceAndSignalRules(
        double confidence, string signalsText, bool alreadyExists, bool expected)
    {
        var exePath = Path.Combine("C:\\Games\\SomeGame", "SomeGame.exe");
        var result = MainViewModel.ShouldAutoSelectDesktopCandidate(confidence, exePath, signalsText, alreadyExists);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldAutoSelectDesktopCandidate_NeverAutoSelects_RomFiles()
    {
        // ROMs always need an emulator/platform decision from the user, even at high confidence.
        var result = MainViewModel.ShouldAutoSelectDesktopCandidate(0.99, "C:\\Roms\\game.nes", "rom detected", alreadyExists: false);
        Assert.False(result);
    }

    // ── HasMissingLaunchTarget / ResolveLocalLaunchTargetForCleanup (Clean Missing / repair) ──

    [Fact]
    public void HasMissingLaunchTarget_ReturnsFalse_WhenExecutableExists()
    {
        var exePath = CreateTempExecutable();
        var game = new GameEntryViewModel { InstallPath = exePath };

        Assert.False(MainViewModel.HasMissingLaunchTarget(game));
    }

    [Fact]
    public void HasMissingLaunchTarget_ReturnsTrue_WhenExecutableDoesNotExist()
    {
        var missingPath = Path.Combine(_tempRoot, "DoesNotExist", "Ghost.exe");
        var game = new GameEntryViewModel { InstallPath = missingPath };

        Assert.True(MainViewModel.HasMissingLaunchTarget(game));
    }

    [Fact]
    public void HasMissingLaunchTarget_ReturnsFalse_WhenInstallPathIsAnExistingDirectory()
    {
        // A folder-style install path with no resolvable exe inside still counts as "present"
        // as long as the directory itself exists — RepairLibraryAsync treats total absence
        // (neither file nor directory) as the missing case, not an unrecognized exe inside a
        // real folder.
        var game = new GameEntryViewModel { InstallPath = _tempRoot };

        Assert.False(MainViewModel.HasMissingLaunchTarget(game));
    }

    [Fact]
    public void HasMissingLaunchTarget_ReturnsFalse_ForEmptyInstallPath()
    {
        // No install path at all isn't a "missing" launch target in this codebase's model —
        // it's simply not launchable, which is a distinct state handled elsewhere.
        var game = new GameEntryViewModel { InstallPath = "" };

        Assert.False(MainViewModel.HasMissingLaunchTarget(game));
    }

    [Fact]
    public void ResolveLocalLaunchTargetForCleanup_StripsQuotesAndTrailingArguments()
    {
        var resolved = MainViewModel.ResolveLocalLaunchTargetForCleanup("\"C:\\Games\\Foo\\Foo.exe\" -someArg");
        Assert.Equal("C:\\Games\\Foo\\Foo.exe", resolved);
    }

    [Fact]
    public void ResolveLocalLaunchTargetForCleanup_TruncatesAtDotExe_WhenUnquoted()
    {
        var resolved = MainViewModel.ResolveLocalLaunchTargetForCleanup("C:\\Games\\Foo\\Foo.exe --windowed");
        Assert.Equal("C:\\Games\\Foo\\Foo.exe", resolved);
    }

    [Fact]
    public void ResolveLocalLaunchTargetForCleanup_ReturnsEmpty_ForNullOrWhitespace()
    {
        Assert.Equal(string.Empty, MainViewModel.ResolveLocalLaunchTargetForCleanup(null));
        Assert.Equal(string.Empty, MainViewModel.ResolveLocalLaunchTargetForCleanup("   "));
    }

    // ── GetLibraryRecordQualityScore (duplicate-keep-the-best-record logic) ──

    [Fact]
    public void GetLibraryRecordQualityScore_PrefersRecordWithValidLaunchTargetAndRicherMetadata()
    {
        var exePath = CreateTempExecutable("Complete.exe");

        var richGame = new GameEntryViewModel
        {
            InstallPath = exePath,
            Summary = "A complete, well-described game.",
            CachedCoverImagePath = Path.Combine(_tempRoot, "cover.jpg"),
            CachedBackgroundImagePath = Path.Combine(_tempRoot, "background.jpg"),
            MetadataProviderName = "IGDB",
        };

        var sparseGame = new GameEntryViewModel
        {
            InstallPath = Path.Combine(_tempRoot, "DoesNotExist", "Ghost.exe"),
            Summary = "",
        };

        var richScore = MainViewModel.GetLibraryRecordQualityScore(richGame);
        var sparseScore = MainViewModel.GetLibraryRecordQualityScore(sparseGame);

        Assert.True(richScore > sparseScore,
            $"Expected the fully-populated, launchable record ({richScore}) to outscore the sparse, " +
            $"broken-path record ({sparseScore}) so duplicate cleanup keeps the better entry.");
    }

    [Fact]
    public void GameEntryViewModel_RoundTripsDistinctSaveLocations()
    {
        var model = new GameRecord
        {
            SaveLocations = ["C:\\Saves\\Game", "C:\\Saves\\profile.sav"]
        };

        var viewModel = new GameEntryViewModel(model);
        viewModel.SaveLocationsText += Environment.NewLine + "C:\\Saves\\Game";
        var roundTripped = viewModel.ToModel();

        Assert.Equal(2, roundTripped.SaveLocations.Count);
        Assert.Equal(model.SaveLocations, roundTripped.SaveLocations);
    }
}
