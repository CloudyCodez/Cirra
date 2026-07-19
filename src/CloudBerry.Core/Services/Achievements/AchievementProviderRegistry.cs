using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services.Achievements;

public sealed class AchievementProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IGameAchievementProvider> _providers;

    public AchievementProviderRegistry(HttpClient? httpClient = null)
    {
        var sharedClient = httpClient ?? new HttpClient();
        _providers = new IGameAchievementProvider[]
        {
            new UniversalAchievementProvider(sharedClient),
            new SteamAchievementProvider(sharedClient),
            new RetroAchievementsProvider(sharedClient)
        }.ToDictionary(provider => provider.Descriptor.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<AchievementProviderDescriptor> Providers =>
        _providers.Values.Select(provider => provider.Descriptor).OrderBy(provider => provider.DisplayName).ToArray();

    public IGameAchievementProvider GetProvider(string providerId)
    {
        if (_providers.TryGetValue(providerId, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"No achievement provider is registered for '{providerId}'.");
    }

    public bool TryGetProvider(string providerId, out IGameAchievementProvider? provider)
        => _providers.TryGetValue(providerId, out provider);
}
