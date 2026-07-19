using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services.Achievements;

public interface IGameAchievementProvider
{
    AchievementProviderDescriptor Descriptor { get; }

    Task<GameAchievementSnapshot> GetAchievementsAsync(
        GameAchievementRequest request,
        CloudBerrySettings settings,
        CancellationToken cancellationToken = default);
}
