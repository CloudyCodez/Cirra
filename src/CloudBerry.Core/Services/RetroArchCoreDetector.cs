using System.Text.RegularExpressions;

namespace CloudBerry.Core.Services;

public sealed class RetroArchCoreDetector
{
    private static readonly IReadOnlyList<RetroArchPlatformRule> PlatformRules =
    [
        new("NES", ["mesen", "fceumm", "nestopia"]),
        new("SNES", ["bsnes", "snes9x", "mesen-s"]),
        new("Nintendo 64", ["mupen64plus_next", "parallel_n64"]),
        new("Game Boy", ["gambatte", "mgba", "sameboy"]),
        new("Game Boy Color", ["gambatte", "mgba", "sameboy"]),
        new("Game Boy Advance", ["mgba", "gpsp", "vba_next"]),
        new("Nintendo DS", ["melonds", "desmume"]),
        new("Nintendo 3DS", ["citra", "citra_canary", "citra2018"]),
        new("GameCube", ["dolphin"]),
        new("Wii", ["dolphin"]),
        new("PlayStation", ["swanstation", "beetle_psx_hw", "beetle_psx", "pcsx_rearmed"]),
        new("PlayStation 2", ["pcsx2"]),
        new("PSP", ["ppsspp"]),
        new("Mega Drive", ["genesis_plus_gx", "picodrive"]),
        new("Dreamcast", ["flycast"]),
        new("Sega Saturn", ["beetle_saturn", "yabasanshiro", "yabause"])
    ];

    public RetroArchDetectionResult Detect(string? explicitRetroArchPath = null)
    {
        var retroArchPath = ResolveRetroArchPath(explicitRetroArchPath);
        if (string.IsNullOrWhiteSpace(retroArchPath))
        {
            return new RetroArchDetectionResult(
                string.Empty,
                string.Empty,
                [],
                [],
                "RetroArch was not found automatically. Browse to retroarch.exe, then scan cores again.");
        }

        var coresDirectory = Path.Combine(Path.GetDirectoryName(retroArchPath) ?? string.Empty, "cores");
        var installedCores = Directory.Exists(coresDirectory)
            ? Directory.EnumerateFiles(coresDirectory, "*_libretro.dll")
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

        var suggestions = PlatformRules
            .Select(rule => BuildSuggestion(rule, retroArchPath, installedCores))
            .Where(suggestion => suggestion is not null)
            .Cast<RetroArchPlatformSuggestion>()
            .ToList();

        var summary = installedCores.Count == 0
            ? $"Found RetroArch at {retroArchPath}, but no installed cores were found in {coresDirectory}."
            : $"Found RetroArch with {installedCores.Count} installed core(s). Cirra matched {suggestions.Count} platform mapping(s).";

        return new RetroArchDetectionResult(retroArchPath, coresDirectory, installedCores, suggestions, summary);
    }

    public static string BuildLaunchTemplate(string retroArchPath, string corePath) =>
        $"\"{retroArchPath}\" -L \"{corePath}\" {{rom}}";

