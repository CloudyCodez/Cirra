using CloudBerry.Core.Models;
using CloudBerry.Core.Services;

namespace CloudBerry.Core.Tests;

public sealed class ActivityCenterServiceTests
{
    [Fact]
    public void Operation_ReportsProgressAndCompletes()
    {
        var center = new ActivityCenterService();
        var handle = center.Begin("metadata.batch", "Fill metadata", canCancel: true, retryKey: "metadata.fill");

        handle.Report(0.45, "9 of 20 games");
        var running = Assert.Single(center.Snapshot());
        Assert.Equal(0.45, running.Progress);
        Assert.Equal("45%", running.ProgressLabel);

        handle.Complete("Updated 20 games.");
        var complete = Assert.Single(center.Snapshot());
        Assert.Equal(ActivityOperationStatus.Succeeded, complete.Status);
        Assert.Equal(1, complete.Progress);
        Assert.False(complete.CanCancel);
        Assert.NotNull(complete.CompletedAtUtc);
    }

    [Fact]
    public void Cancel_RequestsCancellationButWaitsForWorkerAcknowledgement()
    {
        var center = new ActivityCenterService();
        var handle = center.Begin("scan", "Scan folder", canCancel: true);

        var cancelled = center.Cancel(handle.Id);

        Assert.True(cancelled);
        Assert.True(handle.CancellationToken.IsCancellationRequested);
        var operation = Assert.Single(center.Snapshot());
        Assert.Equal(ActivityOperationStatus.Cancelling, operation.Status);
        Assert.Equal("Stopping", operation.StatusLabel);
        Assert.True(operation.IsRunning);
        Assert.False(operation.CanCancel);
        Assert.Null(operation.CompletedAtUtc);
        Assert.False(center.Cancel(handle.Id));

        handle.Report(0.5, "Late progress callback");
        Assert.Contains("Cancellation requested", Assert.Single(center.Snapshot()).Message);
        handle.Cancelled("Stopped after cleanup.");
        var acknowledged = Assert.Single(center.Snapshot());
        Assert.Equal(ActivityOperationStatus.Cancelled, acknowledged.Status);
        Assert.False(acknowledged.IsRunning);
        Assert.NotNull(acknowledged.CompletedAtUtc);
    }

    [Fact]
    public void NonCancellableOperation_RejectsCancellation()
    {
        var center = new ActivityCenterService();
        var handle = center.Begin("backup", "Create backup");

        Assert.False(center.Cancel(handle.Id));
        Assert.False(handle.CancellationToken.IsCancellationRequested);
        Assert.Equal(ActivityOperationStatus.Running, Assert.Single(center.Snapshot()).Status);
    }

