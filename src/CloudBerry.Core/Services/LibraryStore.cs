using System.Text.Json;
using System.Text.Json.Serialization;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services;

public sealed class LibraryStore
{
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    // Compact for live saves (smaller file, faster serialisation).
    // Indented only for exports/backups where human readability matters.
    // AllowNamedFloatingPointLiterals is required because WindowSettings.Left/Top intentionally
    // default to double.NaN as a "window hasn't been positioned yet" sentinel — without this,
    // System.Text.Json throws on any save/load that still has that sentinel in place (e.g. every
    // save before the window's real position is first captured), crashing the whole persist call
    // silently on the dispatcher thread.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    private static readonly JsonSerializerOptions IndentedSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public LibraryStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CloudBerry",
            "library.json");
        History = new LibraryHistoryService(FilePath);
    }

    public string FilePath { get; }

    public LibraryHistoryService History { get; }

    public string BackupDirectory
    {
        get
        {
            var directoryPath = Path.GetDirectoryName(FilePath);
            return string.IsNullOrWhiteSpace(directoryPath)
                ? "backups"
                : Path.Combine(directoryPath, "backups");
        }
    }

    public async Task<LibrarySnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
        {
            return new LibrarySnapshot();
        }

        try
        {
            await using var stream = File.OpenRead(FilePath);
            return await JsonSerializer.DeserializeAsync<LibrarySnapshot>(stream, SerializerOptions, cancellationToken)
                   ?? new LibrarySnapshot();
        }
        catch (JsonException)
        {
            // A3: Keep startup resilient when library.json is partially written or manually edited into invalid JSON.
            // Aurora 1.2: before giving up and handing the player an empty library, try the most recent
            // backup snapshot first. A corrupt live file with a good backup sitting right next to it
            // should never present as "you lost everything" if we can help it.
            QuarantineCorruptSnapshot();

            var lastKnownGood = await TryLoadMostRecentBackupAsync(cancellationToken);
            if (lastKnownGood is not null)
            {
                return lastKnownGood;
            }

            return new LibrarySnapshot();
        }
    }

    /// <summary>
    /// Attempts to load the newest backup file in <see cref="BackupDirectory"/> as a fallback
    /// when the live library file is corrupt or unreadable. Returns null if no backup exists or
    /// every backup found also fails to parse, so the caller can fall back to an empty snapshot.
    /// </summary>
    private async Task<LibrarySnapshot?> TryLoadMostRecentBackupAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!Directory.Exists(BackupDirectory))
            {
                return null;
            }

            var newestBackup = Directory.GetFiles(BackupDirectory, "library-*.json")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (newestBackup is null)
            {
                return null;
            }

            await using var stream = File.OpenRead(newestBackup);
            return await JsonSerializer.DeserializeAsync<LibrarySnapshot>(stream, SerializerOptions, cancellationToken);
        }
        catch
        {
            // A corrupt backup is no better than a corrupt live file — swallow and let the caller
            // fall back to an empty snapshot rather than throwing during startup.
            return null;
        }
    }

    public async Task SaveAsync(LibrarySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            await SaveCoreAsync(snapshot, cancellationToken);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private async Task SaveCoreAsync(LibrarySnapshot snapshot, CancellationToken cancellationToken)
    {
        // Aurora 1.2 safety guard: a save that would wipe a previously non-empty library down to
        // zero games is far more likely to be a bug (a bad state mutation, a race during load, an
        // exception mid-import leaving Games cleared) than a deliberate user action. Take an extra
        // backup first so a silent wipe is always recoverable, without blocking the save itself.
        if (snapshot.Games.Count == 0 && File.Exists(FilePath))
        {
            try
            {
                var existing = await LoadAsync(cancellationToken);
                if (existing.Games.Count > 0)
                {
                    await CreateBackupAsync(cancellationToken);
                }
            }
            catch
            {
                // Never let the safety-backup attempt block or fail the actual save.
            }
        }

        snapshot.LastUpdatedUtc = DateTime.UtcNow;
        var directoryPath = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // Write to a temp file then atomic-move to avoid corrupt library.json on crash mid-write.
        var tmpPath = FilePath + ".tmp";
        await using (var stream = File.Create(tmpPath))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions, cancellationToken);
        }
        File.Move(tmpPath, FilePath, overwrite: true);
    }

    public async Task<LibraryHistoryEntry?> SaveWithHistoryAsync(
        LibrarySnapshot snapshot,
        LibraryHistoryContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);

        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            var before = File.Exists(FilePath)
                ? await LoadAsync(cancellationToken)
                : new LibrarySnapshot();

            await SaveCoreAsync(snapshot, cancellationToken);
			try
			{
				return await History.RecordAsync(before, snapshot, context, cancellationToken);
			}
			catch (Exception ex)
			{
				// The atomic library commit is authoritative. A secondary history failure must not
				// make callers roll a successfully saved import back only in memory.
				System.Diagnostics.Trace.TraceError(
					"Cirra saved the library, but could not record history: {0}",
					ex);
				return null;
			}
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public async Task<LibrarySnapshot> UndoHistoryEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            var current = await LoadAsync(cancellationToken);
            var historical = await History.LoadBeforeAsync(entryId, cancellationToken);
            var restored = new LibrarySnapshot
            {
                Games = historical.Games,
                Settings = current.Settings,
                Dashboard = current.Dashboard
            };
            await SaveCoreAsync(restored, cancellationToken);
            await History.RecordAsync(
                current,
                restored,
                new LibraryHistoryContext("history.undo", "Undid a library change", $"Restored the state before history entry {entryId}."),
                cancellationToken);
            return restored;
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public async Task<string> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
        {
            await SaveAsync(new LibrarySnapshot(), cancellationToken);
        }

        Directory.CreateDirectory(BackupDirectory);
        var fileName = $"library-{DateTime.Now:yyyyMMdd-HHmmss}.json";
        var backupPath = Path.Combine(BackupDirectory, fileName);

        await using var source = File.OpenRead(FilePath);
        await using var target = File.Create(backupPath);
        await source.CopyToAsync(target, cancellationToken);
        return backupPath;
    }

    public async Task<string> ExportAsync(string destinationFilePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationFilePath))
        {
            throw new ArgumentException("Choose a destination file for the library export.", nameof(destinationFilePath));
        }

        if (!File.Exists(FilePath))
        {
            await SaveAsync(new LibrarySnapshot(), cancellationToken);
        }

        var directoryPath = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // Re-serialize with indentation so exported files are human-readable.
        var snapshot = await LoadAsync(cancellationToken);
        await using var target = File.Create(destinationFilePath);
        await JsonSerializer.SerializeAsync(target, snapshot, IndentedSerializerOptions, cancellationToken);
        return destinationFilePath;
    }

    /// <summary>
    /// Reads a backup or export file (a full <see cref="LibrarySnapshot"/> JSON document) without
    /// touching the live library file, so a restore flow can show the player what they're about
    /// to get back before committing to anything.
    /// </summary>
    public async Task<LibrarySnapshot> RestorePreviewAsync(string sourceFilePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            throw new ArgumentException("Choose a backup or export file to restore from.", nameof(sourceFilePath));
        }
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("That backup file could not be found.", sourceFilePath);
        }

        await using var stream = File.OpenRead(sourceFilePath);
        LibrarySnapshot? snapshot;
        try
        {
            snapshot = await JsonSerializer.DeserializeAsync<LibrarySnapshot>(stream, SerializerOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("That file doesn't look like a valid Cirra library backup.", ex);
        }

        return snapshot ?? throw new InvalidDataException("That backup file was empty or unreadable.");
    }

    /// <summary>
    /// Commits a restore: takes a fresh safety backup of whatever is currently live (so a restore
    /// can never be a one-way trip), then replaces the live library with the snapshot from
    /// <paramref name="sourceFilePath"/>. Callers should have already shown the player a preview
    /// (see <see cref="RestorePreviewAsync"/>) and gotten explicit confirmation before calling this.
    /// </summary>
    public async Task<LibrarySnapshot> RestoreAsync(string sourceFilePath, CancellationToken cancellationToken = default)
    {
        var snapshot = await RestorePreviewAsync(sourceFilePath, cancellationToken);

        if (File.Exists(FilePath))
        {
            await CreateBackupAsync(cancellationToken);
        }

        await SaveAsync(snapshot, cancellationToken);
        return snapshot;
    }

    private void QuarantineCorruptSnapshot()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return;
            }

            var directoryPath = Path.GetDirectoryName(FilePath);
            var fileName = Path.GetFileName(FilePath);
            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var quarantineFileName = $"{fileName}.corrupt.{stamp}.json";

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                File.Move(FilePath, quarantineFileName, overwrite: true);
                return;
            }

            Directory.CreateDirectory(directoryPath);
            var quarantinePath = Path.Combine(directoryPath, quarantineFileName);
            File.Move(FilePath, quarantinePath, overwrite: true);
        }
        catch
        {
            // Swallow quarantine failures; the primary goal is to keep the app booting.
        }
    }
}
