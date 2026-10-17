namespace CloudBerry.Core.Models;

public sealed class DeviceProfileSettings
{
    public string ActiveDeviceId { get; set; } = string.Empty;

    public List<CirraDeviceProfile> Items { get; set; } = [];
}

public sealed class CirraDeviceProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string DisplayName { get; set; } = "This device";

    /// <summary>Desktop, Handheld, or LivingRoom.</summary>
    public string DeviceKind { get; set; } = "Desktop";

    public string ThemePreset { get; set; } = "Cirra Core";

    public string Density { get; set; } = "Comfortable";

    public string EffectsQuality { get; set; } = "Full";

    public bool VisualEffectsEnabled { get; set; } = true;

    public double VisualEffectIntensity { get; set; } = 1.0;

    public bool AudioEnabled { get; set; } = true;

    public string AudioProfile { get; set; } = "Subtle";

    public string StartupView { get; set; } = "Cirra";

    /// <summary>Reserved for device-specific emulator selections without changing shared defaults.</summary>
    public Dictionary<string, string> EmulatorOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
