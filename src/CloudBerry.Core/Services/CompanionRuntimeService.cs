using CloudBerry.Core.Models;
using System.Diagnostics;

namespace CloudBerry.Core.Services;

public sealed class CompanionRuntimeService
{
    private static readonly string[] ExecutableExtensions = OperatingSystem.IsWindows()
        ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        : [string.Empty];

    public IReadOnlyList<CompanionToolDescriptor> DiscoverTools()
    {
        return
        [
            Discover("python", "Python", "Python", "Metadata normalization, scraper-style enrichment, and batch maintenance scripts.", ["py.exe", "python3.exe", "python.exe", "python"], "--version"),
            Discover("node", "Node.js", "JavaScript", "Fast web/API helpers, artwork processing pipelines, and future plugin tooling.", ["node.exe", "node"], "--version"),
            Discover("powershell", "PowerShell", "PowerShell", "Windows library discovery, diagnostics, and local automation helpers.", ["pwsh.exe", "powershell.exe"], "-NoProfile -Command \"$PSVersionTable.PSVersion.ToString()\""),
            Discover("ffmpeg", "FFmpeg", "Native CLI", "Trailer thumbnails, media conversion, and artwork/video cleanup jobs.", ["ffmpeg.exe", "ffmpeg"], "-version")
        ];
    }

    public async Task<CompanionToolRunResult> RunAsync(
        string executablePath,
        string arguments,
        string? standardInput = null,
        int timeoutMilliseconds = 15000,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return new CompanionToolRunResult
            {
                ExitCode = -1,
                StandardError = "The companion tool executable could not be found."
            };
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeoutMilliseconds);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            RedirectStandardInput = standardInput != null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
            if (standardInput != null)
            {
                await process.StandardInput.WriteAsync(standardInput.AsMemory(), timeoutSource.Token);
                process.StandardInput.Close();
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeoutSource.Token);
            await process.WaitForExitAsync(timeoutSource.Token);

            return new CompanionToolRunResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = await outputTask,
                StandardError = await errorTask
            };
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return new CompanionToolRunResult
            {
                ExitCode = -1,
                TimedOut = true,
                StandardError = "The companion tool timed out."
            };
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return new CompanionToolRunResult
            {
                ExitCode = -1,
                StandardError = exception.Message
            };
        }
    }

    private static CompanionToolDescriptor Discover(
        string id,
        string displayName,
        string language,
        string purpose,
        IReadOnlyList<string> executableNames,
        string versionArguments)
    {
        var executablePath = executableNames
            .Select(FindExecutable)
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)) ?? string.Empty;

        return new CompanionToolDescriptor
        {
            Id = id,
            DisplayName = displayName,
            Language = language,
            Purpose = purpose,
            ExecutablePath = executablePath,
            Version = string.IsNullOrWhiteSpace(executablePath) ? string.Empty : ReadVersion(executablePath, versionArguments),
            IsAvailable = !string.IsNullOrWhiteSpace(executablePath)
        };
    }

    private static string FindExecutable(string executableName)
    {
        if (Path.IsPathFullyQualified(executableName) && File.Exists(executableName))
        {
            return executableName;
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var candidateName in ExpandCandidateNames(executableName))
            {
                var candidate = Path.Combine(directory, candidateName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> ExpandCandidateNames(string executableName)
    {
        if (Path.HasExtension(executableName) || !OperatingSystem.IsWindows())
        {
            yield return executableName;
            yield break;
        }

        foreach (var extension in ExecutableExtensions)
        {
            yield return executableName + extension;
        }
    }

    private static string ReadVersion(string executablePath, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            if (!process.WaitForExit(1200))
            {
                TryKill(process);
                return "Detected";
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            var error = process.StandardError.ReadToEnd().Trim();
            return CleanVersionLine(string.IsNullOrWhiteSpace(output) ? error : output);
        }
        catch
        {
            return "Detected";
        }
    }

    private static string CleanVersionLine(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Detected";
        }

        var firstLine = value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "Detected";

        return firstLine.Length > 80 ? firstLine[..80] : firstLine;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}
