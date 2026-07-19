using CloudBerry.Core.Models;
using CloudBerry.Core.Services;

namespace CloudBerry.Core.Tests;

public sealed class DashboardStateProjectorTests
{
    [Fact]
    public void ProjectPlatformSummaries_AggregatesAndSortsByTitleCount()
    {
        var input = new[]
        {
            new DashboardProjectionInput { Platform = "PC", IsInstalled = true, PlaytimeMinutes = 120, NeedsMetadataRefresh = false },
            new DashboardProjectionInput { Platform = "PC", IsRunning = true, PlaytimeMinutes = 45, NeedsMetadataRefresh = true },
            new DashboardProjectionInput { Platform = "Switch", IsInstalled = true, PlaytimeMinutes = 300, NeedsMetadataRefresh = false }
        };

        var result = DashboardStateProjector.ProjectPlatformSummaries(input).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("PC", result[0].Platform);
        Assert.Equal(2, result[0].TitleCount);
        Assert.Equal(2, result[0].InstalledCount);
        Assert.Equal(165, result[0].PlaytimeMinutes);
        Assert.Equal(1, result[0].MetadataReadyCount);
    }

    [Fact]
    public void ProjectInsights_ComputesPlatformContextAndActivityMix()
    {
        var games = new[]
        {
            new DashboardProjectionInput
            {
                Platform = "PC",
                IsInstalled = true,
                PlaytimeMinutes = 120,
                GamerScoreEarned = 200,
                GamerScoreTotal = 500,
                HasAchievements = true
            },
            new DashboardProjectionInput
            {
                Platform = "Switch",
                IsInstalled = true,
                PlaytimeMinutes = 300,
                NeedsMetadataRefresh = true
            }
        };

        var activity = new[]
        {
            new DashboardActivityEntry { Kind = DashboardActivityKinds.Launch },
            new DashboardActivityEntry { Kind = DashboardActivityKinds.Launch },
            new DashboardActivityEntry { Kind = DashboardActivityKinds.MetadataApply }
        };

        var insights = DashboardStateProjector.ProjectInsights(games, activity);

        Assert.Equal(2, insights.TotalGames);
        Assert.Equal(1, insights.MetadataReadyGames);
        Assert.Equal(1, insights.MissingMetadataGames);
        Assert.Equal(420, insights.TotalPlaytimeMinutes);
        Assert.Equal("PC", insights.DominantPlatform);
        Assert.Equal("Switch", insights.MostPlayedPlatform);
        Assert.Equal(300, insights.MostPlayedPlatformMinutes);
        Assert.Equal(2, insights.ActivityMix[0].Count);
        Assert.Equal(DashboardActivityKinds.Launch, insights.ActivityMix[0].Kind);
    }
}
