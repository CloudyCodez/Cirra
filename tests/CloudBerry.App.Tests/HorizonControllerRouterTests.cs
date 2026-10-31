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

    [Fact]
    public void ReturningFromGameRequiresAnalogNeutralBeforeNavigating()
    {
        var source = new FakeControllerSource();
        source.States[0] = new ControllerRawState(true, LeftThumbX: 20000);
        var router = new HorizonControllerRouter(source);
        router.Poll(1000);
        router.Suspend();
        Assert.Null(router.Poll(1100).Direction);
        Assert.Null(router.Poll(1500).Direction);
        source.States[0] = Connected();
        Assert.Null(router.Poll(1600).Direction);
        source.States[0] = new ControllerRawState(true, LeftThumbX: 20000);
        Assert.Equal(System.Windows.Input.FocusNavigationDirection.Right, router.Poll(1700).Direction);
    }

    private sealed class FakeControllerSource : IControllerInputSource
    {
        public Dictionary<int, ControllerRawState> States { get; } = [];
        public ControllerRawState Read(int slot) => States.TryGetValue(slot, out var state) ? state : new ControllerRawState(false);
    }
}
