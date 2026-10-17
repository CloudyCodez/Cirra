using CloudBerry.Core.Models;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CloudBerry.Core.Services.Metadata;

/// <summary>
/// Keyless metadata fan-out. CloudBerry deliberately avoids providers that require
/// hidden shared secrets in the desktop app.
/// </summary>
public sealed class AggregatedMetadataProvider : IGameMetadataProvider
{
    private readonly SteamStoreMetadataProvider _steam;
    private readonly PcgamingWikiMetadataProvider _pcgamingWiki;
    private readonly WikipediaMetadataProvider _wikipedia;
    private readonly OpenCriticMetadataProvider _openCritic;
    private readonly IgdbProxyMetadataProvider _igdbProxy;
    private readonly IgdbMetadataProvider _igdbDirect;

    public AggregatedMetadataProvider(HttpClient httpClient)
    {
        _steam = new SteamStoreMetadataProvider(httpClient);
        _pcgamingWiki = new PcgamingWikiMetadataProvider(httpClient);
        _wikipedia = new WikipediaMetadataProvider(httpClient);
        _openCritic = new OpenCriticMetadataProvider(httpClient);
        _igdbProxy = new IgdbProxyMetadataProvider(httpClient);
        _igdbDirect = new IgdbMetadataProvider(httpClient);
    }

    public MetadataProviderDescriptor Descriptor { get; } = new()
    {
        Id = "aggregated",
        DisplayName = "IGDB",
        Description = "Searches IGDB first, then quietly enriches missing fields from Cirra fallback sources.",
        RequiresApiKey = false,
        ApiKeyLabel = string.Empty,
        ApiKeyHelpText = string.Empty
    };

    public async Task<IReadOnlyList<GameMetadataRecord>> SearchAsync(
        GameMetadataSearchRequest request,
        CloudBerrySettings settings,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return [];
        }

        var normalizedRequest = new GameMetadataSearchRequest
        {
            Query = BuildPrimaryMetadataQuery(request.Query),
            Platform = request.Platform,
            MaxResults = request.MaxResults
        };

        var disabled = settings.DisabledMetadataProviderIds ?? [];
        var steamTask = disabled.Contains(_steam.Descriptor.Id, StringComparer.OrdinalIgnoreCase) ? Task.FromResult<IReadOnlyList<GameMetadataRecord>>([]) : SafeSearch(_steam, normalizedRequest, settings, cancellationToken);
        var pcgwTask = disabled.Contains(_pcgamingWiki.Descriptor.Id, StringComparer.OrdinalIgnoreCase) ? Task.FromResult<IReadOnlyList<GameMetadataRecord>>([]) : SafeSearch(_pcgamingWiki, normalizedRequest, settings, cancellationToken);
        var wikiTask = disabled.Contains(_wikipedia.Descriptor.Id, StringComparer.OrdinalIgnoreCase) ? Task.FromResult<IReadOnlyList<GameMetadataRecord>>([]) : SafeSearch(_wikipedia, normalizedRequest, settings, cancellationToken);
        var ocTask = disabled.Contains(_openCritic.Descriptor.Id, StringComparer.OrdinalIgnoreCase) ? Task.FromResult<IReadOnlyList<GameMetadataRecord>>([]) : SafeSearch(_openCritic, normalizedRequest, settings, cancellationToken);
        var igdbProxyTask = disabled.Contains(_igdbProxy.Descriptor.Id, StringComparer.OrdinalIgnoreCase) ? Task.FromResult<IReadOnlyList<GameMetadataRecord>>([]) : SafeSearch(_igdbProxy, normalizedRequest, settings, cancellationToken);
        var igdbDirectTask = disabled.Contains(_igdbDirect.Descriptor.Id, StringComparer.OrdinalIgnoreCase) ? Task.FromResult<IReadOnlyList<GameMetadataRecord>>([]) : SafeSearch(_igdbDirect, normalizedRequest, settings, cancellationToken);

        await Task.WhenAll(steamTask, pcgwTask, wikiTask, ocTask, igdbProxyTask, igdbDirectTask);

        var merged = new List<GameMetadataRecord>();
        foreach (var result in igdbProxyTask.Result
                     .Concat(igdbDirectTask.Result)
                     .Concat(steamTask.Result)
                     .Concat(pcgwTask.Result)
                     .Concat(wikiTask.Result)
                     .Concat(ocTask.Result))
        {
            var match = merged.FirstOrDefault(existing => TitlesMatch(existing.Title, result.Title));
            if (match is null)
            {
                merged.Add(result);
            }
            else
            {
                MergeMissingFields(match, result);
            }
        }

