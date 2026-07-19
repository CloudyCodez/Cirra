using System.Diagnostics;

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

    public event EventHandler<GameLaunchEndedEventArgs>? Ended;

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
