using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services;

public sealed class ActivityCenterService
{
    private readonly object _sync = new();
    private readonly List<ActivityOperation> _operations = [];
    private readonly Dictionary<Guid, OperationCancellation> _cancellationSources = [];

    private sealed class OperationCancellation
    {
        public CancellationTokenSource Source { get; } = new();
        public bool IsSignalling { get; set; }
        public bool IsFinished { get; set; }
    }

    public ActivityCenterService(int retentionCount = 40)
    {
        RetentionCount = Math.Clamp(retentionCount, 10, 200);
    }

    public int RetentionCount { get; }

    public event Action<ActivityOperation>? Changed;

    public ActivityOperationHandle Begin(
        string kind,
        string title,
        bool canCancel = false,
        string retryKey = "")
    {
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("An operation kind is required.", nameof(kind));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("An operation title is required.", nameof(title));

        var operation = new ActivityOperation(
            Guid.NewGuid(),
            kind.Trim(),
            title.Trim(),
            ActivityOperationStatus.Running,
            null,
            "Starting...",
            DateTime.UtcNow,
            null,
            canCancel,
            retryKey.Trim());
        var cancellation = new OperationCancellation();
        var token = cancellation.Source.Token;

        lock (_sync)
        {
            _operations.Insert(0, operation);
            _cancellationSources[operation.Id] = cancellation;
            TrimCompletedUnsafe();
        }
        RaiseChanged(operation);
        return new ActivityOperationHandle(this, operation.Id, token);
    }

    public IReadOnlyList<ActivityOperation> Snapshot()
    {
        lock (_sync)
        {
            return _operations.ToList();
        }
    }

    public bool Cancel(Guid operationId)
    {
        OperationCancellation cancellation;
        ActivityOperation? changed;
        lock (_sync)
        {
            var index = _operations.FindIndex(operation => operation.Id == operationId);
            if (index < 0 || !_operations[index].IsRunning || !_operations[index].CanCancel)
            {
                return false;
            }

            cancellation = _cancellationSources[operationId];
            cancellation.IsSignalling = true;
            changed = _operations[index] with
            {
                Status = ActivityOperationStatus.Cancelling,
                Message = "Cancellation requested. Waiting for the operation to stop and clean up.",
                CanCancel = false
            };
            _operations[index] = changed;
        }

        try
        {
            cancellation.Source.Cancel();
        }
        catch (AggregateException ex)
        {
            // All callbacks were attempted. Only the worker can acknowledge that it stopped.
            System.Diagnostics.Trace.TraceError("Activity cancellation callback failed: {0}", ex);
        }
        finally
        {
            bool dispose;
            lock (_sync)
            {
                cancellation.IsSignalling = false;
                dispose = cancellation.IsFinished;
                changed = _operations.FirstOrDefault(operation => operation.Id == operationId);
            }
            if (dispose) cancellation.Source.Dispose();
        }
        RaiseChanged(changed);
        return true;
    }

    internal void Report(Guid operationId, double? progress, string message)
    {
        ActivityOperation? changed = null;
        lock (_sync)
        {
            var index = _operations.FindIndex(operation => operation.Id == operationId);
            if (index < 0 || !_operations[index].IsRunning) return;

            changed = _operations[index] with
            {
                Progress = progress.HasValue && double.IsFinite(progress.Value) ? Math.Clamp(progress.Value, 0, 1) : null,
                Message = _operations[index].Status == ActivityOperationStatus.Cancelling
                    ? _operations[index].Message
                    : message?.Trim() ?? string.Empty
            };
            _operations[index] = changed;
        }
        RaiseChanged(changed);
    }

    internal void Finish(Guid operationId, ActivityOperationStatus status, string message)
    {
        ActivityOperation? changed = null;
        CancellationTokenSource? source = null;
        lock (_sync)
        {
            var index = _operations.FindIndex(operation => operation.Id == operationId);
            if (index < 0 || !_operations[index].IsRunning) return;

            changed = _operations[index] with
            {
                Status = status,
                Progress = status == ActivityOperationStatus.Succeeded ? 1 : _operations[index].Progress,
                Message = message?.Trim() ?? string.Empty,
                CompletedAtUtc = DateTime.UtcNow,
                CanCancel = false
            };
            _operations[index] = changed;
            if (_cancellationSources.Remove(operationId, out var cancellation))
            {
                // Completion can run inside a cancellation callback. Do not dispose its
                // source until signalling (including the remaining callbacks) has returned.
                cancellation.IsFinished = true;
                if (!cancellation.IsSignalling) source = cancellation.Source;
                TrimCompletedUnsafe();
            }
        }
        source?.Dispose();
        RaiseChanged(changed);
    }

    private void TrimCompletedUnsafe()
    {
        var completed = _operations.Where(operation => !operation.IsRunning).Skip(RetentionCount).ToList();
        foreach (var expired in completed)
        {
            _operations.Remove(expired);
        }
    }

    private void RaiseChanged(ActivityOperation? operation)
    {
        if (operation != null) Changed?.Invoke(operation);
    }
}

public sealed class ActivityOperationHandle
{
    private readonly ActivityCenterService _owner;

    internal ActivityOperationHandle(ActivityCenterService owner, Guid id, CancellationToken cancellationToken)
    {
        _owner = owner;
        Id = id;
        CancellationToken = cancellationToken;
    }

    public Guid Id { get; }

    public CancellationToken CancellationToken { get; }

    public void Report(double? progress, string message) => _owner.Report(Id, progress, message);

    public void Complete(string message) => _owner.Finish(Id, ActivityOperationStatus.Succeeded, message);

    public void Fail(string message) => _owner.Finish(Id, ActivityOperationStatus.Failed, message);

    public void Cancelled(string message = "Cancelled.") => _owner.Finish(Id, ActivityOperationStatus.Cancelled, message);
}
