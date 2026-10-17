using CloudBerry.Core.Models;
using CloudBerry.Core.Services;

namespace CloudBerry.Core.Tests;

public sealed class LibraryHistoryServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CirraHistoryTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveWithHistoryAsync_RecordsAddedRemovedAndModifiedGames()
    {
        var store = CreateStore();
        var keptId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        await store.SaveAsync(new LibrarySnapshot
        {
            Games =
            [
                new GameRecord { Id = keptId, Title = "Before", Platform = "PC" },
                new GameRecord { Id = removedId, Title = "Removed", Platform = "PC" }
            ]
        });

        var entry = await store.SaveWithHistoryAsync(
            new LibrarySnapshot
            {
                Games =
                [
                    new GameRecord { Id = keptId, Title = "After", Platform = "PC" },
                    new GameRecord { Title = "Added", Platform = "PC" }
                ]
            },
            new LibraryHistoryContext("library.import", "Imported games", "Scan review accepted."));

        Assert.NotNull(entry);
        Assert.Equal(1, entry.AddedCount);
        Assert.Equal(1, entry.RemovedCount);
        Assert.Equal(1, entry.ModifiedCount);
        Assert.Equal("1 added, 1 removed, 1 changed", entry.Summary);

        var timeline = await store.History.ListAsync();
        Assert.Single(timeline);
        Assert.Equal("Imported games", timeline[0].Title);
    }

    [Fact]
    public async Task UndoHistoryEntryAsync_RestoresBeforeSnapshotAndRecordsTheUndo()
    {
        var store = CreateStore();
        var gameId = Guid.NewGuid();
        await store.SaveAsync(new LibrarySnapshot
        {
            Games = [new GameRecord { Id = gameId, Title = "Original", Platform = "PC" }]
        });
        var entry = await store.SaveWithHistoryAsync(
            new LibrarySnapshot
            {
                Games = [new GameRecord { Id = gameId, Title = "Edited", Platform = "PC" }]
            },
            new LibraryHistoryContext("metadata.apply", "Changed metadata"));

        Assert.NotNull(entry);
        var restored = await store.UndoHistoryEntryAsync(entry.Id);

        Assert.Equal("Original", Assert.Single(restored.Games).Title);
        Assert.Equal("Original", Assert.Single((await store.LoadAsync()).Games).Title);
        var timeline = await store.History.ListAsync();
        Assert.Equal(2, timeline.Count);
        Assert.Equal("history.undo", timeline[0].Kind);
    }

    [Fact]
    public async Task RecordAsync_DoesNotCreateNoiseWhenGamesAreUnchanged()
    {
        var store = CreateStore();
        var snapshot = new LibrarySnapshot
        {
            Games = [new GameRecord { Title = "Unchanged", Platform = "PC" }]
        };
        await store.SaveAsync(snapshot);

        var entry = await store.SaveWithHistoryAsync(
            snapshot,
            new LibraryHistoryContext("settings.save", "Saved settings"));

        Assert.Null(entry);
        Assert.Empty(await store.History.ListAsync());
    }

    private LibraryStore CreateStore()
    {
        Directory.CreateDirectory(_root);
        return new LibraryStore(Path.Combine(_root, "library.json"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }
}
