namespace CloudBerry.Core.Models;

public enum LaunchReadiness
{
    Ready,
    Warning,
    Blocked
}

public sealed record LaunchPreflightCheck(
    string Key,
    string Label,
    LaunchReadiness Readiness,
    string Detail,
    string ActionLabel = "");

public sealed class LaunchPreflightResult
{
    public required Guid GameId { get; init; }

    public required string GameTitle { get; init; }

    public required IReadOnlyList<LaunchPreflightCheck> Checks { get; init; }

    public LaunchReadiness Readiness => Checks.Any(check => check.Readiness == LaunchReadiness.Blocked)
        ? LaunchReadiness.Blocked
        : Checks.Any(check => check.Readiness == LaunchReadiness.Warning)
            ? LaunchReadiness.Warning
            : LaunchReadiness.Ready;

    public bool CanLaunch => Readiness != LaunchReadiness.Blocked;

    public string Summary => Readiness switch
    {
        LaunchReadiness.Ready => "Ready to launch",
        LaunchReadiness.Warning => "Ready with a warning",
        _ => Checks.First(check => check.Readiness == LaunchReadiness.Blocked).Detail
    };
}
