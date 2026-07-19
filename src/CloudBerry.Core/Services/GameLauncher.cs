using System.Collections.Concurrent;
using System.Diagnostics;

namespace CloudBerry.Core.Services;

public sealed class GameLauncher
{
    // Caches the winning executable for a given directory so subsequent launches
    // skip the AllDirectories scan entirely. Thread-safe for concurrent access.
    private static readonly ConcurrentDictionary<string, string> _resolvedExecutableCache =
        new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Launches a native game or ROM directly through the shell.
    /// </summary>
    public GameLaunchSession Launch(string installPath)
    {
        if (!TryResolveLaunchTemplate(installPath, out var launchTemplate, out var workingDirectory))
        {
            throw new FileNotFoundException("The selected launch target could not be found.", installPath);
        }

        var startedAt = DateTime.Now;
        var startInfo = new ProcessStartInfo
        {
            FileName = launchTemplate.ExecutablePath,
            Arguments = launchTemplate.ArgumentTemplate,
            UseShellExecute = true,
            WorkingDirectory = workingDirectory
        };

        var process = Process.Start(startInfo);
        return new GameLaunchSession(process, startedAt);
    }

    public static bool TryResolveLaunchTarget(string? installPath, out string launchTarget, out string workingDirectory)
    {
        launchTarget = string.Empty;
        workingDirectory = string.Empty;

        if (!TryResolveLaunchTemplate(installPath, out var launchTemplate, out workingDirectory))
        {
            return false;
        }

        launchTarget = launchTemplate.ExecutablePath;
        return true;
    }

    private static bool TryResolveLaunchTemplate(string? installPath, out EmulatorLaunchTemplate launchTemplate, out string workingDirectory)
    {
        launchTemplate = new EmulatorLaunchTemplate(string.Empty, string.Empty);
        workingDirectory = string.Empty;

        if (string.IsNullOrWhiteSpace(installPath))
        {
            return false;
        }

        var trimmed = installPath.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            if (!uri.IsFile)
            {
                launchTemplate = new EmulatorLaunchTemplate(trimmed, string.Empty);
                return true;
            }

            trimmed = uri.LocalPath;
        }

        var parsedTemplate = ParseLaunchTemplate(trimmed);
        if (File.Exists(parsedTemplate.ExecutablePath))
        {
            launchTemplate = parsedTemplate;
            workingDirectory = Path.GetDirectoryName(parsedTemplate.ExecutablePath) ?? string.Empty;
            return true;
        }

        if (!Directory.Exists(trimmed))
        {
            return false;
        }

        // Cache hit — skip the directory scan entirely on repeat launches.
        if (_resolvedExecutableCache.TryGetValue(trimmed, out var cached) &&
            File.Exists(cached))
        {
            launchTemplate = new EmulatorLaunchTemplate(cached, string.Empty);
            workingDirectory = Path.GetDirectoryName(cached) ?? trimmed;
            return true;
        }

        var resolvedExecutable = ResolveBestExecutableFromDirectory(trimmed);
        if (string.IsNullOrWhiteSpace(resolvedExecutable))
        {
            return false;
        }

