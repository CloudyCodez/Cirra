using CloudBerry.Core.Models;
using CloudBerry.Core.Services;

namespace CloudBerry.Core.Tests;

public sealed class LaunchOrchestratorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"cirra-launch-{Guid.NewGuid():N}");

    [Fact]
    public async Task ExecuteAsync_BlockedPreflightNeverStartsProcess()
    {
        var started = false;
        var updates = new RecordingProgress<LaunchOperationUpdate>();
        var game = CreateGame(Path.Combine(_root, "missing.exe"));

        var result = await new LaunchOrchestrator().ExecuteAsync(
            game,
            null,
            _ =>
            {
                started = true;
                return Task.FromResult(GameLaunchSession.CreateUntracked());
            },
            updates);

        Assert.False(started);
        Assert.Equal(LaunchOperationState.RepairRequired, result.State);
        Assert.Equal(
            [LaunchOperationState.Checking, LaunchOperationState.RepairRequired],
            updates.Values.Select(update => update.State));
    }

    [Fact]
    public async Task ExecuteAsync_UntrackedLaunchCompletesThroughOrderedStates()
    {
        Directory.CreateDirectory(_root);
        var executable = Path.Combine(_root, "game.exe");
        await File.WriteAllBytesAsync(executable, [1, 2, 3]);
        var updates = new RecordingProgress<LaunchOperationUpdate>();

        var result = await new LaunchOrchestrator().ExecuteAsync(
            CreateGame(executable),
            null,
            _ => Task.FromResult(GameLaunchSession.CreateUntracked()),
            updates);

        Assert.True(result.Started);
        Assert.Equal(LaunchOperationState.Completed, result.State);
        Assert.Equal(
            [
                LaunchOperationState.Checking,
                LaunchOperationState.Preparing,
                LaunchOperationState.Starting,
                LaunchOperationState.Completed
            ],
            updates.Values.Select(update => update.State));
    }

    [Fact]
    public async Task ExecuteAsync_CancellationHasTruthfulTerminalState()
    {
        Directory.CreateDirectory(_root);
        var executable = Path.Combine(_root, "game.exe");
        await File.WriteAllBytesAsync(executable, [1]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var updates = new RecordingProgress<LaunchOperationUpdate>();

        var result = await new LaunchOrchestrator().ExecuteAsync(
            CreateGame(executable),
            null,
            _ => Task.FromResult(GameLaunchSession.CreateUntracked()),
            updates,
            cancellation.Token);

        Assert.Equal(LaunchOperationState.Cancelled, result.State);
        Assert.Equal(LaunchOperationState.Cancelled, updates.Values[^1].State);
    }

    private static GameRecord CreateGame(string path) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Test Game",
        Platform = "Windows PC",
        InstallPath = path,
        IsInstalled = true
    };

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class RecordingProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = [];

        public void Report(T value) => Values.Add(value);
    }
}
