namespace CloudBerry.App.Models;

public sealed class StartupProgressUpdate
{
    public double ProgressRatio { get; init; }

    public string StatusText { get; init; } = string.Empty;

    public string DetailText { get; init; } = string.Empty;
}
