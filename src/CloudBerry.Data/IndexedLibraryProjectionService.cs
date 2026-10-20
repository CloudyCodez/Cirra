using CloudBerry.Core.Models;

namespace CloudBerry.Data;

public sealed record LibraryGameProjection(
    Guid GameId,
    string Title,
    string Platform,
    string SourceKind,
    bool IsAvailable,
    string LaunchTarget);

public sealed class IndexedLibraryProjectionService
{
    private readonly CirraLibraryDatabase _database;

    public IndexedLibraryProjectionService(CirraLibraryDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM game_titles;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<LibraryGameProjection>> SearchAsync(
        string? query,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Choose a result limit from 1 to 1000.");
        }

        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT t.id, t.name, r.platform, i.source_kind, i.is_available, p.target_path
            FROM game_titles t
            JOIN game_releases r ON r.game_title_id = t.id
            JOIN game_installations i ON i.game_release_id = r.id
            LEFT JOIN launch_profiles p ON p.game_installation_id = i.id
            WHERE $query = '' OR t.search_text LIKE $pattern ESCAPE '\'
            ORDER BY t.sort_name COLLATE NOCASE, t.id
            LIMIT $limit;
            """;
        var normalizedQuery = query?.Trim() ?? string.Empty;
        command.Parameters.AddWithValue("$query", normalizedQuery);
        command.Parameters.AddWithValue("$pattern", $"%{EscapeLike(normalizedQuery)}%");
        command.Parameters.AddWithValue("$limit", limit);

        var results = new List<LibraryGameProjection>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new LibraryGameProjection(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4) != 0,
                reader.IsDBNull(5) ? string.Empty : reader.GetString(5)));
        }

        return results;
    }

    public async Task<IReadOnlySet<Guid>> SearchIdsAsync(
        string? query,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (normalizedQuery.Length == 0)
        {
            return new HashSet<Guid>();
        }

        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id
            FROM game_titles
            WHERE search_text LIKE $pattern ESCAPE '\';
            """;
        command.Parameters.AddWithValue("$pattern", $"%{EscapeLike(normalizedQuery)}%");

        var results = new HashSet<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(Guid.Parse(reader.GetString(0)));
        }

        return results;
    }

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
