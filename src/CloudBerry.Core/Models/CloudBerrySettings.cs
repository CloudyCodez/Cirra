namespace CloudBerry.Core.Models;

public sealed class CloudBerrySettings
{
    public const string BuiltInIgdbProxyEndpoint = "https://cloudberry-igdb-proxy.cloudberryxo.workers.dev";
    public const string BuiltInSocialEndpoint = "https://cloudberry-social.cloudberryxo.workers.dev";
    public const string BuiltInUpdateFeedEndpoint = "https://cloudberry-updates.pages.dev/latest.json";

    /// <summary>CloudBerry uses the unified IGDB-first metadata pipeline by default.</summary>
    public string SelectedMetadataProviderId { get; set; } = "aggregated";

    public RawgMetadataSettings Rawg { get; set; } = new();

    public IgdbMetadataSettings Igdb { get; set; } = new();

    public string SelectedAchievementProviderId { get; set; } = "cloudberry-universal";

    public SteamAchievementSettings Steam { get; set; } = new();

    // ── RetroAchievements ────────────────────────────────────────────────────
    /// <summary>
    /// Free Web API key from retroachievements.org/settings.
    /// Required for personal unlock status; metadata-only works without a key.
    /// </summary>
    public string RetroAchievementsApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Your retroachievements.org username. Used alongside the API key to
    /// fetch personal trophy/achievement progress for emulated games.
    /// </summary>
    public string RetroAchievementsUsername { get; set; } = string.Empty;

    // ── Achievement UX ───────────────────────────────────────────────────────
    /// <summary>
    /// Show the Xbox/PSN-style "achievement unlocked" pop toast when a new
    /// achievement is detected in real time. Default true.
    /// </summary>
    public bool AchievementPopEnabled { get; set; } = true;

    /// <summary>
    /// How many seconds the achievement pop toast is visible before sliding out.
    /// Range 2–10. Default 5.
    /// </summary>
    public int AchievementPopDurationSeconds { get; set; } = 5;

    /// <summary>
    /// Corner of the screen where the achievement pop appears.
    /// Values: "BottomRight" (default), "BottomLeft", "TopRight", "TopLeft".
    /// </summary>
    public string AchievementPopCorner { get; set; } = "BottomRight";

    /// <summary>Per-platform emulator executable paths configured by the user.</summary>
    public EmulatorSettings Emulators { get; set; } = new();

    /// <summary>Visual personalization preferences for the desktop shell.</summary>
    public AppearanceSettings Appearance { get; set; } = new();

    /// <summary>Audio personalization preferences for UI sound design.</summary>
    public AudioSettings Audio { get; set; } = new();

    /// <summary>Optional external runtimes CloudBerry can use for advanced helper jobs.</summary>
    public CompanionToolsSettings CompanionTools { get; set; } = new();

    /// <summary>Profile cards, personalization, and the currently active local player profile.</summary>
    public ProfileSettings Profiles { get; set; } = new();

    /// <summary>Automatic update settings for the desktop app.</summary>
    public UpdateSettings Updates { get; set; } = new();

    /// <summary>Optional free host sources used by the hidden Cloud's Theatre surface.</summary>
    public TheatreSettings Theatre { get; set; } = new();

    /// <summary>User-created, manually-curated game collections (Library "My Collections" section).</summary>
    public CollectionSettings Collections { get; set; } = new();
}

public sealed class RawgMetadataSettings
{
    public string ApiKey { get; set; } = string.Empty;
}

public sealed class IgdbMetadataSettings
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string ProxyEndpoint { get; set; } = CloudBerrySettings.BuiltInIgdbProxyEndpoint;

    public bool ProxyEnabled { get; set; } = true;

    /// <summary>
    /// Serialised as "clientId:clientSecret" so it fits in the single API-key text box in the UI.
    /// </summary>
    public string CombinedKey
    {
        get => string.IsNullOrWhiteSpace(ClientId) && string.IsNullOrWhiteSpace(ClientSecret)
            ? string.Empty
            : $"{ClientId}:{ClientSecret}";
    }

    public void SetFromCombined(string combined)
    {
        if (string.IsNullOrWhiteSpace(combined))
        {
            ClientId = ClientSecret = string.Empty;
            return;
        }

        var idx = combined.IndexOf(':', StringComparison.Ordinal);
        if (idx < 0)
        {
            ClientId = combined.Trim();
            ClientSecret = string.Empty;
        }
        else
        {
            ClientId = combined[..idx].Trim();
            ClientSecret = combined[(idx + 1)..].Trim();
        }
    }
}

public sealed class SteamAchievementSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public string SteamId { get; set; } = string.Empty;

    public int PollingIntervalSeconds { get; set; } = 30;
}

public sealed class AppearanceSettings
{
    public string ThemePreset { get; set; } = "CloudBerry Dark";

    public string Density { get; set; } = "Comfortable";

    public int HomeRotationSeconds { get; set; } = 18;

    public bool VisualEffectsEnabled { get; set; } = true;

    public double VisualEffectIntensity { get; set; } = 1.0;

