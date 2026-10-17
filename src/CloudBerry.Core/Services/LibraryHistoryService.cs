using System.Text.Json;
using System.Text.Json.Serialization;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services;

public sealed class LibraryHistoryService
{
    private const int DefaultRetentionCount = 60;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public LibraryHistoryService(string libraryFilePath, int retentionCount = DefaultRetentionCount)
    {
        if (string.IsNullOrWhiteSpace(libraryFilePath))
        {
            throw new ArgumentException("A library file path is required.", nameof(libraryFilePath));
        }

        var libraryDirectory = Path.GetDirectoryName(libraryFilePath);
        HistoryDirectory = Path.Combine(
            string.IsNullOrWhiteSpace(libraryDirectory) ? Environment.CurrentDirectory : libraryDirectory,
            "history");
        RetentionCount = Math.Clamp(retentionCount, 5, 500);
    }

    public string HistoryDirectory { get; }

    public int RetentionCount { get; }

    private string ManifestPath => Path.Combine(HistoryDirectory, "timeline.json");

    public async Task<LibraryHistoryEntry?> RecordAsync(
        LibrarySnapshot before,
        LibrarySnapshot after,
        LibraryHistoryContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(context);

        var difference = CalculateDifference(before, after);
        if (!difference.HasChanges)
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(HistoryDirectory);
            var entryId = Guid.NewGuid();
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
            var beforeFile = $"{stamp}-{entryId:N}-before.json";
            var afterFile = $"{stamp}-{entryId:N}-after.json";

            await WriteSnapshotAsync(Path.Combine(HistoryDirectory, beforeFile), before, cancellationToken);
            await WriteSnapshotAsync(Path.Combine(HistoryDirectory, afterFile), after, cancellationToken);

            var entry = new LibraryHistoryEntry
            {
                Id = entryId,
                TimestampUtc = DateTime.UtcNow,
                Kind = string.IsNullOrWhiteSpace(context.Kind) ? "library.change" : context.Kind.Trim(),
                Title = string.IsNullOrWhiteSpace(context.Title) ? "Library changed" : context.Title.Trim(),
                Detail = context.Detail?.Trim() ?? string.Empty,
                AddedCount = difference.Added,
                RemovedCount = difference.Removed,
                ModifiedCount = difference.Modified,
                BeforeSnapshotFile = beforeFile,
                AfterSnapshotFile = afterFile
            };

            var timeline = await ReadTimelineUnsafeAsync(cancellationToken);
            timeline.Insert(0, entry);
            await TrimTimelineUnsafeAsync(timeline, cancellationToken);
            await WriteTimelineUnsafeAsync(timeline, cancellationToken);
            return entry;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<LibraryHistoryEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadTimelineUnsafeAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LibrarySnapshot> LoadBeforeAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var timeline = await ReadTimelineUnsafeAsync(cancellationToken);
            var entry = timeline.FirstOrDefault(candidate => candidate.Id == entryId)
                ?? throw new KeyNotFoundException("That Cirra history entry no longer exists.");
            return await ReadSnapshotAsync(entry.BeforeSnapshotFile, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LibrarySnapshot> LoadAfterAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var timeline = await ReadTimelineUnsafeAsync(cancellationToken);
            var entry = timeline.FirstOrDefault(candidate => candidate.Id == entryId)
                ?? throw new KeyNotFoundException("That Cirra history entry no longer exists.");
            return await ReadSnapshotAsync(entry.AfterSnapshotFile, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<LibrarySnapshot> ReadSnapshotAsync(string relativePath, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(Path.Combine(HistoryDirectory, relativePath));
        var historyRoot = Path.GetFullPath(HistoryDirectory) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(historyRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            throw new FileNotFoundException("The snapshot for that Cirra history entry is unavailable.", fullPath);
        }

        await using var stream = File.OpenRead(fullPath);
        return await JsonSerializer.DeserializeAsync<LibrarySnapshot>(stream, SerializerOptions, cancellationToken)
            ?? throw new InvalidDataException("The selected Cirra history snapshot is empty.");
    }

    private async Task<List<LibraryHistoryEntry>> ReadTimelineUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(ManifestPath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(ManifestPath);
            return await JsonSerializer.DeserializeAsync<List<LibraryHistoryEntry>>(stream, SerializerOptions, cancellationToken) ?? [];
        }
        catch (JsonException)
        {
            var quarantinePath = ManifestPath + $".corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}";
            File.Move(ManifestPath, quarantinePath, overwrite: true);
            return [];
        }
    }

    private async Task WriteTimelineUnsafeAsync(List<LibraryHistoryEntry> timeline, CancellationToken cancellationToken)
    {
        var temporaryPath = ManifestPath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, timeline, SerializerOptions, cancellationToken);
        }
        File.Move(temporaryPath, ManifestPath, overwrite: true);
    }

    private static async Task WriteSnapshotAsync(string path, LibrarySnapshot snapshot, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions, cancellationToken);
    }

    private async Task TrimTimelineUnsafeAsync(List<LibraryHistoryEntry> timeline, CancellationToken cancellationToken)
    {
        foreach (var expired in timeline.Skip(RetentionCount).ToList())
        {
            DeleteSnapshotIfPresent(expired.BeforeSnapshotFile);
            DeleteSnapshotIfPresent(expired.AfterSnapshotFile);
        }
        if (timeline.Count > RetentionCount)
        {
            timeline.RemoveRange(RetentionCount, timeline.Count - RetentionCount);
        }
        await Task.CompletedTask;
    }

    private void DeleteSnapshotIfPresent(string relativePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(HistoryDirectory, relativePath));
            var historyRoot = Path.GetFullPath(HistoryDirectory) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(historyRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static LibraryDifference CalculateDifference(LibrarySnapshot before, LibrarySnapshot after)
    {
        var beforeGames = before.Games.ToDictionary(game => game.Id);
        var afterGames = after.Games.ToDictionary(game => game.Id);
        var added = afterGames.Keys.Count(id => !beforeGames.ContainsKey(id));
        var removed = beforeGames.Keys.Count(id => !afterGames.ContainsKey(id));
        var modified = beforeGames.Keys.Intersect(afterGames.Keys)
            .Count(id => !GamesAreEquivalent(beforeGames[id], afterGames[id]));
        return new LibraryDifference(added, removed, modified);
    }

    private static bool GamesAreEquivalent(GameRecord left, GameRecord right)
    {
        return JsonSerializer.Serialize(left, SerializerOptions) == JsonSerializer.Serialize(right, SerializerOptions);
    }

    private sealed record LibraryDifference(int Added, int Removed, int Modified)
    {
        public bool HasChanges => Added > 0 || Removed > 0 || Modified > 0;
    }
}
