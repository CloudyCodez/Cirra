namespace CloudBerry.Core.Models;

public sealed class GameRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string Platform { get; set; } = "Unknown";

    public string SourceKind { get; set; } = "Manual";

    public string InstallPath { get; set; } = string.Empty;

    public bool IsInstalled { get; set; } = true;

    public bool IsFavorite { get; set; }

    public string Status { get; set; } = "Unplayed";

    public DateTime AddedOn { get; set; } = DateTime.Now;

    public DateTime? LastPlayedOn { get; set; }

    public int PlaytimeMinutes { get; set; }

    public string Notes { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public string Summary { get; set; } = string.Empty;

    public DateTime? ReleaseDate { get; set; }

    public string CoverImageUrl { get; set; } = string.Empty;

    public string BackgroundImageUrl { get; set; } = string.Empty;

    public string BannerImageUrl { get; set; } = string.Empty;

    public string IconImageUrl { get; set; } = string.Empty;

    public string CachedCoverImagePath { get; set; } = string.Empty;

    public string CachedBackgroundImagePath { get; set; } = string.Empty;

    public string CachedBannerImagePath { get; set; } = string.Empty;

    public string CachedIconImagePath { get; set; } = string.Empty;

    public string WebsiteUrl { get; set; } = string.Empty;

    public string MetadataProviderId { get; set; } = string.Empty;

    public string MetadataProviderName { get; set; } = string.Empty;

    public string MetadataItemUrl { get; set; } = string.Empty;

    public double? CommunityRating { get; set; }

    public List<string> Genres { get; set; } = [];

    public List<string> Developers { get; set; } = [];

    public List<string> Publishers { get; set; } = [];

    public double ImportConfidence { get; set; } = 1.0;

    public List<string> ImportSignals { get; set; } = [];

    public string AccentStart { get; set; } = "#D94B65";

    public string AccentEnd { get; set; } = "#6E8DD8";

    public string SteamAppId { get; set; } = string.Empty;

    public string AchievementProviderId { get; set; } = string.Empty;

    public string AchievementProviderName { get; set; } = string.Empty;

    public DateTime? AchievementsLastSyncedUtc { get; set; }

    public List<AchievementRecord> Achievements { get; set; } = [];

    /// <summary>Remote screenshot URLs pulled from metadata (up to 8).</summary>
    public List<string> ScreenshotUrls { get; set; } = [];

    /// <summary>Direct MP4 URL for the primary game trailer, if available.</summary>
    public string TrailerUrl { get; set; } = string.Empty;

    /// <summary>
    /// 0–5 star rating set by the user for this game.
    /// Stored as a double so half-stars are supported (e.g. 4.5).
    /// Null means the user has not rated the game yet.
    /// </summary>
    public double? UserRating { get; set; }

    /// <summary>
    /// Aggregated critic/community rating on a 0–5 scale, sourced from
    /// whichever metadata provider last enriched this game.
    /// Null when no external rating has been pulled.
    /// </summary>
    public double? CriticRating { get; set; }

    /// <summary>
    /// What the user paid for this game in USD, entered manually. Null means unknown/free.
    /// Powers the Backlog Debt tracker (DIFFERENTIATORS.md #1) — dollars tied up in games
    /// that were bought but never played.
    /// </summary>
    public double? PurchasePriceUsd { get; set; }

    /// <summary>
    /// Override emulator path for this specific game entry.  When non-empty,
    /// this takes priority over the platform-level emulator configured in
    /// EmulatorSettings.  Useful for multi-emulator setups or edge cases.
    /// </summary>
    public string EmulatorPath { get; set; } = string.Empty;

    /// <summary>Play session history.</summary>
    public List<PlaySession> Sessions { get; set; } = [];

    public class PlaySession
    {
        public DateTime StartedAtLocal { get; set; }
        public DateTime? EndedAtLocal { get; set; }
        public int DurationMinutes { get; set; }

        /// <summary>Alias used by XAML bindings (StartedAt → StartedAtLocal).</summary>
        public DateTime StartedAt => StartedAtLocal;

        /// <summary>Human-readable duration label, e.g. "1h 23m" or "45m".</summary>
        public string DurationLabel
        {
            get
            {
                if (DurationMinutes <= 0) return "< 1m";
                var h = DurationMinutes / 60;
                var m = DurationMinutes % 60;
                if (h > 0 && m > 0) return $"{h}h {m}m";
                if (h > 0)          return $"{h}h";
                return $"{m}m";
            }
        }
    }
}
