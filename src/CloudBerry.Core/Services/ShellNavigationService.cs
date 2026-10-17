using System.Diagnostics;

namespace CloudBerry.Core.Services;

public sealed class ShellNavigationService
{
    public void OpenContainingFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Cirra couldn't find a launch target to open.");
        }

        var fullPath = GameLauncher.TryResolveLaunchTarget(path, out var resolvedTarget, out var workingDirectory)
            ? resolvedTarget
            : path.Trim();

        if (Uri.TryCreate(fullPath, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            throw new InvalidOperationException("Cirra cannot open a local folder for storefront launch links.");
        }

        if (Uri.TryCreate(fullPath, UriKind.Absolute, out uri) && uri.IsFile)
        {
            fullPath = uri.LocalPath;
        }

        fullPath = Path.GetFullPath(fullPath.Trim('"'));
        if (File.Exists(fullPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{fullPath}\"",
                UseShellExecute = true
            });

            return;
        }

        var directoryPath = Directory.Exists(fullPath)
            ? fullPath
            : !string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory)
                ? workingDirectory
                : Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = directoryPath,
                UseShellExecute = true
            });

            return;
        }

        throw new FileNotFoundException("The selected game's folder could not be found.", fullPath);
    }

    public void OpenFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new InvalidOperationException("Cirra needs a folder path to open.");
        }

        var fullPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = fullPath,
            UseShellExecute = true
        });
    }

    public void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Cirra needs a valid web link before it can open that page.");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = uri.AbsoluteUri,
            UseShellExecute = true
        });
    }
}
