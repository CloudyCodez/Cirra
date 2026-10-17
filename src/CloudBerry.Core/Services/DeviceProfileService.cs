using CloudBerry.Core.Models;

namespace CloudBerry.Core.Services;

public sealed class DeviceProfileService
{
    private static readonly HashSet<string> DeviceKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Desktop", "Handheld", "LivingRoom"
    };

    private static readonly HashSet<string> EffectsQualities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Off", "Efficient", "Full"
    };

    public CirraDeviceProfile EnsureCurrent(
        CloudBerrySettings settings,
        string? deviceName = null,
        string deviceKind = "Desktop")
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Devices ??= new DeviceProfileSettings();
        settings.Devices.Items ??= [];

        var current = settings.Devices.Items.FirstOrDefault(profile =>
            string.Equals(profile.Id, settings.Devices.ActiveDeviceId, StringComparison.Ordinal));
        if (current is not null)
        {
            Normalize(current);
            return current;
        }

        current = new CirraDeviceProfile
        {
            DisplayName = string.IsNullOrWhiteSpace(deviceName) ? "This device" : deviceName.Trim(),
            DeviceKind = NormalizeDeviceKind(deviceKind)
        };
        Capture(settings, current);
        settings.Devices.Items.Add(current);
        settings.Devices.ActiveDeviceId = current.Id;
        return current;
    }

    public CirraDeviceProfile Create(
        CloudBerrySettings settings,
        string displayName,
        string deviceKind)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Devices ??= new DeviceProfileSettings();
        settings.Devices.Items ??= [];

        var profile = new CirraDeviceProfile
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "New device" : displayName.Trim(),
            DeviceKind = NormalizeDeviceKind(deviceKind)
        };
        Capture(settings, profile);
        settings.Devices.Items.Add(profile);
        return profile;
    }

    public void Capture(CloudBerrySettings settings, CirraDeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(profile);
        settings.Appearance ??= new AppearanceSettings();
        settings.Audio ??= new AudioSettings();

        profile.ThemePreset = Default(settings.Appearance.ThemePreset, "Cirra Core");
        profile.Density = Default(settings.Appearance.Density, "Comfortable");
        profile.EffectsQuality = NormalizeEffectsQuality(settings.Appearance.EffectsQuality);
        profile.VisualEffectsEnabled = settings.Appearance.VisualEffectsEnabled;
        profile.VisualEffectIntensity = Math.Clamp(settings.Appearance.VisualEffectIntensity, 0.35, 1.6);
        profile.AudioEnabled = settings.Audio.Enabled;
        profile.AudioProfile = Default(settings.Audio.Profile, "Subtle");
        profile.StartupView = Default(settings.Appearance.StartupView, "Cirra");
        profile.UpdatedUtc = DateTime.UtcNow;
        Normalize(profile);
    }

    public CirraDeviceProfile Activate(CloudBerrySettings settings, string profileId)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Devices ??= new DeviceProfileSettings();
        var profile = settings.Devices.Items?.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, profileId, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException("The selected device profile no longer exists.");

        Apply(settings, profile);
        settings.Devices.ActiveDeviceId = profile.Id;
        return profile;
    }

    public void Apply(CloudBerrySettings settings, CirraDeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(profile);
        settings.Appearance ??= new AppearanceSettings();
        settings.Audio ??= new AudioSettings();
        Normalize(profile);

        settings.Appearance.ThemePreset = profile.ThemePreset;
        settings.Appearance.Density = profile.Density;
        settings.Appearance.EffectsQuality = profile.EffectsQuality;
        settings.Appearance.VisualEffectsEnabled = profile.VisualEffectsEnabled &&
                                                  !string.Equals(profile.EffectsQuality, "Off", StringComparison.OrdinalIgnoreCase);
        settings.Appearance.VisualEffectIntensity = profile.VisualEffectIntensity;
        settings.Appearance.StartupView = profile.StartupView;
        settings.Audio.Enabled = profile.AudioEnabled;
        settings.Audio.Profile = profile.AudioProfile;
    }

    private static void Normalize(CirraDeviceProfile profile)
    {
        profile.Id = Default(profile.Id, Guid.NewGuid().ToString("N"));
        profile.DisplayName = Default(profile.DisplayName, "This device");
        profile.DeviceKind = NormalizeDeviceKind(profile.DeviceKind);
        profile.ThemePreset = Default(profile.ThemePreset, "Cirra Core");
        profile.Density = Default(profile.Density, "Comfortable");
        profile.EffectsQuality = NormalizeEffectsQuality(profile.EffectsQuality);
        profile.VisualEffectIntensity = Math.Clamp(profile.VisualEffectIntensity <= 0 ? 1.0 : profile.VisualEffectIntensity, 0.35, 1.6);
        profile.AudioProfile = Default(profile.AudioProfile, "Subtle");
        profile.StartupView = Default(profile.StartupView, "Cirra");
        profile.EmulatorOverrides ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeDeviceKind(string? value) =>
        DeviceKinds.Contains(value ?? string.Empty) ? value! : "Desktop";

    private static string NormalizeEffectsQuality(string? value) =>
        EffectsQualities.Contains(value ?? string.Empty) ? value! : "Full";

    private static string Default(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
