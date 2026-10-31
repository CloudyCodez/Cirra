using System.Windows.Input;
using CloudBerry.Core.Models;
using CloudBerry.Core.Services;

namespace CloudBerry.App.Services;

/// <summary>
/// Gives Horizon one dependable controller owner while still discovering all four XInput slots.
/// A connected owner keeps navigation until it disconnects, preventing two people from moving
/// focus at once. When that device disappears, the lowest available slot takes over immediately.
/// </summary>
public sealed class HorizonControllerRouter
{
    private const int SlotCount = 4;
    private readonly IControllerInputSource _source;
    private readonly ControllerButtons[] _previousButtons = new ControllerButtons[SlotCount];
    private readonly ControllerDirectionRepeater[] _directions =
        Enumerable.Range(0, SlotCount).Select(_ => new ControllerDirectionRepeater()).ToArray();
    private bool _suppressNextInput;

    public HorizonControllerRouter(IControllerInputSource? source = null)
    {
        _source = source ?? new XInputControllerSource();
    }

    public int ActiveSlot { get; private set; } = -1;

    public void Suspend()
    {
        _suppressNextInput = true;
        foreach (var repeater in _directions) repeater.Reset();
    }

    public ControllerInputFrame Poll(long milliseconds)
    {
        var states = new ControllerRawState[SlotCount];
        var connectedSlots = new List<int>(SlotCount);
        for (var slot = 0; slot < SlotCount; slot++)
        {
            states[slot] = _source.Read(slot);
            if (states[slot].IsConnected) connectedSlots.Add(slot);
        }

        if (connectedSlots.Count == 0)
        {
            var disconnected = ActiveSlot >= 0;
            ActiveSlot = -1;
            Array.Clear(_previousButtons);
            foreach (var repeater in _directions) repeater.Reset();
            _suppressNextInput = false;
            return new ControllerInputFrame(false, 0, -1, new ControllerRawState(false), ControllerButtons.None, default, false, disconnected);
        }

        bool changed = false;
        bool disconnectedOwner = ActiveSlot >= 0 && !states[ActiveSlot].IsConnected;
        if (ActiveSlot < 0 || disconnectedOwner)
        {
            ActiveSlot = connectedSlots[0];
            changed = true;
            _previousButtons[ActiveSlot] = ControllerButtons.None;
            _directions[ActiveSlot].Reset();
        }

        var state = states[ActiveSlot];
        if (_suppressNextInput)
        {
            // Returning from a game must wait for neutral controls, including the stick.
            _suppressNextInput = state.Buttons != ControllerButtons.None ||
                Math.Max(Math.Abs((int)state.LeftThumbX), Math.Abs((int)state.LeftThumbY)) >= 9000;
            _previousButtons[ActiveSlot] = state.Buttons;
            _directions[ActiveSlot].Reset();
            return new ControllerInputFrame(true, connectedSlots.Count, ActiveSlot, state,
                ControllerButtons.None, null, changed, disconnectedOwner);
        }

        var newButtons = state.Buttons & ~_previousButtons[ActiveSlot];
        _previousButtons[ActiveSlot] = state.Buttons;
        var direction = _directions[ActiveSlot].Read((ushort)state.Buttons, state.LeftThumbX, state.LeftThumbY, milliseconds);
        return new ControllerInputFrame(true, connectedSlots.Count, ActiveSlot, state, newButtons,
            direction, changed, disconnectedOwner);
    }
}

public readonly record struct ControllerInputFrame(
    bool IsAnyConnected,
    int ConnectedCount,
    int ActiveSlot,
    ControllerRawState State,
    ControllerButtons NewButtons,
    FocusNavigationDirection? Direction,
    bool ActiveSlotChanged,
    bool ActiveSlotDisconnected);
