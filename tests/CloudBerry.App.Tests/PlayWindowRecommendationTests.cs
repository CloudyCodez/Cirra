using CloudBerry.App.ViewModels;
using CloudBerry.Core.Models;

namespace CloudBerry.App.Tests;

public sealed class PlayWindowRecommendationTests
{
    [Fact]
    public void EstimateSessionMinutes_UsesMedianOfRecentSessions()
    {
        var game = CreateGame("Session Game", 18, 28, 42, 95);

        Assert.Equal(42, MainViewModel.EstimateSessionMinutes(game));
    }

    [Fact]
    public void FifteenMinuteWindow_PrefersShortSessionHistory()
    {
        var shortGame = CreateGame("Quick Match", 12, 16, 20);
        var longGame = CreateGame("Long Campaign", 65, 80, 110);

        var shortResult = MainViewModel.CreatePlayWindowRecommendation(shortGame, 15);
        var longResult = MainViewModel.CreatePlayWindowRecommendation(longGame, 15);

        Assert.True(shortResult.Score > longResult.Score);
        Assert.Equal(16, shortResult.Recommendation.ExpectedMinutes);
        Assert.Contains("16 min", shortResult.Recommendation.FitLabel);
    }

    [Fact]
    public void ResumeContext_BreaksOtherwiseEqualTie()
    {
        var resumable = CreateGame("Resume Me", 25, 30, 35);
        resumable.NextTimeNote = "Return to the final checkpoint.";
        var fresh = CreateGame("Fresh Pick", 25, 30, 35);

        var resumableResult = MainViewModel.CreatePlayWindowRecommendation(resumable, 30);
        var freshResult = MainViewModel.CreatePlayWindowRecommendation(fresh, 30);

        Assert.True(resumableResult.Score > freshResult.Score);
        Assert.Contains("Next Time", resumableResult.Recommendation.Reason);
    }

    private static GameEntryViewModel CreateGame(string title, params int[] durations)
    {
        return new GameEntryViewModel(new GameRecord
        {
            Title = title,
            IsInstalled = true,
            Status = "Playing",
            Sessions = durations.Select((duration, index) => new GameRecord.PlaySession
            {
                StartedAtLocal = DateTime.Now.AddDays(-index),
                EndedAtLocal = DateTime.Now.AddDays(-index).AddMinutes(duration),
                DurationMinutes = duration
            }).ToList()
        });
    }
}
