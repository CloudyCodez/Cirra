using CloudBerry.App.Services;
using CloudBerry.Core.Models;
using CloudBerry.Core.Services;

namespace CloudBerry.App.Tests;

public sealed class HorizonControllerRouterTests
{
    [Fact]
    public void DiscoversAControllerInAnyXInputSlot()
    {
        var source = new FakeControllerSource { States = { [2] = Connected(ControllerButtons.A) } };
        var frame = new HorizonControllerRouter(source).Poll(1000);

        Assert.True(frame.IsAnyConnected);
        Assert.Equal(2, frame.ActiveSlot);
        Assert.Equal(ControllerButtons.A, frame.NewButtons);
        Assert.Equal(1, frame.ConnectedCount);
    }

    [Fact]
    public void KeepsTheFirstOwnerWhenAnotherControllerConnects()
    {
        var source = new FakeControllerSource { States = { [1] = Connected() } };
        var router = new HorizonControllerRouter(source);
        Assert.Equal(1, router.Poll(1000).ActiveSlot);

        source.States[0] = Connected(ControllerButtons.A);
        var frame = router.Poll(1100);

        Assert.Equal(1, frame.ActiveSlot);
        Assert.Equal(ControllerButtons.None, frame.NewButtons);
        Assert.Equal(2, frame.ConnectedCount);
    }

    [Fact]
    public void ReassignsNavigationWhenTheActiveControllerDisconnects()
    {
        var source = new FakeControllerSource
        {
            States = { [1] = Connected(), [3] = Connected(ControllerButtons.DPadRight) }
        };
        var router = new HorizonControllerRouter(source);
        Assert.Equal(1, router.Poll(1000).ActiveSlot);

        source.States.Remove(1);
        var frame = router.Poll(1400);

        Assert.Equal(3, frame.ActiveSlot);
        Assert.True(frame.ActiveSlotChanged);
        Assert.True(frame.ActiveSlotDisconnected);
        Assert.Equal(ControllerButtons.DPadRight, frame.NewButtons);
    }

    [Fact]
    public void SuspendConsumesHeldInputUntilThePlayerReleasesAndPressesAgain()
    {
        var source = new FakeControllerSource { States = { [0] = Connected(ControllerButtons.A) } };
        var router = new HorizonControllerRouter(source);
        Assert.Equal(ControllerButtons.A, router.Poll(1000).NewButtons);

        router.Suspend();
        Assert.Equal(ControllerButtons.None, router.Poll(1100).NewButtons);
        source.States[0] = Connected();
        router.Poll(1200);
        source.States[0] = Connected(ControllerButtons.A);
        Assert.Equal(ControllerButtons.A, router.Poll(1300).NewButtons);
    }

    [Fact]
    public void ReportsACleanDisconnectedState()
    {
        var source = new FakeControllerSource { States = { [0] = Connected() } };
        var router = new HorizonControllerRouter(source);
        router.Poll(1000);
        source.States.Clear();

        var frame = router.Poll(1200);

        Assert.False(frame.IsAnyConnected);
        Assert.Equal(-1, frame.ActiveSlot);
        Assert.True(frame.ActiveSlotDisconnected);
    }

    private static ControllerRawState Connected(ControllerButtons buttons = ControllerButtons.None) =>
        new(true, Buttons: buttons);

    private sealed class FakeControllerSource : IControllerInputSource
    {
        public Dictionary<int, ControllerRawState> States { get; } = [];
        public ControllerRawState Read(int slot) => States.TryGetValue(slot, out var state) ? state : new ControllerRawState(false);
    }
}
