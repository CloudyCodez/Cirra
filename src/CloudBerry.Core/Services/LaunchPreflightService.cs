using CloudBerry.Core.Models;
using System.Text.RegularExpressions;

namespace CloudBerry.Core.Services;

public sealed class LaunchPreflightService
{
    public LaunchPreflightResult Check(GameRecord game, string? configuredEmulator = null)
    {
        ArgumentNullException.ThrowIfNull(game);

        List<LaunchPreflightCheck> checks = [];
        var launchValue = game.InstallPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(launchValue))
        {
            checks.Add(new LaunchPreflightCheck(
                "game-path",
                "Game files",
                LaunchReadiness.Blocked,
                "No launch path is saved for this game.",
                "Choose Game File"));
            return BuildResult(game, checks);
        }

        if (GameLauncher.IsRomFile(launchValue))
        {
            AddRomChecks(checks, launchValue, game.EmulatorPath, configuredEmulator);
        }
        else
        {
            AddNativeChecks(checks, launchValue);
        }

        if (!game.IsInstalled)
        {
            checks.Add(new LaunchPreflightCheck(
                "install-state",
                "Install state",
                LaunchReadiness.Warning,
                "This title is marked not installed, although its launch target is available.",
                "Mark Installed"));
        }
        else
        {
            checks.Add(new LaunchPreflightCheck(
                "install-state",
                "Install state",
                LaunchReadiness.Ready,
                "This title is marked installed."));
        }

        return BuildResult(game, checks);
    }

    private static void AddNativeChecks(List<LaunchPreflightCheck> checks, string launchValue)
    {
        if (Uri.TryCreate(launchValue, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            checks.Add(new LaunchPreflightCheck(
                "launcher-uri",
                "Launcher link",
                LaunchReadiness.Ready,
                $"Cirra will hand this title to the {uri.Scheme} launcher."));
            return;
        }

        var executablePath = ExtractExecutablePath(launchValue);
        if (File.Exists(executablePath))
        {
            checks.Add(new LaunchPreflightCheck(
                "game-path",
                "Game files",
                LaunchReadiness.Ready,
                $"Launch target found in {Path.GetDirectoryName(executablePath) ?? executablePath}."));
            return;
        }

        if (Directory.Exists(launchValue))
        {
            checks.Add(new LaunchPreflightCheck(
                "game-folder",
                "Game folder",
                LaunchReadiness.Warning,
                "The install folder is available. Cirra will resolve its best executable during launch."));
            return;
        }

        checks.Add(new LaunchPreflightCheck(
            "game-path",
            "Game files",
            LaunchReadiness.Blocked,
            "The saved game file or install folder cannot be found.",
            "Repair Path"));
    }

    private static void AddRomChecks(
        List<LaunchPreflightCheck> checks,
        string romPath,
        string? gameEmulator,
        string? configuredEmulator)
    {
        checks.Add(File.Exists(romPath)
            ? new LaunchPreflightCheck("rom-path", "ROM file", LaunchReadiness.Ready, "The ROM file is available.")
            : new LaunchPreflightCheck("rom-path", "ROM file", LaunchReadiness.Blocked, "The ROM file cannot be found.", "Repair ROM Path"));

        var emulatorValue = !string.IsNullOrWhiteSpace(gameEmulator) ? gameEmulator : configuredEmulator;
        if (string.IsNullOrWhiteSpace(emulatorValue))
        {
            checks.Add(new LaunchPreflightCheck(
                "emulator",
                "Emulator",
                LaunchReadiness.Blocked,
                "No emulator is assigned to this game or platform.",
                "Map Emulator"));
            return;
        }

        var emulatorExecutable = ExtractExecutablePath(emulatorValue);
        if (!File.Exists(emulatorExecutable))
        {
            checks.Add(new LaunchPreflightCheck(
                "emulator",
                "Emulator",
                LaunchReadiness.Blocked,
                "The configured emulator executable cannot be found.",
                "Update Emulator"));
            return;
        }

        checks.Add(new LaunchPreflightCheck(
            "emulator",
            "Emulator",
            LaunchReadiness.Ready,
            $"Emulator found: {Path.GetFileName(emulatorExecutable)}"));

        if (!GameLauncher.IsRetroArchLaunchTemplate(emulatorValue))
        {
            return;
        }

        var corePath = ExtractRetroArchCorePath(emulatorValue);
        if (string.IsNullOrWhiteSpace(corePath))
        {
            checks.Add(new LaunchPreflightCheck(
                "retroarch-core",
                "RetroArch core",
                LaunchReadiness.Blocked,
                "RetroArch is mapped without a core for this platform.",
                "Rescan Cores"));
            return;
        }

        if (!File.Exists(corePath))
        {
            checks.Add(new LaunchPreflightCheck(
                "retroarch-core",
                "RetroArch core",
                LaunchReadiness.Blocked,
                $"The configured RetroArch core cannot be found: {Path.GetFileName(corePath)}",
                "Rescan Cores"));
            return;
        }

        checks.Add(new LaunchPreflightCheck(
            "retroarch-core",
            "RetroArch core",
            LaunchReadiness.Ready,
            $"Core found: {Path.GetFileName(corePath)}"));

        var supportedExtensions = RetroArchCoreDetector.ReadSupportedExtensions(corePath);
        var romExtension = Path.GetExtension(romPath);
        if (supportedExtensions.Count > 0 && !supportedExtensions.Contains(romExtension))
        {
            checks.Add(new LaunchPreflightCheck(
                "core-content",
                "ROM compatibility",
                LaunchReadiness.Blocked,
                $"{Path.GetFileName(corePath)} does not support {romExtension} files.",
                "Choose Compatible Core"));
            return;
        }

        if (supportedExtensions.Count > 0)
        {
            checks.Add(new LaunchPreflightCheck(
                "core-content",
                "ROM compatibility",
                LaunchReadiness.Ready,
                $"The selected core accepts {romExtension} files."));
        }

        if (Path.GetFileNameWithoutExtension(corePath).Contains("citra", StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(new LaunchPreflightCheck(
                "core-requirements",
                "Citra content",
                LaunchReadiness.Warning,
                "Citra cores require decrypted 3DS content. Encrypted cartridge dumps will not start."));
        }
    }

    private static string ExtractRetroArchCorePath(string value)
    {
        var match = Regex.Match(
            value,
            "(?:^|\\s)(?:-L|--libretro)\\s+(?:\"(?<quoted>[^\"]+)\"|(?<plain>\\S+))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success
            ? (match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["plain"].Value)
            : string.Empty;
    }

    private static string ExtractExecutablePath(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith('"'))
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            if (closingQuote > 1)
            {
                return trimmed[1..closingQuote];
            }
        }

        var executableEnd = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return executableEnd >= 0
            ? trimmed[..(executableEnd + 4)].Trim().Trim('"')
            : trimmed.Trim('"');
    }

    private static LaunchPreflightResult BuildResult(GameRecord game, List<LaunchPreflightCheck> checks)
    {
        return new LaunchPreflightResult
        {
            GameId = game.Id,
            GameTitle = game.Title,
            Checks = checks
        };
    }
}
