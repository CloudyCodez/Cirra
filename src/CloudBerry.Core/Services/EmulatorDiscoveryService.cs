namespace CloudBerry.Core.Services;

public sealed class EmulatorDiscoveryService
{
    private static readonly string[] RootSubfolders =
    [
        string.Empty,
        "Emulators",
        "Games",
        Path.Combine("Games", "Emulators"),
        "Applications",
        "Apps",
        "Tools"
    ];

    private static readonly IReadOnlyList<EmulatorDetectorRule> DirectRules =
    [
        new("PlayStation 3", "RPCS3", ["rpcs3.exe"], ["RPCS3"]),
        new("PlayStation 2", "PCSX2", ["pcsx2-qt.exe", "pcsx2.exe"], ["PCSX2"]),
        new("PlayStation", "DuckStation", ["duckstation-qt-x64-releaseltcg.exe", "duckstation-qt.exe", "duckstation.exe"], ["DuckStation"]),
        new("PSP", "PPSSPP", ["ppssppwindows64.exe", "ppssppqt.exe", "ppsspp.exe"], ["PPSSPP"]),
        new("Nintendo Switch", "Ryujinx / Yuzu", ["ryujinx.exe", "yuzu.exe", "sudachi.exe"], ["Ryujinx", "Yuzu", "Sudachi"]),
        new("Wii U", "Cemu", ["cemu.exe"], ["Cemu"]),
        new("GameCube", "Dolphin", ["dolphin.exe"], ["Dolphin"]),
        new("Wii", "Dolphin", ["dolphin.exe"], ["Dolphin"]),
        new("Nintendo 3DS", "Lime3DS / Citra", ["lime3ds-gui.exe", "citra-qt.exe", "azahar.exe"], ["Lime3DS", "Citra", "Azahar"]),
        new("Nintendo DS", "melonDS", ["melonds.exe", "desmume.exe"], ["melonDS", "DeSmuME"]),
        new("Nintendo 64", "Project64", ["project64.exe", "simple64-gui.exe", "simple64.exe"], ["Project64", "simple64"]),
        new("Game Boy", "mGBA", ["mgba.exe", "sameboy.exe"], ["mGBA", "SameBoy"]),
        new("Game Boy Color", "mGBA", ["mgba.exe", "sameboy.exe"], ["mGBA", "SameBoy"]),
        new("Game Boy Advance", "mGBA", ["mgba.exe"], ["mGBA"]),
        new("NES", "Mesen / FCEUX", ["mesen.exe", "fceux.exe"], ["Mesen", "FCEUX"]),
        new("SNES", "bsnes / Snes9x", ["bsnes.exe", "snes9x.exe"], ["bsnes", "Snes9x"]),
        new("Mega Drive", "BlastEm / Fusion", ["blastem.exe", "fusion.exe"], ["BlastEm", "Fusion"]),
        new("Dreamcast", "Flycast / Redream", ["flycast.exe", "redream.exe"], ["Flycast", "Redream"]),
        new("Sega Saturn", "YabaSanshiro / Mednafen", ["yabasanshiro.exe", "mednafen.exe"], ["YabaSanshiro", "Mednafen"]),
        new("Xbox", "xemu", ["xemu.exe"], ["xemu"]),
        new("Xbox 360", "Xenia", ["xenia.exe", "xenia_canary.exe"], ["Xenia"])
    ];

    public EmulatorDiscoveryResult Detect(RetroArchDetectionResult? retroArchResult = null)
    {
        var directSuggestions = DirectRules
            .Select(FindDirectSuggestion)
            .Where(item => item is not null)
            .Cast<DetectedEmulatorSuggestion>()
            .ToDictionary(item => item.Platform, StringComparer.OrdinalIgnoreCase);

        if (retroArchResult != null)
        {
            foreach (var suggestion in retroArchResult.Suggestions)
            {
                if (!directSuggestions.ContainsKey(suggestion.Platform))
                {
                    directSuggestions[suggestion.Platform] = new DetectedEmulatorSuggestion(
                        suggestion.Platform,
                        suggestion.CoreName,
                        suggestion.LaunchTemplate,
                        "RetroArch core",
                        true);
                }
            }
        }

        var ordered = directSuggestions.Values
            .OrderBy(item => item.Platform, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var summary = ordered.Count == 0
            ? "No emulator installs were found yet. Scan again after adding RetroArch or standalone emulators."
            : $"Found {ordered.Count} emulator mapping suggestion(s). Standalone emulators are preferred when present, then RetroArch cores fill the rest.";

        return new EmulatorDiscoveryResult(ordered, summary);
    }

    private static DetectedEmulatorSuggestion? FindDirectSuggestion(EmulatorDetectorRule rule)
    {
        foreach (var root in EnumerateSearchRoots())
        {
            foreach (var subfolder in RootSubfolders)
            {
                foreach (var folder in rule.FolderHints)
                {
                    var baseDirectory = string.IsNullOrWhiteSpace(subfolder) ? root : Path.Combine(root, subfolder);
                    foreach (var executable in rule.ExecutableNames)
                    {
                        var candidate = Path.Combine(baseDirectory, folder, executable);
                        if (File.Exists(candidate))
                        {
                            return new DetectedEmulatorSuggestion(rule.Platform, rule.DisplayName, Path.GetFullPath(candidate), "Installed emulator", false);
                        }
                    }
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSearchRoots()
    {
        var standardRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        };

        var driveRoots = DriveInfo.GetDrives()
            .Where(drive => drive.DriveType is DriveType.Fixed && drive.IsReady)
            .Select(drive => drive.RootDirectory.FullName);

        return standardRoots
            .Concat(driveRoots)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}

public sealed record EmulatorDiscoveryResult(
    IReadOnlyList<DetectedEmulatorSuggestion> Suggestions,
    string Summary);

public sealed record DetectedEmulatorSuggestion(
    string Platform,
    string EmulatorName,
    string LaunchValue,
    string SourceLabel,
    bool UsesRetroArch);

internal sealed record EmulatorDetectorRule(
    string Platform,
    string DisplayName,
    IReadOnlyList<string> ExecutableNames,
    IReadOnlyList<string> FolderHints);
