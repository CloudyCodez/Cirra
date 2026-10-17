namespace CloudBerry.App.Services;

internal sealed class HeroRotationPolicy
{
    private long _lastInteraction;

    public void Interact(long milliseconds) => _lastInteraction = milliseconds;

    public bool CanAdvance(long milliseconds, bool activeHome, bool blocked, bool paused = false) =>
        activeHome && !blocked && !paused && milliseconds - _lastInteraction >= 12000;
}
