using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services.Achievements;

public static class AchievementScoreCalculator
{
    public static int Calculate(double? globalUnlockPercent)
    {
        if (globalUnlockPercent is null)
        {
            return 10;
        }

        return globalUnlockPercent switch
        {
            <= 1 => 100,
            <= 5 => 50,
            <= 10 => 25,
            <= 25 => 15,
            _ => 10
        };
    }

    public static void AssignScores(IReadOnlyList<AchievementRecord> achievements, int targetTotal = 1000)
    {
        if (achievements.Count == 0)
        {
            return;
        }

        if (achievements.Any(achievement => achievement.GamerScoreValue != 10))
        {
            return;
        }

        if (achievements.Any(achievement => achievement.GlobalUnlockPercent is not null))
        {
            foreach (var achievement in achievements)
            {
                achievement.GamerScoreValue = Calculate(achievement.GlobalUnlockPercent);
            }

            return;
        }

        var baseValue = Math.Max(5, RoundToNearestFive(targetTotal / Math.Max(1, achievements.Count)));
        foreach (var achievement in achievements)
        {
            achievement.GamerScoreValue = baseValue;
        }
    }

    private static int RoundToNearestFive(int value)
    {
        return Math.Max(5, (int)Math.Round(value / 5.0, MidpointRounding.AwayFromZero) * 5);
    }
}
