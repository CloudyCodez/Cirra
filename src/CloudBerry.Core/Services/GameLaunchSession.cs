using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CloudBerry.Core.Services;

public sealed class GameLaunchSession : IDisposable
{
    private readonly Process? _process;
    private bool _disposed;

    internal GameLaunchSession(Process? process, DateTime startedAtLocal)
    {
        _process = process;
        StartedAtLocal = startedAtLocal;

        if (_process is not null)
        {
            _process.EnableRaisingEvents = true;
            _process.Exited += OnProcessExited;

            if (_process.HasExited)
            {
                OnProcessExited(_process, EventArgs.Empty);
            }
        }
    }

    public DateTime StartedAtLocal { get; }

    public bool IsTrackingAvailable => _process is not null;

    public int? ProcessId => _process?.Id;

    /// <summary>
    /// Polls for this session's main window handle, refreshing the underlying <see cref="Process"/>
    /// snapshot each attempt since a just-started process (especially an SDL/Qt-based emulator that
    /// creates its window well after the process object exists) often reports a stale or zero handle
    /// immediately after <see cref="Process.Start(ProcessStartInfo)"/> returns. Used by the embedded
    /// play window-reparenting flow to know when (and whether) there's actually a window to attach to —
    /// returns <see cref="IntPtr.Zero"/> on timeout, on an already-exited process, or when no process
    /// is being tracked at all, so callers can treat "couldn't find a window" as a normal, safe outcome
    /// rather than an error.
    /// </summary>
    public async Task<IntPtr> WaitForMainWindowHandleAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (_process is null || _disposed)
        {
            return IntPtr.Zero;
        }

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (cancellationToken.IsCancellationRequested || _disposed)
            {
                return IntPtr.Zero;
            }

            try
            {
                _process.Refresh();
                if (_process.HasExited)
                {
                    return IntPtr.Zero;
                }

                var handle = _process.MainWindowHandle;
                if (handle != IntPtr.Zero)
                {
                    return handle;
                }
            }
            catch (InvalidOperationException)
            {
                // Process exited between the HasExited check and reading MainWindowHandle — treat like any other "no window" outcome.
                return IntPtr.Zero;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return IntPtr.Zero;
            }
        }

        return IntPtr.Zero;
    }

    public event EventHandler<GameLaunchEndedEventArgs>? Ended;

    /// <summary>
    /// Asks the tracked process to close (WM_CLOSE via <see cref="Process.CloseMainWindow"/>, letting
    /// it save state/settings normally), then force-kills it if it hasn't exited within
    /// <paramref name="gracePeriod"/>. Used when the embedded play window is closed while the game
    /// is still running — closing that window should behave like closing any other game window, not
    /// leave a headless process running with no visible way back to it. Safe to call multiple times
    /// or on an already-exited/disposed session; never throws.
    /// </summary>
    public async Task RequestExitAsync(TimeSpan gracePeriod, CancellationToken cancellationToken = default)
    {
        if (_process is null || _disposed)
        {
            return;
        }

        try
        {
            _process.Refresh();
            if (_process.HasExited)
            {
                return;
            }

            _process.CloseMainWindow();

            var deadline = DateTime.UtcNow + gracePeriod;
            while (DateTime.UtcNow < deadline)
            {
                _process.Refresh();
                if (_process.HasExited)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
            }

            _process.Refresh();
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // Best-effort shutdown — the process may have already exited on its own between checks,
            // or refuse the close for reasons outside CloudBerry's control. Either way, this must
            // never throw out of a window-closing code path.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_process is not null)
        {
            _process.Exited -= OnProcessExited;
            _process.Dispose();
        }

        _disposed = true;
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        var duration = DateTime.Now - StartedAtLocal;
        Ended?.Invoke(this, new GameLaunchEndedEventArgs(duration));
        Dispose();
    }
}

public sealed class GameLaunchEndedEventArgs(TimeSpan duration) : EventArgs
{
    public TimeSpan Duration { get; } = duration;
}
