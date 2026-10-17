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
    // This offline allowlist is a beta invitation gate, not server-enforced licensing.
    // Existing keys remain valid; never put new plaintext invitations in the source.
    private static readonly HashSet<string> ValidKeyHashes = new(StringComparer.Ordinal)
    {
        "BC98BB50E8094B2AC3CEB90BA2512587C0513CD294A07EFCFDCF467198DA6266",
        "095CAC40C31D4BEC0DDBEB2999E792DE517C364B9EDB8FA33DA6580FE76853F5",
        "D6F7EF93CEFAB4D11F99C075523FB126ACBA1B4BF52153EA680FFA0B8C05B214",
        // September 2026 closed beta: invitations 1-5, in the issued order.
        "EE9EC5A4B144A216C84138D6E917C97E44C74FCB4BE2D1C4E4DBBCD04BC01D1E",
        "060B3885C7F407575CF5DC2E5ADC0E76765128F846590B91772225A866C2727C",
        "A21D9A57E605927827668D98D39D7D5FE5988A4245A40EAD358242AA6EFFD45B",
        "010523CF176DBEF9D5E97A2FE60A7696F738EB0799DDB8A819EBDF3517C58E0E",
        "6D168B5EB2AC08C554D94E57B66D5E5BC32512997CEE50AB889F42D2C4882C64",
    };

    // Keep this legacy value and marker location so previously activated installs still work.
    // Entropy is application context, not a secret or protection from other code under this user.
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
    public static bool HasValidAccess() => HasValidAccess(MarkerFilePath);

    internal static bool HasValidAccess(string markerFilePath)
    {
        try
        {
            if (!File.Exists(markerFilePath))
            {
                return false;
            }

            var encrypted = File.ReadAllBytes(markerFilePath);
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
    public static bool TryRedeem(string? enteredKey) => TryRedeem(enteredKey, MarkerFilePath);

    internal static bool IsValidKey(string? enteredKey) =>
        !string.IsNullOrWhiteSpace(enteredKey) && ValidKeyHashes.Contains(Hash(enteredKey.Trim().ToLowerInvariant()));

    internal static bool TryRedeem(string? enteredKey, string markerFilePath)
    {
        if (!IsValidKey(enteredKey))
        {
            return false;
        }

        try
        {
            var marker = $"{GrantedMarkerPrefix}{DateTime.UtcNow.Ticks}";
            var plain = Encoding.UTF8.GetBytes(marker);
            var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);

            var directory = Path.GetDirectoryName(markerFilePath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(markerFilePath, encrypted);
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
