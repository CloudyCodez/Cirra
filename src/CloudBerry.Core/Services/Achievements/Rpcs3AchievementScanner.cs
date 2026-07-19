using System.Text.Json;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services.Achievements;

/// <summary>
/// Scans RPCS3's trophy directories for unlock data.
/// RPCS3 stores trophy data in:
///   %APPDATA%\RPCS3\dev_hdd0\home\00000001\trophy\NPWR*\TROPUSR.DAT (binary)
///   and a JSON sidecar it writes when trophies are earned.
///
/// CloudBerry reads the JSON sidecar (TROPCONF.SFM decoded or trophy*.json)
/// that RPCS3 exports when RA integration or the trophy viewer is used.
/// </summary>
public sealed class Rpcs3AchievementScanner
{
    private static readonly string[] SearchRoots =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "rpcs3"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RPCS3"),
        @"C:\rpcs3",
        @"D:\rpcs3",
    ];

    /// <summary>
    /// Given a PS3 game serial (e.g. BLUS30481) or title, try to find and load
    /// trophy data from RPCS3's local trophy directories.
    /// </summary>
    public GameAchievementSyncResult? TryLoadForGame(string gameTitle, string? gameSerial = null)
    {
        foreach (var root in SearchRoots)
        {
            var result = TryScanRoot(root, gameTitle, gameSerial);
            if (result is not null)
            {
                return result;
            }
        }
        return null;
    }

    private static GameAchievementSyncResult? TryScanRoot(string root, string gameTitle, string? gameSerial)
    {
        if (!Directory.Exists(root))
        {
            return null;
        }

        // RPCS3 trophy path: dev_hdd0/home/00000001/trophy/NPWR*/
        var trophyBase = Path.Combine(root, "dev_hdd0", "home", "00000001", "trophy");
        if (!Directory.Exists(trophyBase))
        {
            return null;
        }

        foreach (var npwrDir in Directory.EnumerateDirectories(trophyBase, "NPWR*"))
        {
            // Check for JSON sidecar files CloudBerry can read
            var candidates = Directory.EnumerateFiles(npwrDir, "*.json", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(npwrDir, "TROPHY.XML", SearchOption.AllDirectories))
                .ToList();

            if (candidates.Count == 0)
            {
                continue;
            }

            // Cheap title match: look for the game title inside the first JSON
            var firstJson = candidates.FirstOrDefault(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
            if (firstJson is null)
            {
                continue;
            }

            try
            {
                var text = File.ReadAllText(firstJson);
                if (!string.IsNullOrWhiteSpace(gameSerial) &&
                    !text.Contains(gameSerial, StringComparison.OrdinalIgnoreCase) &&
                    !text.Contains(gameTitle, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var achievements = ParseRpcs3TrophyJson(text, Path.GetDirectoryName(firstJson) ?? root);
                if (achievements.Count == 0)
                {
                    continue;
                }

                AchievementScoreCalculator.AssignScores(achievements);

                return new GameAchievementSyncResult
                {
                    Snapshot = new GameAchievementSnapshot
                    {
                        ProviderId = "rpcs3-trophies",
                        ProviderName = "RPCS3 Trophies",
                        SyncedAtUtc = DateTime.UtcNow,
                        Achievements = achievements
                    },
                    TrackingFilePaths = candidates,
                    SupportsLiveTracking = true,
                    SourceDescription = $"PS3 trophy data from RPCS3 ({Path.GetFileName(npwrDir)})."
                };
            }
            catch
            {
                continue;
            }
        }

        return null;
    }

    private static List<AchievementRecord> ParseRpcs3TrophyJson(string json, string baseDir)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Look for a trophies array
        JsonElement trophiesEl = default;
        if (!TryGetProperty(root, out trophiesEl, "trophies", "Trophies", "achievements", "trophy"))
        {
            return [];
        }

        var list = new List<AchievementRecord>();
        int order = 0;

        if (trophiesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in trophiesEl.EnumerateArray())
            {
                var record = ParseTrophyItem(item, order++, baseDir);
                if (record is not null)
                {
                    list.Add(record);
                }
            }
        }
        else if (trophiesEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in trophiesEl.EnumerateObject())
            {
                var record = ParseTrophyItem(prop.Value, order++, baseDir);
                if (record is not null)
                {
                    list.Add(record);
                }
            }
        }

        return list;
    }

    private static AchievementRecord? ParseTrophyItem(JsonElement el, int order, string baseDir)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var id = GetString(el, "id", "ID", "trophyId") ?? $"trophy_{order}";
        var name = GetString(el, "name", "Name", "title", "trophyName") ?? $"Trophy {order + 1}";
        var detail = GetString(el, "detail", "Detail", "description", "Description") ?? string.Empty;
        var iconFile = GetString(el, "icon", "iconPath", "image");
        var grade = GetString(el, "grade", "Grade", "type", "trophyType") ?? "B";
        var earned = GetBool(el, "earned", "isEarned", "unlocked", "Unlocked");
        var earnedDate = GetDateTime(el, "timestamp", "earned_at", "earnedDate");

        // Resolve icon path
        var iconUrl = string.Empty;
        if (!string.IsNullOrWhiteSpace(iconFile))
        {
            var combined = Path.Combine(baseDir, iconFile);
            iconUrl = File.Exists(combined) ? combined : iconFile;
        }

        // PS3 trophy grade → gamer score
        int score = grade.ToUpperInvariant() switch
        {
            "P" or "PLATINUM" => 180,
            "G" or "GOLD" => 90,
            "S" or "SILVER" => 30,
            _ => 15 // Bronze
        };

        return new AchievementRecord
        {
            ApiName = $"ps3_{id}",
            Title = name,
            Description = detail,
            IconUrl = iconUrl,
            IsHidden = false,
            IsUnlocked = earned || earnedDate.HasValue,
            UnlockedAtUtc = earnedDate,
            DisplayOrder = order,
            GamerScoreValue = score
        };
    }

    private static bool TryGetProperty(JsonElement el, out JsonElement value, params string[] names)
    {
        foreach (var name in names)
        {
            foreach (var prop in el.EnumerateObject())
            {
                if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    private static string? GetString(JsonElement el, params string[] names)
    {
        if (TryGetProperty(el, out var v, names))
        {
            return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }
        return null;
    }

    private static bool GetBool(JsonElement el, params string[] names)
    {
        if (TryGetProperty(el, out var v, names))
        {
            return v.ValueKind == JsonValueKind.True ||
                   (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) && n > 0) ||
                   (v.ValueKind == JsonValueKind.String && (v.GetString() is "1" or "true" or "yes"));
        }
        return false;
    }

    private static DateTime? GetDateTime(JsonElement el, params string[] names)
    {
        if (TryGetProperty(el, out var v, names))
        {
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var unix) && unix > 0)
            {
                return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
            }
            if (v.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(v.GetString(), out var parsed))
            {
                return parsed.ToUniversalTime();
            }
        }
        return null;
    }
}
