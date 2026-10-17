using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace CloudBerry.App.Motion;

/// <summary>
/// Shared Cirra motion foundation. Themes can layer a distinct cadence on top, but
/// navigation remains fast, predictable, and respectful of reduced-motion settings.
/// </summary>
internal static class MotionTokens
{
    // Standard interaction durations.
    public static readonly Duration Micro        = TimeSpan.FromMilliseconds(35);
    public static readonly Duration Tap          = TimeSpan.FromMilliseconds(55);
    public static readonly Duration Hover        = TimeSpan.FromMilliseconds(95);
    public static readonly Duration Standard     = TimeSpan.FromMilliseconds(160);
    public static readonly Duration Crossfade    = TimeSpan.FromMilliseconds(220);
    public static readonly Duration Breathe      = TimeSpan.FromMilliseconds(1600);
    public static readonly Duration TapReturn    = TimeSpan.FromMilliseconds(75);
    public static readonly Duration HoverReturn  = TimeSpan.FromMilliseconds(130);

    // ── Impact Noir Impact Durations ─────────────────────────────────────────
    /// <summary>Impact flash: instantaneous energy burst on interaction.</summary>
    public static readonly Duration FlashMicro   = TimeSpan.FromMilliseconds(35);
    
    /// <summary>Button press with cursed energy feedback.</summary>
    public static readonly Duration FlashTap     = TimeSpan.FromMilliseconds(55);
    
    /// <summary>Energy burst animation for high-impact moments.</summary>
    public static readonly Duration FlashBurst   = TimeSpan.FromMilliseconds(85);
    
    /// <summary>Return/decay after impact — natural cursed energy dissipation.</summary>
    public static readonly Duration FlashSettle  = TimeSpan.FromMilliseconds(140);

    // ── Easing functions — Impact Noir kinetic language ──────────────────────
    /// <summary>Impact snap: aggressive deceleration for punchy feedback.</summary>
    public static IEasingFunction FlashSnap    { get; } = new QuarticEase { EasingMode = EasingMode.EaseOut };
    
    /// <summary>Energy bloom: smooth in-out for glow/pulse effects.</summary>
    public static IEasingFunction FlashBloom   { get; } = new SineEase    { EasingMode = EasingMode.EaseInOut };
    
    /// <summary>Recoil bounce: slight overshoot for tactile impact feel.</summary>
    public static IEasingFunction FlashRecoil  { get; } = new CubicEase   { EasingMode = EasingMode.EaseOut };
    
    /// <summary>Standard premium deceleration for UI elements.</summary>
    public static IEasingFunction PremiumOut   { get; } = new QuarticEase { EasingMode = EasingMode.EaseOut };
    
    /// <summary>Soft ease-out for subtle exit animations and crossfades.</summary>
    public static IEasingFunction SoftOut      { get; } = new SineEase    { EasingMode = EasingMode.EaseOut };
    
    /// <summary>Smooth ambient motion for background effects.</summary>
    public static IEasingFunction SoftInOut    { get; } = new SineEase    { EasingMode = EasingMode.EaseInOut };
    
    /// <summary>Quick micro-feedback for instant responsiveness.</summary>
    public static IEasingFunction QuickIn      { get; } = new QuadraticEase { EasingMode = EasingMode.EaseIn };

    // ── Scale tokens — Impact Noir impact dynamics ───────────────────────────
    /// <summary>Hover lift scale — subtle depth without distraction.</summary>
    public const double HoverScale    = 1.012;
    
    /// <summary>Impact scale — punchy expansion on high-energy interactions.</summary>
    public const double FlashImpactScale = 1.025;
    
    /// <summary>Recoil scale — slight compression after impact for tactile feedback.</summary>
    public const double FlashRecoilScale = 0.98;
    
    /// <summary>Press-down scale for standard tactile feedback.</summary>
    public const double PressScale    = 0.97;

    // ── Translation tokens — precise pixel movements ─────────────────────────
    /// <summary>Card hover Y-lift in pixels — subtle elevation.</summary>
    public const double HoverLiftY    = -3.0;
    
    /// <summary>Nav-item hover X-nudge in pixels.</summary>
    public const double NavNudgeX     = 2.5;
    
    /// <summary>Impact nudge — slight punch-forward on high-energy actions.</summary>
    public const double FlashNudgeX   = 4.0;

    // ── Opacity tokens — Impact Noir visual hierarchy ────────────────────────
    public const double TraceHoverOpacity   = 0.72;
    public const double NavIconResting      = 0.52;
    public const double NavIconHover        = 0.88;
    public const double NavIconSelected     = 1.00;
    public const double NavLabelResting     = 0.68;
    public const double NavLabelHover       = 0.96;
    public const double NavLabelSelected    = 1.00;
    
    // ── Impact Noir effect opacities ─────────────────────────────────────────
    public const double FlashParticleOpacity = 0.85;
    public const double FlashArcOpacity      = 0.72;
    public const double FlashGlowOpacity     = 0.38;
    public const double FlashBurstOpacity    = 0.92;
}
