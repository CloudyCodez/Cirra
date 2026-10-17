using CloudBerry.Core.Models;
using CloudBerry.Core.Services;

namespace CloudBerry.Core.Tests;

public sealed class SaveContinuityServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CirraSaveTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DiscoverAsync_FingerprintsNestedFilesAndDeduplicatesOverlappingRoots()
    {
        var saves = Path.Combine(_root, "live");
        Directory.CreateDirectory(Path.Combine(saves, "slot1"));
        var first = Path.Combine(saves, "profile.sav");
        var second = Path.Combine(saves, "slot1", "world.dat");
        await File.WriteAllTextAsync(first, "profile");
        await File.WriteAllTextAsync(second, "world");
        var game = new GameRecord { SaveLocations = [saves, first] };
        var service = new SaveContinuityService(Path.Combine(_root, "backups"));

        var result = await service.DiscoverAsync(game);

        Assert.Equal(2, result.Artifacts.Count);
        Assert.All(result.Artifacts, item => Assert.Equal(64, item.Sha256.Length));
        Assert.Contains(result.Artifacts, item => item.RelativePath == Path.Combine("slot1", "world.dat"));
    }

    [Fact]
    public async Task CreateBackupAsync_WritesAndVerifiesManifest()
    {
        var saves = Path.Combine(_root, "live");
        Directory.CreateDirectory(saves);
        await File.WriteAllTextAsync(Path.Combine(saves, "slot.sav"), "level 12");
        var game = new GameRecord { Title = "Test Game", SaveLocations = [saves] };
        var service = new SaveContinuityService(Path.Combine(_root, "backups"));

        var backup = await service.CreateBackupAsync(game);
        var verification = await service.VerifyAsync(backup.ManifestPath);

        Assert.Equal(1, backup.FileCount);
        Assert.True(verification.IsValid);
        Assert.Equal(1, verification.VerifiedFiles);

        var timeline = await service.ListBackupsAsync(game.Id);
        Assert.Single(timeline);
        Assert.Equal(backup.ManifestPath, timeline[0].ManifestPath);
    }

    [Fact]
    public async Task VerifyAsync_DetectsTamperedBackup()
    {
        var saves = Path.Combine(_root, "live");
        Directory.CreateDirectory(saves);
        await File.WriteAllTextAsync(Path.Combine(saves, "slot.sav"), "original");
        var game = new GameRecord { SaveLocations = [saves] };
        var service = new SaveContinuityService(Path.Combine(_root, "backups"));
        var backup = await service.CreateBackupAsync(game);
        var backupFile = Directory.GetFiles(Path.GetDirectoryName(backup.ManifestPath)!, "*.sav", SearchOption.AllDirectories).Single();
        await File.WriteAllTextAsync(backupFile, "tampered");

        var verification = await service.VerifyAsync(backup.ManifestPath);

        Assert.False(verification.IsValid);
        Assert.Contains(verification.Errors, error => error.Contains("Integrity", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RestoreAsync_RestoresVerifiedDataAndKeepsRollbackCopy()
    {
        var saves = Path.Combine(_root, "live");
        Directory.CreateDirectory(saves);
        var saveFile = Path.Combine(saves, "slot.sav");
        await File.WriteAllTextAsync(saveFile, "before");
        var game = new GameRecord { SaveLocations = [saves] };
        var service = new SaveContinuityService(Path.Combine(_root, "backups"));
        var backup = await service.CreateBackupAsync(game);
        await File.WriteAllTextAsync(saveFile, "after");

        var restore = await service.RestoreAsync(game, backup.ManifestPath);

        Assert.Equal("before", await File.ReadAllTextAsync(saveFile));
        Assert.Equal(1, restore.RestoredFiles);
        var rollbackFile = Directory.GetFiles(restore.RollbackDirectory, "slot.sav", SearchOption.AllDirectories).Single();
        Assert.Equal("after", await File.ReadAllTextAsync(rollbackFile));
    }

    [Fact]
    public async Task VerifyAsync_RejectsManifestOutsideBackupRoot()
    {
        Directory.CreateDirectory(_root);
        var manifest = Path.Combine(_root, "manifest.json");
        await File.WriteAllTextAsync(manifest, "{}");
        var service = new SaveContinuityService(Path.Combine(_root, "backups"));

        var result = await service.VerifyAsync(manifest);

        Assert.False(result.IsValid);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
