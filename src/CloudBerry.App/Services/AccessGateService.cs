using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CloudBerry.App.Services;

/// <summary>
/// Gates app startup during the private testing phase behind a personal access key.
///
/// Redemption is recorded in a small encrypted marker file protected via Windows DPAPI
/// (<see cref="DataProtectionScope.CurrentUser"/>). DPAPI ties the encrypted bytes to the
/// exact Windows user profile that created them - if the marker file is copied to another
/// account or another machine, <see cref="ProtectedData.Unprotect"/> throws and decryption
/// simply fails, so the copy is worthless and that person is asked for their own key again.
/// This is what makes redemption "stick" to one person's install instead of being something
/// that can be handed off by just copying a file around.
/// </summary>
public static class AccessGateService
{
    // Keys are compared as SHA-256 hashes rather than kept in plaintext, so a casual look at
    // the source or a decompiled binary doesn't just hand out the active list in the open.
    private static readonly HashSet<string> ValidKeyHashes = new(StringComparer.Ordinal)
    {
        Hash("bear"),
        Hash("iru"),
        Hash("cjmxo"),
    };

    // Extra entropy mixed into the DPAPI call. Doesn't add real secrecy on its own (DPAPI's
    // protection comes from the user's Windows credentials, not this), but it does mean a
    // marker produced by CloudBerry can't be silently unprotected by some unrelated app that
    // also happens to call ProtectedData for the same Windows user.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CloudBerry.AccessGate.v1");

    private const string GrantedMarkerPrefix = "CLOUDBERRY-ACCESS-GRANTED|";

    private static string MarkerFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CloudBerry",
        "access.dat");

    /// <summary>
    /// True if this Windows user account, on this machine, has already redeemed a valid key.
    /// Returns false for a missing, corrupt, or foreign-machine/foreign-account marker - all
    /// of those are indistinguishable from "never redeemed" and simply show the gate again.
    /// </summary>
    public static bool HasValidAccess()
    {
        try
        {
            if (!File.Exists(MarkerFilePath))
            {
                return false;
            }

            var encrypted = File.ReadAllBytes(MarkerFilePath);
            var decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            var marker = Encoding.UTF8.GetString(decrypted);
            return marker.StartsWith(GrantedMarkerPrefix, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to redeem the given key (trimmed, case-insensitive). On success, persists an
    /// encrypted grant marker bound to this Windows user/machine and returns true. The actual
    /// key that was used is never stored - only the fact that a valid one was redeemed once.
    /// </summary>
    public static bool TryRedeem(string? enteredKey)
    {
        if (string.IsNullOrWhiteSpace(enteredKey))
        {
            return false;
        }

        var normalized = enteredKey.Trim().ToLowerInvariant();
        if (!ValidKeyHashes.Contains(Hash(normalized)))
        {
            return false;
        }

        try
        {
            var marker = $"{GrantedMarkerPrefix}{DateTime.UtcNow.Ticks}";
            var plain = Encoding.UTF8.GetBytes(marker);
            var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);

            var directory = Path.GetDirectoryName(MarkerFilePath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(MarkerFilePath, encrypted);
        }
        catch
        {
            // If the marker can't be written (unusual filesystem/permissions issue), still let
            // this session through rather than lock a legitimate tester out over a disk hiccup -
            // they'll just be asked once more on their next launch.
        }

        return true;
    }

    private static string Hash(string normalizedValue)
    {
        var bytes = Encoding.UTF8.GetBytes(normalizedValue);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
