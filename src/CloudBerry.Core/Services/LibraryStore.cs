using System.Text.Json;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services;

public sealed class LibraryStore
{
    // Compact for live saves (smaller file, faster serialisation).
    // Indented only for exports/backups where human readability matters.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions IndentedSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public LibraryStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CloudBerry",
            "library.json");
    }

    public string FilePath { get; }

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
            QuarantineCorruptSnapshot();
            return new LibrarySnapshot();
        }
    }

    public async Task SaveAsync(LibrarySnapshot snapshot, CancellationToken cancellationToken = default)
    {
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
