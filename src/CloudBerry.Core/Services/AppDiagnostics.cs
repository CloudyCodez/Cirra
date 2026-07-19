using System.Text;

namespace CloudBerry.Core.Services;

public sealed class AppDiagnostics
{
    public AppDiagnostics(string? logPath = null)
    {
        LogPath = logPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CloudBerry",
            "logs",
            "cloudberry.log");
    }

    public string LogPath { get; }

    public void WriteInfo(string context, string message)
    {
        WriteEntry("INFO", context, message);
    }

    public void WriteWarning(string context, string message)
    {
        WriteEntry("WARN", context, message);
    }

    public void WriteException(string context, Exception exception)
    {
        var builder = new StringBuilder()
            .AppendLine(exception.Message)
            .AppendLine(exception.StackTrace ?? string.Empty);

        if (exception.InnerException is not null)
        {
            builder
                .AppendLine("--- INNER EXCEPTION ---")
                .AppendLine(exception.InnerException.Message)
                .AppendLine(exception.InnerException.StackTrace ?? string.Empty);
        }

        WriteEntry("ERROR", context, builder.ToString().Trim());
    }

    private void WriteEntry(string level, string context, string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        var content = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [{context}] {message}{Environment.NewLine}";
        File.AppendAllText(LogPath, content);
    }
}
