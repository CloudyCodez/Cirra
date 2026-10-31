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
        int horizontal = Math.Abs((int)x);
        int vertical = Math.Abs((int)y);
        int releaseThreshold = _held.HasValue ? 7000 : 9000;
        if (direction is null && Math.Max(horizontal, vertical) >= releaseThreshold)
        {
            // Retain the axis near a diagonal instead of rapidly alternating rows and columns.
            bool keepHorizontal = _held is FocusNavigationDirection.Left or FocusNavigationDirection.Right &&
                horizontal >= releaseThreshold && vertical < horizontal * 1.2;
            bool keepVertical = _held is FocusNavigationDirection.Up or FocusNavigationDirection.Down &&
                vertical >= releaseThreshold && horizontal < vertical * 1.2;
            direction = keepHorizontal || (!keepVertical && horizontal > vertical)
                ? x > 0 ? FocusNavigationDirection.Right : FocusNavigationDirection.Left
                : y > 0 ? FocusNavigationDirection.Up : FocusNavigationDirection.Down;
        }
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