    /// <summary>
    /// Which view opens on launch. "CloudBerry" (default) opens the desktop shell;
    /// "Cloudscape" immediately opens the full-screen Big Picture mode.
    /// </summary>
    public string StartupView { get; set; } = "CloudBerry";

    /// <summary>Per-widget show/hide preferences for the Home page hero stat bar.</summary>
    public HomeWidgetSettings HomeWidgets { get; set; } = new();

    /// <summary>
    /// Library browse layout. "List" (default) shows the dense row layout;
    /// "Grid" shows a Playnite/GOG-Galaxy-style cover-art tile grid.
    /// </summary>
    public string LibraryViewMode { get; set; } = "List";

    /// <summary>
    /// Cover tile size used when <see cref="LibraryViewMode"/> is "Grid".
    /// Values: "Compact", "Normal" (default), "Large".
    /// </summary>
    public string LibraryGridTileSize { get; set; } = "Normal";

    /// <summary>
    /// Whether the sidebar's collapsible "More maintenance" action group (Scan ROMs,
    /// Scan Steam, Scan Launchers, Scan All, Repair Library, Polish Library, Clean Missing)
    /// is expanded. Defaults to collapsed so a fresh sidebar stays short.
    /// </summary>
    public bool SidebarMaintenanceExpanded { get; set; } = false;
}

/// <summary>
/// Show/hide toggles for the five Home page stat cards (PS5-style Home customization).
/// All default to true so existing installs render identically until the user opts out.
/// </summary>
public sealed class HomeWidgetSettings
{
    public bool ShowLibrary { get; set; } = true;

    public bool ShowMetadata { get; set; } = true;

    public bool ShowAchievements { get; set; } = true;

    public bool ShowGamerScore { get; set; } = true;

    public bool ShowRunning { get; set; } = true;

    /// <summary>Sixth Home stat card: hours played this week + current daily play streak.</summary>
    public bool ShowPlaytime { get; set; } = true;
}

public sealed class AudioSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>Subtle, Balanced, or Cinematic.</summary>
    public string Profile { get; set; } = "Subtle";
}

public sealed class ProfileSettings
{
    public string ActiveProfileId { get; set; } = string.Empty;

    public List<CloudBerryProfile> Items { get; set; } = [];

    public List<CloudBerryFriend> Friends { get; set; } = [];

    public string PresenceEndpoint { get; set; } = CloudBerrySettings.BuiltInSocialEndpoint;
}

public sealed class UpdateSettings
{
    public string FeedEndpoint { get; set; } = CloudBerrySettings.BuiltInUpdateFeedEndpoint;

    public bool AutoCheckEnabled { get; set; } = true;

    public bool AutoDownloadEnabled { get; set; } = true;

    public bool InstallOnExit { get; set; } = true;
}

public sealed class TheatreSettings
{
    public bool IsUnlocked { get; set; }

    /// <summary>User-saved sites shown in the Theatre sidebar.</summary>
    public List<TheatreSavedSite> SavedSites { get; set; } =
    [
        new TheatreSavedSite { DisplayName = "YouTube",      Url = "https://www.youtube.com" },
        new TheatreSavedSite { DisplayName = "Twitch",       Url = "https://www.twitch.tv" },
        new TheatreSavedSite { DisplayName = "Archive.org",  Url = "https://archive.org" },
    ];
}

public sealed class TheatreSavedSite
{
    public string DisplayName { get; set; } = "My Site";
    public string Url         { get; set; } = string.Empty;
}

/// <summary>Holds every user-created manual collection (name + explicit game membership).</summary>
public sealed class CollectionSettings
{
    public List<CloudBerryCollection> Items { get; set; } = [];
}

