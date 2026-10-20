namespace CloudBerry.Core.Models;

public sealed class GameTitle
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string SortName { get; set; } = string.Empty;

    public Guid? LegacyGameId { get; set; }
}

public sealed class GameRelease
{
    public Guid Id { get; set; }

    public Guid GameTitleId { get; set; }

    public string Platform { get; set; } = "Unknown";

    public string Region { get; set; } = string.Empty;

    public string Edition { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public DateTime? ReleasedOn { get; set; }
}

public sealed class GameInstallation
{
    public Guid Id { get; set; }

    public Guid GameReleaseId { get; set; }

    public string SourceKind { get; set; } = "Manual";

    public bool IsAvailable { get; set; } = true;

    public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;
}

public enum LaunchProfileKind
{
    Executable,
    Uri,
    Emulator,
    Script,
    Unknown
}

public sealed class LaunchProfile
{
    public Guid Id { get; set; }

    public Guid GameInstallationId { get; set; }

    public string Name { get; set; } = "Play";

    public LaunchProfileKind Kind { get; set; }

    public string TargetPath { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public string EmulatorPath { get; set; } = string.Empty;

    public string CorePath { get; set; } = string.Empty;
}

public enum ArtifactAvailability
{
    Available,
    Missing,
    Offline,
    Unknown
}

public sealed class LibraryArtifact
{
    public Guid Id { get; set; }

    public string CanonicalKey { get; set; } = string.Empty;

    public string OriginalPath { get; set; } = string.Empty;

    public string CanonicalPath { get; set; } = string.Empty;

    public string SourceKind { get; set; } = string.Empty;

    public string SourceRoot { get; set; } = string.Empty;

    public long? Length { get; set; }

    public DateTimeOffset? LastWriteUtc { get; set; }

    public string QuickHash { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;

    public string ContainerGroupKey { get; set; } = string.Empty;

    public ArtifactAvailability Availability { get; set; } = ArtifactAvailability.Unknown;

    public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;
}
