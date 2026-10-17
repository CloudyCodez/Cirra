using CloudBerry.Core.Models;
using CloudBerry.Core.Services;

namespace CloudBerry.Core.Tests;

public sealed class LaunchPreflightServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CirraPreflightTests", Guid.NewGuid().ToString("N"));
    private readonly LaunchPreflightService _service = new();

    public LaunchPreflightServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task Check_NativeExecutableExists_IsReady()
    {
        var executable = Path.Combine(_root, "Game.exe");
        await File.WriteAllTextAsync(executable, "stub");

        var result = _service.Check(new GameRecord { Title = "Game", InstallPath = executable });

        Assert.True(result.CanLaunch);
        Assert.Equal(LaunchReadiness.Ready, result.Readiness);
        Assert.Equal("Ready to launch", result.Summary);
    }

    [Fact]
    public void Check_MissingNativeTarget_IsBlockedWithRepairAction()
    {
        var result = _service.Check(new GameRecord
        {
            Title = "Missing",
            InstallPath = Path.Combine(_root, "missing.exe")
        });

        Assert.False(result.CanLaunch);
        var blocking = Assert.Single(result.Checks, check => check.Readiness == LaunchReadiness.Blocked);
        Assert.Equal("Repair Path", blocking.ActionLabel);
    }

    [Fact]
    public async Task Check_RomAndEmulatorExist_IsReady()
    {
        var rom = Path.Combine(_root, "game.gba");
        var emulator = Path.Combine(_root, "mGBA.exe");
        await File.WriteAllTextAsync(rom, "rom");
        await File.WriteAllTextAsync(emulator, "emulator");

        var result = _service.Check(
            new GameRecord { Title = "ROM", InstallPath = rom },
            $"\"{emulator}\" --fullscreen");

        Assert.True(result.CanLaunch);
        Assert.Equal(LaunchReadiness.Ready, result.Readiness);
        Assert.Contains(result.Checks, check => check.Key == "rom-path" && check.Readiness == LaunchReadiness.Ready);
        Assert.Contains(result.Checks, check => check.Key == "emulator" && check.Readiness == LaunchReadiness.Ready);
    }

    [Fact]
    public async Task Check_RomWithoutEmulator_IsBlockedAndExplainsSetup()
    {
        var rom = Path.Combine(_root, "game.gba");
        await File.WriteAllTextAsync(rom, "rom");

        var result = _service.Check(new GameRecord { Title = "ROM", InstallPath = rom });

        Assert.False(result.CanLaunch);
        Assert.Contains(result.Checks, check => check.Key == "emulator" && check.ActionLabel == "Map Emulator");
    }

    [Fact]
    public async Task Check_RetroArchCxiWithCompatibleCore_IsReady()
    {
        var rom = Path.Combine(_root, "game.cxi");
        var emulator = Path.Combine(_root, "retroarch.exe");
        var coresPath = Path.Combine(_root, "cores");
        var infoPath = Path.Combine(_root, "info");
        Directory.CreateDirectory(coresPath);
        Directory.CreateDirectory(infoPath);
        var core = Path.Combine(coresPath, "citra2018_libretro.dll");
        await File.WriteAllTextAsync(rom, "rom");
        await File.WriteAllTextAsync(emulator, "emulator");
        await File.WriteAllTextAsync(core, "core");
        await File.WriteAllTextAsync(
            Path.Combine(infoPath, "citra2018_libretro.info"),
            "supported_extensions = \"3ds|3dsx|cci|cxi|axf\"");

        var result = _service.Check(
            new GameRecord { Title = "3DS ROM", InstallPath = rom },
            RetroArchCoreDetector.BuildLaunchTemplate(emulator, core));

        Assert.True(result.CanLaunch);
        Assert.Contains(result.Checks, check => check.Key == "retroarch-core" && check.Readiness == LaunchReadiness.Ready);
        Assert.Contains(result.Checks, check => check.Key == "core-content" && check.Readiness == LaunchReadiness.Ready);
        Assert.Contains(result.Checks, check => check.Key == "core-requirements" && check.Readiness == LaunchReadiness.Warning);
    }

    [Fact]
    public async Task Check_RetroArchContentUnsupportedByCore_IsBlocked()
    {
        var rom = Path.Combine(_root, "game.cia");
        var emulator = Path.Combine(_root, "retroarch.exe");
        var coresPath = Path.Combine(_root, "cores");
        var infoPath = Path.Combine(_root, "info");
        Directory.CreateDirectory(coresPath);
        Directory.CreateDirectory(infoPath);
        var core = Path.Combine(coresPath, "citra2018_libretro.dll");
        await File.WriteAllTextAsync(rom, "rom");
        await File.WriteAllTextAsync(emulator, "emulator");
        await File.WriteAllTextAsync(core, "core");
        await File.WriteAllTextAsync(
            Path.Combine(infoPath, "citra2018_libretro.info"),
            "supported_extensions = \"3ds|3dsx|cci|cxi|axf\"");

        var result = _service.Check(
            new GameRecord { Title = "3DS ROM", InstallPath = rom },
            RetroArchCoreDetector.BuildLaunchTemplate(emulator, core));

        Assert.False(result.CanLaunch);
        Assert.Contains(result.Checks, check => check.Key == "core-content" && check.Readiness == LaunchReadiness.Blocked);
    }

    [Fact]
    public async Task Check_RetroArchMissingMappedCore_IsBlocked()
    {
        var rom = Path.Combine(_root, "game.cxi");
        var emulator = Path.Combine(_root, "retroarch.exe");
        var missingCore = Path.Combine(_root, "cores", "missing_libretro.dll");
        await File.WriteAllTextAsync(rom, "rom");
        await File.WriteAllTextAsync(emulator, "emulator");

        var result = _service.Check(
            new GameRecord { Title = "3DS ROM", InstallPath = rom },
            RetroArchCoreDetector.BuildLaunchTemplate(emulator, missingCore));

        Assert.False(result.CanLaunch);
        Assert.Contains(result.Checks, check =>
            check.Key == "retroarch-core" &&
            check.Readiness == LaunchReadiness.Blocked &&
            check.ActionLabel == "Rescan Cores");
    }

    [Fact]
    public void Check_StorefrontUri_IsReadyForLauncherHandoff()
    {
        var result = _service.Check(new GameRecord
        {
            Title = "Store Game",
            InstallPath = "steam://rungameid/12345"
        });

        Assert.True(result.CanLaunch);
        Assert.Contains(result.Checks, check => check.Key == "launcher-uri" && check.Readiness == LaunchReadiness.Ready);
    }

    [Fact]
    public void Check_InstallDirectory_DoesNotBlockWhileSearchingAndReturnsWarning()
    {
        var installDirectory = Path.Combine(_root, "Large Install");
        Directory.CreateDirectory(installDirectory);

        var result = _service.Check(new GameRecord { Title = "Folder Game", InstallPath = installDirectory });

        Assert.True(result.CanLaunch);
        Assert.Equal(LaunchReadiness.Warning, result.Readiness);
        Assert.Contains(result.Checks, check => check.Key == "game-folder");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }
}
