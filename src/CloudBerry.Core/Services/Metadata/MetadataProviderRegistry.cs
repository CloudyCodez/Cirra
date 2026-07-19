using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services.Metadata;

public sealed class MetadataProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IGameMetadataProvider> _providers;

    public MetadataProviderRegistry(HttpClient? httpClient = null)
    {
        var sharedClient = httpClient ?? new HttpClient();

        _providers = new IGameMetadataProvider[]
        {
            // Single unified source — no API keys required
            new AggregatedMetadataProvider(sharedClient)
        }.ToDictionary(p => p.Descriptor.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<MetadataProviderDescriptor> Providers =>
        _providers.Values
            .Select(p => p.Descriptor)
            .ToArray();

    /// <summary>Always returns the single aggregated provider.</summary>
    public IGameMetadataProvider GetProvider(string? providerId = null) =>
        _providers["aggregated"];
}
