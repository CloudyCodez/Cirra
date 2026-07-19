namespace CloudBerry.Core.Models;

public sealed class CompanionToolDescriptor
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string ExecutablePath { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public bool IsAvailable { get; set; }

    public string StatusLabel => IsAvailable
        ? string.IsNullOrWhiteSpace(Version) ? "Ready" : Version
        : "Not found";
}

public sealed class CompanionToolRunResult
{
    public int ExitCode { get; set; }

    public string StandardOutput { get; set; } = string.Empty;

    public string StandardError { get; set; } = string.Empty;

    public bool TimedOut { get; set; }

    public bool Succeeded => !TimedOut && ExitCode == 0;
}
