using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CloudBerry.Core.Models;
using CloudBerry.Core.Services;
using Microsoft.Data.Sqlite;

namespace CloudBerry.Data;

public sealed record LibraryMigrationPreview(
    int Games,
    int UniqueArtifacts,
    int DuplicateArtifactLinks,
    int MissingLaunchTargets,
    IReadOnlyList<string> Warnings);

public sealed record LibraryMigrationResult(
    LibraryMigrationPreview Preview,
    int TitlesInserted,
    int ReleasesInserted,
    int InstallationsInserted,
    int LaunchProfilesInserted,
    int ArtifactsInserted,
    int ArtifactLinksInserted,
    string DatabasePath);

public sealed class LibraryIndexMigration
{
    private readonly CirraLibraryDatabase _database;
    private readonly SemaphoreSlim _migrationGate = new(1, 1);

    public LibraryIndexMigration(CirraLibraryDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public LibraryMigrationPreview Preview(LibrarySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var artifactOwners = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var missingTargets = 0;

        foreach (var game in snapshot.Games)
        {
            if (string.IsNullOrWhiteSpace(game.InstallPath))
            {
                missingTargets++;
                continue;
            }

            try
            {
                var identity = LibraryArtifactIdentity.Create(game.InstallPath);
                if (!artifactOwners.TryGetValue(identity.CanonicalKey, out var owners))
                {
                    owners = [];
                    artifactOwners.Add(identity.CanonicalKey, owners);
                }

                owners.Add(string.IsNullOrWhiteSpace(game.Title) ? game.Id.ToString("D") : game.Title);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                missingTargets++;
            }
        }

        var duplicateLinks = artifactOwners.Values.Sum(owners => Math.Max(0, owners.Count - 1));
        var warnings = artifactOwners.Values
            .Where(owners => owners.Count > 1)
            .Take(20)
            .Select(owners => $"One launch artifact is shared by: {string.Join(", ", owners.Take(4))}.")
            .ToList();

        if (artifactOwners.Values.Count(owners => owners.Count > 1) > warnings.Count)
        {
            warnings.Add("Additional shared launch artifacts were omitted from this preview.");
        }

        if (missingTargets > 0)
        {
            warnings.Add($"{missingTargets} game record{(missingTargets == 1 ? " has" : "s have")} no usable launch target; the records will be preserved without artifacts.");
        }

        return new LibraryMigrationPreview(
            snapshot.Games.Count,
            artifactOwners.Count,
            duplicateLinks,
            missingTargets,
            warnings);
    }

    public async Task<LibraryMigrationResult> MigrateAsync(
        LibrarySnapshot snapshot,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await _migrationGate.WaitAsync(cancellationToken);
        try
        {
            return await MigrateCoreAsync(snapshot, sourcePath, cancellationToken);
        }
        finally
        {
            _migrationGate.Release();
        }
    }

    private async Task<LibraryMigrationResult> MigrateCoreAsync(
        LibrarySnapshot snapshot,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        var preview = Preview(snapshot);
        await _database.InitializeAsync(cancellationToken);

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var titlesInserted = 0;
        var releasesInserted = 0;
        var installationsInserted = 0;
        var profilesInserted = 0;
        var artifactsInserted = 0;
        var linksInserted = 0;

        try
        {
            await InsertAsync(connection, transaction, """
                CREATE TEMP TABLE IF NOT EXISTS migration_seen_titles (
                    id TEXT PRIMARY KEY
                );
                DELETE FROM migration_seen_titles;
                """, cancellationToken);

            foreach (var game in snapshot.Games)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var legacyId = game.Id == Guid.Empty
                    ? DeterministicGuid.Create("legacy-game", $"{game.Title}|{game.Platform}|{game.InstallPath}")
                    : game.Id;
                var titleId = legacyId;
                var releaseId = DeterministicGuid.Create("release", legacyId.ToString("D"));
                var installationId = DeterministicGuid.Create("installation", legacyId.ToString("D"));
                var profileId = DeterministicGuid.Create("launch-profile", legacyId.ToString("D"));

                var searchText = BuildSearchText(game);
                titlesInserted += await InsertAsync(connection, transaction, """
                    INSERT OR IGNORE INTO game_titles (id, name, sort_name, legacy_game_id, search_text)
                    VALUES ($id, $name, $sortName, $legacyGameId, $searchText);
                    """, cancellationToken,
                    ("$id", titleId.ToString("D")),
                    ("$name", game.Title),
                    ("$sortName", game.Title),
                    ("$legacyGameId", legacyId.ToString("D")),
                    ("$searchText", searchText));

                await InsertAsync(connection, transaction, """
                    UPDATE game_titles
                    SET name = $name, sort_name = $sortName, legacy_game_id = $legacyGameId,
                        search_text = $searchText
                    WHERE id = $id AND
                        (name <> $name OR sort_name <> $sortName OR
                         IFNULL(legacy_game_id, '') <> $legacyGameId OR search_text <> $searchText);
                    INSERT OR IGNORE INTO migration_seen_titles (id) VALUES ($id);
                    """, cancellationToken,
                    ("$id", titleId.ToString("D")),
                    ("$name", game.Title),
                    ("$sortName", game.Title),
                    ("$legacyGameId", legacyId.ToString("D")),
                    ("$searchText", searchText));

				var metadataFields = (game.MetadataFields ?? [])
					.Where(field => !string.IsNullOrWhiteSpace(field.Field))
					.GroupBy(field => field.Field.Trim(), StringComparer.OrdinalIgnoreCase)
					.Select(group => group.OrderByDescending(field => field.Revision).First())
					.ToList();
				if (metadataFields.Count == 0)
				{
					await InsertAsync(connection, transaction,
						"DELETE FROM metadata_field_values WHERE game_title_id = $titleId;",
						cancellationToken,
						("$titleId", titleId.ToString("D")));
				}
				else
				{
					var keepParameters = metadataFields
						.Select((_, index) => $"$field{index}")
						.ToArray();
					var cleanupParameters = new List<(string Name, object? Value)>
					{
						("$titleId", titleId.ToString("D"))
					};
					cleanupParameters.AddRange(metadataFields.Select((field, index) =>
						(keepParameters[index], (object?)field.Field.Trim())));
					await InsertAsync(connection, transaction, $"""
						DELETE FROM metadata_field_values
						WHERE game_title_id = $titleId
						  AND field_key NOT IN ({string.Join(", ", keepParameters)});
						""", cancellationToken, cleanupParameters.ToArray());

					foreach (var field in metadataFields)
					{
						await InsertAsync(connection, transaction, """
							INSERT INTO metadata_field_values
								(game_title_id, field_key, value_json, source_id, source_name,
								 confidence, acquired_at_utc, is_user_locked, revision)
							VALUES
								($titleId, $fieldKey, $valueJson, $sourceId, $sourceName,
								 $confidence, $acquiredAtUtc, $isUserLocked, $revision)
							ON CONFLICT(game_title_id, field_key) DO UPDATE SET
								value_json = excluded.value_json,
								source_id = excluded.source_id,
								source_name = excluded.source_name,
								confidence = excluded.confidence,
								acquired_at_utc = excluded.acquired_at_utc,
								is_user_locked = excluded.is_user_locked,
								revision = excluded.revision
							WHERE value_json <> excluded.value_json
							   OR source_id <> excluded.source_id
							   OR source_name <> excluded.source_name
							   OR confidence <> excluded.confidence
							   OR acquired_at_utc <> excluded.acquired_at_utc
							   OR is_user_locked <> excluded.is_user_locked
							   OR revision <> excluded.revision;
							""", cancellationToken,
							("$titleId", titleId.ToString("D")),
							("$fieldKey", field.Field.Trim()),
							("$valueJson", SerializeMetadataFieldValue(game, field.Field)),
							("$sourceId", field.SourceId ?? string.Empty),
							("$sourceName", field.SourceName ?? string.Empty),
							("$confidence", Math.Clamp(field.Confidence, 0, 1)),
							("$acquiredAtUtc", field.AcquiredAtUtc.ToString("O", CultureInfo.InvariantCulture)),
							("$isUserLocked", field.IsUserLocked ? 1 : 0),
							("$revision", Math.Max(1, field.Revision)));
					}
				}

                releasesInserted += await InsertAsync(connection, transaction, """
                    INSERT OR IGNORE INTO game_releases
                        (id, game_title_id, platform, released_on)
                    VALUES ($id, $titleId, $platform, $releasedOn);
                    """, cancellationToken,
                    ("$id", releaseId.ToString("D")),
                    ("$titleId", titleId.ToString("D")),
                    ("$platform", string.IsNullOrWhiteSpace(game.Platform) ? "Unknown" : game.Platform),
                    ("$releasedOn", game.ReleaseDate?.ToString("O", CultureInfo.InvariantCulture)));

                await InsertAsync(connection, transaction, """
                    UPDATE game_releases
                    SET platform = $platform, released_on = $releasedOn
                    WHERE id = $id AND
                        (platform <> $platform OR IFNULL(released_on, '') <> IFNULL($releasedOn, ''));
                    """, cancellationToken,
                    ("$id", releaseId.ToString("D")),
                    ("$platform", string.IsNullOrWhiteSpace(game.Platform) ? "Unknown" : game.Platform),
                    ("$releasedOn", game.ReleaseDate?.ToString("O", CultureInfo.InvariantCulture)));

                installationsInserted += await InsertAsync(connection, transaction, """
                    INSERT OR IGNORE INTO game_installations
                        (id, game_release_id, source_kind, is_available, last_seen_utc)
                    VALUES ($id, $releaseId, $sourceKind, $isAvailable, $lastSeenUtc);
                    """, cancellationToken,
                    ("$id", installationId.ToString("D")),
                    ("$releaseId", releaseId.ToString("D")),
                    ("$sourceKind", string.IsNullOrWhiteSpace(game.SourceKind) ? "Manual" : game.SourceKind),
                    ("$isAvailable", game.IsInstalled ? 1 : 0),
                    ("$lastSeenUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));

                await InsertAsync(connection, transaction, """
                    UPDATE game_installations
                    SET source_kind = $sourceKind, is_available = $isAvailable,
                        last_seen_utc = $lastSeenUtc
                    WHERE id = $id;
                    """, cancellationToken,
                    ("$id", installationId.ToString("D")),
                    ("$sourceKind", string.IsNullOrWhiteSpace(game.SourceKind) ? "Manual" : game.SourceKind),
                    ("$isAvailable", game.IsInstalled ? 1 : 0),
                    ("$lastSeenUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));

                profilesInserted += await InsertAsync(connection, transaction, """
                    INSERT OR IGNORE INTO launch_profiles
                        (id, game_installation_id, name, kind, target_path, working_directory, emulator_path)
                    VALUES ($id, $installationId, 'Play', $kind, $targetPath, $workingDirectory, $emulatorPath);
                    """, cancellationToken,
                    ("$id", profileId.ToString("D")),
                    ("$installationId", installationId.ToString("D")),
                    ("$kind", (int)ResolveLaunchKind(game)),
                    ("$targetPath", game.InstallPath ?? string.Empty),
                    ("$workingDirectory", ResolveWorkingDirectory(game.InstallPath)),
                    ("$emulatorPath", game.EmulatorPath ?? string.Empty));

                await InsertAsync(connection, transaction, """
                    UPDATE launch_profiles
                    SET kind = $kind, target_path = $targetPath, working_directory = $workingDirectory,
                        emulator_path = $emulatorPath
                    WHERE id = $id;
                    """, cancellationToken,
                    ("$id", profileId.ToString("D")),
                    ("$kind", (int)ResolveLaunchKind(game)),
                    ("$targetPath", game.InstallPath ?? string.Empty),
                    ("$workingDirectory", ResolveWorkingDirectory(game.InstallPath)),
                    ("$emulatorPath", game.EmulatorPath ?? string.Empty));

                if (string.IsNullOrWhiteSpace(game.InstallPath))
                {
                    continue;
                }

                LibraryArtifactIdentity identity;
                try
                {
                    identity = LibraryArtifactIdentity.Create(game.InstallPath);
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    continue;
                }

                var artifactId = DeterministicGuid.Create("artifact", identity.CanonicalKey);
                var file = TryGetFileInfo(identity.CanonicalPath);
                var quickHash = file is null
                    ? string.Empty
                    : await LibraryArtifactFingerprint.ComputeQuickHashAsync(identity.CanonicalPath, cancellationToken);
                artifactsInserted += await InsertAsync(connection, transaction, """
                    INSERT OR IGNORE INTO library_artifacts
                        (id, canonical_key, original_path, canonical_path, source_kind, length,
                         last_write_utc, quick_hash, availability, last_seen_utc)
                    VALUES
                        ($id, $canonicalKey, $originalPath, $canonicalPath, $sourceKind, $length,
                         $lastWriteUtc, $quickHash, $availability, $lastSeenUtc);
                    """, cancellationToken,
                    ("$id", artifactId.ToString("D")),
                    ("$canonicalKey", identity.CanonicalKey),
                    ("$originalPath", identity.OriginalPath),
                    ("$canonicalPath", identity.CanonicalPath),
                    ("$sourceKind", game.SourceKind ?? string.Empty),
                    ("$length", file?.Length),
                    ("$lastWriteUtc", file?.LastWriteTimeUtc.ToString("O", CultureInfo.InvariantCulture)),
                    ("$quickHash", quickHash),
                    ("$availability", (int)(file is null ? ArtifactAvailability.Missing : ArtifactAvailability.Available)),
                    ("$lastSeenUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));

                await InsertAsync(connection, transaction, """
                    UPDATE library_artifacts
                    SET original_path = $originalPath, canonical_path = $canonicalPath,
                        source_kind = $sourceKind, length = $length, last_write_utc = $lastWriteUtc,
                        quick_hash = $quickHash, availability = $availability, last_seen_utc = $lastSeenUtc
                    WHERE id = $id;
                    """, cancellationToken,
                    ("$id", artifactId.ToString("D")),
                    ("$originalPath", identity.OriginalPath),
                    ("$canonicalPath", identity.CanonicalPath),
                    ("$sourceKind", game.SourceKind ?? string.Empty),
                    ("$length", file?.Length),
                    ("$lastWriteUtc", file?.LastWriteTimeUtc.ToString("O", CultureInfo.InvariantCulture)),
                    ("$quickHash", quickHash),
                    ("$availability", (int)(file is null ? ArtifactAvailability.Missing : ArtifactAvailability.Available)),
                    ("$lastSeenUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));

                linksInserted += await InsertAsync(connection, transaction, """
                    INSERT OR IGNORE INTO installation_artifacts
                        (game_installation_id, library_artifact_id)
                    VALUES ($installationId, $artifactId);
                    """, cancellationToken,
                    ("$installationId", installationId.ToString("D")),
                    ("$artifactId", artifactId.ToString("D")));
            }

            await InsertAsync(connection, transaction, """
                DELETE FROM game_titles
                WHERE NOT EXISTS (
                    SELECT 1 FROM migration_seen_titles seen WHERE seen.id = game_titles.id
                );
                DELETE FROM library_artifacts
                WHERE NOT EXISTS (
                    SELECT 1 FROM installation_artifacts links
                    WHERE links.library_artifact_id = library_artifacts.id
                );
                """, cancellationToken);

            var migrationId = CreateMigrationId(snapshot, sourcePath);
            await InsertAsync(connection, transaction, """
                INSERT OR IGNORE INTO migration_runs
                    (id, source_path, source_updated_utc, completed_utc, games_seen,
                     artifacts_seen, duplicate_artifact_links)
                VALUES
                    ($id, $sourcePath, $sourceUpdatedUtc, $completedUtc, $gamesSeen,
                     $artifactsSeen, $duplicateArtifactLinks);
                """, cancellationToken,
                ("$id", migrationId),
                ("$sourcePath", sourcePath ?? string.Empty),
                ("$sourceUpdatedUtc", snapshot.LastUpdatedUtc.ToString("O", CultureInfo.InvariantCulture)),
                ("$completedUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)),
                ("$gamesSeen", preview.Games),
                ("$artifactsSeen", preview.UniqueArtifacts),
                ("$duplicateArtifactLinks", preview.DuplicateArtifactLinks));

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return new LibraryMigrationResult(
            preview,
            titlesInserted,
            releasesInserted,
            installationsInserted,
            profilesInserted,
            artifactsInserted,
            linksInserted,
            _database.DatabasePath);
    }

    private static string BuildSearchText(GameRecord game)
    {
        var tags = string.Join(", ", game.Tags ?? []);
        var genres = string.Join(", ", game.Genres ?? []);
        var developers = string.Join(", ", game.Developers ?? []);
        var publishers = string.Join(", ", game.Publishers ?? []);
        return $"{game.Title} {game.Platform} {game.Status} {tags} {game.SourceKind} {game.Summary} {genres} {developers} {publishers}";
    }

	private static string SerializeMetadataFieldValue(GameRecord game, string field)
	{
		object? value = field switch
		{
			nameof(GameRecord.Title) => game.Title,
			nameof(GameRecord.Platform) => game.Platform,
			nameof(GameRecord.Summary) => game.Summary,
			nameof(GameRecord.ReleaseDate) => game.ReleaseDate,
			nameof(GameRecord.CoverImageUrl) => game.CoverImageUrl,
			nameof(GameRecord.BackgroundImageUrl) => game.BackgroundImageUrl,
			nameof(GameRecord.BannerImageUrl) => game.BannerImageUrl,
			nameof(GameRecord.IconImageUrl) => game.IconImageUrl,
			nameof(GameRecord.WebsiteUrl) => game.WebsiteUrl,
			nameof(GameRecord.CommunityRating) => game.CommunityRating,
			nameof(GameRecord.CriticRating) => game.CriticRating,
			nameof(GameRecord.Genres) => game.Genres,
			nameof(GameRecord.Developers) => game.Developers,
			nameof(GameRecord.Publishers) => game.Publishers,
			nameof(GameRecord.Tags) => game.Tags,
			nameof(GameRecord.ScreenshotUrls) => game.ScreenshotUrls,
			nameof(GameRecord.TrailerUrl) => game.TrailerUrl,
			_ => null
		};
		return JsonSerializer.Serialize(value, value?.GetType() ?? typeof(object));
	}

    private static string CreateMigrationId(LibrarySnapshot snapshot, string sourcePath)
    {
        var input = $"{Path.GetFullPath(sourcePath)}|{snapshot.LastUpdatedUtc:O}|{snapshot.Games.Count}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    private static LaunchProfileKind ResolveLaunchKind(GameRecord game)
    {
        if (!string.IsNullOrWhiteSpace(game.EmulatorPath))
        {
            return LaunchProfileKind.Emulator;
        }

        if (Uri.TryCreate(game.InstallPath, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return LaunchProfileKind.Uri;
        }

        return string.IsNullOrWhiteSpace(game.InstallPath)
            ? LaunchProfileKind.Unknown
            : LaunchProfileKind.Executable;
    }

    private static string ResolveWorkingDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static FileInfo? TryGetFileInfo(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return file.Exists ? file : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<int> InsertAsync(
        SqliteConnection connection,
        System.Data.Common.DbTransaction transaction,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = commandText;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
