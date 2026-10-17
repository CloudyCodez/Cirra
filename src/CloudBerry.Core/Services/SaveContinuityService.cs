using System.Security.Cryptography;
using System.Text.Json;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services;

public sealed class SaveContinuityService
{
    private const int MaxFilesPerGame = 10_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public SaveContinuityService(string backupRoot)
    {
        if (string.IsNullOrWhiteSpace(backupRoot))
        {
            throw new ArgumentException("Choose a save backup directory.", nameof(backupRoot));
        }

        BackupRoot = Path.GetFullPath(backupRoot);
    }

    public string BackupRoot { get; }

    public async Task<SaveDiscoveryResult> DiscoverAsync(GameRecord game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        var result = new SaveDiscoveryResult();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var locations = game.SaveLocations ?? [];

        for (var rootIndex = 0; rootIndex < locations.Count; rootIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var configured = locations[rootIndex];
            if (string.IsNullOrWhiteSpace(configured))
            {
                continue;
            }

            string rootPath;
            try
            {
                rootPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured.Trim()));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                result.Warnings.Add($"Save location {rootIndex + 1} is invalid.");
                continue;
            }

            if (File.Exists(rootPath))
            {
                await AddArtifactAsync(result, seen, rootIndex, rootPath, rootPath, Path.GetFileName(rootPath), cancellationToken);
                continue;
            }

            if (!Directory.Exists(rootPath))
            {
                result.Warnings.Add($"Save location not found: {rootPath}");
                continue;
            }

