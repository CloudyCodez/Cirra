namespace CloudBerry.Core.Models;

public sealed class GameAchievementRequest
{
    public string ProviderGameId { get; set; } = string.Empty;

    public string PlayerId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;
}
