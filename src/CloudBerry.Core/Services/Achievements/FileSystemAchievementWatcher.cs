using System.Text.Json;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services.Achievements;

/// <summary>
/// Real-time achievement watcher using FileSystemWatcher instead of polling.
/// Fires <see cref="AchievementsChanged"/> within ~200 ms of a file change,
/// which is the same latency used by Steam, Playnite, and LaunchBox.
///
/// Replaces the 30-second poll loop in MainViewModel.StartAchievementPollingIfAvailable
/// for all local-file-based achievement sources (Steam emu, RPCS3, PCSX2, Dolphin,
/// SteamEmu, codex cracks, etc.).
/// </summary>
public sealed class FileSystemAchievementWatcher : IDisposable
{
    private readonly LocalAchievementScanner _scanner;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly SemaphoreSlim _debounce = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public FileSystemAchievementWatcher(LocalAchievementScanner scanner)
    {
        _scanner = scanner;
    }

    /// <summary>Fired on the thread pool when achievement files change.</summary>
    public event EventHandler<AchievementsChangedEventArgs>? AchievementsChanged;

    /// <summary>
    /// Begin watching the given files. Each distinct directory gets a
    /// <see cref="FileSystemWatcher"/>; each file name is filtered individually.
    /// </summary>
    public void Watch(IEnumerable<string> filePaths)
    {
        StopWatching();

        var grouped = filePaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .GroupBy(p => Path.GetDirectoryName(Path.GetFullPath(p)) ?? string.Empty,
                     StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            if (!Directory.Exists(group.Key))
            {
                continue;
            }

            foreach (var filePath in group)
            {
                try
                {
                    var watcher = new FileSystemWatcher(group.Key, Path.GetFileName(filePath))
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                        EnableRaisingEvents = true
                    };

                    var allPaths = group.ToList();
                    watcher.Changed += (_, _) => OnFileChanged(allPaths);
                    watcher.Created += (_, _) => OnFileChanged(allPaths);

                    _watchers.Add(watcher);
                }
                catch
                {
                    // directory disappeared between check and watch – skip
                }
            }
        }
    }

    public void StopWatching()
    {
        foreach (var w in _watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }
        _watchers.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _cts.Cancel();
        StopWatching();
        _debounce.Dispose();
        _cts.Dispose();
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private void OnFileChanged(IReadOnlyList<string> allTrackedPaths)
    {
        // Debounce: wait 200 ms after the last change before scanning
        // (editors and emulators often do multiple writes per save)
        _ = DebounceAsync(allTrackedPaths);
    }

    private async Task DebounceAsync(IReadOnlyList<string> allTrackedPaths)
    {
        if (!await _debounce.WaitAsync(0))
        {
            return; // already queued
        }

        try
        {
            await Task.Delay(200, _cts.Token);
            var result = _scanner.LoadFromFiles(allTrackedPaths);
            if (result.Snapshot.Achievements.Count > 0)
            {
                AchievementsChanged?.Invoke(this, new AchievementsChangedEventArgs(result));
            }
        }
        catch (OperationCanceledException)
        {
            // disposed – exit silently
        }
        catch
        {
            // swallow parse errors
        }
        finally
        {
            _debounce.Release();
        }
    }
}

public sealed class AchievementsChangedEventArgs(GameAchievementSyncResult result) : EventArgs
{
    public GameAchievementSyncResult Result { get; } = result;
}
