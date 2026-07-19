namespace CloudBerry.Core.Models;

public sealed class MetadataProviderDescriptor
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string Description { get; init; }

    public bool RequiresApiKey { get; init; }

    public string ApiKeyLabel { get; init; } = "API key";

    public string ApiKeyHelpText { get; init; } = string.Empty;
}
