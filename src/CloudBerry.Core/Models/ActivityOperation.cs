namespace CloudBerry.Core.Models;

public enum ActivityOperationStatus
{
    Running,
    Succeeded,
    Failed,
    Cancelled,
    Cancelling
}

public sealed record ActivityOperation(
    Guid Id,
    string Kind,
    string Title,
    ActivityOperationStatus Status,
    double? Progress,
    string Message,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    bool CanCancel,
    string RetryKey = "")
{
    public bool IsRunning => Status is ActivityOperationStatus.Running or ActivityOperationStatus.Cancelling;

    public bool IsIndeterminate => IsRunning && !Progress.HasValue;

    public string ProgressLabel => Progress.HasValue ? $"{Math.Round(Progress.Value * 100):0}%" : string.Empty;

    public string StatusLabel => Status switch
    {
        ActivityOperationStatus.Running => "In progress",
        ActivityOperationStatus.Cancelling => "Stopping",
        ActivityOperationStatus.Succeeded => "Complete",
        ActivityOperationStatus.Failed => "Needs attention",
        _ => "Cancelled"
    };
}
