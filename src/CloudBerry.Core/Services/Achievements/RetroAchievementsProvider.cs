using System.Net;
using System.Text.Json;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services.Achievements;

/// <summary>
/// Pulls achievement metadata and earned status from RetroAchievements.org.
/// Covers every console they support (NES, SNES, N64, GB/GBC/GBA, DS, GCN, Wii,
/// PS1, PS2, PSP, Saturn, Dreamcast, Mega Drive, Atari, Neo Geo, and more).
///
/// API key setup: Settings → Achievements → RetroAchievements API Key.
/// Free account at retroachievements.org. The key goes to _settings.RetroAchievementsApiKey.
/// Without a key, metadata-only (no unlock status) is returned via the public endpoint.
/// </summary>
public sealed class RetroAchievementsProvider : IGameAchievementProvider
{
    private const string BaseUrl = "https://retroachievements.org/API";
    private readonly HttpClient _http;

    public RetroAchievementsProvider(HttpClient httpClient)
    {
        _http = httpClient;
    }

    public AchievementProviderDescriptor Descriptor { get; } = new()
    {
        Id = "retroachievements",
        DisplayName = "RetroAchievements",
        Description = "Fetches real achievements for emulated games from retroachievements.org. " +
                      "Covers 500+ consoles. Requires a free API key and your RA username.",
        ApiKeyLabel = "RetroAchievements API Key",
        ApiKeyHelpText = "Free key from retroachievements.org/settings — copy the Web API Key field.",
        PlayerIdLabel = "RetroAchievements Username",
        PlayerIdHelpText = "Your retroachievements.org username. Used to fetch your personal unlock status."
    };

