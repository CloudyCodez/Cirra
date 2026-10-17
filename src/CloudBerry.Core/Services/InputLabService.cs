using System.Runtime.InteropServices;
using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services;

public interface IControllerInputSource
{
    ControllerRawState Read(int slot);
}

public sealed class InputLabService
{
    private readonly IControllerInputSource _source;

    public InputLabService(IControllerInputSource? source = null)
    {
        _source = source ?? new XInputControllerSource();
    }

    public IReadOnlyList<ControllerSnapshot> SampleAll(double driftThreshold = 0.12)
    {
        driftThreshold = Math.Clamp(driftThreshold, 0.02, 0.5);
        var snapshots = new List<ControllerSnapshot>(4);
        for (var slot = 0; slot < 4; slot++)
        {
            snapshots.Add(Analyze(slot, _source.Read(slot), driftThreshold));
        }
        return snapshots;
    }

    public static ControllerSnapshot Analyze(int slot, ControllerRawState state, double driftThreshold = 0.12)
    {
        if (!state.IsConnected)
        {
            return new ControllerSnapshot(slot, false, 0, ControllerButtons.None, 0, 0, 0, 0, 0, 0, false,
                "Connect a controller to test inputs and disconnect recovery.");
        }

        var lx = NormalizeAxis(state.LeftThumbX);
        var ly = NormalizeAxis(state.LeftThumbY);
        var rx = NormalizeAxis(state.RightThumbX);
        var ry = NormalizeAxis(state.RightThumbY);
        var hasDrift = Math.Sqrt(lx * lx + ly * ly) > driftThreshold ||
                       Math.Sqrt(rx * rx + ry * ry) > driftThreshold;
        var message = hasDrift && state.Buttons == ControllerButtons.None && state.LeftTrigger == 0 && state.RightTrigger == 0
            ? $"Stick movement exceeds the {driftThreshold:P0} quiet-zone threshold."
            : "Input stream is healthy.";

        return new ControllerSnapshot(
            slot,
            true,
            state.PacketNumber,
            state.Buttons,
            state.LeftTrigger / 255d,
            state.RightTrigger / 255d,
            lx,
            ly,
            rx,
            ry,
            hasDrift,
            message);
    }

    private static double NormalizeAxis(short value) => value < 0 ? value / 32768d : value / 32767d;
}

public sealed class XInputControllerSource : IControllerInputSource
{
    private const uint Success = 0;

    public ControllerRawState Read(int slot)
    {
        if (slot is < 0 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        try
        {
            if (XInputGetState((uint)slot, out var state) != Success)
            {
                return new ControllerRawState(false);
            }

            return new ControllerRawState(
                true,
                state.PacketNumber,
                (ControllerButtons)state.Gamepad.Buttons,
                state.Gamepad.LeftTrigger,
                state.Gamepad.RightTrigger,
                state.Gamepad.LeftThumbX,
                state.Gamepad.LeftThumbY,
                state.Gamepad.RightThumbX,
                state.Gamepad.RightThumbY);
        }
        catch (DllNotFoundException)
        {
            return new ControllerRawState(false);
        }
        catch (EntryPointNotFoundException)
        {
            return new ControllerRawState(false);
        }
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState(uint userIndex, out NativeState state);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeState
    {
        public uint PacketNumber;
        public NativeGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short LeftThumbX;
        public short LeftThumbY;
        public short RightThumbX;
        public short RightThumbY;
    }
}
