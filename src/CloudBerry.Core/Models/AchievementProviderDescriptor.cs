namespace CloudBerry.Core.Models;

public sealed class AchievementProviderDescriptor
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string Description { get; init; }

    public string ApiKeyLabel { get; init; } = "API key";

    public string ApiKeyHelpText { get; init; } = string.Empty;

    public string PlayerIdLabel { get; init; } = "Player ID";

    public string PlayerIdHelpText { get; init; } = string.Empty;
}
