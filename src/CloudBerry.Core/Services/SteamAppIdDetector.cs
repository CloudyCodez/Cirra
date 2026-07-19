using System.Text.RegularExpressions;

namespace CloudBerry.Core.Services;

public sealed partial class SteamAppIdDetector
{
    public string TryDetectAppId(string installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return string.Empty;
        }

        var fileDirectory = Path.GetDirectoryName(Path.GetFullPath(installPath));
        if (string.IsNullOrWhiteSpace(fileDirectory) || !Directory.Exists(fileDirectory))
        {
            return string.Empty;
        }

        var appIdFromFile = TryReadSteamAppIdFile(fileDirectory);
        if (!string.IsNullOrWhiteSpace(appIdFromFile))
        {
            return appIdFromFile;
        }

        return TryReadSteamManifest(fileDirectory);
    }

    private static string TryReadSteamAppIdFile(string directoryPath)
    {
        var steamAppIdPath = Path.Combine(directoryPath, "steam_appid.txt");
        if (!File.Exists(steamAppIdPath))
        {
            return string.Empty;
        }

        var content = File.ReadAllText(steamAppIdPath).Trim();
        return DigitsOnlyRegex().IsMatch(content) ? content : string.Empty;
    }

    private static string TryReadSteamManifest(string directoryPath)
    {
        var commonDirectory = new DirectoryInfo(directoryPath);
        while (commonDirectory is not null &&
               !commonDirectory.Name.Equals("common", StringComparison.OrdinalIgnoreCase))
        {
            commonDirectory = commonDirectory.Parent;
        }

        if (commonDirectory?.Parent is null)
        {
            return string.Empty;
        }

        var expectedInstallDir = commonDirectory.FullName == directoryPath
            ? string.Empty
            : Path.GetRelativePath(commonDirectory.FullName, directoryPath)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .FirstOrDefault()
                ?? string.Empty;

        if (string.IsNullOrWhiteSpace(expectedInstallDir))
        {
            expectedInstallDir = new DirectoryInfo(directoryPath).Name;
        }

        var steamAppsDirectory = commonDirectory.Parent.FullName;
        foreach (var manifestPath in Directory.EnumerateFiles(steamAppsDirectory, "appmanifest_*.acf", SearchOption.TopDirectoryOnly))
        {
            var content = File.ReadAllText(manifestPath);
            var installDirMatch = InstallDirRegex().Match(content);
            var appIdMatch = AppIdRegex().Match(content);

            if (!installDirMatch.Success || !appIdMatch.Success)
            {
                continue;
            }

            var manifestInstallDir = installDirMatch.Groups["value"].Value;
            if (!manifestInstallDir.Equals(expectedInstallDir, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return appIdMatch.Groups["value"].Value;
        }

        return string.Empty;
    }

    [GeneratedRegex("^\\d+$", RegexOptions.Compiled)]
    private static partial Regex DigitsOnlyRegex();

    [GeneratedRegex("\"appid\"\\s+\"(?<value>\\d+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex AppIdRegex();

    [GeneratedRegex("\"installdir\"\\s+\"(?<value>[^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex InstallDirRegex();
}
