namespace CloudBerry.Core.Models;

public enum LaunchOperationState
{
    Idle,
    Checking,
    RepairRequired,
    Preparing,
    Starting,
    Tracking,
    Returning,
    Completed,
    Failed,
    Cancelled
}

public sealed record LaunchOperationUpdate(
    Guid OperationId,
    Guid GameId,
    string GameTitle,
    LaunchOperationState State,
    string Message,
    DateTimeOffset UpdatedAtUtc);

public sealed record LaunchOrchestrationResult(
    Guid OperationId,
    LaunchOperationState State,
    LaunchPreflightResult Preflight,
    CloudBerry.Core.Services.GameLaunchSession? Session,
    Exception? Error)
{
    public bool Started => State is LaunchOperationState.Tracking or LaunchOperationState.Completed;
}