        var aliasQueries = BuildAliasQueries(normalizedRequest.Query)
            .Where(query => !query.Equals(normalizedRequest.Query, StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToList();

        if (aliasQueries.Count > 0)
        {
            foreach (var aliasQuery in aliasQueries)
            {
                var aliasRequest = new GameMetadataSearchRequest
                {
                    Query = aliasQuery,
                    Platform = normalizedRequest.Platform,
                    MaxResults = normalizedRequest.MaxResults
                };
                var aliasResults = await SafeSearch(_wikipedia, aliasRequest, settings, cancellationToken);
                foreach (var result in aliasResults)
                {
                    var match = merged.FirstOrDefault(existing => TitlesMatch(existing.Title, result.Title));
                    if (match is null)
                    {
                        merged.Add(result);
                    }
                    else
                    {
                        MergeMissingFields(match, result);
                    }
                }
            }
        }

        return merged
            .Select(result => new
            {
                Result = result,
                Score = ScoreTitleMatch(request.Query, result.Title) + ProviderPriority(result.ProviderId)
            })
            .OrderByDescending(entry => entry.Score)
            .ThenByDescending(entry => entry.Result.CommunityRating ?? 0)
            .Take(Math.Clamp(request.MaxResults, 1, 30))
            .Select(entry => entry.Result)
            .ToArray();
    }

    public async Task<GameMetadataRecord?> GetDetailsAsync(
        GameMetadataRecord searchResult,
        CloudBerrySettings settings,
        CancellationToken cancellationToken = default)
    {
        var disabledProviders = settings.DisabledMetadataProviderIds ?? [];
        var details = searchResult.ProviderId switch
        {
            "steam-store" => await SafeDetails(_steam, searchResult, settings, cancellationToken),
            "pcgamingwiki" => await SafeDetails(_pcgamingWiki, searchResult, settings, cancellationToken),
            "wikipedia" => await SafeDetails(_wikipedia, searchResult, settings, cancellationToken),
            "opencritic" => await SafeDetails(_openCritic, searchResult, settings, cancellationToken),
            "igdb-proxy" => await SafeDetails(_igdbProxy, searchResult, settings, cancellationToken),
            "igdb-direct" => await SafeDetails(_igdbDirect, searchResult, settings, cancellationToken),
            _ => searchResult
        } ?? searchResult;

        if (details.ProviderId == "steam-store")
        {
            var openCritic = disabledProviders.Contains(_openCritic.Descriptor.Id, StringComparer.OrdinalIgnoreCase)
                ? null
                : await TryGetOpenCriticDetailsAsync(details.Title, settings, cancellationToken);
            if (openCritic is not null)
            {
                MergeMissingFields(details, openCritic);
            }
        }

        if (details.ProviderId != "pcgamingwiki" && !disabledProviders.Contains(_pcgamingWiki.Descriptor.Id, StringComparer.OrdinalIgnoreCase))
        {
            var pcgw = await TryGetDetailsByTitleAsync(_pcgamingWiki, details.Title, settings, cancellationToken);
            if (pcgw is not null && TitlesMatch(pcgw.Title, details.Title))
            {
                MergeMissingFields(details, pcgw);
            }
        }

        if (details.ProviderId != "wikipedia" && !disabledProviders.Contains(_wikipedia.Descriptor.Id, StringComparer.OrdinalIgnoreCase))
        {
            var wiki = await TryGetDetailsByTitleAsync(_wikipedia, details.Title, settings, cancellationToken);
            if (wiki is not null)
            {
                MergeMissingFields(details, wiki);
            }
        }

        return details;
    }

    private async Task<GameMetadataRecord?> TryGetOpenCriticDetailsAsync(
        string gameTitle,
        CloudBerrySettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await _openCritic.SearchAsync(
                new GameMetadataSearchRequest { Query = gameTitle, MaxResults = 5 },
                settings,
                cancellationToken);

            var match = results.FirstOrDefault(result => TitlesMatch(result.Title, gameTitle));
            return match is null ? null : await SafeDetails(_openCritic, match, settings, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<GameMetadataRecord?> TryGetDetailsByTitleAsync(
        IGameMetadataProvider provider,
        string gameTitle,
        CloudBerrySettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await provider.SearchAsync(
                new GameMetadataSearchRequest { Query = gameTitle, MaxResults = 4 },
                settings,
                cancellationToken);
            var match = results.FirstOrDefault(result => TitlesMatch(result.Title, gameTitle)) ?? results.FirstOrDefault();
            return match is null ? null : await SafeDetails(provider, match, settings, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static void MergeMissingFields(GameMetadataRecord target, GameMetadataRecord source)
    {
        if (string.IsNullOrWhiteSpace(target.Summary))
            target.Summary = source.Summary;
        if (target.ReleaseDate is null)
            target.ReleaseDate = source.ReleaseDate;
        if (string.IsNullOrWhiteSpace(target.CoverImageUrl))
            target.CoverImageUrl = source.CoverImageUrl;
        if (string.IsNullOrWhiteSpace(target.BackgroundImageUrl))
            target.BackgroundImageUrl = source.BackgroundImageUrl;
        if (string.IsNullOrWhiteSpace(target.BannerImageUrl))
            target.BannerImageUrl = source.BannerImageUrl;
        if (string.IsNullOrWhiteSpace(target.IconImageUrl))
            target.IconImageUrl = source.IconImageUrl;
        if (string.IsNullOrWhiteSpace(target.WebsiteUrl))
            target.WebsiteUrl = source.WebsiteUrl;
        if (string.IsNullOrWhiteSpace(target.ProviderItemUrl))
            target.ProviderItemUrl = source.ProviderItemUrl;
        if (target.CommunityRating is null)
            target.CommunityRating = source.CommunityRating;
        if (target.Platforms.Count == 0)
            target.Platforms = source.Platforms;
        if (target.Genres.Count == 0)
            target.Genres = source.Genres;
        if (target.Developers.Count == 0)
            target.Developers = source.Developers;
        if (target.Publishers.Count == 0)
            target.Publishers = source.Publishers;
        if (target.Tags.Count == 0)
            target.Tags = source.Tags;
        if (target.Screenshots.Count == 0)
            target.Screenshots = source.Screenshots;
        if (string.IsNullOrWhiteSpace(target.TrailerUrl))
            target.TrailerUrl = source.TrailerUrl;
        foreach (var guidebook in source.Guidebooks ?? [])
        {
            if (!(target.Guidebooks ??= []).Any(existing =>
                    existing.PdfLocation.Equals(guidebook.PdfLocation, StringComparison.OrdinalIgnoreCase)))
            {
                target.Guidebooks.Add(guidebook.Clone());
            }
        }
    }

    private static async Task<IReadOnlyList<GameMetadataRecord>> SafeSearch(
        IGameMetadataProvider provider,
        GameMetadataSearchRequest request,
        CloudBerrySettings settings,
        CancellationToken cancellationToken)
    {
        try { return await provider.SearchAsync(request, settings, cancellationToken); }
        catch { return []; }
    }

    private static async Task<GameMetadataRecord?> SafeDetails(
        IGameMetadataProvider provider,
        GameMetadataRecord result,
        CloudBerrySettings settings,
        CancellationToken cancellationToken)
    {
        try { return await provider.GetDetailsAsync(result, settings, cancellationToken); }
        catch { return result; }
    }

    private static bool TitlesMatch(string a, string b)
    {
        var normalizedA = NormalizeTitle(a);
        var normalizedB = NormalizeTitle(b);
        if (string.IsNullOrWhiteSpace(normalizedA) || string.IsNullOrWhiteSpace(normalizedB))
        {
            return false;
        }

        return normalizedA == normalizedB;
    }

    private static double ScoreTitleMatch(string query, string title)
    {
        var normalizedQuery = NormalizeTitle(query);
        var normalizedTitle = NormalizeTitle(title);
        if (string.IsNullOrWhiteSpace(normalizedQuery) || string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return 0;
        }

        if (normalizedQuery.Equals(normalizedTitle, StringComparison.Ordinal))
        {
            return 100;
        }

        var queryTokens = normalizedQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
        var titleTokens = normalizedTitle
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
        if (queryTokens.Count == 0 || titleTokens.Count == 0)
        {
            return 0;
        }

        var intersection = queryTokens.Intersect(titleTokens, StringComparer.Ordinal).Count();
        var union = queryTokens.Union(titleTokens, StringComparer.Ordinal).Count();
        var score = union == 0 ? 0 : intersection / (double)union * 78;

        if (normalizedTitle.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            score += 18;
        }

        if (normalizedTitle.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            score += 8;
        }

        if (ContainsSeriesOrListMarkers(normalizedTitle))
        {
            score -= 32;
        }

        score -= MissingRequestedModifierPenalty(queryTokens, titleTokens);

        if (ContainsSeriesOrListMarkers(normalizedQuery))
        {
            score += 8;
        }

        if (LooksLikeFranchiseRoot(normalizedTitle, queryTokens))
        {
            score -= 26;
        }

        score -= Math.Abs(normalizedTitle.Length - normalizedQuery.Length) * 0.08;
        return score;
    }

    private static double ProviderPriority(string providerId)
    {
        return providerId switch
        {
            "igdb-proxy" => 34,
            "igdb-direct" => 30,
            "steam-store" => 12,
            "pcgamingwiki" => 4,
            "opencritic" => 2,
            "wikipedia" => -10,
            _ => 0
        };
    }

    private static double MissingRequestedModifierPenalty(
        IReadOnlySet<string> queryTokens,
        IReadOnlySet<string> titleTokens)
    {
        var penalty = 0d;
        foreach (var token in queryTokens)
        {
            if (titleTokens.Contains(token))
            {
                continue;
            }

            penalty += token switch
            {
                "collection" or "anthology" or "trilogy" => 28,
                "hd" or "remaster" or "remastered" or "definitive" or "complete" => 18,
                "gx" or "xd" => 16,
                _ => 0
            };
        }

        return penalty;
    }

    private static bool ContainsSeriesOrListMarkers(string value)
    {
        return value.Contains("list of ", StringComparison.Ordinal) ||
               value.Contains("series", StringComparison.Ordinal) ||
               value.Contains("franchise", StringComparison.Ordinal) ||
               value.Contains("characters", StringComparison.Ordinal) ||
               value.Contains("episode", StringComparison.Ordinal) ||
               value.Contains("disambiguation", StringComparison.Ordinal);
    }

    private static bool LooksLikeFranchiseRoot(string normalizedTitle, IReadOnlySet<string> queryTokens)
    {
        if (queryTokens.Count == 0)
        {
            return false;
        }

        var titleTokens = normalizedTitle
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        if (titleTokens.Count > 2)
        {
            return false;
        }

        var overlap = titleTokens.Intersect(queryTokens, StringComparer.Ordinal).Count();
        return overlap >= 1 && queryTokens.Count >= 3;
    }

    private static string BuildPrimaryMetadataQuery(string query)
    {
        var normalized = Regex.Replace(query ?? string.Empty, @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return normalized
            .Replace("yu-gi-oh", "yu gi oh", StringComparison.OrdinalIgnoreCase)
            .Replace("yugioh", "yu gi oh", StringComparison.OrdinalIgnoreCase)
            .Replace("pokémon", "pokemon", StringComparison.OrdinalIgnoreCase)
            .Replace("é", "e", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> BuildAliasQueries(string query)
    {
        var normalized = BuildPrimaryMetadataQuery(query);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        var aliases = new List<string> { normalized };
        var noPunctuation = Regex.Replace(normalized, @"[^\w\s]", " ");
        noPunctuation = Regex.Replace(noPunctuation, @"\s+", " ").Trim();
        if (!string.IsNullOrWhiteSpace(noPunctuation) &&
            !aliases.Any(alias => alias.Equals(noPunctuation, StringComparison.OrdinalIgnoreCase)))
        {
            aliases.Add(noPunctuation);
        }

        var withVideoGame = $"{normalized} video game";
        if (!aliases.Any(alias => alias.Equals(withVideoGame, StringComparison.OrdinalIgnoreCase)))
        {
            aliases.Add(withVideoGame);
        }

        return aliases;
    }

    private static string NormalizeTitle(string value)
    {
        var canonical = (value ?? string.Empty)
            .Replace("yu-gi-oh", "yu gi oh", StringComparison.OrdinalIgnoreCase)
            .Replace("yugioh", "yu gi oh", StringComparison.OrdinalIgnoreCase)
            .Replace("pokémon", "pokemon", StringComparison.OrdinalIgnoreCase);

        // Decompose accented characters (é → e + combining accent) then strip the
        // non-spacing marks so "Pokémon" matches "Pokemon", "Nier" matches "NieR", etc.
        var decomposed = canonical.Normalize(NormalizationForm.FormD);
        var asciiOnly = new string(decomposed
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return string.Join(
            " ",
            new string(asciiOnly
                    .ToLowerInvariant()
                    .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
                    .ToArray())
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
