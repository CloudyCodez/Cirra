using System.Diagnostics;
using CloudBerry.Core.Models;
using CloudBerry.Data;
using Microsoft.Data.Sqlite;

namespace CloudBerry.Core.Tests;

public sealed class LibraryIndexMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"cirra-index-{Guid.NewGuid():N}");

    [Fact]
    public void Preview_IsReadOnly_AndExplainsSharedArtifacts()
    {
        Directory.CreateDirectory(_root);
        var databasePath = Path.Combine(_root, "cirra-library.db");
        var migration = new LibraryIndexMigration(new CirraLibraryDatabase(databasePath));
        var sharedPath = Path.Combine(_root, "Games", "Shared.exe");
        var snapshot = new LibrarySnapshot
        {
            Games =
            [
                CreateGame("First", sharedPath),
                CreateGame("Second", sharedPath.ToUpperInvariant()),
                CreateGame("No target", string.Empty)
            ]
        };

        var preview = migration.Preview(snapshot);

        Assert.False(File.Exists(databasePath));
        Assert.Equal(3, preview.Games);
        Assert.Equal(1, preview.UniqueArtifacts);
        Assert.Equal(1, preview.DuplicateArtifactLinks);
        Assert.Equal(1, preview.MissingLaunchTargets);
        Assert.Contains(preview.Warnings, warning => warning.Contains("First", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Migration_IsIdempotent_AndStoresAnArtifactOnce()
    {
        Directory.CreateDirectory(_root);
        var databasePath = Path.Combine(_root, "cirra-library.db");
        var sourcePath = Path.Combine(_root, "library.json");
        var sharedPath = Path.Combine(_root, "Games", "Shared.exe");
        var snapshot = new LibrarySnapshot
        {
            Games =
            [
                CreateGame("First", sharedPath),
                CreateGame("Second", sharedPath)
            ]
        };
        var database = new CirraLibraryDatabase(databasePath);
        var migration = new LibraryIndexMigration(database);

        var first = await migration.MigrateAsync(snapshot, sourcePath);
        var second = await migration.MigrateAsync(snapshot, sourcePath);
        var registry = new LibraryArtifactRegistry(database);
        var projections = new IndexedLibraryProjectionService(database);

        Assert.Equal(2, first.TitlesInserted);
        Assert.Equal(1, first.ArtifactsInserted);
        Assert.Equal(2, first.ArtifactLinksInserted);
        Assert.Equal(0, second.TitlesInserted);
        Assert.Equal(0, second.ArtifactsInserted);
        Assert.Equal(0, second.ArtifactLinksInserted);
        Assert.Equal(1, await registry.CountAsync());
        Assert.Equal(2, await projections.CountAsync());
    }

    [Fact]
    public async Task Registry_UsesCanonicalPathIdentity()
    {
        Directory.CreateDirectory(_root);
        var database = new CirraLibraryDatabase(Path.Combine(_root, "cirra-library.db"));
        var migration = new LibraryIndexMigration(database);
        var path = Path.Combine(_root, "Games", "One.exe");
        var snapshot = new LibrarySnapshot { Games = [CreateGame("One", path)] };

        await migration.MigrateAsync(snapshot, Path.Combine(_root, "library.json"));
        var registry = new LibraryArtifactRegistry(database);

        var artifact = await registry.FindByPathAsync(path.ToUpperInvariant());

        Assert.NotNull(artifact);
        Assert.Equal(path.ToUpperInvariant(), artifact.CanonicalPath);
        Assert.Equal(ArtifactAvailability.Missing, artifact.Availability);
    }

    [Fact]
    public async Task Reconciliation_ClassifiesExactNewAndInvalidArtifacts()
    {
        Directory.CreateDirectory(_root);
        var database = new CirraLibraryDatabase(Path.Combine(_root, "cirra-library.db"));
        var migration = new LibraryIndexMigration(database);
        var existingPath = Path.Combine(_root, "Games", "One.exe");
        await migration.MigrateAsync(
            new LibrarySnapshot { Games = [CreateGame("One", existingPath)] },
            Path.Combine(_root, "library.json"));
        var reconciliation = new LibraryArtifactReconciliationService(database);

        var results = await reconciliation.ReconcileAsync(
            [existingPath.ToUpperInvariant(), Path.Combine(_root, "Games", "Two.exe"), "\0invalid"]);

        Assert.Equal(ArtifactReconciliationKind.ExactArtifact, results[0].Kind);
        Assert.NotNull(results[0].ExistingArtifactId);
        Assert.Equal(ArtifactReconciliationKind.NewArtifact, results[1].Kind);
        Assert.Equal(ArtifactReconciliationKind.InvalidPath, results[2].Kind);
    }

    [Fact]
    public async Task Reconciliation_HandlesMoreThanSqliteParameterLimit()
    {
        Directory.CreateDirectory(_root);
        var database = new CirraLibraryDatabase(Path.Combine(_root, "cirra-library.db"));
        var migration = new LibraryIndexMigration(database);
        var snapshot = LibraryFixtureFactory.Create(1_500, _root);
        await migration.MigrateAsync(snapshot, Path.Combine(_root, "library.json"));
        var paths = snapshot.Games.Select(game => game.InstallPath).ToList();
        paths.Add(Path.Combine(_root, "Games", "brand-new.bin"));

        var results = await new LibraryArtifactReconciliationService(database).ReconcileAsync(paths);

        Assert.Equal(1_501, results.Count);
        Assert.Equal(1_500, results.Count(result => result.AlreadyIndexed));
        Assert.Equal(ArtifactReconciliationKind.NewArtifact, results[^1].Kind);
    }

    [Fact]
    public async Task Reconciliation_HoldsMovedContentForReview()
    {
        Directory.CreateDirectory(_root);
        var originalPath = Path.Combine(_root, "Original", "game.bin");
        var movedPath = Path.Combine(_root, "Moved", "game.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(originalPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(movedPath)!);
        var content = Enumerable.Range(0, 350_000).Select(index => (byte)(index % 251)).ToArray();
        await File.WriteAllBytesAsync(originalPath, content);
        await File.WriteAllBytesAsync(movedPath, content);
        var database = new CirraLibraryDatabase(Path.Combine(_root, "cirra-library.db"));
        await new LibraryIndexMigration(database).MigrateAsync(
            new LibrarySnapshot { Games = [CreateGame("Original", originalPath)] },
            Path.Combine(_root, "library.json"));

        var result = Assert.Single(
            await new LibraryArtifactReconciliationService(database).ReconcileAsync([movedPath]));

        Assert.Equal(ArtifactReconciliationKind.ProbableMovedArtifact, result.Kind);
        Assert.False(result.AlreadyIndexed);
        Assert.NotNull(result.ExistingArtifactId);
        Assert.Contains(originalPath, result.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IndexedProjection_SearchesLiteralWildcardCharacters()
    {
        Directory.CreateDirectory(_root);
        var database = new CirraLibraryDatabase(Path.Combine(_root, "cirra-library.db"));
        var migration = new LibraryIndexMigration(database);
        var snapshot = new LibrarySnapshot
        {
            Games =
            [
                CreateGame("100% Orange Juice", Path.Combine(_root, "orange.exe")),
                CreateGame("Under_score", Path.Combine(_root, "under.exe")),
                CreateGame("Ordinary", Path.Combine(_root, "ordinary.exe"))
            ]
        };
        await migration.MigrateAsync(snapshot, Path.Combine(_root, "library.json"));
        var projections = new IndexedLibraryProjectionService(database);

        var percent = await projections.SearchAsync("%");
        var underscore = await projections.SearchAsync("_");

        Assert.Single(percent);
        Assert.Equal("100% Orange Juice", percent[0].Title);
        Assert.Single(underscore);
        Assert.Equal("Under_score", underscore[0].Title);
    }

    [Fact]
    public async Task Migration_SynchronizesChangesAndRemovesDeletedTitles()
    {
        Directory.CreateDirectory(_root);
        var database = new CirraLibraryDatabase(Path.Combine(_root, "cirra-library.db"));
        var migration = new LibraryIndexMigration(database);
        var first = CreateGame("Before", Path.Combine(_root, "before.exe"));
        var removed = CreateGame("Remove me", Path.Combine(_root, "remove.exe"));
        var snapshot = new LibrarySnapshot { Games = [first, removed] };
        await migration.MigrateAsync(snapshot, Path.Combine(_root, "library.json"));

        first.Title = "After";
        first.Tags = ["Co-op", "Shared screen"];
        snapshot.Games.Remove(removed);
        await migration.MigrateAsync(snapshot, Path.Combine(_root, "library.json"));

        var projections = new IndexedLibraryProjectionService(database);
        Assert.Equal(1, await projections.CountAsync());
        Assert.Single(await projections.SearchAsync("After"));
        Assert.Single(await projections.SearchAsync("Co-op"));
        Assert.Single(await projections.SearchAsync("Co-op, Shared"));
        Assert.Empty(await projections.SearchAsync("Remove me"));
        Assert.Equal(1, await new LibraryArtifactRegistry(database).CountAsync());
    }

    [Fact]
    public async Task Migration_PersistsAndSynchronizesMetadataProvenance()
    {
        Directory.CreateDirectory(_root);
        var database = new CirraLibraryDatabase(Path.Combine(_root, "cirra-library.db"));
        var migration = new LibraryIndexMigration(database);
        var game = CreateGame("Player title", Path.Combine(_root, "game.exe"));
        game.MetadataFields =
        [
            new MetadataFieldProvenance
            {
                Field = nameof(GameRecord.Title),
                SourceId = "user",
                SourceName = "Player edit",
                IsUserLocked = true,
                Revision = 2,
                AcquiredAtUtc = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero)
            }
        ];
        var snapshot = new LibrarySnapshot { Games = [game] };

        await migration.MigrateAsync(snapshot, Path.Combine(_root, "library.json"));
        var repository = new MetadataProvenanceRepository(database);
        var stored = Assert.Single(await repository.GetForGameAsync(game.Id));

        Assert.Equal("\"Player title\"", stored.ValueJson);
        Assert.True(stored.Provenance.IsUserLocked);
        Assert.Equal(2, stored.Provenance.Revision);

        game.MetadataFields.Clear();
        await migration.MigrateAsync(snapshot, Path.Combine(_root, "library.json"));
        Assert.Empty(await repository.GetForGameAsync(game.Id));
    }

    [Fact]
    public async Task SearchIds_ReturnsMoreThanOneThousandMatches()
    {
        Directory.CreateDirectory(_root);
        var database = new CirraLibraryDatabase(Path.Combine(_root, "cirra-library.db"));
        var migration = new LibraryIndexMigration(database);
        var snapshot = LibraryFixtureFactory.Create(1_500, _root);
        await migration.MigrateAsync(snapshot, Path.Combine(_root, "library.json"));

        var ids = await new IndexedLibraryProjectionService(database).SearchIdsAsync("Fixture");

        Assert.Equal(1_500, ids.Count);
    }

    [Fact]
    public async Task TenThousandTitleFixture_MigratesAndQueriesDeterministically()
    {
        Directory.CreateDirectory(_root);
        var database = new CirraLibraryDatabase(Path.Combine(_root, "cirra-library.db"));
        var migration = new LibraryIndexMigration(database);
        var snapshot = LibraryFixtureFactory.Create(10_000, _root);
        var stopwatch = Stopwatch.StartNew();

        var result = await migration.MigrateAsync(snapshot, Path.Combine(_root, "library.json"));
        var projections = new IndexedLibraryProjectionService(database);
        var matches = await projections.SearchAsync("Game 0999", 20);
        stopwatch.Stop();

        Assert.Equal(10_000, result.TitlesInserted);
        Assert.Equal(10_000, result.ArtifactsInserted);
        Assert.Equal(10_000, await projections.CountAsync());
        Assert.Equal(10, matches.Count);
        Assert.All(matches, game => Assert.StartsWith("Game 0999", game.Title, StringComparison.Ordinal));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMinutes(1), $"10k fixture took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task FailedMigration_RollsBackEveryInsertedRow()
    {
        Directory.CreateDirectory(_root);
        var database = new CirraLibraryDatabase(Path.Combine(_root, "cirra-library.db"));
        await database.InitializeAsync();
        var migration = new LibraryIndexMigration(database);
        var snapshot = LibraryFixtureFactory.Create(100, _root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            migration.MigrateAsync(snapshot, Path.Combine(_root, "library.json"), cancellation.Token));

        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM game_titles;";
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Initialize_UpgradesAnEarlierIndexSchema()
    {
        Directory.CreateDirectory(_root);
        var databasePath = Path.Combine(_root, "cirra-library.db");
        await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE game_titles (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    sort_name TEXT NOT NULL,
                    legacy_game_id TEXT NULL
                );
                PRAGMA user_version=1;
                """;
            await command.ExecuteNonQueryAsync();
        }

        var database = new CirraLibraryDatabase(databasePath);
        await database.InitializeAsync();

        await using var verification = database.CreateConnection();
        await verification.OpenAsync();
        await using var columns = verification.CreateCommand();
        columns.CommandText = "PRAGMA table_info(game_titles);";
        await using var reader = await columns.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(1));
        }

        Assert.Contains("search_text", names);
        Assert.Single(Directory.GetFiles(_root, "cirra-library.db.schema-v1-*.bak"));
        await reader.DisposeAsync();
        await using var version = verification.CreateCommand();
        version.CommandText = "PRAGMA user_version;";
        Assert.Equal(CirraLibraryDatabase.CurrentSchemaVersion, Convert.ToInt32(await version.ExecuteScalarAsync()));
    }

    private static GameRecord CreateGame(string title, string path) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Platform = "Windows PC",
        SourceKind = "Test",
        InstallPath = path,
        IsInstalled = true
    };

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

internal static class LibraryFixtureFactory
{
    public static LibrarySnapshot Create(int count, string root)
    {
        var games = new List<GameRecord>(count);
        for (var index = 0; index < count; index++)
        {
            games.Add(new GameRecord
            {
                Id = CreateGuid(index),
                Title = $"Game {index:00000}",
                Platform = (index % 3) switch
                {
                    0 => "Windows PC",
                    1 => "Nintendo Switch",
                    _ => "PlayStation 2"
                },
                SourceKind = "Fixture",
                InstallPath = Path.Combine(root, "Games", $"game-{index:00000}.bin"),
                IsInstalled = true
            });
        }

        return new LibrarySnapshot
        {
            Games = games,
            LastUpdatedUtc = new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static Guid CreateGuid(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        return new Guid(bytes);
    }
}
