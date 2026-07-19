using CloudBerry.Core.Services;

namespace CloudBerry.Core.Tests;

public sealed class RetroArchCoreDetectorTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "CloudBerryRetroArchTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Detect_MapsInstalledCoresToPlatformLaunchTemplates()
    {
        var retroArchPath = Path.Combine(_rootPath, "retroarch.exe");
        var coresPath = Path.Combine(_rootPath, "cores");
        Directory.CreateDirectory(coresPath);
        File.WriteAllText(retroArchPath, "test");
        File.WriteAllText(Path.Combine(coresPath, "snes9x_libretro.dll"), "test");
        File.WriteAllText(Path.Combine(coresPath, "mgba_libretro.dll"), "test");
        File.WriteAllText(Path.Combine(coresPath, "mupen64plus_next_libretro.dll"), "test");

        var detector = new RetroArchCoreDetector();
        var result = detector.Detect(retroArchPath);

        Assert.Equal(retroArchPath, result.RetroArchPath);
        Assert.Contains(result.Suggestions, suggestion =>
            suggestion.Platform == "SNES" &&
            suggestion.LaunchTemplate.Contains("-L", StringComparison.Ordinal) &&
            suggestion.LaunchTemplate.Contains("{rom}", StringComparison.Ordinal));
        Assert.Contains(result.Suggestions, suggestion => suggestion.Platform == "Game Boy Advance");
        Assert.Contains(result.Suggestions, suggestion => suggestion.Platform == "Nintendo 64");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
