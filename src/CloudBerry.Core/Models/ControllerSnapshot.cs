namespace CloudBerry.Core.Models;

[Flags]
public enum ControllerButtons : ushort
{
    None = 0,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftStick = 0x0040,
    RightStick = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000
}

public sealed record ControllerRawState(
    bool IsConnected,
    uint PacketNumber = 0,
    ControllerButtons Buttons = ControllerButtons.None,
    byte LeftTrigger = 0,
    byte RightTrigger = 0,
    short LeftThumbX = 0,
    short LeftThumbY = 0,
    short RightThumbX = 0,
    short RightThumbY = 0);

public sealed record ControllerSnapshot(
    int Slot,
    bool IsConnected,
    uint PacketNumber,
    ControllerButtons Buttons,
    double LeftTrigger,
    double RightTrigger,
    double LeftStickX,
    double LeftStickY,
    double RightStickX,
    double RightStickY,
    bool HasStickDrift,
    string HealthMessage)
{
    public string SlotLabel => $"Controller {Slot + 1}";

    public string ConnectionLabel => IsConnected ? "Connected" : "Not connected";

    public string ButtonLabel => Buttons == ControllerButtons.None ? "No buttons pressed" : Buttons.ToString();

    public string TriggerLabel => $"LT {LeftTrigger:P0}  ·  RT {RightTrigger:P0}";

    public string StickLabel => $"L {LeftStickX:+0.00;-0.00;0.00}, {LeftStickY:+0.00;-0.00;0.00}  ·  R {RightStickX:+0.00;-0.00;0.00}, {RightStickY:+0.00;-0.00;0.00}";
}
