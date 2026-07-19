namespace CloudBerry.Core.Models;

public sealed class GameAchievementSnapshot
{
    public string ProviderId { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public string ProviderGameId { get; set; } = string.Empty;

    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;

    public List<AchievementRecord> Achievements { get; set; } = [];
}
