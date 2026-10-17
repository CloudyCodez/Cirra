namespace CloudBerry.Core.Models;

public sealed record SaveArtifact(
    int RootIndex,
    string RootPath,
    string AbsolutePath,
    string RelativePath,
    long SizeBytes,
    DateTime LastWriteUtc,
    string Sha256);

public sealed class SaveDiscoveryResult
{
    public List<SaveArtifact> Artifacts { get; init; } = [];

    public List<string> Warnings { get; init; } = [];

    public long TotalBytes => Artifacts.Sum(item => item.SizeBytes);
}

public sealed class SaveBackupManifest
{
    public int FormatVersion { get; set; } = 1;

    public Guid GameId { get; set; }

    public string GameTitle { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public List<string> SourceRoots { get; set; } = [];

    public List<SaveBackupEntry> Entries { get; set; } = [];
}

public sealed class SaveBackupEntry
{
    public int RootIndex { get; set; }

    public bool RootWasFile { get; set; }

    public string RelativePath { get; set; } = string.Empty;

    public string BackupRelativePath { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTime LastWriteUtc { get; set; }

    public string Sha256 { get; set; } = string.Empty;
}

public sealed record SaveBackupResult(string ManifestPath, int FileCount, long TotalBytes);

public sealed record SaveBackupSummary(string ManifestPath, DateTime CreatedUtc, int FileCount, long TotalBytes);

public sealed record SaveVerificationResult(bool IsValid, int VerifiedFiles, IReadOnlyList<string> Errors);

public sealed record SaveRestoreResult(int RestoredFiles, string RollbackDirectory);
