using System.Globalization;
using CloudBerry.Core.Models;
using CloudBerry.Core.Services;

namespace CloudBerry.Data;

public sealed class LibraryArtifactRegistry
{
    private readonly CirraLibraryDatabase _database;

    public LibraryArtifactRegistry(CirraLibraryDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM library_artifacts;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    public async Task<LibraryArtifact?> FindByPathAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var identity = LibraryArtifactIdentity.Create(path);
        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, canonical_key, original_path, canonical_path, source_kind, source_root,
                   length, last_write_utc, quick_hash, content_hash, container_group_key,
                   availability, last_seen_utc
            FROM library_artifacts
            WHERE canonical_key = $canonicalKey;
            """;
        command.Parameters.AddWithValue("$canonicalKey", identity.CanonicalKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new LibraryArtifact
        {
            Id = Guid.Parse(reader.GetString(0)),
            CanonicalKey = reader.GetString(1),
            OriginalPath = reader.GetString(2),
            CanonicalPath = reader.GetString(3),
            SourceKind = reader.GetString(4),
            SourceRoot = reader.GetString(5),
            Length = reader.IsDBNull(6) ? null : reader.GetInt64(6),
            LastWriteUtc = ParseDate(reader, 7),
            QuickHash = reader.GetString(8),
            ContentHash = reader.GetString(9),
            ContainerGroupKey = reader.GetString(10),
            Availability = (ArtifactAvailability)reader.GetInt32(11),
            LastSeenUtc = DateTimeOffset.Parse(reader.GetString(12), CultureInfo.InvariantCulture)
        };
    }

    private static DateTimeOffset? ParseDate(Microsoft.Data.Sqlite.SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? null
            : DateTimeOffset.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture);
    }
}
