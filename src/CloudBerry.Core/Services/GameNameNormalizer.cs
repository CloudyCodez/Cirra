using System.Text.RegularExpressions;

namespace CloudBerry.Core.Services;

public static partial class GameNameNormalizer
{
    private static readonly string[] NoiseTokens =
    [
        "x64",
        "x86",
        "portable",
        "goty",
        "definitive edition",
        "enhanced edition",
        "complete edition",
        "steamrip",
        "gog",
        "fitgirl"
    ];

    // ── Static sets for LooksLikeNonGamePayload — allocated once at startup,
    //    never again. Previously these were inline array literals reallocated
    //    on every call (called thousands of times during a library scan).
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll", ".pdb", ".lib", ".exp", ".obj", ".a",
        ".log", ".txt", ".cfg", ".config", ".ini", ".json",
        ".xml", ".yaml", ".yml", ".toml",
        ".pak", ".dat", ".db", ".sqlite", ".cache",
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico",
        ".mp3", ".ogg", ".wav", ".flac",
        ".mp4", ".avi", ".mkv",
        ".sys", ".drv", ".ocx",
        ".tmp", ".bak"
    };

    private static readonly HashSet<string> BlockedExactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "dxwebsetup", "ue4prereqsetup_x64", "ue4prereqsetup_x86",
        "ue5prereqsetup_x64", "ue5prereqsetup_x86", "dxsetup",
        "dotnetfx", "msvcr", "msvcp",
        "steam_api", "steam_api64", "steamclient", "steamclient64",
        "gog", "gog_galaxy",
        "easyanticheat", "easyanticheat_setup",
        "be_service", "bshelper", "battleye", "bedaisy",
        "crash_reporter", "crashreporter", "sentry", "hook"
    };

    // Stored as a plain array — we iterate linearly with Contains(OrdinalIgnoreCase)
    // which is O(n) but n is small and avoids HashSet overhead for substring checks.
    private static readonly string[] BlockedTokens =
    [
        "unins", "uninstall", "setup", "installer", "install",
        "patch", "crash", "crashreport", "crashhandler",
        "redist", "directx", "vc_redist", "vcredist",
        "supporttool", "benchmark", "launcherupdater",
        "autoupdater", "updater", "update", "patcher",
        "bugreport", "bugsplat", "dotnetruntime",
        "windowsdesktop", "aspnetcore", "prereq",
        "register", "activate", "drm", "safedisc", "securom", "denuvo",
        "helper", "service", "daemon", "tray", "monitor", "watchdog",
        "injector", "hook32", "hook64", "shader",
        "cefsubprocess", "subprocess", "webhelper",
        "singleinstancecheck", "gamesync", "overlay"
    ];

    private static readonly string[] BlockedFolders =
    [
        "/_commonredist/", "/commonredist/", "/redist/", "/redistributables/",
        "/support/", "/installer/", "/prereq/", "/prerequisites/",
        "/crashpad/", "/crash_reports/", "/dotnet/", "/directx/",
        "/vcredist/", "/physx/", "/easyanticheat/", "/battleye/",
        "/mono/", "/engine/", "/plugins/", "/tools/",
        "/logs/", "/log/", "/cache/", "/shadercache/", "/shader_cache/",
        "/saves/", "/savegames/", "/userdata/", "/localization/",
        "/locales/", "/intermediate/"
    ];

    [GeneratedRegex(@"\[[^\]]*\]|\([^\)]*\)", RegexOptions.Compiled)]
    private static partial Regex BracketNoiseRegex();

    [GeneratedRegex(@"[_\.]+", RegexOptions.Compiled)]
    private static partial Regex SeparatorNoiseRegex();

    [GeneratedRegex(@"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.Compiled)]
    private static partial Regex CamelCaseBoundaryRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex MultiSpaceRegex();

    public static string NormalizeTitleFromPath(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var title = BracketNoiseRegex().Replace(fileName, " ");
        title = SeparatorNoiseRegex().Replace(title, " ");
        title = CamelCaseBoundaryRegex().Replace(title, " ");

        foreach (var token in NoiseTokens)
        {
            title = Regex.Replace(title, $@"\b{Regex.Escape(token)}\b", " ", RegexOptions.IgnoreCase);
        }

        title = Regex.Replace(title, @"\b(v|ver|build)\s*\d+(\.\d+)*\b", " ", RegexOptions.IgnoreCase);
        title = Regex.Replace(title, @"\b\d{4}\b", match =>
        {
            var year = int.Parse(match.Value);
            return year is >= 1970 and <= 2099 ? $" {match.Value} " : " ";
        });

        title = MultiSpaceRegex().Replace(title, " ").Trim();
        title = title.Trim('-', '_', '.', ' ');

        return string.IsNullOrWhiteSpace(title)
            ? Path.GetFileNameWithoutExtension(path)
            : title;
    }

    public static bool LooksLikeNonGamePayload(string path)
    {
        var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();
        var fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        var extension = Path.GetExtension(path).ToLowerInvariant();

        // Extension block — O(1) HashSet lookup
        if (BlockedExtensions.Contains(extension))
            return true;

        // Exact filename block — O(1) HashSet lookup
        if (BlockedExactNames.Contains(fileName))
            return true;

        // Substring token block — linear over small static array, no allocation
        foreach (var token in BlockedTokens)
        {
            if (fileName.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Unity helper binaries
        if (fileName.StartsWith("unity", StringComparison.OrdinalIgnoreCase) &&
            !fileName.Equals("unity", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Path folder block — linear over small static array, no allocation
        foreach (var folder in BlockedFolders)
        {
            if (normalizedPath.Contains(folder, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
