using Microsoft.Data.Sqlite;

namespace CloudBerry.Data;

public sealed class CirraLibraryDatabase
{
    public const int CurrentSchemaVersion = 4;

    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private volatile bool _initialized;

    public CirraLibraryDatabase(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("A database path is required.", nameof(databasePath));
        }

        DatabasePath = Path.GetFullPath(databasePath);
    }

    public string DatabasePath { get; }

    public string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = DatabasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared,
        ForeignKeys = true
    }.ToString();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationGate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            var directory = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await ExecuteAsync(connection, "PRAGMA journal_mode=WAL;", cancellationToken);
            await ExecuteAsync(connection, "PRAGMA foreign_keys=ON;", cancellationToken);
            var existingVersion = await ReadSchemaVersionAsync(connection, cancellationToken);
            if (existingVersion > CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    $"This Cirra index uses schema {existingVersion}, but this build supports up to {CurrentSchemaVersion}.");
            }

            if (existingVersion is > 0 and < CurrentSchemaVersion)
            {
                CreateSchemaBackup(connection, existingVersion);
            }

            await ExecuteAsync(connection, SchemaSql, cancellationToken);
            await EnsureColumnAsync(
                connection,
                "game_titles",
                "search_text",
                "ALTER TABLE game_titles ADD COLUMN search_text TEXT NOT NULL DEFAULT '';",
                cancellationToken);
            await ExecuteAsync(connection, """
                CREATE INDEX IF NOT EXISTS ix_game_titles_search_text
                    ON game_titles(search_text COLLATE NOCASE);
                CREATE INDEX IF NOT EXISTS ix_library_artifacts_quick_hash
                    ON library_artifacts(quick_hash)
                    WHERE quick_hash <> '';
                """, cancellationToken);

            await using var versionCommand = connection.CreateCommand();
            versionCommand.CommandText = $"PRAGMA user_version={CurrentSchemaVersion};";
            await versionCommand.ExecuteNonQueryAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    public SqliteConnection CreateConnection() => new(ConnectionString);

    public static string GetDefaultIndexPath(string snapshotPath)
    {
        if (string.IsNullOrWhiteSpace(snapshotPath))
        {
            throw new ArgumentException("A snapshot path is required.", nameof(snapshotPath));
        }

        var fullPath = Path.GetFullPath(snapshotPath);
        var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
        return Path.Combine(directory, "cirra-library.db");
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> ReadSchemaVersionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string table,
        string column,
        string migrationSql,
        CancellationToken cancellationToken)
    {
        await using var inspect = connection.CreateCommand();
        inspect.CommandText = $"PRAGMA table_info({table});";
        await using var reader = await inspect.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        await reader.DisposeAsync();
        await ExecuteAsync(connection, migrationSql, cancellationToken);
    }

    private static void CreateSchemaBackup(SqliteConnection source, int existingVersion)
    {
        var directory = Path.GetDirectoryName(source.DataSource) ?? string.Empty;
        var fileName = Path.GetFileName(source.DataSource);
        var backupPath = Path.Combine(
            directory,
            $"{fileName}.schema-v{existingVersion}-{DateTime.UtcNow:yyyyMMddHHmmssfff}.bak");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = backupPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
        using var backup = new SqliteConnection(connectionString);
        backup.Open();
        source.BackupDatabase(backup);
    }

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS game_titles (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            sort_name TEXT NOT NULL,
            legacy_game_id TEXT NULL,
            search_text TEXT NOT NULL DEFAULT ''
        );

        CREATE INDEX IF NOT EXISTS ix_game_titles_sort_name
            ON game_titles(sort_name COLLATE NOCASE);

        CREATE TABLE IF NOT EXISTS metadata_field_values (
            game_title_id TEXT NOT NULL REFERENCES game_titles(id) ON DELETE CASCADE,
            field_key TEXT NOT NULL,
            value_json TEXT NOT NULL,
            source_id TEXT NOT NULL DEFAULT '',
            source_name TEXT NOT NULL DEFAULT '',
            confidence REAL NOT NULL DEFAULT 1,
            acquired_at_utc TEXT NOT NULL,
            is_user_locked INTEGER NOT NULL DEFAULT 0,
            revision INTEGER NOT NULL DEFAULT 1,
            PRIMARY KEY (game_title_id, field_key)
        );

        CREATE INDEX IF NOT EXISTS ix_metadata_field_values_source
            ON metadata_field_values(source_id);

        CREATE TABLE IF NOT EXISTS game_releases (
            id TEXT PRIMARY KEY,
            game_title_id TEXT NOT NULL REFERENCES game_titles(id) ON DELETE CASCADE,
            platform TEXT NOT NULL,
            region TEXT NOT NULL DEFAULT '',
            edition TEXT NOT NULL DEFAULT '',
            language TEXT NOT NULL DEFAULT '',
            released_on TEXT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_game_releases_platform
            ON game_releases(platform COLLATE NOCASE);

        CREATE TABLE IF NOT EXISTS game_installations (
            id TEXT PRIMARY KEY,
            game_release_id TEXT NOT NULL REFERENCES game_releases(id) ON DELETE CASCADE,
            source_kind TEXT NOT NULL,
            is_available INTEGER NOT NULL,
            last_seen_utc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS launch_profiles (
            id TEXT PRIMARY KEY,
            game_installation_id TEXT NOT NULL REFERENCES game_installations(id) ON DELETE CASCADE,
            name TEXT NOT NULL,
            kind INTEGER NOT NULL,
            target_path TEXT NOT NULL,
            arguments TEXT NOT NULL DEFAULT '',
            working_directory TEXT NOT NULL DEFAULT '',
            emulator_path TEXT NOT NULL DEFAULT '',
            core_path TEXT NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS library_artifacts (
            id TEXT PRIMARY KEY,
            canonical_key TEXT NOT NULL UNIQUE,
            original_path TEXT NOT NULL,
            canonical_path TEXT NOT NULL,
            source_kind TEXT NOT NULL,
            source_root TEXT NOT NULL DEFAULT '',
            length INTEGER NULL,
            last_write_utc TEXT NULL,
            quick_hash TEXT NOT NULL DEFAULT '',
            content_hash TEXT NOT NULL DEFAULT '',
            container_group_key TEXT NOT NULL DEFAULT '',
            availability INTEGER NOT NULL,
            last_seen_utc TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_library_artifacts_canonical_path
            ON library_artifacts(canonical_path COLLATE NOCASE);

        CREATE TABLE IF NOT EXISTS installation_artifacts (
            game_installation_id TEXT NOT NULL REFERENCES game_installations(id) ON DELETE CASCADE,
            library_artifact_id TEXT NOT NULL REFERENCES library_artifacts(id) ON DELETE CASCADE,
            PRIMARY KEY (game_installation_id, library_artifact_id)
        );

        CREATE TABLE IF NOT EXISTS migration_runs (
            id TEXT PRIMARY KEY,
            source_path TEXT NOT NULL,
            source_updated_utc TEXT NOT NULL,
            completed_utc TEXT NOT NULL,
            games_seen INTEGER NOT NULL,
            artifacts_seen INTEGER NOT NULL,
            duplicate_artifact_links INTEGER NOT NULL
        );
        """;
}
