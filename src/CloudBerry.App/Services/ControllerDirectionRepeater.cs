using System.Windows.Input;

namespace CloudBerry.App.Services;

public sealed class ControllerDirectionRepeater
{
    private FocusNavigationDirection? _held;
    private long _nextRepeat;

    public void Reset() { _held = null; _nextRepeat = 0; }

    public FocusNavigationDirection? Read(ushort buttons, short x, short y, long milliseconds)
    {
        FocusNavigationDirection? direction = (buttons & 1) != 0 ? FocusNavigationDirection.Up
            : (buttons & 2) != 0 ? FocusNavigationDirection.Down
            : (buttons & 4) != 0 ? FocusNavigationDirection.Left
            : (buttons & 8) != 0 ? FocusNavigationDirection.Right : null;
        if (direction is null && Math.Max(Math.Abs((int)x), Math.Abs((int)y)) >= 9000)
            direction = Math.Abs((int)x) > Math.Abs((int)y)
                ? x > 0 ? FocusNavigationDirection.Right : FocusNavigationDirection.Left
                : y > 0 ? FocusNavigationDirection.Up : FocusNavigationDirection.Down;
        if (direction is null) { Reset(); return null; }
        if (direction != _held)
        {
            _held = direction;
            _nextRepeat = milliseconds + 320;
            return direction;
        }
        if (milliseconds < _nextRepeat) return null;
        _nextRepeat = milliseconds + 110;
        return direction;
    }
}
