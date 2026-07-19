using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services.Metadata;

public interface IGameMetadataProvider
{
    MetadataProviderDescriptor Descriptor { get; }

    Task<IReadOnlyList<GameMetadataRecord>> SearchAsync(
        GameMetadataSearchRequest request,
        CloudBerrySettings settings,
        CancellationToken cancellationToken = default);

    Task<GameMetadataRecord?> GetDetailsAsync(
        GameMetadataRecord searchResult,
        CloudBerrySettings settings,
        CancellationToken cancellationToken = default);
}
