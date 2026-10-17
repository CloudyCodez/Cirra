using CloudBerry.Core.Models;
using CloudBerry.Core.Services;

namespace CloudBerry.Core.Tests;

public sealed class InputLabServiceTests
{
    [Fact]
    public void SampleAll_ReturnsEveryXInputSlot()
    {
        var service = new InputLabService(new FakeSource(slot =>
            slot == 1 ? new ControllerRawState(true, Buttons: ControllerButtons.A) : new ControllerRawState(false)));

        var snapshots = service.SampleAll();

        Assert.Equal(4, snapshots.Count);
        Assert.False(snapshots[0].IsConnected);
        Assert.True(snapshots[1].IsConnected);
        Assert.Equal(ControllerButtons.A, snapshots[1].Buttons);
    }

    [Fact]
    public void Analyze_NormalizesAxesTriggersAndButtons()
    {
        var raw = new ControllerRawState(
            true,
            42,
            ControllerButtons.A | ControllerButtons.DPadUp,
            128,
            255,
            short.MinValue,
            short.MaxValue,
            0,
            0);

        var snapshot = InputLabService.Analyze(0, raw);

        Assert.Equal(-1, snapshot.LeftStickX);
        Assert.Equal(1, snapshot.LeftStickY);
        Assert.Equal(1, snapshot.RightTrigger);
        Assert.InRange(snapshot.LeftTrigger, 0.50, 0.51);
        Assert.True(snapshot.HasStickDrift);
    }

    [Fact]
    public void Analyze_ReportsQuietConnectedControllerAsHealthy()
    {
        var snapshot = InputLabService.Analyze(2, new ControllerRawState(true, 1, LeftThumbX: 1200), 0.12);

        Assert.False(snapshot.HasStickDrift);
        Assert.Equal("Input stream is healthy.", snapshot.HealthMessage);
    }

    private sealed class FakeSource(Func<int, ControllerRawState> read) : IControllerInputSource
    {
        public ControllerRawState Read(int slot) => read(slot);
    }
}
