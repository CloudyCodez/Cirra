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

    [Fact]
    public void Detect_PrefersExactModernCitraCoreOverLegacyPartialMatch()
    {
        var retroArchPath = Path.Combine(_rootPath, "retroarch.exe");
        var coresPath = Path.Combine(_rootPath, "cores");
        Directory.CreateDirectory(coresPath);
        File.WriteAllText(retroArchPath, "test");
        File.WriteAllText(Path.Combine(coresPath, "citra2018_libretro.dll"), "test");
        File.WriteAllText(Path.Combine(coresPath, "citra_libretro.dll"), "test");

        var result = new RetroArchCoreDetector().Detect(retroArchPath);

        var suggestion = Assert.Single(result.Suggestions, item => item.Platform == "Nintendo 3DS");
        Assert.Equal("citra_libretro.dll", suggestion.CoreName);
    }

    [Fact]
    public void ReadSupportedExtensions_ParsesRetroArchCoreInfo()
    {
        var coresPath = Path.Combine(_rootPath, "cores");
        var infoPath = Path.Combine(_rootPath, "info");
        Directory.CreateDirectory(coresPath);
        Directory.CreateDirectory(infoPath);
        var corePath = Path.Combine(coresPath, "citra2018_libretro.dll");
        File.WriteAllText(corePath, "test");
        File.WriteAllText(
            Path.Combine(infoPath, "citra2018_libretro.info"),
            "display_name = \"Citra 2018\"\nsupported_extensions = \"3ds|3dsx|cci|cxi|axf\"\n");

        var extensions = RetroArchCoreDetector.ReadSupportedExtensions(corePath);

        Assert.Contains(".cxi", extensions);
        Assert.Contains(".3ds", extensions);
        Assert.DoesNotContain(".cia", extensions);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
