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
                    Title = "Hades"
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