    public static IReadOnlySet<string> ReadSupportedExtensions(string corePath)
    {
        if (string.IsNullOrWhiteSpace(corePath))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var coresDirectory = Path.GetDirectoryName(corePath);
        var retroArchDirectory = string.IsNullOrWhiteSpace(coresDirectory)
            ? null
            : Directory.GetParent(coresDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(retroArchDirectory))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var coreName = Path.GetFileNameWithoutExtension(corePath);
        var infoPath = Path.Combine(retroArchDirectory, "info", $"{coreName}.info");
        if (!File.Exists(infoPath))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var supportedLine = File.ReadLines(infoPath).FirstOrDefault(line =>
                line.TrimStart().StartsWith("supported_extensions", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(supportedLine))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var separator = supportedLine.IndexOf('=');
            if (separator < 0)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return supportedLine[(separator + 1)..]
                .Trim()
                .Trim('"')
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(extension => $".{extension.TrimStart('.')}".ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (UnauthorizedAccessException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static RetroArchPlatformSuggestion? BuildSuggestion(
        RetroArchPlatformRule rule,
        string retroArchPath,
        IReadOnlyList<string> installedCores)
    {
        var corePath = FindPreferredCore(rule, installedCores);

        return string.IsNullOrWhiteSpace(corePath)
            ? null
            : new RetroArchPlatformSuggestion(
                rule.Platform,
                Path.GetFileName(corePath),
                corePath,
                BuildLaunchTemplate(retroArchPath, corePath));
    }

    private static string? FindPreferredCore(
        RetroArchPlatformRule rule,
        IReadOnlyList<string> installedCores)
    {
        foreach (var preference in rule.CoreNamePreferences)
        {
            var exactMatch = installedCores.FirstOrDefault(core =>
                string.Equals(NormalizeCoreName(core), preference, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(exactMatch))
            {
                return exactMatch;
            }
        }

        foreach (var preference in rule.CoreNamePreferences)
        {
            var partialMatch = installedCores.FirstOrDefault(core =>
                NormalizeCoreName(core).Contains(preference, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(partialMatch))
            {
                return partialMatch;
            }
        }

        return null;
    }

    private static string NormalizeCoreName(string corePath) =>
        Path.GetFileNameWithoutExtension(corePath)
            .Replace("_libretro", string.Empty, StringComparison.OrdinalIgnoreCase);

    private static string ResolveRetroArchPath(string? explicitRetroArchPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitRetroArchPath) && File.Exists(explicitRetroArchPath))
        {
            return Path.GetFullPath(explicitRetroArchPath);
        }

        return FindOnEnvironmentPath()
            ?? FindViaSteamLibraries()
            ?? FindViaCommonRoots()
            ?? string.Empty;
    }

    /// <summary>Fastest check first — catches anyone who's added RetroArch to PATH.</summary>
    private static string? FindOnEnvironmentPath()
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVariable))
        {
            return null;
        }

        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), "retroarch.exe");
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
            catch (ArgumentException)
            {
                // Malformed PATH entry — skip it and keep scanning.
            }
        }

        return null;
    }

    /// <summary>
    /// RetroArch is distributed on Steam, so this checks the default Steam install plus every
    /// registered Steam library folder (parsed from libraryfolders.vdf) for steamapps\common\RetroArch.
    /// </summary>
    private static string? FindViaSteamLibraries()
    {
        var steamRoots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam")
        };

        var libraryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var steamRoot in steamRoots)
        {
            libraryPaths.Add(steamRoot);

            var manifest = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(manifest))
            {
                continue;
            }

            try
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(manifest), "\"path\"\\s*\"([^\"]+)\""))
                {
                    libraryPaths.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
                }
            }
            catch (IOException)
            {
                // Manifest locked or unreadable — fall back to whatever roots we already have.
            }
        }

        foreach (var library in libraryPaths)
        {
            var candidate = Path.Combine(library, "steamapps", "common", "RetroArch", "retroarch.exe");
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    private static readonly string[] RetroArchFolderNames = ["RetroArch-Win64", "RetroArch-Win32", "RetroArch"];

    private static readonly string[] RetroArchRootSubfolders =
    [
        string.Empty, "Emulators", "Emulation", "Games", Path.Combine("Games", "Emulators"),
        Path.Combine("Games", "Emulation"), "Applications", "Apps", "Tools", "Downloads"
    ];

    /// <summary>
    /// Broad, still-fast probe across Program Files/AppData/every fixed drive root, crossed with
    /// common emulator-folder names and both the official zip's folder name and the doubly-nested
    /// copy that shows up when someone extracts it without "extract here".
    /// </summary>
    private static string? FindViaCommonRoots()
    {
        var standardRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        var driveRoots = DriveInfo.GetDrives()
            .Where(drive => drive.DriveType is DriveType.Fixed && drive.IsReady)
            .Select(drive => drive.RootDirectory.FullName);

        var roots = standardRoots.Concat(driveRoots)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            foreach (var subfolder in RetroArchRootSubfolders)
            {
                var baseDirectory = string.IsNullOrEmpty(subfolder) ? root : Path.Combine(root, subfolder);
                foreach (var folderName in RetroArchFolderNames)
                {
                    var direct = Path.Combine(baseDirectory, folderName, "retroarch.exe");
                    if (File.Exists(direct))
                    {
                        return Path.GetFullPath(direct);
                    }

                    // Double-nested — what you get extracting the official zip without "extract here".
                    var nested = Path.Combine(baseDirectory, folderName, folderName, "retroarch.exe");
                    if (File.Exists(nested))
                    {
                        return Path.GetFullPath(nested);
                    }
                }
            }
        }

        return null;
    }

    private sealed record RetroArchPlatformRule(string Platform, IReadOnlyList<string> CoreNamePreferences);
}

public sealed record RetroArchDetectionResult(
    string RetroArchPath,
    string CoresDirectory,
    IReadOnlyList<string> InstalledCores,
    IReadOnlyList<RetroArchPlatformSuggestion> Suggestions,
    string Summary);

public sealed record RetroArchPlatformSuggestion(
    string Platform,
    string CoreName,
    string CorePath,
    string LaunchTemplate);
