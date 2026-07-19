using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Threading;
using CloudBerry.Core.Models;

namespace CloudBerry.App.Services;

/// <summary>
/// Xbox/PSN-style "Achievement Unlocked" pop service.
/// Call <see cref="ShowAsync"/> from any thread; the toast is queued and shown
/// one at a time on the UI thread. Excess pops are held in a bounded queue so
/// a burst of unlocks doesn't stack-overflow the screen.
/// </summary>
public sealed class AchievementPopService
{
    private readonly ConcurrentQueue<AchievementUnlockEvent> _queue = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Window? _ownerWindow;
    private int _displaySeconds = 5;
    private string _popCorner = "BottomRight";

    /// <summary>Wire up the owner window before any game sessions start.</summary>
    public void Initialize(Window ownerWindow, int displaySeconds = 5, string popCorner = "BottomRight")
    {
        _ownerWindow = ownerWindow;
        _displaySeconds = Math.Clamp(displaySeconds, 2, 10);
        _popCorner = string.IsNullOrWhiteSpace(popCorner) ? "BottomRight" : popCorner;
    }

    /// <summary>
    /// Queue an achievement pop. Safe to call from any thread.
    /// At most 8 pops are queued; extras are discarded to prevent floods.
    /// </summary>
    public void Show(AchievementUnlockEvent unlockEvent)
    {
        if (_queue.Count >= 8)
        {
            return;
        }

        _queue.Enqueue(unlockEvent);
        _ = DrainAsync();
    }

    /// <summary>Convenience overload that builds the event inline.</summary>
    public void Show(
        string gameTitle,
        string achievementTitle,
        string description,
        string iconUrl,
        int gamerScore,
        string providerName = "CloudBerry")
    {
        Show(new AchievementUnlockEvent
        {
            GameTitle = gameTitle,
            AchievementTitle = achievementTitle,
            Description = description,
            IconUrl = iconUrl,
            GamerScore = gamerScore,
            ProviderName = providerName
        });
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private async Task DrainAsync()
    {
        if (!await _gate.WaitAsync(0))
        {
            return; // another drain is running
        }

        try
        {
            while (_queue.TryDequeue(out var ev))
            {
                await ShowOneAsync(ev);
                // small breathing gap between consecutive pops
                await Task.Delay(400);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private Task ShowOneAsync(AchievementUnlockEvent ev)
    {
        var tcs = new TaskCompletionSource();

        var dispatcher = _ownerWindow?.Dispatcher ?? Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            tcs.SetResult();
            return tcs.Task;
        }

        dispatcher.InvokeAsync(() =>
        {
            try
            {
                var popup = new AchievementPopWindow(ev, _ownerWindow, _displaySeconds, _popCorner);
                popup.Closed += (_, _) => tcs.TrySetResult();
                popup.Show();
            }
            catch
            {
                tcs.TrySetResult();
            }
        });

        return tcs.Task;
    }
}
