using System.Globalization;
using CloudBerry.Core.Models;

namespace CloudBerry.Data;

public sealed record StoredMetadataField(
    Guid GameTitleId,
    string Field,
    string ValueJson,
    MetadataFieldProvenance Provenance);

public sealed class MetadataProvenanceRepository
{
    private readonly CirraLibraryDatabase _database;

    public MetadataProvenanceRepository(CirraLibraryDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<IReadOnlyList<StoredMetadataField>> GetForGameAsync(
        Guid gameTitleId,
        CancellationToken cancellationToken = default)
    {
        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT field_key, value_json, source_id, source_name, confidence,
                   acquired_at_utc, is_user_locked, revision
            FROM metadata_field_values
            WHERE game_title_id = $gameTitleId
            ORDER BY field_key COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$gameTitleId", gameTitleId.ToString("D"));

        var results = new List<StoredMetadataField>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var field = reader.GetString(0);
            results.Add(new StoredMetadataField(
                gameTitleId,
                field,
                reader.GetString(1),
                new MetadataFieldProvenance
                {
                    Field = field,
                    SourceId = reader.GetString(2),
                    SourceName = reader.GetString(3),
                    Confidence = reader.GetDouble(4),
                    AcquiredAtUtc = DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
                    IsUserLocked = reader.GetInt32(6) != 0,
                    Revision = reader.GetInt32(7)
                }));
        }

        return results;
    }
}
