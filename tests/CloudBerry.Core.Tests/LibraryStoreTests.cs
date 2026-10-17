using CloudBerry.Core.Models;
using CloudBerry.Core.Services;

namespace CloudBerry.Core.Tests;

public sealed class LibraryStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_PersistsDashboardState()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "CloudBerryTests", Guid.NewGuid().ToString("N"), "library.json");
        var store = new LibraryStore(filePath);

        var gameId = Guid.NewGuid();
        var snapshot = new LibrarySnapshot
        {
            Games =
            [
                new GameRecord
                {
                    Id = gameId,
                    Title = "Hades",
                    NextTimeNote = "Try the spear aspect on the next run.",
                    NextTimeUpdatedOn = new DateTime(2026, 9, 4, 9, 30, 0, DateTimeKind.Local)
                }
            ],
            Dashboard = new DashboardSnapshotState
            {
                SelectedGameId = gameId,
                SelectedGameStage = new SelectedGameStageSnapshot
                {
                    GameId = gameId,
                    Title = "Hades",
                    Platform = "PC",
                    Reason = "launch",
                    ChangedAtUtc = DateTime.UtcNow
                },
                SelectedPrimaryPage = 2,
                SelectedWorkspaceTab = 1,
                RandomBacklogGameId = gameId,
                RecentActivity =
                [
                    new DashboardActivityEntry
                    {
                        Kind = "launch",
                        GameId = gameId,
                        GameTitle = "Hades",
                        Platform = "PC",
                        Detail = "Launched with active session tracking."
                    }
                ]
            }
        };

        try
        {
            await store.SaveAsync(snapshot);
            var loaded = await store.LoadAsync();

            Assert.Equal(gameId, loaded.Dashboard.SelectedGameId);
            Assert.Equal(gameId, loaded.Dashboard.SelectedGameStage.GameId);
            Assert.Equal("launch", loaded.Dashboard.SelectedGameStage.Reason);
            Assert.Equal(2, loaded.Dashboard.SelectedPrimaryPage);
            Assert.Equal(1, loaded.Dashboard.SelectedWorkspaceTab);
            Assert.Equal(gameId, loaded.Dashboard.RandomBacklogGameId);
            Assert.Single(loaded.Dashboard.RecentActivity);
            Assert.Equal("launch", loaded.Dashboard.RecentActivity[0].Kind);
            Assert.Equal("Hades", loaded.Dashboard.RecentActivity[0].GameTitle);
            Assert.Equal("Try the spear aspect on the next run.", loaded.Games[0].NextTimeNote);
            Assert.Equal(new DateTime(2026, 9, 4, 9, 30, 0, DateTimeKind.Local), loaded.Games[0].NextTimeUpdatedOn);
        }
        finally
        {
            var root = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_WhenSnapshotIsCorrupt_ReturnsEmptySnapshotAndQuarantinesFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "CloudBerryTests", Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(root, "library.json");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(filePath, "{ this-is-not-valid-json");

        var store = new LibraryStore(filePath);

        try
        {
            var loaded = await store.LoadAsync();

            Assert.NotNull(loaded);
            Assert.Empty(loaded.Games);
            Assert.False(File.Exists(filePath));
            Assert.True(Directory.GetFiles(root, "library.json.corrupt.*.json").Length == 1);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CreateBackupAsync_CopiesCurrentSnapshotIntoBackupFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "CloudBerryTests", Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(root, "library.json");
        var store = new LibraryStore(filePath);

        try
        {
            await store.SaveAsync(new LibrarySnapshot
            {
                Games =
                [
                    new GameRecord
                    {
                        Title = "Stardew Valley",
                        Platform = "Windows PC"
                    }
                ]
            });

            var backupPath = await store.CreateBackupAsync();

            Assert.True(File.Exists(backupPath));
            Assert.StartsWith(store.BackupDirectory, backupPath, StringComparison.OrdinalIgnoreCase);

            var exportedStore = new LibraryStore(backupPath);
            var backup = await exportedStore.LoadAsync();
            Assert.Single(backup.Games);
            Assert.Equal("Stardew Valley", backup.Games[0].Title);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RestorePreviewAsync_ReadsSnapshotWithoutTouchingLiveLibrary()
    {
        var root = Path.Combine(Path.GetTempPath(), "CloudBerryTests", Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(root, "library.json");
        var backupFilePath = Path.Combine(root, "backups", "library-old.json");
        var store = new LibraryStore(filePath);

        try
        {
            // Live library currently has one game...
            await store.SaveAsync(new LibrarySnapshot
            {
                Games = [new GameRecord { Title = "Live Game", Platform = "Windows PC" }]
            });

            // ...but the backup we're about to preview has a different game entirely.
            Directory.CreateDirectory(Path.GetDirectoryName(backupFilePath)!);
            var backupStore = new LibraryStore(backupFilePath);
            await backupStore.SaveAsync(new LibrarySnapshot
            {
                Games = [new GameRecord { Title = "Backed Up Game", Platform = "Windows PC" }]
            });

            var preview = await store.RestorePreviewAsync(backupFilePath);

            Assert.Single(preview.Games);
            Assert.Equal("Backed Up Game", preview.Games[0].Title);

            // The live library must be completely untouched by a preview.
            var stillLive = await store.LoadAsync();
            Assert.Single(stillLive.Games);
            Assert.Equal("Live Game", stillLive.Games[0].Title);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RestorePreviewAsync_MissingFile_ThrowsPlayerReadableException()
    {
        var root = Path.Combine(Path.GetTempPath(), "CloudBerryTests", Guid.NewGuid().ToString("N"));
        var store = new LibraryStore(Path.Combine(root, "library.json"));

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(
            () => store.RestorePreviewAsync(Path.Combine(root, "does-not-exist.json")));

        Assert.Contains("could not be found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestorePreviewAsync_InvalidJson_ThrowsPlayerReadableException()
    {
        var root = Path.Combine(Path.GetTempPath(), "CloudBerryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var badFilePath = Path.Combine(root, "not-a-backup.json");
        await File.WriteAllTextAsync(badFilePath, "{ not valid json");
        var store = new LibraryStore(Path.Combine(root, "library.json"));

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => store.RestorePreviewAsync(badFilePath));

            Assert.Contains("valid Cirra library backup", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RestoreAsync_ReplacesLiveLibraryAndBacksUpWhatWasThereFirst()
    {
        var root = Path.Combine(Path.GetTempPath(), "CloudBerryTests", Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(root, "library.json");
        var backupFilePath = Path.Combine(root, "external", "cloudberry-library.json");
        var store = new LibraryStore(filePath);

        try
        {
            await store.SaveAsync(new LibrarySnapshot
            {
                Games = [new GameRecord { Title = "Old Live Game", Platform = "Windows PC" }]
            });

            Directory.CreateDirectory(Path.GetDirectoryName(backupFilePath)!);
            var exportedStore = new LibraryStore(backupFilePath);
            await exportedStore.SaveAsync(new LibrarySnapshot
            {
                Games =
                [
                    new GameRecord { Title = "Restored Game One", Platform = "Windows PC" },
                    new GameRecord { Title = "Restored Game Two", Platform = "Windows PC" }
                ]
            });

            var restored = await store.RestoreAsync(backupFilePath);

            Assert.Equal(2, restored.Games.Count);

            // The live file on disk now reflects the restored snapshot...
            var reloaded = await store.LoadAsync();
            Assert.Equal(2, reloaded.Games.Count);
            Assert.Contains(reloaded.Games, g => g.Title == "Restored Game One");

            // ...and restoring took a safety backup of what was live before the swap, so the
            // "Old Live Game" library isn't a one-way trip even though it's no longer live.
            Assert.True(Directory.Exists(store.BackupDirectory));
            var backupFiles = Directory.GetFiles(store.BackupDirectory, "library-*.json");
            Assert.NotEmpty(backupFiles);

            var preRestoreBackup = new LibraryStore(backupFiles.OrderByDescending(File.GetLastWriteTimeUtc).First());
            var preRestoreSnapshot = await preRestoreBackup.LoadAsync();
            Assert.Contains(preRestoreSnapshot.Games, g => g.Title == "Old Live Game");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_WipingANonEmptyLibraryToZero_TakesSafetyBackupFirst()
    {
        var root = Path.Combine(Path.GetTempPath(), "CloudBerryTests", Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(root, "library.json");
        var store = new LibraryStore(filePath);

        try
        {
            await store.SaveAsync(new LibrarySnapshot
            {
                Games = [new GameRecord { Title = "About To Vanish", Platform = "Windows PC" }]
            });

            // Simulate the bug this guard defends against: a save call that would wipe a
            // previously non-empty library down to zero games (bad state mutation, race, etc.).
            await store.SaveAsync(new LibrarySnapshot { Games = [] });

            Assert.True(Directory.Exists(store.BackupDirectory));
            var backupFiles = Directory.GetFiles(store.BackupDirectory, "library-*.json");
            Assert.NotEmpty(backupFiles);

            var safetyBackup = new LibraryStore(backupFiles.OrderByDescending(File.GetLastWriteTimeUtc).First());
            var safetySnapshot = await safetyBackup.LoadAsync();
            Assert.Contains(safetySnapshot.Games, g => g.Title == "About To Vanish");

            // The wipe itself is still allowed to go through \u2014 this is a safety net, not a block.
            var liveAfterWipe = await store.LoadAsync();
            Assert.Empty(liveAfterWipe.Games);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExportAsync_CopiesSnapshotToChosenPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "CloudBerryTests", Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(root, "library.json");
        var exportPath = Path.Combine(root, "exports", "cloudberry-library.json");
        var store = new LibraryStore(filePath);

        try
        {
            await store.SaveAsync(new LibrarySnapshot
            {
                Games =
                [
                    new GameRecord
                    {
                        Title = "Hollow Knight",
                        Platform = "Windows PC"
                    }
                ]
            });

            var writtenPath = await store.ExportAsync(exportPath);

            Assert.Equal(exportPath, writtenPath);
            Assert.True(File.Exists(exportPath));

            var exportedStore = new LibraryStore(exportPath);
            var exported = await exportedStore.LoadAsync();
            Assert.Single(exported.Games);
            Assert.Equal("Hollow Knight", exported.Games[0].Title);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