    public async Task<GameAchievementSnapshot> GetAchievementsAsync(
        GameAchievementRequest request,
        CloudBerrySettings settings,
        CancellationToken cancellationToken = default)
    {
        var apiKey = settings.RetroAchievementsApiKey?.Trim() ?? string.Empty;
        var username = (!string.IsNullOrWhiteSpace(request.PlayerId) ? request.PlayerId : settings.RetroAchievementsUsername)?.Trim() ?? string.Empty;

        // Step 1: resolve game ID from title if not given
        var raGameId = request.ProviderGameId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raGameId) && !string.IsNullOrWhiteSpace(request.Title))
        {
            raGameId = await SearchGameIdAsync(request.Title, apiKey, username, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(raGameId))
        {
            return EmptySnapshot(request);
        }

        // Step 2: fetch achievement list + user progress
        List<AchievementRecord> achievements;
        if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(username))
        {
            achievements = await FetchWithUserProgressAsync(raGameId, username, apiKey, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(apiKey))
        {
            achievements = await FetchMetadataOnlyAsync(raGameId, apiKey, cancellationToken);
        }
        else
        {
            // No key: use public game info endpoint (metadata only, no rarity or unlock status)
            achievements = await FetchPublicMetadataAsync(raGameId, cancellationToken);
        }

        return new GameAchievementSnapshot
        {
            ProviderId = Descriptor.Id,
            ProviderName = Descriptor.DisplayName,
            ProviderGameId = raGameId,
            SyncedAtUtc = DateTime.UtcNow,
            Achievements = achievements
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<string> SearchGameIdAsync(
        string title,
        string apiKey,
        string username,
        CancellationToken ct)
    {
        // RA search API requires a key
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return string.Empty;
        }

        try
        {
            var encoded = Uri.EscapeDataString(title);
            var url = $"{BaseUrl}/API_GetGameList.php?z={Uri.EscapeDataString(username)}&y={apiKey}&i=1"; // console id 1 = NES fallback for search
            // Actually use the search endpoint
            var searchUrl = $"{BaseUrl}/API_SearchGameByTitle.php?z={Uri.EscapeDataString(username)}&y={apiKey}&t={encoded}";
            var json = await _http.GetStringAsync(searchUrl, ct);
            using var doc = JsonDocument.Parse(json);

            // Response is an array of game objects
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (TryGetString(item, out var id, "ID", "GameID", "id") &&
                        !string.IsNullOrWhiteSpace(id))
                    {
                        return id;
                    }
                }
            }

            // Fallback: try GetGameID endpoint
            var altUrl = $"{BaseUrl}/API_GetGameID.php?z={Uri.EscapeDataString(username)}&y={apiKey}&t={encoded}";
            var altJson = await _http.GetStringAsync(altUrl, ct);
            using var altDoc = JsonDocument.Parse(altJson);
            if (TryGetString(altDoc.RootElement, out var altId, "GameID", "ID"))
            {
                return altId ?? string.Empty;
            }
        }
        catch
        {
            // swallow – no RA ID resolved
        }

        return string.Empty;
    }

    private async Task<List<AchievementRecord>> FetchWithUserProgressAsync(
        string gameId, string username, string apiKey, CancellationToken ct)
    {
        var url = $"{BaseUrl}/API_GetGameInfoAndUserProgress.php" +
                  $"?z={Uri.EscapeDataString(username)}&y={apiKey}&u={Uri.EscapeDataString(username)}&g={gameId}";

        var json = await _http.GetStringAsync(url, ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!TryGetProperty(root, out var achievementsEl, "Achievements"))
        {
            return [];
        }

        // Total distinct players who've played the game — needed to turn each
        // achievement's raw NumAwarded count into an actual unlock percentage.
        var totalPlayers = TryGetDouble(root, out var casualPlayers, "NumDistinctPlayersCasual")
            ? casualPlayers
            : (TryGetDouble(root, out var hardcorePlayers, "NumDistinctPlayersHardcore") ? hardcorePlayers : null);

        return ParseAchievementObject(achievementsEl, withUserData: true, totalPlayers);
    }

    private async Task<List<AchievementRecord>> FetchMetadataOnlyAsync(
        string gameId, string apiKey, CancellationToken ct)
    {
        var url = $"{BaseUrl}/API_GetGameExtended.php?y={apiKey}&i={gameId}";
        var json = await _http.GetStringAsync(url, ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!TryGetProperty(root, out var achievementsEl, "Achievements"))
        {
            return [];
        }

        var totalPlayers = TryGetDouble(root, out var casualPlayers, "NumDistinctPlayersCasual")
            ? casualPlayers
            : (TryGetDouble(root, out var hardcorePlayers, "NumDistinctPlayersHardcore") ? hardcorePlayers : null);

        return ParseAchievementObject(achievementsEl, withUserData: false, totalPlayers);
    }

    private async Task<List<AchievementRecord>> FetchPublicMetadataAsync(
        string gameId, CancellationToken ct)
    {
        // Public endpoint – no key needed. This legacy page doesn't reliably expose a
        // distinct-player total, so rarity is intentionally omitted here rather than guessed.
        var url = $"https://retroachievements.org/game/{gameId}/json";
        try
        {
            var json = await _http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            if (TryGetProperty(doc.RootElement, out var achievementsEl, "achievements", "Achievements"))
            {
                return ParseAchievementObject(achievementsEl, withUserData: false, totalPlayers: null);
            }
        }
        catch
        {
            // ignore – no public data
        }
        return [];
    }

    private static List<AchievementRecord> ParseAchievementObject(JsonElement achievementsEl, bool withUserData, double? totalPlayers)
    {
        var list = new List<AchievementRecord>();
        int order = 0;

        // Can be an object (keyed by ID) or an array
        if (achievementsEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in achievementsEl.EnumerateObject())
            {
                var record = ParseSingle(prop.Value, order++, withUserData, totalPlayers);
                if (record is not null)
                {
                    list.Add(record);
                }
            }
        }
        else if (achievementsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in achievementsEl.EnumerateArray())
            {
                var record = ParseSingle(item, order++, withUserData, totalPlayers);
                if (record is not null)
                {
                    list.Add(record);
                }
            }
        }

        return list;
    }

    private static AchievementRecord? ParseSingle(JsonElement el, int order, bool withUserData, double? totalPlayers)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        TryGetString(el, out var id, "ID", "AchievementID");
        TryGetString(el, out var title, "Title", "title");
        TryGetString(el, out var description, "Description", "description");
        TryGetString(el, out var badgeUrl, "BadgeURL", "BadgeName");
        TryGetDouble(el, out var numAwarded, "NumAwarded");

        // The real rarity percent is NumAwarded / total distinct players * 100.
        // "TrueRatio" (aka Retro Ratio) is NOT a percentage — it's a difficulty-weighted
        // point value (base Points times a rarity multiplier, per RA's own scoring docs),
        // commonly landing in the same 1-100-ish numeric range as a percent would, which
        // made it easy to mistake for one. Using it directly as GlobalUnlockPercent gave
        // systematically wrong rarity/scoring. NumAwarded alone is a raw unlock COUNT
        // (can run into the thousands), never a percent on its own either — it only
        // becomes meaningful once divided by the total player count for this game.
        double? rarityPercent = null;
        if (numAwarded.HasValue && totalPlayers.HasValue && totalPlayers > 0)
        {
            var computed = numAwarded.Value / totalPlayers.Value * 100.0;
            rarityPercent = computed is > 0 and <= 100 ? computed : null;
        }

        // Resolve badge URL
        var iconUrl = string.Empty;
        if (!string.IsNullOrWhiteSpace(badgeUrl))
        {
            iconUrl = badgeUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? badgeUrl
                : $"https://media.retroachievements.org/Badge/{badgeUrl}.png";
        }

        bool isUnlocked = false;
        DateTime? unlockedAt = null;

        if (withUserData)
        {
            // DateEarned / DateEarnedHardcore are Unix timestamps
            TryGetLong(el, out var dateEarned, "DateEarned", "DateEarnedHardcore", "date_earned");
            if (dateEarned.HasValue && dateEarned > 0)
            {
                isUnlocked = true;
                unlockedAt = DateTimeOffset.FromUnixTimeSeconds(dateEarned.Value).UtcDateTime;
            }
        }

        return new AchievementRecord
        {
            ApiName = !string.IsNullOrWhiteSpace(id) ? $"ra_{id}" : $"ra_achievement_{order}",
            Title = WebUtility.HtmlDecode(title?.Trim() ?? $"Achievement {order + 1}"),
            Description = WebUtility.HtmlDecode(description?.Trim() ?? string.Empty),
            IconUrl = iconUrl,
            LockedIconUrl = iconUrl.Replace(".png", "_lock.png"),
            IsHidden = false,
            IsUnlocked = isUnlocked,
            UnlockedAtUtc = unlockedAt,
            GlobalUnlockPercent = rarityPercent,
            DisplayOrder = order,
            GamerScoreValue = AchievementScoreCalculator.Calculate(rarityPercent)
        };
    }

    private static GameAchievementSnapshot EmptySnapshot(GameAchievementRequest request) => new()
    {
        ProviderId = "retroachievements",
        ProviderName = "RetroAchievements",
        ProviderGameId = request.ProviderGameId,
        SyncedAtUtc = DateTime.UtcNow,
        Achievements = []
    };

    // ── JSON helpers ──────────────────────────────────────────────────────────

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

    private static bool TryGetString(JsonElement el, out string? value, params string[] names)
    {
        if (TryGetProperty(el, out var prop, names))
        {
            value = prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.GetRawText().Trim('"');
            return !string.IsNullOrWhiteSpace(value);
        }
        value = null;
        return false;
    }

    private static bool TryGetDouble(JsonElement el, out double? value, params string[] names)
    {
        if (TryGetProperty(el, out var prop, names) &&
            prop.ValueKind == JsonValueKind.Number &&
            prop.TryGetDouble(out var d))
        {
            value = d;
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryGetLong(JsonElement el, out long? value, params string[] names)
    {
        if (TryGetProperty(el, out var prop, names))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n))
            {
                value = n;
                return true;
            }
            if (prop.ValueKind == JsonValueKind.String &&
                long.TryParse(prop.GetString(), out var s))
            {
                value = s;
                return true;
            }
        }
        value = null;
        return false;
    }
}
