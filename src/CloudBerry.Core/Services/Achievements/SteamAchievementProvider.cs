using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services.Achievements;

public sealed partial class SteamAchievementProvider : IGameAchievementProvider
{
    private readonly HttpClient _httpClient;

    public SteamAchievementProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public AchievementProviderDescriptor Descriptor { get; } = new()
    {
        Id = "steam-community",
        DisplayName = "Steam Community",
        Description = "Uses the public Steam Community achievement page as a no-key fallback for achievement titles, descriptions, icons, and global rarity."
    };

    public async Task<GameAchievementSnapshot> GetAchievementsAsync(
        GameAchievementRequest request,
        CloudBerrySettings settings,
        CancellationToken cancellationToken = default)
    {
        var providerGameId = request.ProviderGameId.Trim();
        if (string.IsNullOrWhiteSpace(providerGameId))
        {
            throw new InvalidOperationException("Cirra needs a Steam App ID before it can pull fallback achievement metadata.");
        }

        // Real per-user unlock state requires the Steam Web API (an API key + the
        // player's SteamID64). Try that first when the user has configured it in
        // Settings > Achievements > Steam. This is the only path that can report
        // IsUnlocked = true for a legitimately-owned Steam game.
        var apiKey = settings?.Steam?.ApiKey?.Trim();
        var steamId = settings?.Steam?.SteamId?.Trim();
        if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(steamId))
        {
            var liveAchievements = await TryGetPlayerAchievementsAsync(providerGameId, apiKey!, steamId!, cancellationToken);
            if (liveAchievements is { Count: > 0 })
            {
                await MergeGlobalPercentagesAsync(providerGameId, liveAchievements, cancellationToken);
                AchievementScoreCalculator.AssignScores(liveAchievements);

                return new GameAchievementSnapshot
                {
                    ProviderId = Descriptor.Id,
                    ProviderName = Descriptor.DisplayName,
                    ProviderGameId = providerGameId,
                    SyncedAtUtc = DateTime.UtcNow,
                    Achievements = liveAchievements
                };
            }
        }

        // No credentials configured, or the API call failed (private profile, game
        // has no stats, network error, etc.) — fall back to the anonymous community
        // page, which has no per-user data and always reports achievements as locked.
        var html = await _httpClient.GetStringAsync(
            $"https://steamcommunity.com/stats/{Uri.EscapeDataString(providerGameId)}/achievements/?l=english",
            cancellationToken);

        var achievements = ParseCommunityHtml(html).ToList();

        if (achievements.Count == 0)
        {
            achievements = await TryGetGlobalPercentageFallbackAsync(providerGameId, cancellationToken);
        }

        AchievementScoreCalculator.AssignScores(achievements);