    [Fact]
    public async Task CancellationRemainsActiveThroughoutAsyncCleanup()
    {
        var center = new ActivityCenterService();
        var handle = center.Begin("download", "Download guide", canCancel: true);
        var cleanupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool cleaned = false;
        var worker = Task.Run(async () =>
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, handle.CancellationToken); }
            catch (OperationCanceledException)
            {
                cleanupStarted.SetResult();
                await releaseCleanup.Task;
                cleaned = true;
                handle.Cancelled();
            }
        });
        try
        {
            Assert.True(center.Cancel(handle.Id));
            await cleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(cleaned);
            Assert.True(Assert.Single(center.Snapshot()).IsRunning);
            Assert.Null(Assert.Single(center.Snapshot()).CompletedAtUtc);
        }
        finally { releaseCleanup.TrySetResult(); await worker.WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.True(cleaned);
        Assert.Equal(ActivityOperationStatus.Cancelled, Assert.Single(center.Snapshot()).Status);
    }

    [Fact]
    public void SynchronousAcknowledgementDoesNotDisposeSourceInsideCallbacks()
    {
        var center = new ActivityCenterService();
        var handle = center.Begin("scan", "Scan", canCancel: true);
        bool remainingCallbackRan = false;
        using var remaining = handle.CancellationToken.Register(() =>
        {
            Assert.True(handle.CancellationToken.WaitHandle.WaitOne(0));
            remainingCallbackRan = true;
        });
        using var acknowledge = handle.CancellationToken.Register(() => handle.Cancelled());
        center.Cancel(handle.Id);
        Assert.True(remainingCallbackRan);
        Assert.Equal(ActivityOperationStatus.Cancelled, Assert.Single(center.Snapshot()).Status);
    }

    [Fact]
    public void CancellationDuringBeginNotificationStillReturnsUsableToken()
    {
        var center = new ActivityCenterService();
        center.Changed += operation =>
        {
            if (operation.Status == ActivityOperationStatus.Running) center.Cancel(operation.Id);
        };
        var handle = center.Begin("scan", "Scan", canCancel: true);
        Assert.True(handle.CancellationToken.IsCancellationRequested);
        handle.Cancelled();
    }

    [Fact]
    public async Task CompletionOnAnotherThreadDoesNotDisposeAnActiveCancellationCallback()
    {
        var center = new ActivityCenterService();
        var handle = center.Begin("backup", "Backup", canCancel: true);
        using var callbackStarted = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        bool callbackFinished = false;
        using var registration = handle.CancellationToken.Register(() =>
        {
            callbackStarted.Set();
            if (!releaseCallback.Wait(TimeSpan.FromSeconds(5))) return;
            callbackFinished = handle.CancellationToken.WaitHandle.WaitOne(0);
        });
        var cancelling = Task.Run(() => center.Cancel(handle.Id));
        try
        {
            Assert.True(callbackStarted.Wait(TimeSpan.FromSeconds(5)));
            handle.Complete("Committed before cancellation.");
        }
        finally { releaseCallback.Set(); await cancelling.WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.True(callbackFinished);
        Assert.Equal(ActivityOperationStatus.Succeeded, Assert.Single(center.Snapshot()).Status);
    }

    [Fact]
    public void CancellationCallbackFailureDoesNotFalselyCompleteWorker()
    {
        var center = new ActivityCenterService();
        var handle = center.Begin("scan", "Scan", canCancel: true);
        using var callback = handle.CancellationToken.Register(() => throw new InvalidOperationException("Injected failure"));
        Assert.True(center.Cancel(handle.Id));
        Assert.Equal(ActivityOperationStatus.Cancelling, Assert.Single(center.Snapshot()).Status);
        handle.Cancelled();
        Assert.Equal(ActivityOperationStatus.Cancelled, Assert.Single(center.Snapshot()).Status);
    }

    [Fact]
    public void WorkerThatAlreadyCommittedCanReportSuccessAfterCancellationRequest()
    {
        var center = new ActivityCenterService();
        var handle = center.Begin("backup", "Backup", canCancel: true);
        center.Cancel(handle.Id);
        handle.Complete("Backup was committed before cancellation was observed.");
        handle.Cancelled();
        handle.Report(0.1, "Late update");
        Assert.Equal(ActivityOperationStatus.Succeeded, Assert.Single(center.Snapshot()).Status);
        Assert.False(center.Cancel(handle.Id));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteProgressBecomesIndeterminate(double progress)
    {
        var center = new ActivityCenterService();
        var handle = center.Begin("scan", "Scan");
        handle.Report(progress, "Searching");
        Assert.Null(Assert.Single(center.Snapshot()).Progress);
        handle.Complete("Done");
    }

    [Fact]
    public void RetentionNeverEvictsStoppingOperations()
    {
        var center = new ActivityCenterService(retentionCount: 10);
        var stopping = center.Begin("scan", "Scan", canCancel: true);
        center.Cancel(stopping.Id);
        for (int i = 0; i < 15; i++) center.Begin("test", "Completed").Complete("Done");
        Assert.Equal(11, center.Snapshot().Count);
        Assert.Contains(center.Snapshot(), operation => operation.Id == stopping.Id && operation.IsRunning);
        stopping.Cancelled();
        Assert.Equal(10, center.Snapshot().Count);
    }
}
