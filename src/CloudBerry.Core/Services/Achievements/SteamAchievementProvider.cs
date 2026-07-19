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
            throw new InvalidOperationException("CloudBerry needs a Steam App ID before it can pull fallback achievement metadata.");
        }

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
