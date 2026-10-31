using System.Diagnostics;

namespace CloudBerry.Core.Services;

/// <summary>
/// Background poller that watches for library games being launched outside CloudBerry — double-
/// clicked directly, started from Steam's own UI, opened from a desktop shortcut, etc. — so playtime
/// tracking isn't limited to games launched through CloudBerry's own Play button.
///
/// Matching is by exact resolved executable path only (a running process's <c>MainModule.FileName</c>
/// against a game's <see cref="GameLauncher.TryResolveLaunchTarget"/> result), never by process name
/// alone — process names collide constantly ("game.exe" is not a rare filename), and a false match
/// would silently credit playtime to the wrong library entry. This means ROM/emulator games and
/// Steam-protocol launches (steam://rungameid/...) aren't detectable this way: the running process is
/// the emulator or Steam itself, not something uniquely identifying which game is loaded. That's a
/// real, known limitation, not an oversight — see the caller for how it's scoped around.
///
/// Runs on a plain <see cref="System.Threading.Timer"/> (thread-pool callback, not a UI-thread timer)
/// since this has no UI dependency and Core has no WPF reference; the caller is responsible for
/// marshaling <see cref="GameDetected"/> handling onto the UI thread before touching any WPF-bound
/// state, since <see cref="Process.GetProcesses"/> enumeration always happens off-thread here.
/// </summary>
public sealed class ExternalGameProcessMonitor : IDisposable
{
    private readonly Func<IReadOnlyList<(Guid GameId, string ResolvedExecutablePath)>> _getWatchTargets;
    private readonly System.Threading.Timer _timer;
    private volatile bool _disposed;
    private int _pollInProgress;

    public ExternalGameProcessMonitor(
        Func<IReadOnlyList<(Guid GameId, string ResolvedExecutablePath)>> getWatchTargets,
        TimeSpan pollInterval)
    {
        _getWatchTargets = getWatchTargets ?? throw new ArgumentNullException(nameof(getWatchTargets));
        _timer = new System.Threading.Timer(_ => Poll(), null, pollInterval, pollInterval);
    }

    /// <summary>
    /// Raised once per matched, not-yet-tracked game per poll. The <see cref="Process"/> handed to
    /// subscribers is NOT disposed by this class — ownership transfers to whoever handles the event
    /// (normally by wrapping it via <see cref="GameLauncher.WrapExternalProcess"/>). If no subscriber
    /// claims it, the caller is responsible for disposing it to avoid a handle leak.
    /// </summary>
    public event EventHandler<ExternalGameDetectedEventArgs>? GameDetected;

    private void Poll()
    {
        if (_disposed)
        {
            return;
        }

        // A poll that takes longer than the interval (a slow system, hundreds of processes with
        // locked MainModule access) should never stack a second overlapping poll on top of it.
        if (System.Threading.Interlocked.Exchange(ref _pollInProgress, 1) == 1)
        {
            return;
        }

        try
        {
            IReadOnlyList<(Guid GameId, string ResolvedExecutablePath)> targets;
            try
            {
                targets = _getWatchTargets();
            }
            catch
            {
                // Caller-supplied delegate threw (e.g. touched UI state incorrectly) — never let that
                // take down the timer callback or, by extension, the process.
                return;
            }

            if (targets.Count == 0)
            {
                return;
            }

            var byPath = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in targets)
            {
                if (!string.IsNullOrWhiteSpace(target.ResolvedExecutablePath))
                {
                    byPath[target.ResolvedExecutablePath] = target.GameId;
                }
            }

            if (byPath.Count == 0)
            {
                return;
            }

            Process[] processes;
            try
            {
                processes = Process.GetProcesses();
            }
            catch
            {
                return;
            }

            foreach (var process in processes)
            {
                if (_disposed)
                {
                    process.Dispose();
                    continue;
                }

                string? path = null;
                try
                {
                    // Throws Win32Exception for elevated/protected/system processes CloudBerry has no
                    // access to inspect, and can throw InvalidOperationException if the process exits
                    // mid-enumeration. Both are ordinary, expected outcomes for most of what
                    // GetProcesses() returns — silently skip rather than let one bad process abort
                    // the whole poll.
                    path = process.MainModule?.FileName;
                }
                catch
                {
                    process.Dispose();
                    continue;
                }

                if (string.IsNullOrWhiteSpace(path) || !byPath.TryGetValue(path, out var gameId))
                {
                    process.Dispose();
                    continue;
                }

                // One detection per game per poll — remove so a second process that happens to also
                // resolve to the same path (unlikely, but not impossible with hardlinks/junctions)
                // doesn't fire the event twice for a game that's already been claimed this pass.
                byPath.Remove(path);

                // Ownership of `process` passes to the single subscriber from here. If shutdown
                // has already detached it, dispose immediately rather than leaking the process handle.
                var handler = GameDetected;
                if (handler is null)
                {
                    process.Dispose();
                    continue;
                }

                try
                {
                    handler.Invoke(this, new ExternalGameDetectedEventArgs(gameId, process));
                }
                catch
                {
                    // A subscriber failed before it could wrap the process. The monitor owns the
                    // handle again in that failure path; timer callbacks must never escape either.
                    process.Dispose();
                }
            }
        }
        finally
        {
            System.Threading.Volatile.Write(ref _pollInProgress, 0);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
    }
}

/// <summary>
/// A library game (<see cref="GameId"/>) was found running under a process CloudBerry didn't start.
/// <see cref="Process"/> is owned by the subscriber once this fires — see
/// <see cref="ExternalGameProcessMonitor.GameDetected"/>.
/// </summary>
public sealed class ExternalGameDetectedEventArgs(Guid gameId, Process process) : EventArgs
{
    public Guid GameId { get; } = gameId;
    public Process Process { get; } = process;
}
