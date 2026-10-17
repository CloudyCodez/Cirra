using CloudBerry.Core.Models;
using CloudBerry.Core.Services.Achievements;

namespace CloudBerry.Core.Tests;

public sealed class AchievementScoreCalculatorTests
{
    [Theory]
    [InlineData(0.5, 100)]   // ultra-rare (<=1%) is worth the most
    [InlineData(1.0, 100)]
    [InlineData(3.0, 50)]    // <=5%
    [InlineData(8.0, 25)]    // <=10%
    [InlineData(20.0, 15)]   // <=25%
    [InlineData(60.0, 10)]   // common
    public void Calculate_TiersScoreByGlobalRarity(double globalUnlockPercent, int expectedScore)
    {
        Assert.Equal(expectedScore, AchievementScoreCalculator.Calculate(globalUnlockPercent));
    }

    [Fact]
    public void Calculate_WithNoPercentData_FallsBackToDefaultTen()
    {
        Assert.Equal(10, AchievementScoreCalculator.Calculate(null));
    }

    [Fact]
    public void AssignScores_WithGlobalPercentPresent_ScoresEachAchievementByItsOwnRarity()
    {
        var achievements = new List<AchievementRecord>
        {
            new() { GlobalUnlockPercent = 0.8 },
            new() { GlobalUnlockPercent = 40.0 }
        };

        AchievementScoreCalculator.AssignScores(achievements);

        Assert.Equal(100, achievements[0].GamerScoreValue);
        Assert.Equal(10, achievements[1].GamerScoreValue);
    }

    [Fact]
    public void AssignScores_WithNoPercentDataAnywhere_SplitsTargetTotalEvenlyAcrossAchievements()
    {
        // 1000 target / 4 achievements = 250 each, already a multiple of five.
        var achievements = new List<AchievementRecord>
        {
            new(), new(), new(), new()
        };

        AchievementScoreCalculator.AssignScores(achievements, targetTotal: 1000);

        Assert.All(achievements, a => Assert.Equal(250, a.GamerScoreValue));
    }

    [Fact]
    public void AssignScores_WithNoPercentData_RoundsUnevenSplitToNearestFive()
    {
        // 100 / 3 = 33.33 -> nearest five is 35.
        var achievements = new List<AchievementRecord>
        {
            new(), new(), new()
        };

        AchievementScoreCalculator.AssignScores(achievements, targetTotal: 100);

        Assert.All(achievements, a => Assert.Equal(35, a.GamerScoreValue));
    }

    [Fact]
    public void AssignScores_WhenScoresAlreadyCustomized_LeavesExistingValuesAlone()
    {
        // Any achievement whose GamerScoreValue isn't still the un-touched default (10)
        // means scoring was already applied elsewhere — AssignScores must be a no-op
        // so it never clobbers real provider-assigned or previously-computed scores.
        var achievements = new List<AchievementRecord>
        {
            new() { GamerScoreValue = 75 },
            new() { GamerScoreValue = 10 }
        };

        AchievementScoreCalculator.AssignScores(achievements);

        Assert.Equal(75, achievements[0].GamerScoreValue);
        Assert.Equal(10, achievements[1].GamerScoreValue);
    }

    [Fact]
    public void AssignScores_WithEmptyList_DoesNothingAndDoesNotThrow()
    {
        var achievements = new List<AchievementRecord>();

        var exception = Record.Exception(() => AchievementScoreCalculator.AssignScores(achievements));

        Assert.Null(exception);
        Assert.Empty(achievements);
    }
}
