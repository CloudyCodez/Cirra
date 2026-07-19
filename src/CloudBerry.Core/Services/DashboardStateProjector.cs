using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services;

public static class DashboardStateProjector
{
    public static IReadOnlyList<PlatformDashboardSummary> ProjectPlatformSummaries(
        IEnumerable<DashboardProjectionInput> games,
        int maxCount = 16)
    {
        var grouped = (games ?? [])
            .GroupBy(game => NormalizePlatform(game.Platform), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var slice = group.ToList();
                return new PlatformDashboardSummary
                {
                    Platform = group.Key,
                    TitleCount = slice.Count,
                    InstalledCount = slice.Count(game => game.IsInstalled || game.IsRunning),
                    PlaytimeMinutes = slice.Sum(game => Math.Max(0, game.PlaytimeMinutes)),
                    MetadataReadyCount = slice.Count(game => !game.NeedsMetadataRefresh)
                };
            })
            .OrderByDescending(item => item.TitleCount)
            .ThenBy(item => item.Platform, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxCount))
            .ToList();

        return grouped;
    }

    public static DashboardInsightsSnapshot ProjectInsights(
        IEnumerable<DashboardProjectionInput> games,
        IEnumerable<DashboardActivityEntry> activity)
    {
        var gameList = (games ?? []).ToList();
        var platformSummaries = ProjectPlatformSummaries(gameList, maxCount: 32);
        var dominant = platformSummaries.FirstOrDefault();
        var mostPlayed = platformSummaries
            .OrderByDescending(item => item.PlaytimeMinutes)
            .ThenBy(item => item.Platform, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        var activityMix = (activity ?? [])
            .GroupBy(entry => entry.Kind, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DashboardActivityMixItem
            {
                Kind = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Kind, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        return new DashboardInsightsSnapshot
        {
            TotalGames = gameList.Count,
            InstalledGames = gameList.Count(game => game.IsInstalled),
            RunningGames = gameList.Count(game => game.IsRunning),
            MetadataReadyGames = gameList.Count(game => !game.NeedsMetadataRefresh),
            MissingMetadataGames = gameList.Count(game => game.NeedsMetadataRefresh),
            TotalPlaytimeMinutes = gameList.Sum(game => Math.Max(0, game.PlaytimeMinutes)),
            GamerScoreEarned = gameList.Sum(game => game.GamerScoreEarned),
            GamerScoreTotal = gameList.Sum(game => game.GamerScoreTotal),
            SyncedAchievementGames = gameList.Count(game => game.HasAchievements),
            RecentActivityCount = (activity ?? []).Count(),
            DominantPlatform = dominant?.Platform ?? "Unknown",
            DominantPlatformCount = dominant?.TitleCount ?? 0,
            MostPlayedPlatform = mostPlayed?.Platform ?? "Unknown",
            MostPlayedPlatformMinutes = mostPlayed?.PlaytimeMinutes ?? 0,
            ActivityMix = activityMix
        };
    }

    private static string NormalizePlatform(string? platform)
    {
        return string.IsNullOrWhiteSpace(platform) ? "Unknown" : platform.Trim();
    }
}
