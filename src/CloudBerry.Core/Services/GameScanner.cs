using CloudBerry.Core.Models;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CloudBerry.Core.Services;

public sealed class GameScanner
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe",
        ".lnk",
        ".bat",
        ".cmd",
        ".url",
        ".iso",
        ".cue",
        ".chd",
        ".bin",
        ".cso",
        ".rvz",
        ".wbfs",
        ".nes",
        ".sfc",
        ".smc",
        ".gba",
        ".gb",
        ".gbc",
        ".n64",
        ".z64",
        ".v64",
        ".nds",
        ".3ds",
        ".3dsx",
        ".cci",
        ".cxi",
        ".axf",
        ".cia",
        ".xci",
        ".nsp",
        ".nca",
        ".gcm",
        ".gcz",
        ".dol",
        ".wad",
        ".wua",
        ".wud",
        ".wux",
        ".pbp",
        ".psx",
        ".cdi",
        ".gdi",
        ".md",
        ".gen",
        ".smd",
        ".32x",
        ".sms",
        ".gg",
        ".pce",
        ".a26",
        ".a52",
        ".a78",
        ".lnx",
        ".j64",
        ".jag",
        ".ws",
        ".wsc",
        ".ngp",
        ".ngc",
        ".xbe",
        ".xex",
        ".xiso",
        ".elf",
        ".m3u",
        ".zip",
        ".7z"
    };

    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "$recycle.bin",
        "recycler",
        "system volume information",
        ".git",
        ".vs",
        "node_modules",
        "_commonredist",
        "commonredist",
        "redist",
        "redistributables",
        "support",
        "prereq",
        "prerequisites",
        "dotnet",
        "directx",
        "vcredist",
        "d3d",
        "physx",
        "easyanticheat",
        "battleye",
        "steamworks_sdk",
        "__modules",
        "mono",
        "msvc",
        "installer",
        "install",
        "engine",
        "plugins",
        "extensions",
        "mods",
        "dlc",
        "patch",
        "update",
        "updates",
        "__redist",
        "crashpad",
        "crash_reports",
        "logs",
        "log",
        "saves",
        "save",
        "savegames",
        "userdata",
        "appdata",
        "cache",
        "shader_cache",
        "shadercache",
        "thumbnails",
        "intermediate",
        "obj",
        "build",
        "tools",
        "resources",
        "localization",
        "locales",
        "langs"
    };

    private static readonly HashSet<string> TechnicalDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "bin32",
        "bin64",
        "binaries",
        "win32",
        "win64",
        "windows",
        "windowsnoeditor",
        "shipping",
        "release",
        "debug",
        "x64",
        "x86"
    };

    private static readonly HashSet<string> CollectionDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "games",
        "game library",
        "gamelibrary",
        "steamapps",
        "common",
        "gog games",
        "launchbox",
        "playnite",
        "library",
        "libraries",
        "roms",
        "emulation",
        "downloads",
        "desktop"
    };

    private static readonly string[] HelperExecutableTokens =
    [
        "launcher",
        "bootstrap",
        "selector",
        "config",
        "settings",
        "tool",
        "editor",
        "server",
        "dedicated",
        "benchmark",
        "sdk",
        "test"
    ];

    private static readonly HashSet<string> GenericHelperExecutableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "launcher",
        "bootstrap",
        "config",
        "configuration",
        "settings",
        "setup",
        "install",
        "installer",
        "uninstall",
        "updater",
        "patcher",
        "editor",
        "tool",
        "server",
        "dedicatedserver",
        "benchmark"
    };

    private static readonly HashSet<string> DiscDescriptorExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".m3u",
        ".cue",
        ".gdi"
    };

    private static readonly HashSet<string> DiscPayloadExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bin",
        ".iso"
    };

    public async Task<ScanReport> ScanFolderAsync(
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => ScanFolder(folderPath, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Reads authoritative storefront manifests before the broader filesystem scan. Storefront
    /// identity and title are treated as primary evidence, so a game is not lost merely because its
    /// executable uses an internal codename or lives below a technical directory.
    /// </summary>
    public IReadOnlyList<ImportCandidate> DiscoverLauncherCandidates()
    {
        var candidates = new List<ImportCandidate>();
        candidates.AddRange(ReadInstalledSteamGames().Select(game => CreateLauncherCandidate(new LauncherGameInfo(
            "Steam",
            game.AppId,
            game.Title,
            game.InstallPath,
            $"steam://rungameid/{game.AppId}"))));
        candidates.AddRange(ReadInstalledEpicGames().Select(CreateLauncherCandidate));
        candidates.AddRange(ReadInstalledXboxGames().Select(CreateLauncherCandidate));

        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Title) &&
                                !string.IsNullOrWhiteSpace(candidate.InstallPath))
            .GroupBy(candidate => candidate.DeduplicationKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public ImportCandidate CreateLauncherCandidate(LauncherGameInfo game)
    {
        ArgumentNullException.ThrowIfNull(game);

        var provider = string.IsNullOrWhiteSpace(game.Provider) ? "Launcher" : game.Provider.Trim();
        var title = string.IsNullOrWhiteSpace(game.Title)
            ? GameNameNormalizer.NormalizeTitleFromPath(game.InstallPath)
            : game.Title.Trim();
        var installRoot = TryNormalizeDirectoryPath(game.InstallPath) ?? game.InstallPath.Trim();
        var launchTarget = ResolveManifestLaunchTarget(installRoot, game.LaunchTarget);
        var sourceIdentity = BuildSourceIdentity(provider, game.ExternalId);
        var rootIdentity = BuildImportIdentity(installRoot, true, launchTarget, "Windows PC");
        var identities = new[] { sourceIdentity, rootIdentity }
            .Where(identity => !string.IsNullOrWhiteSpace(identity))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ImportCandidate
        {
            Title = title,
            Platform = "Windows PC",
            SourceKind = provider,
            InstallPath = launchTarget,
            DeduplicationKey = identities.FirstOrDefault() ?? rootIdentity,
            IdentityKeys = identities,
            Confidence = 1,
            Signals =
            [
                $"Verified by the {provider} install manifest.",
                "Storefront identity retained for future duplicate prevention.",
                launchTarget.Contains("://", StringComparison.Ordinal)
                    ? "Uses the storefront launch protocol instead of guessing an executable."
                    : "Launch target resolved from the installed game record."
            ]
        };
    }

    /// <summary>
    /// Removes duplicate and nested scan roots before a multi-library import starts. Scanning a
    /// parent and one of its children is both slower and a common source of duplicate candidates.
    /// </summary>
    public IReadOnlyList<string> NormalizeScanRoots(IEnumerable<string> folderPaths)
    {
        var normalized = (folderPaths ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(TryNormalizeDirectoryPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path!.Length)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var roots = new List<string>(normalized.Count);
        foreach (var path in normalized)
        {
            if (!roots.Any(root => IsSameOrAncestorDirectory(root, path!)))
            {
                roots.Add(path!);
            }
        }

        return roots;
    }

    public IReadOnlyList<string> DiscoverSteamLibraryRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
        {
            var driveRoot = drive.RootDirectory.FullName;
            TryAddIfExists(roots, Path.Combine(driveRoot, "SteamLibrary", "steamapps", "common"));
            TryAddIfExists(roots, Path.Combine(driveRoot, "Program Files (x86)", "Steam", "steamapps", "common"));
            TryAddIfExists(roots, Path.Combine(driveRoot, "Program Files", "Steam", "steamapps", "common"));

            // Catch custom Steam roots with a shallow sweep for "steamapps/common".
            foreach (var firstLevel in EnumerateDirectoriesSafe(driveRoot))
            {
                TryAddIfExists(roots, Path.Combine(firstLevel, "steamapps", "common"));
            }
        }

        foreach (var steamRoot in FindSteamInstallRoots())
        {
            TryAddIfExists(roots, Path.Combine(steamRoot, "steamapps", "common"));

            var libraryFoldersPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            foreach (var libraryPath in ParseSteamLibraryFolders(libraryFoldersPath))
            {
                TryAddIfExists(roots, Path.Combine(libraryPath, "steamapps", "common"));
            }
        }

        return roots.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyList<string> DiscoverLauncherLibraryRoots()
    {
        var roots = new HashSet<string>(DiscoverSteamLibraryRoots(), StringComparer.OrdinalIgnoreCase);
        string[][] driveRelativeRoots =
        [
            ["Epic Games"],
            ["Games", "Epic Games"],
            ["Battle.net"],
            ["Games", "Battle.net"],
            ["Games", "BattleNet"],
            ["Rockstar Games"],
            ["Games", "Rockstar Games"],
            ["GOG Games"],
            ["Games", "GOG Games"],
            ["XboxGames"],
            ["Games", "XboxGames"],
            ["SteamLibrary", "steamapps", "common"],
            ["Games", "SteamLibrary", "steamapps", "common"],
            ["Ubisoft Game Launcher", "games"],
            ["Games", "Ubisoft Game Launcher", "games"],
            ["EA Games"],
            ["Games", "EA Games"]
        ];

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
        {
            var driveRoot = drive.RootDirectory.FullName;
            foreach (var relativeRoot in driveRelativeRoots)
            {
                TryAddIfExists(roots, Path.Combine(relativeRoot.Prepend(driveRoot).ToArray()));
            }
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string[][] knownProgramRoots =
        [
            [programFiles, "Epic Games"],
            [programFilesX86, "Epic Games"],
            [programFiles, "Rockstar Games"],
            [programFilesX86, "Rockstar Games"],
            [programFiles, "EA Games"],
            [programFilesX86, "EA Games"],
            [programFiles, "GOG Galaxy", "Games"],
            [programFilesX86, "GOG Galaxy", "Games"],
            [programFiles, "Ubisoft", "Ubisoft Game Launcher", "games"],
            [programFilesX86, "Ubisoft", "Ubisoft Game Launcher", "games"]
        ];

        foreach (var root in knownProgramRoots)
        {
            TryAddIfExists(roots, Path.Combine(root));
        }

        foreach (var epicInstall in ParseEpicManifestInstallLocations())
        {
            TryAddIfExists(roots, epicInstall);
        }

        foreach (var battleNetInstall in ParseBattleNetProductDbInstallLocations())
        {
            TryAddIfExists(roots, battleNetInstall);
        }

        foreach (var manifestInstall in ParseLauncherManifestInstallLocations())
        {
            TryAddIfExists(roots, manifestInstall);
        }

        return roots.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public ImportCandidate CreateManualCandidate(string filePath)
    {
        var candidate = BuildEvaluatedCandidate(filePath, Path.GetDirectoryName(filePath) ?? string.Empty).Candidate;
        candidate.SourceKind = "Manual Add";
        candidate.Confidence = 1;
        candidate.Signals =
        [
            "Added manually by the player.",
            $"Launch target: {Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant()} file"
        ];
        return candidate;
    }

    public string GetImportIdentity(string launchTarget)
    {
        if (string.IsNullOrWhiteSpace(launchTarget))
        {
            return string.Empty;
        }

        try
        {
            return BuildEvaluatedCandidate(launchTarget, Path.GetPathRoot(launchTarget) ?? string.Empty)
                .Candidate
                .DeduplicationKey;
        }
        catch
        {
            return NormalizeFileIdentity(launchTarget);
        }
    }

    private ScanReport ScanFolder(string folderPath, CancellationToken cancellationToken)
    {
        var report = new ScanReport();

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            AddWarning(report, "No folder was provided for the scan.");
            return report;
        }

        if (!Directory.Exists(folderPath))
        {
            AddWarning(report, $"The folder '{folderPath}' no longer exists.");
            return report;
        }

        var normalizedScanRoot = Path.GetFullPath(folderPath);
        var pendingDirectories = new Queue<string>();
        var rawCandidates = new List<EvaluatedCandidate>();
        pendingDirectories.Enqueue(normalizedScanRoot);

        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDirectory = pendingDirectories.Dequeue();
            report.DirectoriesInspected++;

            foreach (var subDirectory in EnumerateDirectoriesSafely(currentDirectory, report))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (ShouldSkipDirectory(subDirectory))
                {
                    report.FilesSkipped++;
                    report.DirectoriesSkipped++;
                    continue;
                }

                pendingDirectories.Enqueue(subDirectory);
            }

            foreach (var filePath in EnumerateFilesSafely(currentDirectory, report))
            {
                cancellationToken.ThrowIfCancellationRequested();
                report.FilesInspected++;

                try
                {
                    var extension = Path.GetExtension(filePath);
                    if (!AllowedExtensions.Contains(extension))
                    {
                        report.FilesSkipped++;
                        continue;
                    }

                    if (GameNameNormalizer.LooksLikeNonGamePayload(filePath))
                    {
                        report.FilesSkipped++;
                        continue;
                    }

                    var candidate = BuildEvaluatedCandidate(filePath, normalizedScanRoot);
                    if (candidate.Candidate.Confidence < 0.35)
                    {
                        report.FilesSkipped++;
                        report.LowConfidenceCandidatesSkipped++;
                        continue;
                    }

                    rawCandidates.Add(candidate);
                }
                catch (Exception exception) when (IsExpectedScanException(exception))
                {
                    report.ErrorsSkipped++;
                    AddWarning(report, $"Skipped an unreadable file inside '{currentDirectory}'.");
                }
            }
        }

        report.Candidates = CollapseCandidates(rawCandidates, report)
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return report;
    }

    private static IEnumerable<ImportCandidate> CollapseCandidates(IEnumerable<EvaluatedCandidate> rawCandidates, ScanReport report)
    {
        var collapsed = new List<ImportCandidate>();

        foreach (var group in rawCandidates.Where(candidate => candidate.IsDesktopLaunchable)
                     .GroupBy(candidate => candidate.ProductRootKey, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group
                .OrderByDescending(candidate => candidate.Candidate.Confidence)
                .ThenByDescending(candidate => candidate.PrimaryTitleMatchScore)
                .ThenBy(candidate => candidate.HelperTokenCount)
                .ThenBy(candidate => candidate.Candidate.InstallPath.Length)
                .ToList();

            var best = ordered[0].Candidate;
            if (ordered.Count > 1)
            {
                best.Signals.Add($"Collapsed {ordered.Count - 1} alternate executable(s) from the same install.");
                report.DuplicateCandidatesCollapsed += ordered.Count - 1;
            }

            collapsed.Add(best);
        }

        collapsed.AddRange(CollapseRomCandidates(rawCandidates.Where(candidate => !candidate.IsDesktopLaunchable), report));

        return collapsed;
    }

    private static IEnumerable<ImportCandidate> CollapseRomCandidates(
        IEnumerable<EvaluatedCandidate> evaluatedCandidates,
        ScanReport report)
    {
        var candidates = evaluatedCandidates.ToList();
        var suppressed = new HashSet<EvaluatedCandidate>();

        // A playlist is the authoritative launch target for a multi-disc set. Only collapse
        // matching sibling files when a playlist explicitly exists, so separate discs remain
        // selectable when no playlist is present.
        foreach (var group in candidates
                     .Where(candidate => HasExtension(candidate.Candidate.InstallPath, ".m3u"))
                     .GroupBy(candidate => BuildDiscSetKey(candidate.Candidate.InstallPath, candidate.Candidate.Platform), StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderByDescending(candidate => candidate.Candidate.Confidence).ToList();
            var primary = ordered[0];
            var related = candidates.Where(candidate =>
                !ReferenceEquals(candidate, primary) &&
                BuildDiscSetKey(candidate.Candidate.InstallPath, candidate.Candidate.Platform)
                    .Equals(group.Key, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (related.Count == 0)
            {
                continue;
            }

            foreach (var candidate in related)
            {
                suppressed.Add(candidate);
            }

            primary.Candidate.Signals.Add($"Playlist selected as the launch target; {related.Count} companion file{(related.Count == 1 ? string.Empty : "s")} folded into this set.");
            report.CompanionFilesCollapsed += related.Count;
        }

        // A CUE/GDI descriptor and its same-stem BIN/ISO payload describe one disc, never two
        // games. This is deliberately exact-stem only to avoid dropping separately named discs.
        foreach (var descriptor in candidates.Where(candidate =>
                     DiscDescriptorExtensions.Contains(Path.GetExtension(candidate.Candidate.InstallPath)) &&
                     !suppressed.Contains(candidate)))
        {
            var descriptorStem = BuildExactDiscStemKey(descriptor.Candidate.InstallPath);
            var payloads = candidates.Where(candidate =>
                    !suppressed.Contains(candidate) &&
                    DiscPayloadExtensions.Contains(Path.GetExtension(candidate.Candidate.InstallPath)) &&
                    BuildExactDiscStemKey(candidate.Candidate.InstallPath)
                        .Equals(descriptorStem, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (payloads.Count == 0)
            {
                continue;
            }

            foreach (var payload in payloads)
            {
                suppressed.Add(payload);
            }

            descriptor.Candidate.Signals.Add($"Disc descriptor selected; {payloads.Count} same-disc payload file{(payloads.Count == 1 ? string.Empty : "s")} ignored.");
            report.CompanionFilesCollapsed += payloads.Count;
        }

        foreach (var group in candidates
                     .Where(candidate => !suppressed.Contains(candidate))
                     .GroupBy(candidate => candidate.Candidate.DeduplicationKey, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderByDescending(candidate => candidate.Candidate.Confidence).ToList();
            var primary = ordered[0].Candidate;
            if (ordered.Count > 1)
            {
                primary.Signals.Add($"Collapsed {ordered.Count - 1} duplicate file candidate{(ordered.Count == 2 ? string.Empty : "s")}.");
                report.DuplicateCandidatesCollapsed += ordered.Count - 1;
            }

            yield return primary;
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesSafely(string directory, ScanReport report)
    {
        try
        {
            return Directory.EnumerateDirectories(directory);
        }
        catch (Exception exception) when (IsExpectedScanException(exception))
        {
            report.ErrorsSkipped++;
            AddWarning(report, $"Skipped an unreadable folder: {directory}");
            return [];
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesSafe(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory);
        }
        catch
        {
            return [];
        }
    }

    private static void TryAddIfExists(HashSet<string> roots, string path)
    {
        if (Directory.Exists(path))
        {
            roots.Add(Path.GetFullPath(path));
        }
    }

    private static string? TryNormalizeDirectoryPath(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return null;
            }

            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            return !string.IsNullOrWhiteSpace(root) && fullPath.Equals(root, StringComparison.OrdinalIgnoreCase)
                ? root
                : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception exception) when (IsExpectedScanException(exception) || exception is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool IsSameOrAncestorDirectory(string ancestor, string candidate)
    {
        if (ancestor.Equals(candidate, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = ancestor.EndsWith(Path.DirectorySeparatorChar) || ancestor.EndsWith(Path.AltDirectorySeparatorChar)
            ? ancestor
            : ancestor + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> FindSteamInstallRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                roots.Add(candidate);
            }
        }

        return roots;
    }

    private static IEnumerable<string> ParseSteamLibraryFolders(string libraryFoldersPath)
    {
        if (!File.Exists(libraryFoldersPath))
        {
            return [];
        }

        try
        {
            var text = File.ReadAllText(libraryFoldersPath);
            var matches = Regex.Matches(text, "\"path\"\\s+\"(?<path>[^\"]+)\"", RegexOptions.IgnoreCase);
            var paths = new List<string>();
            foreach (Match match in matches)
            {
                var raw = match.Groups["path"].Value.Replace("\\\\", "\\");
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    paths.Add(raw);
                }
            }

            return paths;
        }
        catch
        {
            return [];
        }
    }

    // ── Steam ACF manifest reader ─────────────────────────────────────────
    /// <summary>
    /// Represents a single installed Steam game parsed from its appmanifest ACF file.
    /// </summary>
    public sealed record LauncherGameInfo(
        string Provider,
        string ExternalId,
        string Title,
        string InstallPath,
        string LaunchTarget);

    public sealed record SteamGameInfo(
        string AppId,
        string Title,
        string InstallPath,
        int PlaytimeMinutes);

    /// <summary>
    /// Reads every appmanifest_*.acf file across all Steam library roots and returns
    /// a clean list of installed Steam games with title, install path, and playtime.
    /// </summary>
    public IReadOnlyList<SteamGameInfo> ReadInstalledSteamGames()
    {
        var results = new List<SteamGameInfo>();

        foreach (var steamRoot in FindSteamInstallRoots())
        {
            var steamAppsDir = Path.Combine(steamRoot, "steamapps");
            var libraryFoldersPath = Path.Combine(steamAppsDir, "libraryfolders.vdf");

            // Collect all library roots including extra library folders
            var acfDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamAppsDir };
            foreach (var libPath in ParseSteamLibraryFolders(libraryFoldersPath))
                acfDirs.Add(Path.Combine(libPath, "steamapps"));

            foreach (var acfDir in acfDirs)
            {
                if (!Directory.Exists(acfDir)) continue;

                foreach (var acfPath in Directory.EnumerateFiles(acfDir, "appmanifest_*.acf", SearchOption.TopDirectoryOnly))
                {
                    var info = TryParseAcf(acfPath, acfDir);
                    if (info != null)
                        results.Add(info);
                }
            }
        }

        return results
            .DistinctBy(g => g.AppId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Title)
            .ToList();
    }

    private static SteamGameInfo? TryParseAcf(string acfPath, string acfDir)
    {
        try
        {
            var text = File.ReadAllText(acfPath);

            var appId  = ExtractAcfValue(text, "appid");
            var name   = ExtractAcfValue(text, "name");
            var installDir = ExtractAcfValue(text, "installdir");
            var playtimeStr = ExtractAcfValue(text, "playtime_forever");

            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(name))
                return null;

            // Skip Steamworks common redistributables, Proton runtimes, etc.
            if (name.StartsWith("Steamworks", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Redistributable", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Proton", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Steam Linux", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Runtime", StringComparison.OrdinalIgnoreCase) && name.Contains("Steam", StringComparison.OrdinalIgnoreCase))
                return null;

            var fullInstallPath = !string.IsNullOrWhiteSpace(installDir)
                ? Path.Combine(acfDir, "common", installDir)
                : string.Empty;

            // playtime_forever is in minutes in Steam ACF files
            int.TryParse(playtimeStr, out var playtimeMinutes);

            return new SteamGameInfo(appId, name, fullInstallPath, playtimeMinutes);
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractAcfValue(string text, string key)
    {
        var match = Regex.Match(text, $"\"(?i:{Regex.Escape(key)})\"\\s+\"(?<val>[^\"]*)\"");
        return match.Success ? match.Groups["val"].Value.Trim() : string.Empty;
    }

    private static IEnumerable<string> ParseEpicManifestInstallLocations()
    {
        return ReadInstalledEpicGames().Select(game => game.InstallPath);
    }

    public static IReadOnlyList<LauncherGameInfo> ReadInstalledEpicGames()
    {
        var manifestDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic",
            "EpicGamesLauncher",
            "Data",
            "Manifests");

        if (!Directory.Exists(manifestDirectory))
        {
            return [];
        }

        var installs = new List<LauncherGameInfo>();
        foreach (var manifestPath in Directory.EnumerateFiles(manifestDirectory, "*.item", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
                var root = document.RootElement;
                if (root.TryGetProperty("bIsApplication", out var isApplication) &&
                    isApplication.ValueKind == JsonValueKind.False)
                {
                    continue;
                }
                var installPath = GetJsonString(root, "InstallLocation");
                if (!string.IsNullOrWhiteSpace(installPath) && Directory.Exists(installPath))
                {
                    var appName = GetJsonString(root, "AppName");
                    var catalogId = GetJsonString(root, "CatalogItemId");
                    var externalId = !string.IsNullOrWhiteSpace(appName)
                        ? appName
                        : !string.IsNullOrWhiteSpace(catalogId)
                            ? catalogId
                            : Path.GetFileNameWithoutExtension(manifestPath);
                    var displayName = GetJsonString(root, "DisplayName");
                    installs.Add(new LauncherGameInfo(
                        "Epic",
                        externalId,
                        string.IsNullOrWhiteSpace(displayName) ? Path.GetFileName(installPath) : displayName,
                        installPath,
                        GetJsonString(root, "LaunchExecutable")));
                }
            }
            catch
            {
                // One malformed launcher manifest should not block the rest of discovery.
            }
        }

        return installs;
    }

    public static IReadOnlyList<LauncherGameInfo> ReadInstalledXboxGames(IEnumerable<string>? libraryRoots = null)
    {
        var roots = libraryRoots?.ToList() ?? DriveInfo.GetDrives()
            .Where(drive => drive.IsReady && drive.DriveType == DriveType.Fixed)
            .Select(drive => Path.Combine(drive.RootDirectory.FullName, "XboxGames"))
            .Where(Directory.Exists)
            .ToList();
        var games = new List<LauncherGameInfo>();

        foreach (var libraryRoot in roots.Where(Directory.Exists))
        {
            foreach (var gameDirectory in EnumerateDirectoriesSafe(libraryRoot))
            {
                var contentDirectory = Path.Combine(gameDirectory, "Content");
                var configPath = new[]
                {
                    Path.Combine(contentDirectory, "MicrosoftGame.Config"),
                    Path.Combine(contentDirectory, "MicrosoftGame.config"),
                    Path.Combine(gameDirectory, "MicrosoftGame.Config"),
                    Path.Combine(gameDirectory, "MicrosoftGame.config")
                }.FirstOrDefault(File.Exists);
                if (configPath is null)
                {
                    continue;
                }

                try
                {
                    var document = XDocument.Load(configPath, LoadOptions.None);
                    var identity = document.Descendants()
                        .FirstOrDefault(element => element.Name.LocalName.Equals("Identity", StringComparison.OrdinalIgnoreCase));
                    var executable = document.Descendants()
                        .FirstOrDefault(element => element.Name.LocalName.Equals("Executable", StringComparison.OrdinalIgnoreCase));
                    var externalId = identity?.Attributes()
                        .FirstOrDefault(attribute => attribute.Name.LocalName.Equals("Name", StringComparison.OrdinalIgnoreCase))?.Value;
                    var executableName = executable?.Attributes()
                        .FirstOrDefault(attribute => attribute.Name.LocalName.Equals("Name", StringComparison.OrdinalIgnoreCase))?.Value;
                    games.Add(new LauncherGameInfo(
                        "Xbox",
                        string.IsNullOrWhiteSpace(externalId) ? Path.GetFileName(gameDirectory) : externalId,
                        Path.GetFileName(gameDirectory),
                        Directory.Exists(contentDirectory) ? contentDirectory : gameDirectory,
                        executableName ?? string.Empty));
                }
                catch
                {
                    // A partial or locked package should not prevent other installed games loading.
                }
            }
        }

        return games;
    }

    private static string GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : string.Empty;
    }

    private static IEnumerable<string> ParseBattleNetProductDbInstallLocations()
    {
        var productDb = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Battle.net",
            "Agent",
            "product.db");

        if (!File.Exists(productDb))
        {
            return [];
        }

        try
        {
            var text = File.ReadAllText(productDb);
            return Regex.Matches(text, @"[A-Z]:\\(?:[^<>:""|?*\r\n]+\\)*[^<>:""|?*\r\n]+", RegexOptions.IgnoreCase)
                .Select(match => match.Value.TrimEnd('\\'))
                .Where(path => Directory.Exists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<string> ParseLauncherManifestInstallLocations()
    {
        var commonApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roots = new[]
        {
            Path.Combine(commonApplicationData, "GOG.com"),
            Path.Combine(commonApplicationData, "GOG.com", "Galaxy"),
            Path.Combine(localApplicationData, "GOG.com", "Galaxy"),
            Path.Combine(commonApplicationData, "Ubisoft"),
            Path.Combine(localApplicationData, "Ubisoft"),
            Path.Combine(commonApplicationData, "Rockstar Games"),
            Path.Combine(localApplicationData, "Rockstar Games"),
            Path.Combine(commonApplicationData, "EA Desktop"),
            Path.Combine(localApplicationData, "Electronic Arts")
        };

        var locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var file in EnumerateManifestFiles(root))
            {
                foreach (var location in ExtractInstallLocations(file))
                {
                    locations.Add(location);
                }
            }
        }

        return locations;
    }

    private static IEnumerable<string> EnumerateManifestFiles(string root)
    {
        try
        {
            return Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(path =>
                {
                    var extension = Path.GetExtension(path);
                    return extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
                           extension.Equals(".item", StringComparison.OrdinalIgnoreCase) ||
                           extension.Equals(".manifest", StringComparison.OrdinalIgnoreCase) ||
                           extension.Equals(".db", StringComparison.OrdinalIgnoreCase);
                })
                .Take(120)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<string> ExtractInstallLocations(string filePath)
    {
        string text;
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > 8 * 1024 * 1024)
            {
                return [];
            }

            text = File.ReadAllText(filePath);
        }
        catch
        {
            return [];
        }

        var locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        TryExtractJsonInstallLocations(text, locations);

        foreach (Match match in Regex.Matches(text, @"[A-Z]:\\(?:[^<>:""|?*\r\n]+\\)*[^<>:""|?*\r\n]+", RegexOptions.IgnoreCase))
        {
            var candidate = match.Value.TrimEnd('\\');
            if (LooksLikeGameInstallPath(candidate))
            {
                locations.Add(candidate);
            }
        }

        return locations;
    }

    private static void TryExtractJsonInstallLocations(string text, HashSet<string> locations)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            CollectJsonInstallLocations(document.RootElement, locations);
        }
        catch
        {
        }
    }

    private static void CollectJsonInstallLocations(JsonElement element, HashSet<string> locations)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String &&
                    IsInstallPathProperty(property.Name))
                {
                    var value = property.Value.GetString();
                    if (LooksLikeGameInstallPath(value))
                    {
                        locations.Add(value!);
                    }
                }

                CollectJsonInstallLocations(property.Value, locations);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                CollectJsonInstallLocations(child, locations);
            }
        }
    }

    private static bool IsInstallPathProperty(string name)
    {
        return name.Equals("InstallLocation", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("InstallPath", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("installationPath", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("install_path", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("baseInstallPath", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("root", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeGameInstallPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = path.Trim().Trim('"').TrimEnd('\\');
        if (!Path.IsPathFullyQualified(normalized) || !Directory.Exists(normalized))
        {
            return false;
        }

        var lowered = normalized.Replace('\\', '/').ToLowerInvariant();
        return lowered.Contains("/games/") ||
               lowered.Contains("/gog") ||
               lowered.Contains("/epic") ||
               lowered.Contains("/ubisoft") ||
               lowered.Contains("/rockstar") ||
               lowered.Contains("/ea games") ||
               lowered.Contains("/battle.net") ||
               lowered.Contains("/xboxgames") ||
               lowered.Contains("/steamapps/common");
    }

    private static IEnumerable<string> EnumerateFilesSafely(string directory, ScanReport report)
    {
        try
        {
            return Directory.EnumerateFiles(directory);
        }
        catch (Exception exception) when (IsExpectedScanException(exception))
        {
            report.ErrorsSkipped++;
            AddWarning(report, $"Skipped files in an unreadable folder: {directory}");
            return [];
        }
    }

    private static bool ShouldSkipDirectory(string directoryPath)
    {
        var directoryName = Path.GetFileName(directoryPath);
        if (IgnoredDirectoryNames.Contains(directoryName))
        {
            return true;
        }

        try
        {
            var attributes = File.GetAttributes(directoryPath);
            return attributes.HasFlag(FileAttributes.ReparsePoint) ||
                   attributes.HasFlag(FileAttributes.System) ||
                   attributes.HasFlag(FileAttributes.Temporary);
        }
        catch (Exception exception) when (IsExpectedScanException(exception))
        {
            return true;
        }
    }

    private static bool IsExpectedScanException(Exception exception)
    {
        return exception is UnauthorizedAccessException or
               IOException or
               PathTooLongException or
               DirectoryNotFoundException or
               FileNotFoundException;
    }

    private static void AddWarning(ScanReport report, string warning)
    {
        if (report.Warnings.Count >= 8 || report.Warnings.Contains(warning, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        report.Warnings.Add(warning);
    }

    private static EvaluatedCandidate BuildEvaluatedCandidate(string filePath, string scanRoot)
    {
        var extension = Path.GetExtension(filePath);
        var isDesktopLaunchable = IsDesktopLaunchable(extension);
        var normalizedPath = filePath.Replace('\\', '/').ToLowerInvariant();
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var normalizedFileName = fileName.ToLowerInvariant();
        var productRootDirectory = ResolveProductRootDirectory(filePath, scanRoot);
        var productRootName = Path.GetFileName(productRootDirectory);
        var productRootKey = string.IsNullOrWhiteSpace(productRootDirectory)
            ? Path.GetDirectoryName(filePath) ?? filePath
            : productRootDirectory;

        var rootTitle = string.IsNullOrWhiteSpace(productRootName)
            ? string.Empty
            : GameNameNormalizer.NormalizeTitleFromPath(productRootName);
        var fileTitle = GameNameNormalizer.NormalizeTitleFromPath(filePath);
        var title = ChooseDisplayTitle(fileTitle, rootTitle, normalizedFileName);

        var signals = new List<string>();
        var score = isDesktopLaunchable ? 0.32d : 0.25d;
        var primaryTitleMatchScore = 0;

        if (isDesktopLaunchable)
        {
            signals.Add("Launchable desktop target detected.");
        }
        else
        {
            signals.Add("Recognized ROM or disc image format.");
        }

        if (normalizedPath.Contains("/games/") ||
            normalizedPath.Contains("/roms/") ||
            normalizedPath.Contains("/emulation/") ||
            normalizedPath.Contains("/steamapps/") ||
            normalizedPath.Contains("/gog games/") ||
            normalizedPath.Contains("/epic games/") ||
            normalizedPath.Contains("/xboxgames/") ||
            normalizedPath.Contains("/battle.net/") ||
            normalizedPath.Contains("/ea games/") ||
            normalizedPath.Contains("/ubisoft game launcher/") ||
            normalizedPath.Contains("/rockstar games/") ||
            normalizedPath.Contains("/launchbox/"))
        {
            score += 0.14;
            signals.Add("Path looks like a game library.");
        }

        if (title.Length >= 4)
        {
            score += 0.10;
            signals.Add("Display title cleaned up successfully.");
        }

        var platform = GuessPlatform(filePath);
        if (platform is not "Unknown" and not "Emulation")
        {
            score += 0.10;
            signals.Add($"Detected platform: {platform}.");
        }
        else if (platform == "Emulation")
        {
            score += 0.05;
            signals.Add("Tagged as emulation content.");
        }

        var hasRootTitleMatch = HasRootTitleMatch(normalizedFileName, rootTitle);
        if (hasRootTitleMatch)
        {
            score += 0.24;
            primaryTitleMatchScore += 3;
            signals.Add("Filename aligns with the install folder title.");
        }

        if (normalizedPath.Contains("/binaries/"))
        {
            score -= 0.04;
            signals.Add("Nested runtime binary detected.");
        }

        if (normalizedFileName.Contains("shipping", StringComparison.OrdinalIgnoreCase))
        {
            score += 0.18;
            primaryTitleMatchScore += 2;
            signals.Add("Shipping build naming suggests the main game executable.");
        }

        if (normalizedPath.Contains("/binaries/win64/") || normalizedPath.Contains("/binaries/win32/"))
        {
            score += 0.08;
            signals.Add("Runtime path looks like a packaged Windows build.");
        }

        var helperTokenCount = HelperExecutableTokens.Count(token => normalizedFileName.Contains(token, StringComparison.OrdinalIgnoreCase));
        if (helperTokenCount > 0)
        {
            score -= Math.Min(0.32, helperTokenCount * 0.16);
            signals.Add("Filename looks more like a launcher or support executable.");
        }

        if (GenericHelperExecutableNames.Contains(normalizedFileName) && !hasRootTitleMatch)
        {
            score -= 0.30;
            signals.Add("Generic helper executable name is not specific enough to import.");
        }

        if (normalizedPath.Contains("/downloads/") || normalizedPath.Contains("/desktop/"))
        {
            score -= 0.08;
            signals.Add("Stored in a temporary-looking location.");
        }

        var confidence = Math.Clamp(score, 0, 1);
        return new EvaluatedCandidate
        {
            ProductRootKey = productRootKey,
            IsDesktopLaunchable = isDesktopLaunchable,
            HelperTokenCount = helperTokenCount,
            PrimaryTitleMatchScore = primaryTitleMatchScore,
            Candidate = new ImportCandidate
            {
                Title = title,
                Platform = platform,
                SourceKind = "Folder Scan",
                InstallPath = filePath,
                DeduplicationKey = BuildImportIdentity(productRootKey, isDesktopLaunchable, filePath, platform),
                IdentityKeys = [BuildImportIdentity(productRootKey, isDesktopLaunchable, filePath, platform)],
                Confidence = confidence,
                Signals = signals
            }
        };
    }

    private static string ResolveProductRootDirectory(string filePath, string scanRoot)
    {
        var scanRootFullPath = string.IsNullOrWhiteSpace(scanRoot)
            ? string.Empty
            : Path.GetFullPath(scanRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var current = new DirectoryInfo(Path.GetDirectoryName(filePath) ?? string.Empty);

        while (current is not null && current.Exists)
        {
            var currentPath = current.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var directoryName = current.Name;

            if (!string.IsNullOrWhiteSpace(scanRootFullPath) &&
                currentPath.Equals(scanRootFullPath, StringComparison.OrdinalIgnoreCase))
            {
                return CollectionDirectoryNames.Contains(directoryName) || TechnicalDirectoryNames.Contains(directoryName)
                    ? string.Empty
                    : current.FullName;
            }

            if (CollectionDirectoryNames.Contains(directoryName))
            {
                break;
            }

            if (!TechnicalDirectoryNames.Contains(directoryName))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return string.Empty;
    }

    private static string ChooseDisplayTitle(string fileTitle, string rootTitle, string normalizedFileName)
    {
        if (string.IsNullOrWhiteSpace(rootTitle))
        {
            return fileTitle;
        }

        if (normalizedFileName.Contains("shipping", StringComparison.OrdinalIgnoreCase) ||
            normalizedFileName.Contains("launcher", StringComparison.OrdinalIgnoreCase) ||
            normalizedFileName.Contains("bootstrap", StringComparison.OrdinalIgnoreCase))
        {
            return rootTitle;
        }

        return rootTitle.Length >= fileTitle.Length && HasRootTitleMatch(normalizedFileName, rootTitle)
            ? rootTitle
            : fileTitle;
    }

    private static bool HasRootTitleMatch(string normalizedFileName, string rootTitle)
    {
        if (string.IsNullOrWhiteSpace(rootTitle))
        {
            return false;
        }

        var normalizedRoot = rootTitle
            .ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty);
        var compactFileName = normalizedFileName
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty);

        return !string.IsNullOrWhiteSpace(normalizedRoot) &&
               compactFileName.Contains(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDesktopLaunchable(string extension)
    {
        return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".url", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildImportIdentity(string productRootKey, bool isDesktopLaunchable, string filePath, string platform)
    {
        if (isDesktopLaunchable)
        {
            var root = NormalizeFileIdentity(productRootKey);
            return string.IsNullOrWhiteSpace(root) ? NormalizeFileIdentity(filePath) : "desktop:" + root;
        }

        return "file:" + NormalizeFileIdentity(filePath);
    }

    private static string BuildSourceIdentity(string provider, string externalId)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(externalId))
        {
            return string.Empty;
        }

        var normalizedProvider = Regex.Replace(provider.Trim().ToLowerInvariant(), @"\s+", string.Empty);
        return $"source:{normalizedProvider}:{externalId.Trim().ToLowerInvariant()}";
    }

    private static string ResolveManifestLaunchTarget(string installRoot, string launchTarget)
    {
        if (!string.IsNullOrWhiteSpace(launchTarget))
        {
            var trimmed = launchTarget.Trim().Trim('"');
            if (trimmed.Contains("://", StringComparison.Ordinal))
            {
                return trimmed;
            }

            var resolved = Path.IsPathFullyQualified(trimmed)
                ? trimmed
                : Path.Combine(installRoot, trimmed.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(resolved) || Directory.Exists(resolved))
            {
                return Path.GetFullPath(resolved);
            }
        }

        return installRoot;
    }

    private static string NormalizeFileIdentity(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            var fullPath = Path.GetFullPath(path.Trim().Trim('"'));
            var root = Path.GetPathRoot(fullPath);
            var normalizedPath = !string.IsNullOrWhiteSpace(root) && fullPath.Equals(root, StringComparison.OrdinalIgnoreCase)
                ? root
                : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return normalizedPath
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .ToLowerInvariant();
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path.Trim().Trim('"').Replace('\\', '/').ToLowerInvariant();
        }
    }

    private static bool HasExtension(string path, string extension)
    {
        return Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDiscSetKey(string path, string platform)
    {
        var parent = Path.GetDirectoryName(path) ?? string.Empty;
        return $"{NormalizeFileIdentity(parent)}|{platform}|{NormalizeDiscStem(Path.GetFileNameWithoutExtension(path))}";
    }

    private static string BuildExactDiscStemKey(string path)
    {
        var parent = Path.GetDirectoryName(path) ?? string.Empty;
        return $"{NormalizeFileIdentity(parent)}|{Path.GetFileNameWithoutExtension(path).Trim().ToLowerInvariant()}";
    }

    private static string NormalizeDiscStem(string value)
    {
        return Regex.Replace(value, @"(?:[\s._-]*\(?\s*(?:disc|disk|cd|track)\s*\d+\s*\)?)$", string.Empty, RegexOptions.IgnoreCase)
            .Trim()
            .ToLowerInvariant();
    }

    private static string GuessPlatform(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".exe" or ".lnk" or ".bat" or ".cmd" or ".url" => "Windows PC",
            ".nes" => "NES",
            ".sfc" or ".smc" => "SNES",
            ".gba" => "Game Boy Advance",
            ".gb" => "Game Boy",
            ".gbc" => "Game Boy Color",
            ".n64" or ".z64" or ".v64" => "Nintendo 64",
            ".nds" => "Nintendo DS",
            ".3ds" or ".3dsx" or ".cci" or ".cxi" or ".axf" or ".cia" => "Nintendo 3DS",
            ".xci" or ".nsp" or ".nca" => "Nintendo Switch",
            ".gcm" or ".gcz" or ".dol" => "GameCube",
            ".rvz" or ".wbfs" or ".wad" => InferNintendoDiscPlatformFromPath(filePath),
            ".wua" or ".wud" or ".wux" => "Wii U",
            ".pbp" or ".psx" => "PlayStation",
            ".cdi" or ".gdi" => "Dreamcast",
            ".md" or ".gen" or ".smd" => "Sega Genesis",
            ".32x" => "Sega 32X",
            ".sms" => "Master System",
            ".gg" => "Game Gear",
            ".pce" => "TurboGrafx-16",
            ".a26" => "Atari 2600",
            ".a52" => "Atari 5200",
            ".a78" => "Atari 7800",
            ".lnx" => "Atari Lynx",
            ".j64" or ".jag" => "Atari Jaguar",
            ".ws" or ".wsc" => "WonderSwan",
            ".ngp" or ".ngc" => "Neo Geo Pocket",
            ".xbe" or ".xiso" => "Xbox",
            ".xex" => "Xbox 360",
            ".elf" => InferElfPlatformFromPath(filePath),
            ".iso" or ".cue" or ".bin" or ".chd" or ".m3u" or ".cso" => InferDiscPlatformFromPath(filePath),
            ".zip" or ".7z" => "Emulation",
            _ => "Unknown"
        };
    }

    private static string InferNintendoDiscPlatformFromPath(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/').ToLowerInvariant();
        return normalizedPath.Contains("wii u") || normalizedPath.Contains("wiiu")
            ? "Wii U"
            : "Wii";
    }

    private static string InferElfPlatformFromPath(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/').ToLowerInvariant();
        return normalizedPath switch
        {
            var path when path.Contains("ps2") || path.Contains("playstation 2") => "PlayStation 2",
            var path when path.Contains("psp") => "PSP",
            var path when path.Contains("dreamcast") => "Dreamcast",
            var path when path.Contains("gamecube") || path.Contains("gcn") => "GameCube",
            _ => "Emulation"
        };
    }

    private static string InferDiscPlatformFromPath(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/').ToLowerInvariant();
        return normalizedPath switch
        {
            var path when path.Contains("ps2") => "PlayStation 2",
            var path when path.Contains("psp") => "PSP",
            var path when path.Contains("ps1") || path.Contains("playstation") => "PlayStation",
            var path when path.Contains("gamecube") || path.Contains("gcn") => "GameCube",
            var path when path.Contains("wii u") || path.Contains("wiiu") => "Wii U",
            var path when path.Contains("wii") => "Wii",
            var path when path.Contains("switch") => "Nintendo Switch",
            var path when path.Contains("xbox 360") || path.Contains("xbox360") => "Xbox 360",
            var path when path.Contains("xbox") => "Xbox",
            var path when path.Contains("dreamcast") => "Dreamcast",
            var path when path.Contains("saturn") => "Sega Saturn",
            var path when path.Contains("sega cd") || path.Contains("segacd") => "Sega CD",
            var path when path.Contains("pc engine") || path.Contains("turbografx") => "TurboGrafx-CD",
            _ => "Emulation"
        };
    }

    private sealed class EvaluatedCandidate
    {
        public required string ProductRootKey { get; init; }

        public required bool IsDesktopLaunchable { get; init; }

        public required int HelperTokenCount { get; init; }

        public required int PrimaryTitleMatchScore { get; init; }

        public required ImportCandidate Candidate { get; init; }
    }
}
