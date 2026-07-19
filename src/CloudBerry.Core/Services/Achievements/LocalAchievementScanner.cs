using System.Globalization;
using System.Text.Json;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services.Achievements;

public sealed class LocalAchievementScanner
{
    private static readonly string[] SupportedExtensions =
    [
        ".json",
        ".ini",
        ".cfg",
        ".vdf",
        ".txt"
    ];

    public GameAchievementSyncResult LoadFromInstallPath(string installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath) || !File.Exists(installPath))
        {
            return new GameAchievementSyncResult();
        }

        var rootDirectory = Path.GetDirectoryName(Path.GetFullPath(installPath));
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            return new GameAchievementSyncResult();
        }

        var candidateFiles = FindCandidateFiles(rootDirectory).ToList();
        return LoadFromFiles(candidateFiles);
    }

    public GameAchievementSyncResult LoadFromFiles(IEnumerable<string> filePaths)
    {
        var parsedFiles = new List<string>();
        var merged = new Dictionary<string, AchievementRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in filePaths
                     .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var achievements = TryParseFile(filePath).ToList();
            if (achievements.Count == 0)
            {
                continue;
            }

            parsedFiles.Add(filePath);
            foreach (var achievement in achievements)
            {
                if (string.IsNullOrWhiteSpace(achievement.ApiName))
                {
                    continue;
                }

                if (!merged.TryGetValue(achievement.ApiName, out var existing))
                {
                    merged[achievement.ApiName] = achievement;
                    continue;
                }

                merged[achievement.ApiName] = Merge(existing, achievement);
            }
        }

        if (merged.Count == 0)
        {
            return new GameAchievementSyncResult();
        }

        var achievementList = merged.Values
            .OrderByDescending(achievement => achievement.IsUnlocked)
            .ThenBy(achievement => achievement.DisplayOrder)
            .ThenBy(achievement => achievement.Title)
            .ToList();

        AchievementScoreCalculator.AssignScores(achievementList);

        return new GameAchievementSyncResult
        {
            Snapshot = new GameAchievementSnapshot
            {
                ProviderId = "local",
                ProviderName = "Local file scan",
                SyncedAtUtc = DateTime.UtcNow,
                Achievements = achievementList
            },
            TrackingFilePaths = parsedFiles,
            SupportsLiveTracking = parsedFiles.Count > 0,
            SourceDescription = $"Parsed achievement data from {parsedFiles.Count} local file(s)."
        };
    }

    private static IEnumerable<string> FindCandidateFiles(string rootDirectory)
    {
        var pending = new Queue<(string Path, int Depth)>();
        pending.Enqueue((rootDirectory, 0));

        while (pending.Count > 0)
        {
            var (currentPath, depth) = pending.Dequeue();
            IEnumerable<string> files;
            IEnumerable<string> directories;

            try
            {
                files = Directory.EnumerateFiles(currentPath, "*", SearchOption.TopDirectoryOnly);
                directories = Directory.EnumerateDirectories(currentPath, "*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var filePath in files)
            {
                var extension = Path.GetExtension(filePath);
                var fileName = Path.GetFileName(filePath);
                if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!LooksLikeAchievementFile(fileName))
                {
                    continue;
                }

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 2_000_000)
                {
                    continue;
                }

                yield return filePath;
            }

            if (depth >= 4)
            {
                continue;
            }

            foreach (var directoryPath in directories)
            {
                pending.Enqueue((directoryPath, depth + 1));
            }
        }
    }

    private static IEnumerable<AchievementRecord> TryParseFile(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".json" => TryParseJsonFile(filePath),
            ".ini" or ".cfg" or ".vdf" or ".txt" => TryParseIniStyleFile(filePath),
            _ => []
        };
    }

    private static bool LooksLikeAchievementFile(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return lower.Contains("achievement", StringComparison.OrdinalIgnoreCase) ||
               lower.Contains("trophy", StringComparison.OrdinalIgnoreCase) ||
               lower.Contains("troph", StringComparison.OrdinalIgnoreCase) ||
               lower.Contains("unlock", StringComparison.OrdinalIgnoreCase) ||
               lower.Equals("stats.json", StringComparison.OrdinalIgnoreCase) ||
               lower.Equals("stats.ini", StringComparison.OrdinalIgnoreCase) ||
               lower.Equals("stats.vdf", StringComparison.OrdinalIgnoreCase) ||
               lower.Equals("user_stats.json", StringComparison.OrdinalIgnoreCase) ||
               lower.Equals("userstats.json", StringComparison.OrdinalIgnoreCase) ||
               lower.Equals("steam_autocloud.vdf", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<AchievementRecord> TryParseJsonFile(string filePath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(filePath));
            return ParseJsonElement(document.RootElement, Path.GetDirectoryName(filePath) ?? string.Empty).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<AchievementRecord> ParseJsonElement(JsonElement root, string baseDirectory)
    {
        return root.ValueKind switch
        {
            JsonValueKind.Array => root.EnumerateArray().Select((item, index) => ParseAchievementObject(item, baseDirectory, index))
                .Where(record => record is not null)
                .Cast<AchievementRecord>()
                .ToList(),
            JsonValueKind.Object => ParseJsonObject(root, baseDirectory),
            _ => []
        };
    }

    private static IEnumerable<AchievementRecord> ParseJsonObject(JsonElement root, string baseDirectory)
    {
        if (TryGetProperty(root, out var achievementsElement, "achievements", "Achievements", "achievement", "Achievement", "trophies", "Trophies", "items", "Items", "data", "Data"))
        {
            return achievementsElement.ValueKind switch
            {
                JsonValueKind.Array => achievementsElement.EnumerateArray().Select((item, index) => ParseAchievementObject(item, baseDirectory, index))
                    .Where(record => record is not null)
                    .Cast<AchievementRecord>()
                    .ToList(),
                JsonValueKind.Object => achievementsElement.EnumerateObject().Select((property, index) => ParseAchievementObject(property.Value, baseDirectory, index, property.Name))
                    .Where(record => record is not null)
                    .Cast<AchievementRecord>()
                    .ToList(),
                _ => []
            };
        }

        var nestedAchievements = root.EnumerateObject()
            .Where(property => property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            .SelectMany(property => ParseJsonElement(property.Value, baseDirectory))
            .ToList();
        if (nestedAchievements.Count > 0)
        {
            return nestedAchievements;
        }

        if (root.EnumerateObject().All(property => property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Number or JsonValueKind.String))
        {
            return root.EnumerateObject()
                .Select((property, index) => ParseAchievementObject(property.Value, baseDirectory, index, property.Name))
                .Where(record => record is not null)
                .Cast<AchievementRecord>()
                .ToList();
        }

        var singleRecord = ParseAchievementObject(root, baseDirectory, 0);
        return singleRecord is null ? [] : [singleRecord];
    }

    private static AchievementRecord? ParseAchievementObject(JsonElement element, string baseDirectory, int index, string? fallbackName = null)
    {
        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Number or JsonValueKind.String)
        {
            return new AchievementRecord
            {
                ApiName = fallbackName ?? $"achievement_{index}",
                Title = FormatAchievementTitle(fallbackName ?? $"achievement_{index}"),
                IsUnlocked = ParseBooleanLike(element),
                DisplayOrder = index
            };
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var apiName = GetString(element, "apiName", "apiname", "id", "name", "key", "statName", "achievementName") ?? fallbackName ?? $"achievement_{index}";
        var title = GetString(element, "displayName", "display_name", "title", "label");
        if (string.IsNullOrWhiteSpace(title))
        {
            var rawName = GetString(element, "name");
            title = string.IsNullOrWhiteSpace(rawName) ? FormatAchievementTitle(apiName) : FormatAchievementTitle(rawName);
        }
        var description = GetString(element, "description", "desc", "howToUnlock", "how_to_unlock", "unlockDescription") ?? string.Empty;
        var unlockedAt = GetDateTime(element, "unlockTime", "unlocktime", "unlockedAt", "unlocked_at", "timestamp", "time_unlocked");
        var rarity = GetDouble(element, "globalPercent", "globalUnlockPercent", "global_unlock_percent", "rarity", "percent", "percentage");

        return new AchievementRecord
        {
            ApiName = apiName,
            Title = title,
            Description = description,
            IconUrl = ResolvePathOrUrl(baseDirectory, GetString(element, "icon", "iconUrl", "icon_url", "image", "imageUrl")),
            LockedIconUrl = ResolvePathOrUrl(baseDirectory, GetString(element, "iconGray", "icongray", "lockedIcon", "locked_icon", "icon_locked")),
            IsHidden = GetBoolean(element, "hidden", "isHidden"),
            IsUnlocked = GetBoolean(element, "unlocked", "isUnlocked", "achieved", "earned", "isAchieved", "completed") || unlockedAt is not null,
            UnlockedAtUtc = unlockedAt,
            GlobalUnlockPercent = rarity,
            DisplayOrder = index,
            GamerScoreValue = GetInt(element, "gamerScore", "gamer_score", "score", "points", "value") ?? AchievementScoreCalculator.Calculate(rarity)
        };
    }

    private static IEnumerable<AchievementRecord> TryParseIniStyleFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var achievements = new List<AchievementRecord>();
        var index = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';') || line.StartsWith('#') || line.StartsWith('['))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            achievements.Add(new AchievementRecord
            {
                ApiName = key,
                Title = FormatAchievementTitle(key),
                IsUnlocked = value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                             value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                             value.Equals("unlocked", StringComparison.OrdinalIgnoreCase) ||
                             value == "1",
                DisplayOrder = index++
            });
        }

        return achievements;
    }

    private static AchievementRecord Merge(AchievementRecord left, AchievementRecord right)
    {
        return new AchievementRecord
        {
            ApiName = left.ApiName,
            Title = PickBest(left.Title, right.Title, FormatAchievementTitle(left.ApiName)),
            Description = PickBest(left.Description, right.Description),
            IconUrl = PickBest(left.IconUrl, right.IconUrl),
            LockedIconUrl = PickBest(left.LockedIconUrl, right.LockedIconUrl),
            IsHidden = left.IsHidden || right.IsHidden,
            IsUnlocked = left.IsUnlocked || right.IsUnlocked,
            UnlockedAtUtc = left.UnlockedAtUtc ?? right.UnlockedAtUtc,
            GlobalUnlockPercent = left.GlobalUnlockPercent ?? right.GlobalUnlockPercent,
            DisplayOrder = Math.Min(left.DisplayOrder, right.DisplayOrder),
            GamerScoreValue = Math.Max(left.GamerScoreValue, right.GamerScoreValue)
        };
    }

    private static string PickBest(string left, string right, string? fallback = null)
    {
        if (!string.IsNullOrWhiteSpace(left))
        {
            return left;
        }

        if (!string.IsNullOrWhiteSpace(right))
        {
            return right;
        }

        return fallback ?? string.Empty;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement value, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetProperty(element, propertyName, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetProperty(element, propertyName, out var value))
            {
                return value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Number => value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => null
                };
            }
        }

        return null;
    }

    private static bool GetBoolean(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetProperty(element, propertyName, out var value))
            {
                return ParseBooleanLike(value);
            }
        }

        return false;
    }

    private static bool ParseBooleanLike(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => value.TryGetInt64(out var numeric) && numeric > 0,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsedBool)
                                    ? parsedBool
                                    : (value.GetString() ?? string.Empty).Trim().ToLowerInvariant() is "1" or "yes" or "unlocked" or "achieved" or "earned" or "complete" or "completed",
            _ => false
        };
    }

    private static double? GetDouble(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetProperty(element, propertyName, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var numeric))
                {
                    return numeric;
                }

                if (value.ValueKind == JsonValueKind.String &&
                    double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out numeric))
                {
                    return numeric;
                }
            }
        }

        return null;
    }

    private static int? GetInt(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetProperty(element, propertyName, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
                {
                    return numeric;
                }

                if (value.ValueKind == JsonValueKind.String &&
                    int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric))
                {
                    return numeric;
                }
            }
        }

        return null;
    }

    private static DateTime? GetDateTime(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetProperty(element, propertyName, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var unixTime))
            {
                if (unixTime > 0)
                {
                    return DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
                }
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var stringValue = value.GetString();
                if (DateTime.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
                {
                    return parsed.ToUniversalTime();
                }

                if (long.TryParse(stringValue, out unixTime) && unixTime > 0)
                {
                    return DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
                }
            }
        }

        return null;
    }

    private static string ResolvePathOrUrl(string baseDirectory, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            return value;
        }

        var combinedPath = Path.Combine(baseDirectory, value);
        return File.Exists(combinedPath) ? Path.GetFullPath(combinedPath) : value;
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
}