            try
            {
                foreach (var filePath in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (result.Artifacts.Count >= MaxFilesPerGame)
                    {
                        result.Warnings.Add($"Discovery stopped at {MaxFilesPerGame:N0} files.");
                        return result;
                    }

                    var relativePath = Path.GetRelativePath(rootPath, filePath);
                    if (relativePath == ".." || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    {
                        result.Warnings.Add($"Skipped a save file outside its configured root: {filePath}");
                        continue;
                    }
                    await AddArtifactAsync(result, seen, rootIndex, rootPath, filePath, relativePath, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                result.Warnings.Add($"Could not fully read save location: {rootPath}");
            }
        }

        return result;
    }

    public async Task<SaveBackupResult> CreateBackupAsync(GameRecord game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        var discovery = await DiscoverAsync(game, cancellationToken);
        if (discovery.Artifacts.Count == 0)
        {
            throw new InvalidOperationException("No readable save files were found for this game.");
        }

        var folderName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        var backupDirectory = Path.Combine(BackupRoot, game.Id.ToString("N"), folderName);
        var filesDirectory = Path.Combine(backupDirectory, "files");
        Directory.CreateDirectory(filesDirectory);

        var manifest = new SaveBackupManifest
        {
            GameId = game.Id,
            GameTitle = game.Title,
            SourceRoots = (game.SaveLocations ?? []).Select(ExpandPath).ToList()
        };

        foreach (var artifact in discovery.Artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var safeRelative = NormalizeRelativePath(artifact.RelativePath);
            var backupRelative = Path.Combine("files", artifact.RootIndex.ToString(), safeRelative);
            var destination = ResolveUnder(backupDirectory, backupRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await CopyAtomicAsync(artifact.AbsolutePath, destination, cancellationToken);

            manifest.Entries.Add(new SaveBackupEntry
            {
                RootIndex = artifact.RootIndex,
                RootWasFile = File.Exists(artifact.RootPath),
                RelativePath = safeRelative,
                BackupRelativePath = backupRelative,
                SizeBytes = artifact.SizeBytes,
                LastWriteUtc = artifact.LastWriteUtc,
                Sha256 = artifact.Sha256
            });
        }

        var manifestPath = Path.Combine(backupDirectory, "manifest.json");
        await using (var stream = File.Create(manifestPath))
        {
            await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken);
        }

        var verification = await VerifyAsync(manifestPath, cancellationToken);
        if (!verification.IsValid)
        {
            throw new IOException("The save backup was written but failed integrity verification.");
        }

        return new SaveBackupResult(manifestPath, manifest.Entries.Count, manifest.Entries.Sum(item => item.SizeBytes));
    }

    public async Task<SaveVerificationResult> VerifyAsync(string manifestPath, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        SaveBackupManifest manifest;
        string backupDirectory;
        try
        {
            (manifest, backupDirectory) = await LoadManifestAsync(manifestPath, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            return new SaveVerificationResult(false, 0, [ex.Message]);
        }

        var verified = 0;
        foreach (var entry in manifest.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var filePath = ResolveUnder(backupDirectory, entry.BackupRelativePath);
                if (!File.Exists(filePath))
                {
                    errors.Add($"Missing backup file: {entry.RelativePath}");
                    continue;
                }

                var info = new FileInfo(filePath);
                var hash = await ComputeHashAsync(filePath, cancellationToken);
                if (info.Length != entry.SizeBytes || !string.Equals(hash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Integrity check failed: {entry.RelativePath}");
                    continue;
                }

                verified++;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                errors.Add($"{entry.RelativePath}: {ex.Message}");
            }
        }

        return new SaveVerificationResult(errors.Count == 0, verified, errors);
    }

    public async Task<IReadOnlyList<SaveBackupSummary>> ListBackupsAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        var gameDirectory = Path.Combine(BackupRoot, gameId.ToString("N"));
        if (!Directory.Exists(gameDirectory))
        {
            return [];
        }

        var results = new List<SaveBackupSummary>();
        foreach (var manifestPath in Directory.EnumerateFiles(gameDirectory, "manifest.json", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var (manifest, _) = await LoadManifestAsync(manifestPath, cancellationToken);
                if (manifest.GameId == gameId)
                {
                    results.Add(new SaveBackupSummary(
                        manifestPath,
                        manifest.CreatedUtc,
                        manifest.Entries.Count,
                        manifest.Entries.Sum(entry => entry.SizeBytes)));
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
            {
                // A broken entry should not hide the rest of a game's healthy timeline.
            }
        }

        return results.OrderByDescending(item => item.CreatedUtc).ToList();
    }

    public async Task<SaveRestoreResult> RestoreAsync(
        GameRecord game,
        string manifestPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        var verification = await VerifyAsync(manifestPath, cancellationToken);
        if (!verification.IsValid)
        {
            throw new InvalidDataException("This save backup failed integrity verification and was not restored.");
        }

        var (manifest, backupDirectory) = await LoadManifestAsync(manifestPath, cancellationToken);
        if (manifest.GameId != game.Id)
        {
            throw new InvalidDataException("This save backup belongs to a different game.");
        }

        var roots = (game.SaveLocations ?? []).Select(ExpandPath).ToList();
        var rollbackDirectory = Path.Combine(backupDirectory, $"rollback-{DateTime.UtcNow:yyyyMMdd-HHmmss}");
        var restored = 0;

        foreach (var entry in manifest.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.RootIndex < 0 || entry.RootIndex >= roots.Count)
            {
                throw new InvalidDataException("The backup references a save root that is no longer configured.");
            }

            var source = ResolveUnder(backupDirectory, entry.BackupRelativePath);
            var destination = ResolveRestoreDestination(roots[entry.RootIndex], entry.RelativePath, entry.RootWasFile);
            if (File.Exists(destination))
            {
                var rollbackPath = ResolveUnder(rollbackDirectory, Path.Combine(entry.RootIndex.ToString(), entry.RelativePath));
                Directory.CreateDirectory(Path.GetDirectoryName(rollbackPath)!);
                await CopyAtomicAsync(destination, rollbackPath, cancellationToken);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await CopyAtomicAsync(source, destination, cancellationToken);
            File.SetLastWriteTimeUtc(destination, entry.LastWriteUtc);
            restored++;
        }

        return new SaveRestoreResult(restored, rollbackDirectory);
    }

    private static async Task AddArtifactAsync(
        SaveDiscoveryResult result,
        HashSet<string> seen,
        int rootIndex,
        string rootPath,
        string filePath,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!seen.Add(fullPath))
        {
            return;
        }

        var info = new FileInfo(fullPath);
        var hash = await ComputeHashAsync(fullPath, cancellationToken);
        result.Artifacts.Add(new SaveArtifact(
            rootIndex,
            rootPath,
            fullPath,
            NormalizeRelativePath(relativePath),
            info.Length,
            info.LastWriteTimeUtc,
            hash));
    }

    private async Task<(SaveBackupManifest Manifest, string Directory)> LoadManifestAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        var fullManifestPath = Path.GetFullPath(manifestPath);
        if (!IsUnder(BackupRoot, fullManifestPath) || !File.Exists(fullManifestPath))
        {
            throw new InvalidDataException("The save backup manifest is outside Cirra's backup directory or no longer exists.");
        }

        await using var stream = File.OpenRead(fullManifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<SaveBackupManifest>(stream, JsonOptions, cancellationToken)
                       ?? throw new InvalidDataException("The save backup manifest is empty.");
        if (manifest.FormatVersion != 1 || manifest.Entries is null)
        {
            throw new InvalidDataException("The save backup format is not supported.");
        }

        return (manifest, Path.GetDirectoryName(fullManifestPath)!);
    }

    private static string ResolveRestoreDestination(string configuredRoot, string relativePath, bool rootWasFile)
    {
        var safeRelative = NormalizeRelativePath(relativePath);
        if (rootWasFile)
        {
            if (!string.Equals(Path.GetFileName(configuredRoot), Path.GetFileName(safeRelative), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The configured save file no longer matches this backup.");
            }

            return configuredRoot;
        }

        return ResolveUnder(configuredRoot, safeRelative);
    }

    private static string ExpandPath(string value) =>
        Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim()));

    private static string NormalizeRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value))
        {
            throw new InvalidDataException("A save backup contains an invalid relative path.");
        }

        var normalized = value.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        if (normalized.Split(Path.DirectorySeparatorChar).Any(part => part is ".." or "." || string.IsNullOrWhiteSpace(part)))
        {
            throw new InvalidDataException("A save backup contains an unsafe relative path.");
        }

        return normalized;
    }

    private static string ResolveUnder(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root);
        var resolved = Path.GetFullPath(Path.Combine(fullRoot, NormalizeRelativePath(relativePath)));
        if (!IsUnder(fullRoot, resolved))
        {
            throw new InvalidDataException("A save backup path escaped its expected directory.");
        }

        return resolved;
    }

    private static bool IsUnder(string root, string candidate)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate);
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static async Task CopyAtomicAsync(string source, string destination, CancellationToken cancellationToken)
    {
        var temp = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, true))
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                await input.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            File.Move(temp, destination, true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }
}
