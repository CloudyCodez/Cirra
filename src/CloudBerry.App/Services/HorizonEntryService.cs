using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace CloudBerry.App.Services;

internal enum HorizonEntryState { Idle, Preparing, Presenting, Ready, Cancelled, Failed }

internal sealed record HorizonEntryResult(
    HorizonEntryState State,
    Window? Destination,
    Exception? Error,
    TimeSpan? LoadingFirstFrame,
    TimeSpan? DashboardFirstFrame,
    TimeSpan? Ready,
    TimeSpan Total);

internal sealed class HorizonEntryService
{
    private CancellationTokenSource? _cancellation;

    public HorizonEntryState State { get; private set; }
    public Task<HorizonEntryResult>? CurrentEntry { get; private set; }
    public bool IsEntering => CurrentEntry is { IsCompleted: false };
    public event Action<HorizonEntryState>? StateChanged;

    public Task<HorizonEntryResult> EnterAsync(
        Window desktop,
        Func<Window> createLoading,
        Func<Window> createDestination,
        Func<Window, CancellationToken, Task> dismissLoading)
    {
        desktop.Dispatcher.VerifyAccess();
        if (IsEntering) return CurrentEntry!;
        CurrentEntry = EnterCoreAsync(desktop, createLoading, createDestination, dismissLoading);
        return CurrentEntry;
    }

    public void Cancel() => _cancellation?.Cancel();

    private async Task<HorizonEntryResult> EnterCoreAsync(
        Window desktop, Func<Window> createLoading, Func<Window> createDestination,
        Func<Window, CancellationToken, Task> dismissLoading)
    {
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        var token = cancellation.Token;
        var clock = Stopwatch.StartNew();
        Window? loading = null;
        Window? destination = null;
        bool desktopClosing = false;
        bool desktopClosed = false;
        bool desktopWasVisible = desktop.IsVisible;
        bool loadingClosed = false;
        bool destinationClosed = false;
        TimeSpan? loadingFrame = null, dashboardFrame = null, ready = null;
        Exception? failure = null;
        var outcome = HorizonEntryState.Cancelled;

        void DesktopClosing(object? sender, CancelEventArgs e) { desktopClosing = true; cancellation.Cancel(); }
        void DesktopClosed(object? sender, EventArgs e) { desktopClosed = true; cancellation.Cancel(); }
        void LoadingClosed(object? sender, EventArgs e) { loadingClosed = true; cancellation.Cancel(); }
        void DestinationClosed(object? sender, EventArgs e) { destinationClosed = true; cancellation.Cancel(); }
        desktop.Closing += DesktopClosing;
        desktop.Closed += DesktopClosed;
        try
        {
            // Publish CurrentEntry before factories or state observers can request another entry.
            await Task.Yield();
            token.ThrowIfCancellationRequested();
            SetState(HorizonEntryState.Preparing);
            token.ThrowIfCancellationRequested();
            if (!desktopWasVisible) throw new InvalidOperationException("Show Cirra before opening Horizon.");
            loading = createLoading();
            loading.Closed += LoadingClosed;
            loading.Owner = desktop;
            await WindowPresentation.ShowFirstFrameAsync(loading, cancellationToken: token);
            loadingFrame = clock.Elapsed;

            SetState(HorizonEntryState.Presenting);
            token.ThrowIfCancellationRequested();
            destination = createDestination();
            destination.Closed += DestinationClosed;
            token.ThrowIfCancellationRequested();
            destination.Owner = desktop;
            await WindowPresentation.ShowFirstFrameAsync(destination, loading, token);
            dashboardFrame = clock.Elapsed;
            await dismissLoading(loading, token);
            token.ThrowIfCancellationRequested();
            if (!destination.IsVisible) throw new OperationCanceledException(token);

            desktop.Hide();
            token.ThrowIfCancellationRequested();
            destination.Activate();
            ready = clock.Elapsed;
            outcome = HorizonEntryState.Ready;
        }
        catch (OperationCanceledException)
        {
            outcome = HorizonEntryState.Cancelled;
        }
        catch (Exception ex)
        {
            failure = ex;
            outcome = HorizonEntryState.Failed;
        }
        finally
        {
            desktop.Closing -= DesktopClosing;
            desktop.Closed -= DesktopClosed;
            if (loading != null) loading.Closed -= LoadingClosed;
            if (destination != null) destination.Closed -= DestinationClosed;
            try
            {
                void Cleanup(Action action)
                {
                    try { action(); }
                    catch (Exception ex)
                    {
                        failure = failure == null ? ex : new AggregateException(failure, ex);
                        outcome = HorizonEntryState.Failed;
                    }
                }

                if (loading != null && !loadingClosed) Cleanup(loading.Close);
                if (outcome != HorizonEntryState.Ready && destination != null && !destinationClosed) Cleanup(destination.Close);
                if (outcome != HorizonEntryState.Ready && desktopWasVisible && !desktopClosing && !desktopClosed &&
                    !desktop.Dispatcher.HasShutdownStarted)
                {
                    Cleanup(() => { desktop.Show(); desktop.Activate(); });
                }
            }
            finally { _cancellation = null; }
        }

        SetState(outcome);
        return new HorizonEntryResult(outcome, outcome == HorizonEntryState.Ready ? destination : null,
            failure, loadingFrame, dashboardFrame, ready, clock.Elapsed);
    }

    private void SetState(HorizonEntryState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }
}
