using System.Text.Json;

namespace CloudBerry.Core.Services;

public sealed class CirraUpdateInstallStateStore
{
    private const string StateFileName = "install-attempt.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _updateDirectory;

    public CirraUpdateInstallStateStore(string updateDirectory)
    {
        _updateDirectory = updateDirectory;
    }

    public string StatePath => Path.Combine(_updateDirectory, StateFileName);

    public CirraUpdateInstallAttempt? Read()
    {
        try
        {
            return File.Exists(StatePath)
                ? JsonSerializer.Deserialize<CirraUpdateInstallAttempt>(File.ReadAllText(StatePath), JsonOptions)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public void WritePending(string version, string packagePath)
    {
        Directory.CreateDirectory(_updateDirectory);
        var attempt = new CirraUpdateInstallAttempt(version, packagePath, DateTime.UtcNow);
        var tempPath = StatePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(attempt, JsonOptions));
        File.Move(tempPath, StatePath, overwrite: true);
    }

    public void Clear(bool deletePackage = false)
    {
        var attempt = Read();
        TryDelete(StatePath);
        TryDelete(StatePath + ".tmp");
        if (deletePackage && attempt is not null)
        {
            TryDelete(attempt.PackagePath);
        }
    }

    public CirraUpdateInstallDisposition Reconcile(Version currentVersion)
    {
        var attempt = Read();
        if (attempt is null || !Version.TryParse(attempt.Version, out var targetVersion))
        {
            return CirraUpdateInstallDisposition.None;
        }

        if (currentVersion >= targetVersion)
        {
            Clear(deletePackage: true);
            return CirraUpdateInstallDisposition.Completed;
        }

        return CirraUpdateInstallDisposition.BlockedRetry;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Cleanup will be attempted again on the next launch.
        }
    }
}

public sealed record CirraUpdateInstallAttempt(string Version, string PackagePath, DateTime AttemptedAtUtc);

public enum CirraUpdateInstallDisposition
{
    None,
    Completed,
    BlockedRetry
}
