namespace CloudBerry.App.Services;

public sealed class SoundDesignService
{
    private static readonly SoundDesignService StartupInstance = new(() => true, () => "Cinematic", () => "CloudBerry Dark");
    private readonly Func<bool> _isEnabled;
    private readonly Func<string> _profile;
    private readonly Func<string>? _theme;
    private readonly Dictionary<string, DateTime> _lastPlayedUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _random = new();

    public SoundDesignService(Func<bool> isEnabled, Func<string> profile, Func<string>? theme = null)
    {
        _isEnabled = isEnabled;
        _profile = profile;
        _theme = theme;
    }

    public void PlayAppReady() => Play("app.ready", 280);

    public void PlayAppBoot() => Play("app.boot", 900);

    public void PlayMenuOpen() => Play("menu.open", 160);

    public void PlayTabSwitch() => Play("tab.switch", 120);

    public void PlaySoftClick() => Play("ui.click", 90);

    public void PlayConfirm() => Play("ui.confirm", 140);

    public void PlaySuccess() => Play("ui.success", 220);

    public void PlayLaunch() => Play("game.launch", 260);

    public void PlayLaunchHandoff() => Play("game.launch.handoff", 300);

    public void PlayWarning() => Play("ui.warning", 260);

    public void PlayError() => Play("ui.error", 180);

    public void PlayMetadataApply() => Play("metadata.apply", 220);

    public void PlayAchievementUnlock() => Play("achievement.unlock", 420);

    public void PlayFocusMove() => Play("ui.focus", 85);

    public static void PlayStartupBoot() => StartupInstance.PlayAppBoot();

    private void Play(string key, int cooldownMilliseconds)
    {
        if (!_isEnabled())
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (_lastPlayedUtc.TryGetValue(key, out var previousUtc) &&
            (now - previousUtc).TotalMilliseconds < ResolveCooldown(cooldownMilliseconds))
        {
            return;
        }

        _lastPlayedUtc[key] = now;
        var profile = _profile();
        var theme = _theme?.Invoke() ?? string.Empty;
        var variant = _random.Next(0, 3);
        Task.Run(() => ProceduralSoundPack.PlayCue(key, profile, theme, variant));
    }

    private int ResolveCooldown(int baseCooldown)
    {
        // Cache the profile string — _profile() is a Func<string> delegate call,
        // and was previously invoked twice per Play() (here + in Play() body).
        return (_profile() ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "cinematic"  => Math.Max(60,  (int)(baseCooldown * 0.75)),
            "digital"    => Math.Max(45,  (int)(baseCooldown * 0.70)),
            "theme sync" => Math.Max(55,  (int)(baseCooldown * 0.82)),
            "balanced"   => baseCooldown,
            _            => (int)(baseCooldown * 1.25)
        };
    }
}
