using System.Security.Cryptography;
using System.Text;

namespace CloudBerry.Core.Services;

public sealed record LibraryArtifactIdentity(
    string CanonicalKey,
    string CanonicalPath,
    string OriginalPath)
{
    public static LibraryArtifactIdentity Create(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("An artifact path is required.", nameof(path));
        }

        var expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        var fullPath = Path.GetFullPath(expanded);
        var canonicalPath = NormalizePath(fullPath);
        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPath));
        return new LibraryArtifactIdentity(
            Convert.ToHexString(keyBytes).ToLowerInvariant(),
            canonicalPath,
            path);
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        normalized = Path.TrimEndingDirectorySeparator(normalized);
        return OperatingSystem.IsWindows() ? normalized.ToUpperInvariant() : normalized;
    }
}
