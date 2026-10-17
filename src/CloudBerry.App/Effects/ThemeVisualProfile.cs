using System.Windows;
using System.Windows.Media;

namespace CloudBerry.App.Effects;

internal sealed record ThemeVisualProfile(
    string Key, string Background, string Surface, string Raised, string Accent,
    string SecondaryAccent, string Text, string Muted, double Tempo, double Parallax)
{
    public bool IsLight => Key == "Archive Ivory";
    public bool IsHighContrast => Key == "High Contrast";

    // Silhouette remains recognizable without colour. Keep reading text familiar;
    // only display and instrument labels adopt the world's typographic voice.
    public CornerRadius PanelCorners => Key switch
    {
        "Cirra Core" => new(18, 18, 6, 6),
        "Midnight Observatory" => new(3),
        "Verdant Glass" => new(24, 6, 24, 6),
        "Signal Rain" => new(0, 12, 0, 0),
        "Arcade Static" => new(12, 12, 3, 3),
        "Archive Ivory" => new(0),
        "Tidal Glass" => new(26),
        "Impact Noir" => new(2, 20, 2, 2),
        _ => new(0)
    };

    public string DisplayTypeface => Key switch
    {
        "Archive Ivory" => "Constantia, Georgia",
        "Signal Rain" => "Cascadia Mono, Consolas",
        "Arcade Static" or "Impact Noir" => "Bahnschrift, Segoe UI",
        "Midnight Observatory" => "Segoe UI Light, Segoe UI",
        _ => "Segoe UI Variable Display, Segoe UI"
    };

    private LinearGradientBrush Material(Color top, Color middle, Color bottom)
    {
        var brush = Gradient(top, middle, bottom).Clone();
        brush.EndPoint = Key switch
        {
            "Signal Rain" or "Archive Ivory" => new(1, 0),
            "Impact Noir" => new(1, 1),
            "Verdant Glass" => new(0, 1),
            "Tidal Glass" => new(0.8, 1),
            "Arcade Static" => new(0, 0.55),
            _ => new(0.25, 1)
        };
        brush.Freeze();
        return brush;
    }

    public static IReadOnlyList<ThemeVisualProfile> All { get; } = Array.AsReadOnly(new[]
    {
        new ThemeVisualProfile("Cirra Core", "#101215", "#191D22", "#262C33", "#99DEF0", "#D8C7A5", "#F3F6F8", "#ADB8C2", 0.65, 3),
        new ThemeVisualProfile("Midnight Observatory", "#0E111C", "#191E2C", "#282F42", "#A4C3FF", "#E5BD80", "#F3F5FE", "#ACB7CF", 0.35, 2),
        new ThemeVisualProfile("Verdant Glass", "#101713", "#1B2721", "#2A3B31", "#B2E8C7", "#DFC890", "#F1F8F3", "#B0C4B8", 0.45, 3),
        new ThemeVisualProfile("Signal Rain", "#101510", "#1A251D", "#2A382B", "#BCF783", "#88D4DC", "#F3FAEF", "#B5C9AD", 0.60, 1),
        new ThemeVisualProfile("Arcade Static", "#171219", "#27202A", "#3B3041", "#F2A1CA", "#86E0E4", "#FCF1F8", "#CCB6C8", 0.85, 2),
        new ThemeVisualProfile("Archive Ivory", "#E9EBE6", "#F5F6F1", "#DDDFD7", "#374E48", "#6E5940", "#202824", "#48554D", 0.30, 1),
        new ThemeVisualProfile("Tidal Glass", "#101B20", "#1A2B32", "#2B4048", "#9BE5E1", "#EDB1A0", "#F0FAFA", "#AEC6CD", 0.55, 4),
        new ThemeVisualProfile("Impact Noir", "#151113", "#241B1F", "#37282E", "#FFACB5", "#D5D9E2", "#FFF3F5", "#CDB8BE", 0.70, 2),
        new ThemeVisualProfile("High Contrast", "#000000", "#000000", "#171717", "#FFFFFF", "#FFFF00", "#FFFFFF", "#FFFFFF", 0, 0)
    });

    public static ThemeVisualProfile Resolve(string? key) => All.FirstOrDefault(profile => profile.Key == key) ?? All[0];
    public static Color ColorOf(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    public static Color Mix(Color from, Color to, double amount) => Color.FromRgb(
        (byte)Math.Round(from.R + (to.R - from.R) * amount),
        (byte)Math.Round(from.G + (to.G - from.G) * amount),
        (byte)Math.Round(from.B + (to.B - from.B) * amount));

    public static SolidColorBrush BrushOf(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public static LinearGradientBrush Gradient(Color top, Color middle, Color bottom)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(0.25, 1),
            GradientStops = new GradientStopCollection { new(top, 0), new(middle, 0.32), new(bottom, 1) }
        };
        brush.Freeze();
        return brush;
    }

    // Reset shared materials on every switch so text and controls never inherit another palette.
    public void ApplyMaterials(Action<string, object> set, string? profileThemeKey = null)
    {
        set("ThemeCornerRadiusPanel", PanelCorners);
        set("ThemeCornerRadiusLarge", PanelCorners);
        var controlCorners = Key switch
        {
            "Tidal Glass" => new CornerRadius(16),
            "Verdant Glass" => new CornerRadius(12, 3, 12, 3),
            "Signal Rain" => new CornerRadius(0, 8, 0, 0),
            "Impact Noir" => new CornerRadius(1, 10, 1, 1),
            "Archive Ivory" or "High Contrast" => new CornerRadius(0),
            _ => new CornerRadius(6)
        };
        foreach (var key in new[] { "ThemeCornerRadiusControl", "GlassButtonCornerRadius", "GlassChipCornerRadius", "ThemeCornerRadiusPill" }) set(key, controlCorners);
        var displayFont = new FontFamily(DisplayTypeface);
        var bodyFont = new FontFamily("Segoe UI Variable Text, Segoe UI");
        set("ThemeDisplayFontFamily", displayFont);
        set("ThemeBodyFontFamily", bodyFont);
        set("ThemeInputFontFamily", bodyFont);
        set("ThemeBorderThickness", Key == "Archive Ivory" ? new Thickness(0, 0, 0, 1) : new Thickness(IsHighContrast ? 2 : 1));
        set("GlassButtonBorderThickness", new Thickness(IsHighContrast ? 2 : 1));
        set("ThemeHoverLiftDistance", IsHighContrast || Key == "Signal Rain" ? 0d : Key == "Tidal Glass" ? -3d : -1.5d);
        set("ThemeHoverScale", IsHighContrast ? 1d : 1.006d);
        set("ThemeHoverAnimationFastMs", IsHighContrast ? 0d : Key == "Impact Noir" ? 90d : 140d);
        set("ThemeHoverAnimationSlowMs", IsHighContrast ? 0d : Key == "Tidal Glass" ? 260d : 200d);
        var surface = ColorOf(Surface);
        var raised = ColorOf(Raised);
        var accent = ColorOf(Accent);
        var secondary = ColorOf(SecondaryAccent);
        var text = ColorOf(Text);
        var edge = IsHighContrast ? Colors.White : Mix(raised, text, IsLight ? 0.30 : 0.24);
        var hover = Mix(raised, accent, IsHighContrast ? 0 : 0.10);
        var selected = IsHighContrast ? raised : Mix(surface, accent, 0.16);
        void Solid(string key, Color color) => set(key, BrushOf(color));
        void Many(Color color, params string[] keys) { foreach (var key in keys) Solid(key, color); }

        Solid("AppBackgroundBrush", ColorOf(Background));
        Solid("ArtworkHoverBrush", Color.FromArgb(20, text.R, text.G, text.B));
        Solid("SidebarBrush", ColorOf(Background));
        Many(surface, "SurfaceBrush", "CardBrush", "ThemePillFillBrush", "ThemeNavItemBackgroundBrush");
        Many(raised, "SurfaceElevatedBrush", "SurfaceHighBrush", "ThemeDeckFrameBrush", "BerryDimBrush", "BerryMutedBrush");
        Many(hover, "CardHoverBrush", "ThemePillHoverFillBrush", "ThemeNavItemHoverBackgroundBrush", "ThemeGhostButtonHoverFillBrush");
        Many(selected, "CardSelectedBrush", "ThemePillSelectedFillBrush", "ThemeNavItemSelectedBackgroundBrush", "ThemeGhostButtonPressedFillBrush");
        Many(text, "TextPrimaryBrush", "ThemePrimaryButtonTextBrush", "ThemeSecondaryButtonTextBrush", "ThemePillLabelActiveBrush");
        Many(ColorOf(Muted), "TextSecondaryBrush", "TextMutedBrush", "ThemePillLabelBrush");
        Solid("TextOnDarkBrush", Colors.White);
        Many(accent, "BerryBrush", "BerryHoverBrush", "BerryPressedBrush", "NeoCyanBrush", "ThemePillSelectedBorderBrush", "ThemeNavItemSelectedBorderBrush", "GlassPanelHoverBorderBrush");
        Many(secondary, "CloudBrush", "GoldBrush", "NeoBerryBrush");
        Many(edge, "BorderBrush", "BorderSubtleBrush", "GlassPanelBorderBrush", "ThemePanelEdgeBrush", "ThemeDeckFrameBorderBrush", "ThemeSecondaryButtonBorderBrush", "ThemePillBorderBrush", "ThemeNavItemBorderBrush", "ThemeGhostButtonBorderBrush");
        Many(accent, "ThemePillHoverBorderBrush", "ThemeNavItemHoverBorderBrush", "ThemeGhostButtonHoverBorderBrush");
        Solid("ThemeNavItemTopEdgeBrush", edge);
        Solid("MintBrush", ColorOf(IsLight ? "#27633F" : "#A2DFC0"));
        Many(raised, "MintDimBrush", "CloudDimBrush", "GoldDimBrush", "ErrorDimBrush");
        Solid("WarningBrush", ColorOf(IsLight ? "#74500C" : "#F0CC87"));
        Solid("ErrorBrush", ColorOf(IsLight ? "#A3263E" : "#FFB0BB"));
        Solid("ErrorHoverBrush", ColorOf(IsLight ? "#81142B" : "#FFC5CD"));

        var glass = IsHighContrast ? Gradient(surface, surface, surface)
            : Material(Mix(surface, text, IsLight ? 0.02 : Key == "Midnight Observatory" ? 0.015 : 0.07), surface, Mix(surface, raised, 0.25));
        var rim = IsHighContrast ? Gradient(edge, edge, edge)
            : Material(Mix(edge, text, 0.22), edge, Mix(edge, surface, 0.35));
        foreach (var key in new[] { "ThemePanelFillBrush", "ThemePanelFillSoftBrush", "GlassButtonFillBrush", "GlassChipFillBrush", "ThemeSecondaryButtonFillBrush", "ThemePrimaryButtonFillBrush" }) set(key, glass);
        foreach (var key in new[] { "GlassButtonBorderBrush", "GlassChipBorderBrush", "ThemePanelBorderGradientBrush" }) set(key, rim);
        foreach (var key in new[] { "GlassButtonHoverFillBrush", "ThemeSecondaryButtonHoverFillBrush", "ThemePrimaryButtonHoverFillBrush", "GlassChipActiveFillBrush" }) set(key, Gradient(hover, raised, surface));
        foreach (var key in new[] { "GlassButtonPressedFillBrush", "ThemeSecondaryButtonPressedFillBrush", "ThemePrimaryButtonPressedFillBrush" }) Solid(key, selected);
        // Primary actions occupy the highest visual tier. Pair their ink with
        // their own fill, independently of the reading text on dark surfaces.
        Solid("ThemePrimaryButtonFillBrush", accent);
        Solid("ThemePrimaryButtonHoverFillBrush", Mix(accent, IsLight ? Colors.Black : Colors.White, 0.10));
        Solid("ThemePrimaryButtonPressedFillBrush", Mix(accent, IsLight ? Colors.Black : Colors.White, 0.18));
        Solid("ThemePrimaryButtonTextBrush", IsLight ? Colors.White : ColorOf(Background));
        Solid("ThemeGhostButtonBorderBrush", IsHighContrast ? Colors.White : Colors.Transparent);
        // Inset surfaces sit behind glass panels rather than repeating the same finish.
        Solid("ThemePanelFillSoftBrush", Mix(ColorOf(Background), surface, 0.60));
        set("GlassSheenBrush", Gradient(Color.FromArgb(IsHighContrast ? (byte)0 : (byte)16, 255, 255, 255), Colors.Transparent, Colors.Transparent));
        // Pressed fills already express state; a second legacy tint changes their
        // contrast and can introduce a colour left over from a previous theme.
        set("GlassPressedOverlayBrush", Brushes.Transparent);
        set("ThemePanelShadowColor", Colors.Black);
        set("ThemePrimaryButtonGlowColor", accent);
        set("MintGlowColor", accent);
        set("ThemePointerGlowCenterColor", Colors.Transparent);
        set("ThemePointerGlowMidColor", Colors.Transparent);
        set("ThemePointerClickColor", Color.FromArgb(72, accent.R, accent.G, accent.B));
        set("ThemeGrainOpacity", IsHighContrast ? 0d : IsLight ? 0.035d : 0.018d);
        set("ThemeShellOverlayOpacity", IsLight ? 0.12d : 0.22d);
        set("HeroOverlayBrush", Gradient(
            Color.FromArgb(IsLight ? (byte)228 : (byte)150, surface.R, surface.G, surface.B),
            Color.FromArgb(IsLight ? (byte)240 : (byte)180, surface.R, surface.G, surface.B), surface));

        // Saved profile identity may differ from the shell, but shares its readable luminance.
        var profileAccent = IsHighContrast ? accent : ColorOf(Resolve(profileThemeKey ?? Key).Accent);
        set("ProfileCardBaseBrush", glass);
        Solid("ProfileScoreBandBrush", surface);
        Solid("ProfileAvatarTileBrush", Mix(surface, profileAccent, IsHighContrast ? 0 : 0.10));
        Solid("ProfileBadgeFillBrush", surface);
        Solid("ProfileBadgeBorderBrush", edge);
        Solid("ProfileBadgeGlowBrush", Colors.Transparent);
        foreach (var key in new[] { "ProfileSurfaceAtmosphereBrush", "ProfileSurfacePatternBrush", "ProfileSurfaceSweepBrush" }) set(key, Brushes.Transparent);
        foreach (var key in new[] { "ProfileSurfaceAtmosphereOpacity", "ProfileSurfacePatternOpacity", "ProfileSurfaceSweepOpacity", "ProfileBadgePulseOpacity" }) set(key, 0d);

        // ThemeEffectLayer owns atmosphere; repeated legacy overlays no longer compete with it.
        foreach (var key in new[] { "ThemeShellPatternBrush", "ThemeSidebarPatternBrush", "MatrixRainBrush", "NierDeletionBrush", "NostalgiaScanlineBrush", "ThemeAtmosphereBlobBrush", "ThemeAtmosphereBlobSecondaryBrush" }) set(key, Brushes.Transparent);
        foreach (var key in new[] { "ThemeShellPatternOpacity", "ThemeSidebarPatternOpacity", "ThemeRainOverlayOpacity", "ThemeSweepOverlayOpacity", "NierDeletionEffectOpacity", "NierArtworkOpacity", "WutheringArtworkOpacity", "BlackFlashArtworkOpacity" }) set(key, 0d);
        set("ThemeShellAtmosphereBrush", Brushes.Transparent);
        set("ThemeSidebarOverlayBrush", Brushes.Transparent);
        set("ThemeSweepOpacity", 0d);
    }
}
