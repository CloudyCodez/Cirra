using CloudBerry.Core.Models;
using CloudBerry.Core.Services.Metadata;

namespace CloudBerry.Core.Services.Achievements;

public sealed class UniversalAchievementProvider : IGameAchievementProvider
{
    private readonly SteamAchievementProvider _steamProvider;
    private readonly IGameMetadataProvider _metadataProvider;

    public UniversalAchievementProvider(HttpClient httpClient)
    {
        _steamProvider = new SteamAchievementProvider(httpClient);
        _metadataProvider = new MetadataProviderRegistry(httpClient).GetProvider("aggregated");
    }

    public AchievementProviderDescriptor Descriptor { get; } = new()
    {
        Id = "cloudberry-universal",
        DisplayName = "Cirra Universal",
        Description = "Tries online sources beyond Steam by title, and auto-generates achievement sets when a game has no public achievement data."
    };

    public async Task<GameAchievementSnapshot> GetAchievementsAsync(
        GameAchievementRequest request,
        CloudBerrySettings settings,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.ProviderGameId))
        {
            try
            {
                var steamSnapshot = await _steamProvider.GetAchievementsAsync(request, settings, cancellationToken);
                if (steamSnapshot.Achievements.Count > 0)
                {
                    return steamSnapshot;
                }
            }
            catch
            {
                // Fall through to metadata-driven generation.
            }
        }

        var title = string.IsNullOrWhiteSpace(request.Title) ? "Unknown game" : request.Title.Trim();
        GameMetadataRecord? details = await TryResolveMetadataAsync(title, settings, cancellationToken);
        var generated = GenerateAchievements(title, details);
        AchievementScoreCalculator.AssignScores(generated);

        return new GameAchievementSnapshot
        {
            ProviderId = Descriptor.Id,
            ProviderName = Descriptor.DisplayName,
            ProviderGameId = request.ProviderGameId,
            SyncedAtUtc = DateTime.UtcNow,
            Achievements = generated
        };
    }

    private async Task<GameMetadataRecord?> TryResolveMetadataAsync(
        string title,
        CloudBerrySettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await _metadataProvider.SearchAsync(
                new GameMetadataSearchRequest
                {
                    Query = title,
                    MaxResults = 5
                },
                settings,
                cancellationToken);

            var best = results.FirstOrDefault();
            if (best is null)
            {
                return null;
            }

            return await _metadataProvider.GetDetailsAsync(best, settings, cancellationToken) ?? best;
        }
        catch
        {
            return null;
        }
    }

    private static List<AchievementRecord> GenerateAchievements(string title, GameMetadataRecord? metadata)
    {
        var genres = metadata?.Genres?.Where(g => !string.IsNullOrWhiteSpace(g)).Take(2).ToList() ?? [];
        var tags = metadata?.Tags?.Where(t => !string.IsNullOrWhiteSpace(t)).Take(3).ToList() ?? [];
        var platform = metadata?.Platforms?.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? "any platform";

        string genrePhrase = genres.Count > 0 ? string.Join(" / ", genres) : "adventure";
        string tagPhrase = tags.Count > 0 ? string.Join(", ", tags) : "story and progression";

        var templates = new (string Slug, string Title, string Description)[]
        {
            ("first_boot", "First Boot", $"Launch {title} for the first time in Cirra."),
            ("first_steps", "First Steps", $"Complete the opening section and establish your run in this {genrePhrase} experience."),
            ("world_tour", "World Tour", $"Visit 3 distinct areas or scenarios in {title}."),
            ("specialist", "Specialist", $"Complete a challenge focused on {tagPhrase}."),
            ("marathon", "Marathon Session", "Play for 60+ minutes in a single tracked session."),
            ("collector", "Collector", "Unlock 25% of available goals for this title."),
            ("dedicated", "Dedicated Player", "Reach 50% completion across this title's achievement set."),
            ("mastery", "Mastery", "Reach 100% completion for this generated set."),
            ("social", "Shareworthy Moment", $"Capture a notable moment or milestone while playing on {platform}."),
            ("veteran", "Veteran Return", "Play on three different days to establish a consistent play streak.")
        };

        var generated = new List<AchievementRecord>(templates.Length);
        for (var i = 0; i < templates.Length; i++)
        {
            var template = templates[i];
            generated.Add(new AchievementRecord
            {
                ApiName = $"cb_auto_{template.Slug}",
                Title = template.Title,
                Description = template.Description,
                DisplayOrder = i,
                IsHidden = false,
                IsUnlocked = false,
                GlobalUnlockPercent = null
            });
        }

        return generated;
    }
}