/// <summary>
/// A single user-created collection: a name plus an explicit, ordered list of game IDs.
/// Fully manual by design — no auto-sorting by genre/tag/platform. Created and managed via
/// the Library page's "New Collection" flow and the CollectionEditorWindow.
/// </summary>
public sealed class CloudBerryCollection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "New Collection";

    public List<string> GameIds { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

// Legacy — kept for settings migration; no longer written
public sealed class TheatreHostSource
{
    public string DisplayName        { get; set; } = "Free Source";
    public string SearchUrlTemplate  { get; set; } = string.Empty;
    public string CategoryUrl        { get; set; } = string.Empty;
}

public sealed class CloudBerryProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string CloudCode { get; set; } = string.Empty;

    public string IconKey { get; set; } = "Controller";

    public string DisplayName { get; set; } = "Player One";

    public string Motto { get; set; } = "Library ready.";

    public string PresenceLine { get; set; } = "Signed in locally";

    public string HighlightTone { get; set; } = "Arcade Gold";

    public string ShellStyle { get; set; } = "Blade";

    public string PreferredTheme { get; set; } = "CloudBerry Dark";

    public bool BroadcastPresence { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastUsedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CloudBerryFriend
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string CloudCode { get; set; } = string.Empty;

    public string IconKey { get; set; } = "Controller";

    public string DisplayName { get; set; } = "CloudBerry Friend";

    public string Motto { get; set; } = "Library ready.";

    public string PresenceLine { get; set; } = "Signed in locally";

    public string HighlightTone { get; set; } = "Arcade Gold";

    public string ShellStyle { get; set; } = "Blade";

    public string PreferredTheme { get; set; } = "CloudBerry Dark";

    public string FavoritePlatform { get; set; } = "Unknown";

    public int LibraryCount { get; set; }

    public int AchievementCount { get; set; }

    public int GamerScore { get; set; }

    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsOnline { get; set; }
}

public sealed class CompanionToolsSettings
{
    public bool AutoDetect { get; set; } = true;

    public string PythonPath { get; set; } = string.Empty;

    public string NodePath { get; set; } = string.Empty;

    public string PowerShellPath { get; set; } = string.Empty;

    public string FfmpegPath { get; set; } = string.Empty;
}

/// <summary>
/// Maps platform names to the path of the emulator executable that should
/// launch ROMs for that platform.  Keys are the same strings GuessPlatform
/// returns so the launcher can look them up directly.
/// </summary>
public sealed class EmulatorSettings
{
    /// <summary>Optional RetroArch executable path used by the setup assistant.</summary>
    public string RetroArchPath           { get; set; } = string.Empty;

    // Nintendo
    public string NesEmulatorPath        { get; set; } = string.Empty;  // NES
    public string SnesEmulatorPath       { get; set; } = string.Empty;  // SNES
    public string N64EmulatorPath        { get; set; } = string.Empty;  // Nintendo 64
    public string GbEmulatorPath         { get; set; } = string.Empty;  // Game Boy
    public string GbcEmulatorPath        { get; set; } = string.Empty;  // Game Boy Color
    public string GbaEmulatorPath        { get; set; } = string.Empty;  // Game Boy Advance
    public string NdsEmulatorPath        { get; set; } = string.Empty;  // Nintendo DS
    public string ThreeDsEmulatorPath    { get; set; } = string.Empty;  // Nintendo 3DS
    public string GameCubeEmulatorPath   { get; set; } = string.Empty;  // GameCube
    public string WiiEmulatorPath        { get; set; } = string.Empty;  // Wii

    // Sony
    public string Ps1EmulatorPath        { get; set; } = string.Empty;  // PlayStation
    public string Ps2EmulatorPath        { get; set; } = string.Empty;  // PlayStation 2
    public string Ps3EmulatorPath        { get; set; } = string.Empty;  // PlayStation 3
    public string PspEmulatorPath        { get; set; } = string.Empty;  // PSP

    // Sega
    public string MegaDriveEmulatorPath  { get; set; } = string.Empty;  // Mega Drive / Genesis
    public string DreamcastEmulatorPath  { get; set; } = string.Empty;  // Dreamcast
    public string SaturnEmulatorPath     { get; set; } = string.Empty;  // Sega Saturn

    // Nintendo / Microsoft newer systems
    public string WiiUEmulatorPath       { get; set; } = string.Empty;  // Wii U
    public string SwitchEmulatorPath     { get; set; } = string.Empty;  // Nintendo Switch
    public string XboxEmulatorPath       { get; set; } = string.Empty;  // Xbox
    public string Xbox360EmulatorPath    { get; set; } = string.Empty;  // Xbox 360

    // Generic fallback
    public string DefaultEmulatorPath    { get; set; } = string.Empty;  // Used when platform has no specific entry

    /// <summary>
    /// Returns the emulator path for the given platform name, falling back to
    /// the default emulator when no specific entry is configured.
    /// Returns an empty string when neither is configured.
    /// </summary>
    public string Resolve(string platform) => platform switch
    {
        "NES"                => NesEmulatorPath,
        "SNES"               => SnesEmulatorPath,
        "Nintendo 64"        => N64EmulatorPath,
        "Game Boy"           => GbEmulatorPath,
        "Game Boy Color"     => GbcEmulatorPath,
        "Game Boy Advance"   => GbaEmulatorPath,
        "Nintendo DS"        => NdsEmulatorPath,
        "Nintendo 3DS"       => ThreeDsEmulatorPath,
        "GameCube"           => GameCubeEmulatorPath,
        "Wii"                => WiiEmulatorPath,
        "PlayStation"        => Ps1EmulatorPath,
        "PlayStation 2"      => Ps2EmulatorPath,
        "PlayStation 3"      => Ps3EmulatorPath,
        "PSP"                => PspEmulatorPath,
        "Mega Drive"         => MegaDriveEmulatorPath,
        "Dreamcast"          => DreamcastEmulatorPath,
        "Sega Saturn"        => SaturnEmulatorPath,
        "Wii U"              => WiiUEmulatorPath,
        "Nintendo Switch"    => SwitchEmulatorPath,
        "Xbox"               => XboxEmulatorPath,
        "Xbox 360"           => Xbox360EmulatorPath,
        _                    => string.IsNullOrWhiteSpace(DefaultEmulatorPath)
                                    ? string.Empty
                                    : DefaultEmulatorPath
    };
}
