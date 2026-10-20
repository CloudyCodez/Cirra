using CloudBerry.Core.Services;

namespace CloudBerry.Data;

public enum ArtifactReconciliationKind
{
    NewArtifact,
    ExactArtifact,
    ProbableMovedArtifact,
    InvalidPath
}

public sealed record ArtifactReconciliationResult(
    string InputPath,
    string CanonicalKey,
    ArtifactReconciliationKind Kind,
    Guid? ExistingArtifactId,
    string Explanation)
{
    public bool AlreadyIndexed => Kind == ArtifactReconciliationKind.ExactArtifact;
}

public sealed class LibraryArtifactReconciliationService
{
    private readonly CirraLibraryDatabase _database;

    public LibraryArtifactReconciliationService(CirraLibraryDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<IReadOnlyList<ArtifactReconciliationResult>> ReconcileAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var inputs = paths.Select(path => path ?? string.Empty).ToList();
        var identities = new Dictionary<string, LibraryArtifactIdentity>(StringComparer.OrdinalIgnoreCase);
        var invalid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in inputs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                identities[path] = LibraryArtifactIdentity.Create(path);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                invalid.Add(path);
            }
        }

        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = """
                CREATE TEMP TABLE IF NOT EXISTS reconciliation_keys (
                    canonical_key TEXT PRIMARY KEY
                );
                DELETE FROM reconciliation_keys;
                """;
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)transaction;
            insert.CommandText = "INSERT OR IGNORE INTO reconciliation_keys (canonical_key) VALUES ($key);";
            var keyParameter = insert.Parameters.Add("$key", Microsoft.Data.Sqlite.SqliteType.Text);
            foreach (var identity in identities.Values.DistinctBy(item => item.CanonicalKey, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                keyParameter.Value = identity.CanonicalKey;
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        var existing = new Dictionary<string, Guid>(StringComparer.Ordinal);
        await using (var query = connection.CreateCommand())
        {
            query.CommandText = """
                SELECT artifacts.canonical_key, artifacts.id
                FROM reconciliation_keys requested
                INNER JOIN library_artifacts artifacts
                    ON artifacts.canonical_key = requested.canonical_key;
                """;
            await using var reader = await query.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                existing[reader.GetString(0)] = Guid.Parse(reader.GetString(1));
            }
        }

        var fingerprints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in identities.Where(pair => !existing.ContainsKey(pair.Value.CanonicalKey)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fingerprint = await LibraryArtifactFingerprint.ComputeQuickHashAsync(
                pair.Value.CanonicalPath,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(fingerprint))
            {
                fingerprints[pair.Key] = fingerprint;
            }
        }

        var movedMatches = new Dictionary<string, (Guid Id, string Path)>(StringComparer.OrdinalIgnoreCase);
        if (fingerprints.Count > 0)
        {
            await using (var createHashes = connection.CreateCommand())
            {
                createHashes.CommandText = """
                    CREATE TEMP TABLE IF NOT EXISTS reconciliation_hashes (
                        quick_hash TEXT PRIMARY KEY
                    );
                    DELETE FROM reconciliation_hashes;
                    """;
                await createHashes.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var hashTransaction = await connection.BeginTransactionAsync(cancellationToken))
            {
                await using var insertHash = connection.CreateCommand();
                insertHash.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)hashTransaction;
                insertHash.CommandText = "INSERT OR IGNORE INTO reconciliation_hashes (quick_hash) VALUES ($hash);";
                var hashParameter = insertHash.Parameters.Add("$hash", Microsoft.Data.Sqlite.SqliteType.Text);
                foreach (var fingerprint in fingerprints.Values.Distinct(StringComparer.Ordinal))
                {
                    hashParameter.Value = fingerprint;
                    await insertHash.ExecuteNonQueryAsync(cancellationToken);
                }

                await hashTransaction.CommitAsync(cancellationToken);
            }

            var indexedByHash = new Dictionary<string, (Guid Id, string Path)>(StringComparer.Ordinal);
            await using (var queryHashes = connection.CreateCommand())
            {
                queryHashes.CommandText = """
                    SELECT artifacts.quick_hash, artifacts.id, artifacts.canonical_path
                    FROM reconciliation_hashes requested
                    INNER JOIN library_artifacts artifacts
                        ON artifacts.quick_hash = requested.quick_hash
                    WHERE artifacts.quick_hash <> '';
                    """;
                await using var reader = await queryHashes.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    indexedByHash.TryAdd(
                        reader.GetString(0),
                        (Guid.Parse(reader.GetString(1)), reader.GetString(2)));
                }
            }

            foreach (var pair in fingerprints)
            {
                if (indexedByHash.TryGetValue(pair.Value, out var match))
                {
                    movedMatches[pair.Key] = match;
                }
            }
        }

        return inputs.Select(path =>
        {
            if (invalid.Contains(path) || !identities.TryGetValue(path, out var identity))
            {
                return new ArtifactReconciliationResult(
                    path,
                    string.Empty,
                    ArtifactReconciliationKind.InvalidPath,
                    null,
                    "The launch path could not be normalized and needs manual review.");
            }

            return existing.TryGetValue(identity.CanonicalKey, out var artifactId)
                ? new ArtifactReconciliationResult(
                    path,
                    identity.CanonicalKey,
                    ArtifactReconciliationKind.ExactArtifact,
                    artifactId,
                    "This exact launch artifact is already registered in the Cirra library.")
                : movedMatches.TryGetValue(path, out var moved)
                    ? new ArtifactReconciliationResult(
                        path,
                        identity.CanonicalKey,
                        ArtifactReconciliationKind.ProbableMovedArtifact,
                        moved.Id,
                        $"This file matches an indexed artifact at {moved.Path} and may have been moved or copied.")
                : new ArtifactReconciliationResult(
                    path,
                    identity.CanonicalKey,
                    ArtifactReconciliationKind.NewArtifact,
                    null,
                    "No matching launch artifact exists in the Cirra library.");
        }).ToList();
    }
}