        _resolvedExecutableCache[trimmed] = resolvedExecutable;
        launchTemplate = new EmulatorLaunchTemplate(resolvedExecutable, string.Empty);
        workingDirectory = Path.GetDirectoryName(resolvedExecutable) ?? trimmed;
        return true;
    }

    /// <summary>
    /// Launches a ROM via a plain emulator path or a launch template.
    /// Templates can include arguments and an optional {rom} placeholder.
    /// </summary>
    public GameLaunchSession LaunchWithEmulator(string emulatorPath, string romPath)
    {
        if (string.IsNullOrWhiteSpace(emulatorPath))
            throw new InvalidOperationException(
                "No emulator is configured for this platform. Open Settings -> Emulators to set one up.");

        var launchTemplate = ParseLaunchTemplate(emulatorPath);
        if (!File.Exists(launchTemplate.ExecutablePath))
            throw new FileNotFoundException(
                $"The emulator executable was not found at:\n{launchTemplate.ExecutablePath}\n\nUpdate the path in Settings -> Emulators.",
                launchTemplate.ExecutablePath);

        if (string.IsNullOrWhiteSpace(romPath))
            throw new InvalidOperationException("CloudBerry can't launch a ROM without a valid file path.");

        if (!File.Exists(romPath))
            throw new FileNotFoundException("The ROM file could not be found.", romPath);

        var startedAt = DateTime.Now;
        var startInfo = new ProcessStartInfo
        {
            FileName = launchTemplate.ExecutablePath,
            Arguments = BuildEmulatorArguments(launchTemplate.ArgumentTemplate, romPath),
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(launchTemplate.ExecutablePath) ?? string.Empty
        };

        var process = Process.Start(startInfo);
        return new GameLaunchSession(process, startedAt);
    }

    /// <summary>
    /// Pre-resolves and caches the winning executable for a directory-based install path.
    /// Designed to be called from Task.Run so the AllDirectories scan never blocks the UI thread.
    /// Subsequent Launch() calls for the same path return instantly from cache.
    /// </summary>
    public void PreResolveExecutable(string installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath)) return;
        var trimmed = installPath.Trim();
        if (!Directory.Exists(trimmed)) return;
        if (_resolvedExecutableCache.ContainsKey(trimmed)) return; // already cached
        var resolved = ResolveBestExecutableFromDirectory(trimmed);
        if (!string.IsNullOrWhiteSpace(resolved))
            _resolvedExecutableCache[trimmed] = resolved;
    }

    /// <summary>
    /// Returns true when installPath is a ROM extension that must be launched
    /// through an emulator rather than the shell directly.
    /// </summary>
    public static bool IsRomFile(string installPath)
    {
        var ext = Path.GetExtension(installPath).ToLowerInvariant();
        return ext is ".nes" or ".sfc" or ".smc" or ".gba" or ".gb" or ".gbc"
                   or ".n64" or ".z64" or ".v64" or ".nds" or ".3ds" or ".cia"
                   or ".gcm" or ".gcz" or ".rvz" or ".wbfs" or ".wad"
                   or ".pbp" or ".psx" or ".iso" or ".cue" or ".bin"
                   or ".chd" or ".m3u" or ".cso";
    }

    private static string ResolveBestExecutableFromDirectory(string directory)
    {
        var rootName = GameNameNormalizer.NormalizeTitleFromPath(directory);
        var rootTokens = Tokenize(rootName);

        return EnumerateExecutableCandidates(directory)
            .Select(path => new
            {
                Path = path,
                Score = ScoreExecutableCandidate(directory, path, rootTokens)
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Path.Length)
            .Select(candidate => candidate.Path)
            .FirstOrDefault() ?? string.Empty;
    }

    private static IEnumerable<string> EnumerateExecutableCandidates(string directory)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in SafeEnumerateFiles(directory, "*.exe", SearchOption.TopDirectoryOnly))
        {
            if (seen.Add(path))
            {
                yield return path;
            }
        }

        foreach (var path in SafeEnumerateFiles(directory, "*.exe", SearchOption.AllDirectories).Take(500))
        {
            if (seen.Add(path))
            {
                yield return path;
            }
        }
    }

    private static int ScoreExecutableCandidate(string rootDirectory, string executablePath, IReadOnlySet<string> rootTokens)
    {
        if (GameNameNormalizer.LooksLikeNonGamePayload(executablePath))
        {
            return 0;
        }

        var score = 20;
        var parent = Path.GetDirectoryName(executablePath) ?? string.Empty;
        if (string.Equals(parent.TrimEnd(Path.DirectorySeparatorChar), rootDirectory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            score += 35;
        }

        var normalizedExe = GameNameNormalizer.NormalizeTitleFromPath(executablePath);
        var exeTokens = Tokenize(normalizedExe);
        if (exeTokens.Count > 0 && rootTokens.Count > 0)
        {
            var overlap = exeTokens.Count(rootTokens.Contains);
            score += overlap * 14;
            if (overlap == rootTokens.Count)
            {
                score += 24;
            }
        }

        var lowerPath = executablePath.Replace('\\', '/').ToLowerInvariant();
        if (lowerPath.Contains("/binaries/", StringComparison.Ordinal) ||
            lowerPath.Contains("/win64/", StringComparison.Ordinal) ||
            lowerPath.Contains("shipping", StringComparison.Ordinal))
        {
            score += 16;
        }

        var fileName = Path.GetFileNameWithoutExtension(executablePath);
        if (fileName.Contains("launcher", StringComparison.OrdinalIgnoreCase))
        {
            score -= 8;
        }

        try
        {
            var sizeMb = new FileInfo(executablePath).Length / (1024 * 1024);
            score += (int)Math.Clamp(sizeMb, 0, 36);
        }
        catch
        {
            score += 1;
        }

        return score;
    }

    private static IEnumerable<string> SafeEnumerateFiles(string directory, string pattern, SearchOption searchOption)
    {
        try
        {
            return Directory.EnumerateFiles(directory, pattern, searchOption);
        }
        catch
        {
            return [];
        }
    }

    private static HashSet<string> Tokenize(string value) =>
        value.Split([' ', '-', '_', '.', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 1)
            .Select(token => token.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string BuildEmulatorArguments(string argumentTemplate, string romPath)
    {
        return string.IsNullOrWhiteSpace(argumentTemplate)
            ? Quote(romPath)
            : argumentTemplate.Contains("{rom}", StringComparison.OrdinalIgnoreCase)
                ? argumentTemplate.Replace("{rom}", Quote(romPath), StringComparison.OrdinalIgnoreCase)
                : $"{argumentTemplate} {Quote(romPath)}";
    }

    private static EmulatorLaunchTemplate ParseLaunchTemplate(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith('"'))
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            if (closingQuote > 1)
            {
                return new EmulatorLaunchTemplate(
                    trimmed[1..closingQuote],
                    trimmed[(closingQuote + 1)..].Trim());
            }
        }

        var exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex >= 0)
        {
            return new EmulatorLaunchTemplate(
                trimmed[..(exeIndex + 4)].Trim().Trim('"'),
                trimmed[(exeIndex + 4)..].Trim());
        }

        return new EmulatorLaunchTemplate(trimmed.Trim('"'), string.Empty);
    }

    private static string Quote(string value) => $"\"{value}\"";

    private sealed record EmulatorLaunchTemplate(string ExecutablePath, string ArgumentTemplate);
}
