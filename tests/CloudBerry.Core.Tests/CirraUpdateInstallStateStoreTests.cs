using CloudBerry.Core.Services;

namespace CloudBerry.Core.Tests;

public sealed class CirraUpdateInstallStateStoreTests
{
    [Fact]
    public void Reconcile_BlocksAutomaticRetryWhenAttemptDidNotAdvanceVersion()
    {
        string directory = CreateTempDirectory();
        try
        {
            var store = new CirraUpdateInstallStateStore(directory);
            string package = Path.Combine(directory, "cirra-update-0.4.2.exe");
            File.WriteAllText(package, "installer");
            store.WritePending("0.4.2", package);

            var disposition = store.Reconcile(new Version(0, 4, 1));

            Assert.Equal(CirraUpdateInstallDisposition.BlockedRetry, disposition);
            Assert.NotNull(store.Read());
            Assert.True(File.Exists(package));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Reconcile_CleansCompletedAttemptAndInstaller()
    {
        string directory = CreateTempDirectory();
        try
        {
            var store = new CirraUpdateInstallStateStore(directory);
            string package = Path.Combine(directory, "cirra-update-0.4.2.exe");
            File.WriteAllText(package, "installer");
            store.WritePending("0.4.2", package);

            var disposition = store.Reconcile(new Version(0, 4, 2));

            Assert.Equal(CirraUpdateInstallDisposition.Completed, disposition);
            Assert.Null(store.Read());
            Assert.False(File.Exists(package));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "cirra-update-state-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