        return new GameAchievementSnapshot
        {
            ProviderId = Descriptor.Id,
            ProviderName = Descriptor.DisplayName,
            ProviderGameId = providerGameId,
            SyncedAtUtc = DateTime.UtcNow,
            Achievements = achievements
        };
    }

    /// <summary>
    /// Calls the Steam Web API's GetPlayerAchievements endpoint, which is the only
    /// endpoint that can tell CloudBerry whether THIS user has actually unlocked each
    /// achievement. Returns null if the call fails or the profile/game has no stats.
    /// </summary>
    private async Task<List<AchievementRecord>?> TryGetPlayerAchievementsAsync(
        string appId,
        string apiKey,
        string steamId,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await _httpClient.GetStringAsync(
                $"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v0001/?appid={Uri.EscapeDataString(appId)}&key={Uri.EscapeDataString(apiKey)}&steamid={Uri.EscapeDataString(steamId)}&l=english",
                cancellationToken);

            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("playerstats", out var playerStats))
            {
                return null;
            }

            if (playerStats.TryGetProperty("success", out var successElement) &&
                successElement.ValueKind is JsonValueKind.False)
            {
                // Private profile, game not owned, or stats not public for this app.
                return null;
            }

            if (!playerStats.TryGetProperty("achievements", out var achievementsElement) ||
                achievementsElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var index = 0;
            var results = new List<AchievementRecord>();
            foreach (var item in achievementsElement.EnumerateArray())
            {
                var apiName = GetString(item, "apiname") ?? $"steam_achievement_{index}";
                var achieved = item.TryGetProperty("achieved", out var achievedElement) &&
                               achievedElement.ValueKind == JsonValueKind.Number &&
                               achievedElement.GetInt32() != 0;
                var unlockTimeSeconds = item.TryGetProperty("unlocktime", out var unlockElement) &&
                                        unlockElement.ValueKind == JsonValueKind.Number
                    ? unlockElement.GetInt64()
                    : 0L;
                var title = GetString(item, "name");
                var description = GetString(item, "description");

                results.Add(new AchievementRecord
                {
                    ApiName = apiName,
                    Title = string.IsNullOrWhiteSpace(title) ? FormatAchievementTitle(apiName) : title!,
                    Description = description ?? string.Empty,
                    IsHidden = false,
                    IsUnlocked = achieved,
                    UnlockedAtUtc = achieved && unlockTimeSeconds > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(unlockTimeSeconds).UtcDateTime
                        : null,
                    DisplayOrder = index++
                });
            }

            return results.Count > 0 ? results : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Fills in global rarity percentages (used for score weighting) on live player achievements.</summary>
    private async Task MergeGlobalPercentagesAsync(
        string appId,
        List<AchievementRecord> achievements,
        CancellationToken cancellationToken)
    {
        var globalPercentages = await TryGetGlobalPercentageFallbackAsync(appId, cancellationToken);
        if (globalPercentages.Count == 0)
        {
            return;
        }

        var byApiName = globalPercentages
            .Where(a => !string.IsNullOrWhiteSpace(a.ApiName))
            .GroupBy(a => a.ApiName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().GlobalUnlockPercent, StringComparer.OrdinalIgnoreCase);

        foreach (var achievement in achievements)
        {
            if (achievement.GlobalUnlockPercent is null &&
                byApiName.TryGetValue(achievement.ApiName, out var percent))
            {
                achievement.GlobalUnlockPercent = percent;
            }
        }
    }

    private static IEnumerable<AchievementRecord> ParseCommunityHtml(string html)
    {
        var blocks = AchievementBlockBoundaryRegex()
            .Matches(html)
            .Select(match => match.Value)
            .ToList();

        if (blocks.Count == 0)
        {
            blocks = AchievementBlockRegex()
                .Matches(html)
                .Select(match => match.Value)
                .ToList();
        }

        var achievements = new List<AchievementRecord>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < blocks.Count; index++)
        {
            var block = blocks[index];
            var title = Decode(FirstGroup(block, TitleRegex()));
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var percent = ParsePercent(FirstGroup(block, PercentRegex()));
            achievements.Add(new AchievementRecord
            {
                ApiName = BuildAchievementId(title, index, seenNames),
                Title = title,
                Description = Decode(FirstGroup(block, DescriptionRegex())),
                IconUrl = Decode(FirstGroup(block, ImageRegex())),
                LockedIconUrl = string.Empty,
                IsHidden = title.Contains("hidden", StringComparison.OrdinalIgnoreCase),
                IsUnlocked = false,
                GlobalUnlockPercent = percent,
                DisplayOrder = index,
                GamerScoreValue = AchievementScoreCalculator.Calculate(percent)
            });
        }

        return achievements;
    }

    private async Task<List<AchievementRecord>> TryGetGlobalPercentageFallbackAsync(
        string providerGameId,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await _httpClient.GetStringAsync(
                $"https://api.steampowered.com/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v0002/?gameid={Uri.EscapeDataString(providerGameId)}&format=json",
                cancellationToken);

            using var document = JsonDocument.Parse(json);
            if (!TryGetProperty(document.RootElement, out var achievementsElement, "achievementpercentages", "achievements"))
            {
                return [];
            }

            if (achievementsElement.ValueKind == JsonValueKind.Object &&
                TryGetProperty(achievementsElement, out var nestedAchievements, "achievements"))
            {
                achievementsElement = nestedAchievements;
            }

            if (achievementsElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return achievementsElement.EnumerateArray()
                .Select((item, index) =>
                {
                    var apiName = GetString(item, "name", "apiname") ?? $"steam_achievement_{index}";
                    var percent = GetDouble(item, "percent");
                    return new AchievementRecord
                    {
                        ApiName = apiName,
                        Title = FormatAchievementTitle(apiName),
                        Description = "Fallback achievement imported from Steam global achievement percentages.",
                        IsUnlocked = false,
                        GlobalUnlockPercent = percent,
                        DisplayOrder = index,
                        GamerScoreValue = AchievementScoreCalculator.Calculate(percent)
                    };
                })
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string BuildAchievementId(string title, int index, HashSet<string> seenNames)
    {
        var normalized = Regex.Replace(title.Trim().ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = $"steam_achievement_{index}";
        }

        var candidate = normalized;
        var suffix = 2;
        while (!seenNames.Add(candidate))
        {
            candidate = $"{normalized}_{suffix++}";
        }

        return candidate;
    }

    private static string Decode(string value)
    {
        return WebUtility.HtmlDecode(value).Trim();
    }

    private static string FirstGroup(string value, Regex regex)
    {
        var match = regex.Match(value);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static double? ParsePercent(string value)
    {
        value = WebUtility.HtmlDecode(value).Replace("%", string.Empty).Trim();
        return double.TryParse(value, out var percent) ? percent : null;
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement value, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetProperty(element, out var value, propertyName))
            {
                return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
            }
        }

        return null;
    }

    private static double? GetDouble(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetProperty(element, out var value, propertyName))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
                {
                    return number;
                }
            }
        }

        return null;
    }

    private static string FormatAchievementTitle(string value)
    {
        return string.Join(
            ' ',
            value
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(token => char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant()));
    }

    [GeneratedRegex(
        "<div class=\"achieveRow[^\"]*\">\\s*<div class=\"achieveImgHolder\">\\s*<img src=\"(?<image>[^\"]+)\"[^>]*>\\s*</div>\\s*<div class=\"achieveTxtHolder\">\\s*<div class=\"achieveFill\"[^>]*>\\s*</div>\\s*<div class=\"achievePercent\">(?<percent>[^<]+)</div>\\s*<div class=\"achieveTxt\">\\s*<h3>(?<title>.*?)</h3>\\s*<h5>(?<description>.*?)</h5>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled)]
    private static partial Regex AchievementBlockRegex();

    [GeneratedRegex("<div[^>]+class=\"[^\"]*achieveRow[^\"]*\"[\\s\\S]*?(?=<div[^>]+class=\"[^\"]*achieveRow|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AchievementBlockBoundaryRegex();

    [GeneratedRegex("<img[^>]+src=\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ImageRegex();

    [GeneratedRegex("<h3[^>]*>([\\s\\S]*?)</h3>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex TitleRegex();

    [GeneratedRegex("<h5[^>]*>([\\s\\S]*?)</h5>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DescriptionRegex();

    [GeneratedRegex("achievePercent[^>]*>([^<]+)<", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PercentRegex();
}
