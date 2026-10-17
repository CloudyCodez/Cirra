using CloudBerry.Core.Models;
using CloudBerry.Core.Services;

namespace CloudBerry.Core.Tests;

public sealed class DeviceProfileServiceTests
{
    private readonly DeviceProfileService _service = new();

    [Fact]
    public void EnsureCurrent_CapturesExistingExperienceAndReusesIdentity()
    {
        var settings = new CloudBerrySettings
        {
            Appearance = new AppearanceSettings
            {
                ThemePreset = "Verdant Glass",
                Density = "Compact",
                EffectsQuality = "Efficient",
                VisualEffectIntensity = 0.8
            },
            Audio = new AudioSettings { Enabled = false, Profile = "Cinematic" }
        };

        var created = _service.EnsureCurrent(settings, "Deck", "Handheld");
        var reused = _service.EnsureCurrent(settings, "Ignored", "Desktop");

        Assert.Same(created, reused);
        Assert.Equal("Deck", created.DisplayName);
        Assert.Equal("Handheld", created.DeviceKind);
        Assert.Equal("Verdant Glass", created.ThemePreset);
        Assert.Equal("Compact", created.Density);
        Assert.Equal("Efficient", created.EffectsQuality);
        Assert.False(created.AudioEnabled);
        Assert.Single(settings.Devices.Items);
    }

    [Fact]
    public void Activate_AppliesDeviceScopedExperience()
    {
        var settings = new CloudBerrySettings();
        var profile = new CirraDeviceProfile
        {
            DisplayName = "TV",
            DeviceKind = "LivingRoom",
            ThemePreset = "Midnight Arcade",
            Density = "Couch",
            EffectsQuality = "Off",
            VisualEffectsEnabled = true,
            VisualEffectIntensity = 1.4,
            AudioEnabled = false,
            AudioProfile = "Cinematic",
            StartupView = "Cloudscape"
        };
        settings.Devices.Items.Add(profile);

        _service.Activate(settings, profile.Id);

        Assert.Equal(profile.Id, settings.Devices.ActiveDeviceId);
        Assert.Equal("Midnight Arcade", settings.Appearance.ThemePreset);
        Assert.Equal("Couch", settings.Appearance.Density);
        Assert.Equal("Off", settings.Appearance.EffectsQuality);
        Assert.False(settings.Appearance.VisualEffectsEnabled);
        Assert.False(settings.Audio.Enabled);
        Assert.Equal("Cloudscape", settings.Appearance.StartupView);
    }

    [Fact]
    public void EnsureCurrent_NormalizesUnsupportedValues()
    {
        var settings = new CloudBerrySettings
        {
            Appearance = new AppearanceSettings { EffectsQuality = "Impossible", VisualEffectIntensity = 99 }
        };

        var profile = _service.EnsureCurrent(settings, null, "Console");

        Assert.Equal("Desktop", profile.DeviceKind);
        Assert.Equal("Full", profile.EffectsQuality);
        Assert.Equal(1.6, profile.VisualEffectIntensity);
    }
}
