using System.IO;
using System.Security.Cryptography;
using System.Text;
using CloudBerry.App.Services;

namespace CloudBerry.App.Tests;

public sealed class AccessGateTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "CirraAccessGateTests", Guid.NewGuid().ToString("N"));
    private string MarkerPath => Path.Combine(_directory, "access.dat");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n\t")]
    [InlineData("not-an-invitation")]
    [InlineData("bear-extra")]
    public void InvalidKeysDoNotCreateGrant(string? key)
    {
        Assert.False(AccessGateService.IsValidKey(key));
        Assert.False(AccessGateService.TryRedeem(key, MarkerPath));
        Assert.False(File.Exists(MarkerPath));
    }

    [Theory]
    [InlineData("bear")]
    [InlineData(" IRU ")]
    [InlineData("\tCjMxO\r\n")]
    public void LegacyKeysKeepTrimAndCaseCompatibility(string key)
    {
        Assert.True(AccessGateService.IsValidKey(key));
    }

    [Fact]
    public void RedemptionPersistsAnEncryptedGrantWithoutTheKey()
    {
        Assert.False(AccessGateService.HasValidAccess(MarkerPath));
        Assert.True(AccessGateService.TryRedeem("bear", MarkerPath));
        Assert.True(AccessGateService.HasValidAccess(MarkerPath));
        var marker = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(MarkerPath),
            Encoding.UTF8.GetBytes("CloudBerry.AccessGate.v1"), DataProtectionScope.CurrentUser));
        Assert.StartsWith("CLOUDBERRY-ACCESS-GRANTED|", marker);
        Assert.DoesNotContain("bear", marker);
    }

    [Fact]
    public void CorruptOrUnrecognizedGrantsDoNotUnlock()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(MarkerPath, "invalid encrypted marker");
        Assert.False(AccessGateService.HasValidAccess(MarkerPath));
        File.WriteAllBytes(MarkerPath, ProtectedData.Protect(Encoding.UTF8.GetBytes("unrelated marker"),
            Encoding.UTF8.GetBytes("CloudBerry.AccessGate.v1"), DataProtectionScope.CurrentUser));
        Assert.False(AccessGateService.HasValidAccess(MarkerPath));
    }

    [Fact]
    public void ValidKeyStillAllowsSessionWhenMarkerCannotBeWritten()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "not-a-directory"), "test");
        Assert.True(AccessGateService.TryRedeem("bear", Path.Combine(_directory, "not-a-directory", "access.dat")));
        Assert.False(AccessGateService.HasValidAccess(MarkerPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
