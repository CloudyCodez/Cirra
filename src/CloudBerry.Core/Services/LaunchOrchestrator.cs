using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services;

public sealed class LaunchOrchestrator
{
    private readonly LaunchPreflightService _preflight;

    public LaunchOrchestrator(LaunchPreflightService? preflight = null)
    {
        _preflight = preflight ?? new LaunchPreflightService();
    }

    public async Task<LaunchOrchestrationResult> ExecuteAsync(
        GameRecord game,
        string? configuredEmulator,
        Func<CancellationToken, Task<GameLaunchSession>> start,
        IProgress<LaunchOperationUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(start);

        var operationId = Guid.NewGuid();
        Report(LaunchOperationState.Checking, "Checking game files and launch requirements...");
        var preflight = _preflight.Check(game, configuredEmulator);
        if (!preflight.CanLaunch)
        {
            var blocker = preflight.Checks.First(check => check.Readiness == LaunchReadiness.Blocked);
            Report(LaunchOperationState.RepairRequired, blocker.Detail);
            return new LaunchOrchestrationResult(
                operationId,
                LaunchOperationState.RepairRequired,
                preflight,
                null,
                null);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(LaunchOperationState.Preparing, "Preparing the launch profile and session tracking...");
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            Report(LaunchOperationState.Starting, "Starting " + game.Title + "...");
            var session = await start(cancellationToken).ConfigureAwait(false);
            if (session.IsTrackingAvailable)
            {
                Report(LaunchOperationState.Tracking, "Game started. Cirra is tracking this session.");
                session.Ended += (_, _) =>
                {
                    Report(LaunchOperationState.Returning, "Game closed. Restoring your Cirra context...");
                    Report(LaunchOperationState.Completed, "Session completed.");
                };
                return new LaunchOrchestrationResult(
                    operationId,
                    LaunchOperationState.Tracking,
                    preflight,
                    session,
                    null);
            }

            Report(LaunchOperationState.Completed, "Game opened, but this target does not expose process tracking.");
            return new LaunchOrchestrationResult(
                operationId,
                LaunchOperationState.Completed,
                preflight,
                session,
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Report(LaunchOperationState.Cancelled, "Launch cancelled before the game started.");
            return new LaunchOrchestrationResult(
                operationId,
                LaunchOperationState.Cancelled,
                preflight,
                null,
                null);
        }
        catch (Exception ex)
        {
            Report(LaunchOperationState.Failed, ex.Message);
            return new LaunchOrchestrationResult(
                operationId,
                LaunchOperationState.Failed,
                preflight,
                null,
                ex);
        }

        void Report(LaunchOperationState state, string message)
        {
            progress?.Report(new LaunchOperationUpdate(
                operationId,
                game.Id,
                game.Title,
                state,
                message,
                DateTimeOffset.UtcNow));
        }
    }
}
