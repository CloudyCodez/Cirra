using CloudBerry.Core.Services;

namespace CloudBerry.Core.Tests;

public sealed class SteamAppIdDetectorTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "CloudBerrySteamDetect", Guid.NewGuid().ToString("N"));

    [Fact]
    public void TryDetectAppId_FromSteamManifest_ReturnsAppId()
    {
        var steamAppsDirectory = Directory.CreateDirectory(Path.Combine(_rootPath, "steamapps"));
        var commonDirectory = Directory.CreateDirectory(Path.Combine(steamAppsDirectory.FullName, "common", "Celeste"));
        var executablePath = Path.Combine(commonDirectory.FullName, "Celeste.exe");
        File.WriteAllText(executablePath, "test");
        File.WriteAllText(
            Path.Combine(steamAppsDirectory.FullName, "appmanifest_504230.acf"),
            """
            "AppState"
            {
                "appid"        "504230"
                "installdir"   "Celeste"
            }
            """
        );

        var detector = new SteamAppIdDetector();

        var appId = detector.TryDetectAppId(executablePath);

        Assert.Equal("504230", appId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
