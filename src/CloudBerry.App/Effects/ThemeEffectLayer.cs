using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace CloudBerry.App.Effects;

/// <summary>
/// Premium ambient effect layer — v3.0 "High-Fidelity" overhaul.
/// Restores dynamic, high-energy theme VFX with AAA-level polish,
/// smooth 60fps performance, and professional color grading.
/// </summary>
public sealed class ThemeEffectLayer : FrameworkElement
{
    // Instance-level cache — avoids static shared state being cleared by one instance
    // while another is mid-render (caused InvalidOperationException on Cloudscape open).
    private readonly Dictionary<string, object> _themeCache = new();
    public static readonly DependencyProperty ThemeKeyProperty = DependencyProperty.Register(
        nameof(ThemeKey), typeof(string), typeof(ThemeEffectLayer),
        new FrameworkPropertyMetadata("CloudBerry Dark", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IntensityProperty = DependencyProperty.Register(
        nameof(Intensity), typeof(double), typeof(ThemeEffectLayer),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsEffectActiveProperty = DependencyProperty.Register(
        nameof(IsEffectActive), typeof(bool), typeof(ThemeEffectLayer),
        new FrameworkPropertyMetadata(true, OnActiveChanged));

    public static readonly DependencyProperty EffectRoleProperty = DependencyProperty.Register(
        nameof(EffectRole), typeof(string), typeof(ThemeEffectLayer),
        new FrameworkPropertyMetadata("Shell", FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _lastFrame;

    // ── Cursor parallax — subtle, theme-agnostic "alive" reactivity ──────────
    // The whole render is drawn slightly oversized (see OnRender's ScaleTransform)
    // and nudged toward the cursor. Because IsHitTestVisible is false on every
    // instance, this element never receives its own MouseMove — we track the
    // hosting Window's PreviewMouseMove instead and convert to local coordinates.
    private const double ParallaxMaxShift = 9.0;   // px, each axis
    private const double ParallaxSmoothing = 0.09; // lerp factor per frame (~30fps)
    private double _parallaxTargetX, _parallaxTargetY;
    private double _parallaxCurrentX, _parallaxCurrentY;
    private Window? _parallaxWindow;

    public ThemeEffectLayer()
    {
        IsHitTestVisible = false;
        ClipToBounds = true; // required so the oversized parallax draw never shows a seam at the edges
        Loaded += (_, _) => Start();
        Unloaded += (_, _) => Stop();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible && IsEffectActive) Start();
            else Stop();
        };
    }

    private void OnHostPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (ActualWidth <= 1 || ActualHeight <= 1) return;
        try
        {
            var pos = e.GetPosition(this);
            var nx = Math.Clamp(pos.X / ActualWidth - 0.5, -0.5, 0.5);
            var ny = Math.Clamp(pos.Y / ActualHeight - 0.5, -0.5, 0.5);
            _parallaxTargetX = nx * ParallaxMaxShift;
            _parallaxTargetY = ny * ParallaxMaxShift;
        }
        catch
        {
            // Element may be mid-teardown (window closing) — coordinate transform can throw; ignore.
        }
    }

    public string ThemeKey 
    {
        get => (string)GetValue(ThemeKeyProperty);
        set 
        {
            if ((string)GetValue(ThemeKeyProperty) != value)
            {
                ClearCache();
                SetValue(ThemeKeyProperty, value);
            }
        }
    }

    private void ClearCache() { _themeCache.Clear(); InvalidateRenderCache(); }
    public double Intensity 
    {
        get => (double)GetValue(IntensityProperty);
        set 
        {
            if ((double)GetValue(IntensityProperty) != value)
            {
                ClearCache();
                SetValue(IntensityProperty, value);
            }
        }
    }
    public bool IsEffectActive { get => (bool)GetValue(IsEffectActiveProperty); set => SetValue(IsEffectActiveProperty, value); }
    public string EffectRole { get => (string)GetValue(EffectRoleProperty); set => SetValue(EffectRoleProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 1 || h <= 1 || !IsEffectActive) return;

        try
        {
            var t = _clock.Elapsed.TotalSeconds;
            var i = Math.Clamp(Intensity, 0.0, 1.6);
            var theme = ThemeKey ?? string.Empty;

            // Invalidate cached brushes/pens when theme or intensity changes.
            if (theme != _cachedTheme || Math.Abs(i - _cachedIntensity) > 0.01)
            {
                _renderCache.Clear();
                _cachedTheme = theme;
                _cachedIntensity = i;
            }

            // Cursor parallax: draw everything ~6% oversized from center, then nudge
            // by the smoothed cursor offset. ClipToBounds (set in the constructor)
            // guarantees the oversize always covers the shift, so no edge ever shows
            // a seam no matter which direction the cursor pulls it.
            var parallaxGroup = new TransformGroup();
            parallaxGroup.Children.Add(new ScaleTransform(1.06, 1.06, w / 2.0, h / 2.0));
            parallaxGroup.Children.Add(new TranslateTransform(_parallaxCurrentX, _parallaxCurrentY));
            dc.PushTransform(parallaxGroup);

            if (theme.Contains("Matrix", StringComparison.OrdinalIgnoreCase)) DrawMatrixRain(dc, w, h, t, i, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            else if (theme.Contains("Wuthering", StringComparison.OrdinalIgnoreCase)) DrawWutheringFluid(dc, w, h, t, i);
            else if (theme.Contains("Glitch", StringComparison.OrdinalIgnoreCase)) DrawCyberGlitch(dc, w, h, t, i);
            else if (theme.Contains("NieR", StringComparison.OrdinalIgnoreCase)) DrawNierMechanism(dc, w, h, t, i, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            else if (theme.Contains("Blueberry", StringComparison.OrdinalIgnoreCase)) DrawBlueberryNebula(dc, w, h, t, i);
            else if (theme.Contains("Mint", StringComparison.OrdinalIgnoreCase)) DrawMintTopography(dc, w, h, t, i);
            else if (theme.Contains("High Contrast", StringComparison.OrdinalIgnoreCase)) DrawContrastArchitecture(dc, w, h, t, i);
            else if (theme.Contains("Black Flash", StringComparison.OrdinalIgnoreCase)) DrawBlackFlashDomain(dc, w, h, t, i);
            else DrawCloudBerryGodRays(dc, w, h, t, i);

            dc.Pop(); // parallax transform — vignette stays fixed as a stable cinematic frame

            DrawCinematicVignette(dc, w, h, i);
        }
        catch
        {
            // Never let a render exception crash the app. Cloudscape stays open.
        }
    }

    private static readonly string MatrixGlyphs = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ#$%*+-";
    private static Color WithAlpha(Color c, double a) => Color.FromArgb((byte)Math.Clamp(a, 0.0, 255.0), c.R, c.G, c.B);

    private static void OnActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var layer = (ThemeEffectLayer)d;
        if ((bool)e.NewValue && layer.IsVisible) layer.Start();
        else layer.Stop();
    }

    private void Start()
    {
        CompositionTarget.Rendering -= OnRendering;
        if (IsEffectActive && IsVisible)
        {
            _lastFrame = _clock.Elapsed;
            CompositionTarget.Rendering += OnRendering;

            // Attach parallax mouse tracking to the hosting window (re-resolved each
            // Start() since the visual tree/owner can change, e.g. Cloudscape re-parenting).
            var window = Window.GetWindow(this);
            if (!ReferenceEquals(window, _parallaxWindow))
            {
                if (_parallaxWindow != null) _parallaxWindow.PreviewMouseMove -= OnHostPreviewMouseMove;
                _parallaxWindow = window;
                if (_parallaxWindow != null) _parallaxWindow.PreviewMouseMove += OnHostPreviewMouseMove;
            }
        }
    }

    private void Stop()
    {
        CompositionTarget.Rendering -= OnRendering;
        if (_parallaxWindow != null)
        {
            _parallaxWindow.PreviewMouseMove -= OnHostPreviewMouseMove;
            _parallaxWindow = null;
        }
    }

    // Throttle to 30 FPS for ambient effects — imperceptible vs 60fps, halves CPU/GPU cost.
    // CompositionTarget.Rendering fires every vsync (60+fps); we skip alternate frames.
    private static readonly TimeSpan FrameBudget = TimeSpan.FromMilliseconds(33);

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!IsLoaded || !IsVisible || !IsEffectActive) return;

        // Only skip for minimized — IsActive check removed because BigPictureWindow
        // (Cloudscape) is a secondary window that may never be IsActive=true while
        // MainWindow holds focus, which previously caused effects to never render.
        var window = Window.GetWindow(this);
        if (window is { WindowState: WindowState.Minimized }) return;

        var elapsed = _clock.Elapsed;
        if (elapsed - _lastFrame < FrameBudget) return;

        _lastFrame = elapsed;

        // Smooth the parallax offset toward its target every tick, independent of
        // whether the theme switch itself changed — this is what makes the drift
        // feel like inertia rather than a snap-to-cursor jump.
        _parallaxCurrentX += (_parallaxTargetX - _parallaxCurrentX) * ParallaxSmoothing;
        _parallaxCurrentY += (_parallaxTargetY - _parallaxCurrentY) * ParallaxSmoothing;

        InvalidateVisual();
    }

    // ── Per-instance render cache ─────────────────────────────────────────────
    // All brushes, pens, and static geometry are built once and reused every frame.
    // Previously these were allocated inside OnRender (FrozenBrush/FrozenPen calls
    // on every tick), generating constant GC pressure at 30fps.
    private string _cachedTheme = string.Empty;
    private double _cachedIntensity = -1;
    private readonly Dictionary<string, object> _renderCache = new();

    private T GetCached<T>(string key, Func<T> build) where T : class
    {
        if (_renderCache.TryGetValue(key, out var v)) return (T)v;
        var result = build();
        _renderCache[key] = result;
        return result;
    }

    private void InvalidateRenderCache()
    {
        _renderCache.Clear();
        _cachedTheme = string.Empty;
        _cachedIntensity = -1;
    }

    private static SolidColorBrush FrozenBrush(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Pen FrozenPen(Color c, double thickness)
    {
        var p = new Pen(FrozenBrush(c), thickness);
        p.Freeze();
        return p;
    }

    private static double Wrap(double value, double limit)
    {
        if (limit <= 0) return 0;
        var wrapped = value % limit;
        return wrapped < 0 ? wrapped + limit : wrapped;
    }

    private static double Hash01(int n)
    {
        var x = Math.Sin(n * 12.9898) * 43758.5453;
        return x - Math.Floor(x);
    }

    private static void DrawRadialGlow(DrawingContext dc, Rect rect, Color color, double intensity)
    {
        var brush = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        brush.GradientStops.Add(new GradientStop(WithAlpha(color, 120 * intensity), 0));
        brush.GradientStops.Add(new GradientStop(WithAlpha(color, 28 * intensity), 0.42));
        brush.GradientStops.Add(new GradientStop(WithAlpha(color, 0), 1));
        brush.Freeze();
        dc.DrawRectangle(brush, null, rect);
    }

    private static StreamGeometry BuildWaveGeometry(double w, double y, double amplitude, double frequency, double phase)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(-40, y), false, false);
            for (var x = -40.0; x <= w + 40; x += 34)
            {
                var yy = y + Math.Sin(x * frequency + phase) * amplitude + Math.Sin(x * frequency * 0.45 + phase * 0.7) * amplitude * 0.38;
                ctx.LineTo(new Point(x, yy), true, false);
            }
        }
        geometry.Freeze();
        return geometry;
    }

    private void DrawCinematicVignette(DrawingContext dc, double w, double h, double i)
    {
        var key = "vignette";
        var v = GetCached(key, () =>
        {
            var brush = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.85,
                RadiusY = 0.85
            };
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 5, 5, 5), 0.4));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Clamp(120 * i, 0, 255), 2, 2, 2), 1.0));
            brush.Freeze();
            return brush;
        });
        dc.DrawRectangle(v, null, new Rect(0, 0, w, h));
    }

    private static void DrawCloudBerryGodRays(DrawingContext dc, double w, double h, double t, double i)
    {
        dc.DrawRectangle(FrozenBrush(Color.FromRgb(202, 191, 154)), null, new Rect(0, 0, w, h));

        DrawRadialGlow(dc, new Rect(w * -0.18, h * -0.2, w * 0.9, h * 0.9), Color.FromRgb(255, 242, 206), 0.42 * i);
        DrawRadialGlow(dc, new Rect(w * 0.54, h * 0.16, w * 0.72, h * 0.76), Color.FromRgb(150, 132, 82), 0.22 * i);

        var rayBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0.18) };
        rayBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 244, 214), 0));
        rayBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Clamp(46 * i, 0, 255), 255, 244, 214), 0.38));
        rayBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 244, 214), 1));
        rayBrush.Freeze();

        var offset = Wrap(t * 36, w * 1.8);
        for (var r = 0; r < 3; r++)
        {
            dc.PushTransform(new RotateTransform(-10 + r * 5, w * 0.5, h * 0.5));
            dc.DrawRectangle(rayBrush, null, new Rect(offset - w * (1.2 - r * 0.22), -h * 0.22, w * (0.46 - r * 0.06), h * 1.5));
            dc.Pop();
        }

        var linePen = FrozenPen(Color.FromArgb((byte)Math.Clamp(18 * i, 0, 255), 74, 62, 38), 1);
        for (var y = 0.0; y < h; y += 34)
        {
            dc.DrawLine(linePen, new Point(0, y + Math.Sin(t * 0.15 + y) * 1.8), new Point(w, y));
        }

        for (int m = 0; m < 34; m++)
        {
            var mx = Wrap(m * 173 + Math.Sin(t * 0.42 + m) * 44, w);
            var my = Wrap(m * 101 + Math.Cos(t * 0.35 + m) * 28, h);
            var r = 0.9 + Hash01(m + 10) * 2.2;
            dc.DrawEllipse(FrozenBrush(Color.FromArgb((byte)Math.Clamp((28 + Hash01(m) * 42) * i, 0, 255), 255, 235, 188)), null, new Point(mx, my), r, r);
        }
    }

    private static void DrawMatrixRain(DrawingContext dc, double w, double h, double t, double i, double pixelsPerDip)
    {
        dc.DrawRectangle(FrozenBrush(Color.FromRgb(2, 9, 5)), null, new Rect(0, 0, w, h));
        DrawRadialGlow(dc, new Rect(w * 0.18, h * -0.12, w * 0.8, h * 0.9), Color.FromRgb(28, 255, 116), 0.16 * i);

        var scanPen = FrozenPen(Color.FromArgb((byte)Math.Clamp(14 * i, 0, 255), 150, 255, 190), 1);
        for (var y = 0.0; y < h; y += 7)
        {
            dc.DrawLine(scanPen, new Point(0, y), new Point(w, y));
        }

        var colW = 30.0;
        var rowH = 22.0;
        var cols = (int)(w / colW) + 2;
        var typeface = new Typeface("Consolas");

        for (int c = 0; c < cols; c++)
        {
            var seed = c * 92821;
            var streamLength = 8 + (int)(Hash01(seed) * 11);
            var speed = 52 + Hash01(seed + 3) * 105;
            var head = Wrap(t * speed + Hash01(seed + 9) * h * 1.7, h + streamLength * rowH) - streamLength * rowH;
            var x = c * colW + Hash01(seed + 17) * 5;

            for (int r = 0; r < streamLength; r++)
            {
                var y = head - r * rowH;
                if (y < -24 || y > h + 24) continue;

                var glyphIndex = (int)((seed + r * 11 + Math.Floor(t * (2 + Hash01(seed) * 4))) % MatrixGlyphs.Length);
                var glyph = MatrixGlyphs[Math.Abs(glyphIndex)].ToString();
                var fade = 1.0 - r / (double)streamLength;
                var alpha = r == 0 ? 230 : 34 + fade * 132;
                var brush = FrozenBrush(r == 0
                    ? Color.FromArgb((byte)Math.Clamp(alpha * i, 0, 255), 232, 255, 238)
                    : Color.FromArgb((byte)Math.Clamp(alpha * i, 0, 255), 48, 255, 124));
                var dpi = pixelsPerDip;
                var text = new FormattedText(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 13 + Hash01(seed + r) * 2, brush, dpi);
                dc.DrawText(text, new Point(x, y));
            }
        }
    }

    private static void DrawNierMechanism(DrawingContext dc, double w, double h, double t, double i, double pixelsPerDip)
    {
        dc.DrawRectangle(FrozenBrush(Color.FromRgb(218, 209, 188)), null, new Rect(0, 0, w, h));
        DrawRadialGlow(dc, new Rect(w * 0.08, h * 0.06, w * 0.9, h * 0.9), Color.FromRgb(245, 238, 213), 0.28 * i);
        DrawRadialGlow(dc, new Rect(w * 0.58, h * 0.18, w * 0.58, h * 0.7), Color.FromRgb(120, 112, 88), 0.12 * i);

        var cx = w * 0.5;
        var cy = h * 0.5;
        var ringPen = FrozenPen(Color.FromArgb((byte)Math.Clamp(64 * i, 0, 255), 80, 72, 55), 1.2);
        var finePen = FrozenPen(Color.FromArgb((byte)Math.Clamp(36 * i, 0, 255), 58, 50, 38), 0.8);

        for (int r = 110; r < 700; r += 86)
        {
            var angle = t * 4 * (r % 2 == 0 ? 1 : -1);
            dc.PushTransform(new RotateTransform(angle, cx, cy));
            dc.DrawEllipse(null, r % 172 == 0 ? finePen : ringPen, new Point(cx, cy), r, r);
            for (int tick = 0; tick < 16; tick++)
            {
                var a = tick * Math.PI / 8;
                var p1 = new Point(cx + Math.Cos(a) * (r - 8), cy + Math.Sin(a) * (r - 8));
                var p2 = new Point(cx + Math.Cos(a) * (r + 8), cy + Math.Sin(a) * (r + 8));
                dc.DrawLine(finePen, p1, p2);
            }
            dc.Pop();
        }

        var scanPen = FrozenPen(Color.FromArgb((byte)Math.Clamp(24 * i, 0, 255), 60, 50, 40), 0.8);
        for (var y = 0.0; y < h; y += 5)
        {
            dc.DrawLine(scanPen, new Point(0, y), new Point(w, y));
        }

        var deletionPen = FrozenPen(Color.FromArgb((byte)Math.Clamp(80 * i, 0, 255), 84, 75, 58), 1);
        for (int row = 0; row < 18; row++)
        {
            var y = Wrap(row * 47 - t * (10 + row % 4), h + 80) - 40;
            var x = Wrap(row * 151 + Math.Sin(t * 0.6 + row) * 80, w);
            var width = 48 + Hash01(row) * 180;
            dc.DrawLine(deletionPen, new Point(x - width * 0.5, y), new Point(x + width * 0.5, y));
            for (int chip = 0; chip < 5; chip++)
            {
                var chipX = Wrap(x + chip * 31 + Hash01(row * 9 + chip) * 50, w);
                dc.DrawRectangle(FrozenBrush(Color.FromArgb((byte)Math.Clamp((28 + chip * 11) * i, 0, 255), 76, 67, 50)), null, new Rect(chipX, y - 2, 7 + chip % 3 * 4, 4));
            }
        }

        var typeface = new Typeface("Consolas");
        for (int p = 0; p < 12; p++)
        {
            var label = p % 3 == 0 ? "DELETE" : p % 3 == 1 ? "SAVE DATA" : "YoRHa";
            var px = Wrap(p * 211 + Math.Sin(t * 0.35 + p) * 40, w);
            var py = Wrap(h - (t * 24 + p * 97), h + 120) - 40;
            var brush = FrozenBrush(Color.FromArgb((byte)Math.Clamp(46 * i, 0, 255), 58, 50, 38));
            var dpi = pixelsPerDip;
            dc.DrawText(new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 10, brush, dpi), new Point(px, py));
        }
    }

    private static void DrawWutheringFluid(DrawingContext dc, double w, double h, double t, double i)
    {
        dc.DrawRectangle(FrozenBrush(Color.FromRgb(6, 18, 31)), null, new Rect(0, 0, w, h));

        DrawRadialGlow(dc, new Rect(w * -0.15, h * 0.05, w * 0.78, h * 0.82), Color.FromRgb(94, 180, 255), 0.24 * i);
        DrawRadialGlow(dc, new Rect(w * 0.42, h * -0.16, w * 0.9, h * 0.95), Color.FromRgb(114, 246, 232), 0.34 * i);
        DrawRadialGlow(dc, new Rect(w * 0.68, h * 0.2, w * 0.48, h * 0.62), Color.FromRgb(245, 92, 112), 0.18 * i);

        var tealPen = FrozenPen(Color.FromArgb((byte)Math.Clamp(92 * i, 0, 255), 114, 246, 232), 1.4);
        var tealSoftPen = FrozenPen(Color.FromArgb((byte)Math.Clamp(32 * i, 0, 255), 114, 246, 232), 5.5);
        var coralPen = FrozenPen(Color.FromArgb((byte)Math.Clamp(72 * i, 0, 255), 255, 126, 148), 1.1);

        for (int band = 0; band < 8; band++)
        {
            var y = h * (0.16 + band * 0.105) + Math.Sin(t * 0.24 + band) * 28;
            var phase = t * (0.55 + band * 0.05) + band * 0.8;
            var geometry = BuildWaveGeometry(w, y, 20 + band * 3, 0.006 + band * 0.0007, phase);
            dc.DrawGeometry(null, band % 2 == 0 ? tealSoftPen : coralPen, geometry);
            dc.DrawGeometry(null, tealPen, geometry);
        }

        for (int p = 0; p < 42; p++)
        {
            var speed = 15 + Hash01(p) * 28;
            var px = Wrap(p * 149 + t * speed, w);
            var py = Wrap(p * 83 + Math.Sin(t * 0.7 + p) * 38, h);
            var color = p % 5 == 0 ? Color.FromRgb(255, 176, 150) : Color.FromRgb(142, 255, 239);
            dc.DrawEllipse(FrozenBrush(WithAlpha(color, (34 + Hash01(p + 4) * 70) * i)), null, new Point(px, py), 1.2 + Hash01(p) * 2.3, 1.2 + Hash01(p) * 2.3);
        }
    }

    private static void DrawCyberGlitch(DrawingContext dc, double w, double h, double t, double i)
    {
        dc.DrawRectangle(FrozenBrush(Color.FromRgb(13, 8, 24)), null, new Rect(0, 0, w, h));
        DrawRadialGlow(dc, new Rect(w * 0.08, h * 0.04, w * 0.8, h * 0.7), Color.FromRgb(255, 79, 208), 0.22 * i);
        DrawRadialGlow(dc, new Rect(w * 0.42, h * 0.3, w * 0.7, h * 0.74), Color.FromRgb(88, 183, 255), 0.18 * i);

        var gridPen = FrozenPen(Color.FromArgb((byte)Math.Clamp(28 * i, 0, 255), 255, 79, 208), 1);
        var gridOffset = Wrap(t * 22, 44);
        for (var x = -44 + gridOffset; x < w + 44; x += 44)
        {
            dc.DrawLine(gridPen, new Point(x, 0), new Point(x + w * 0.18, h));
        }

        var scanPen = FrozenPen(Color.FromArgb((byte)Math.Clamp(18 * i, 0, 255), 255, 255, 255), 1);
        for (var y = 0.0; y < h; y += 6)
        {
            dc.DrawLine(scanPen, new Point(0, y), new Point(w, y));
        }

        for (int tear = 0; tear < 8; tear++)
        {
            var phase = Wrap(t * (0.75 + tear * 0.1) + Hash01(tear) * 2, 1);
            if (phase > 0.42) continue;
            var ty = Wrap(h * Hash01(tear * 17) + Math.Sin(t * 5 + tear) * 24, h);
            var height = 2 + Hash01(tear + 5) * 13;
            var shift = Math.Sin(t * 11 + tear) * (18 + Hash01(tear) * 32);
            var magenta = FrozenBrush(Color.FromArgb((byte)Math.Clamp((68 - phase * 70) * i, 0, 255), 255, 79, 208));
            var cyan = FrozenBrush(Color.FromArgb((byte)Math.Clamp((54 - phase * 60) * i, 0, 255), 88, 183, 255));
            dc.DrawRectangle(magenta, null, new Rect(shift - 16, ty, w * (0.36 + Hash01(tear) * 0.5), height));
            dc.DrawRectangle(cyan, null, new Rect(shift + 12, ty + height * 0.42, w * (0.28 + Hash01(tear + 2) * 0.42), Math.Max(1, height * 0.4)));
        }
    }

    private static void DrawBlackFlashDomain(DrawingContext dc, double w, double h, double t, double i)
    {
        // ── BASE LAYER: Void black (clean, no grain for subtlety) ───────────────
        dc.DrawRectangle(FrozenBrush(Color.FromRgb(10, 10, 15)), null, new Rect(0, 0, w, h));

        // ── CORE: Very subtle ambient cursed energy glow ─────────────────────────
        var cx = w * 0.5; // Fixed center (no movement for subtlety)
        var cy = h * 0.5;
        
        // Single soft crimson ambient glow (no chromatic aberration)
        DrawRadialGlow(dc, new Rect(cx - w * 0.35, cy - h * 0.35, w * 0.7, h * 0.7), 
            Color.FromRgb(229, 27, 45), 0.06 * i); // Very low opacity
        
        // Subtle violet undertone (ambient depth)
        DrawRadialGlow(dc, new Rect(w * -0.05, h * 0.1, w * 0.5, h * 0.6), 
            Color.FromRgb(74, 21, 75), 0.04 * i);

        // ── SUBTLE ENERGY TRACES: Minimal curved lines (no jagged lightning) ────
        if (i > 0.4) // Only appear at moderate interaction
        {
            var tracePen = FrozenPen(Color.FromArgb((byte)Math.Clamp(22 * i, 0, 255), 229, 27, 45), 0.8);
            for (int trace = 0; trace < 3; trace++) // Only 3 traces max
            {
                DrawSubtleTrace(dc, cx, cy, w, h, t, i, trace, tracePen);
            }
        }

        // ── MINIMAL PARTICLES: Very sparse, slow drift (no trails) ──────────────
        if (i > 0.6) // Only at higher interaction
        {
            DrawSubtleParticles(dc, w, h, t, i, cx, cy);
        }

        // ── SOFT PULSE: Barely-there core animation ─────────────────────────────
        if (i > 0.8)
        {
            DrawSoftPulse(dc, cx, cy, w, h, t, i);
        }

        // No screen distortion, no impact slashes, no bloom flash, no film grain
        // Performance: all resources frozen, frame budget enforced via OnRendering throttle
    }

    // Film grain removed for subtlety - clean void aesthetic preferred

    // ChromaticGlow removed - replaced with single subtle radial in main method

    // VolumetricCones removed - too busy for subtle aesthetic

    // ElectricArc removed - replaced with subtle curved traces below

    // ImpactSlash removed - too aggressive for subtle theme

    // CoreBloomFlash removed - replaced with soft pulse below

    // CursedParticles removed - replaced with subtle sparse particles below

    // ScreenDistortion removed - too noticeable for subtle aesthetic

    /// <summary>
    /// Subtle curved energy trace — gentle bezier curve, very low opacity.
    /// Replaces aggressive lightning with elegant cursed energy flow.
    /// </summary>
    private static void DrawSubtleTrace(DrawingContext dc, double cx, double cy, double w, double h,
        double t, double i, int trace, Pen tracePen)
    {
        var baseAngle = trace * Math.PI / 1.5; // Wider spacing for fewer traces
        var radius = Math.Min(w, h) * (0.18 + trace * 0.08);
        
        // Very slow, gentle movement
        var drift = Math.Sin(t * 0.08 + trace) * 4;
        
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var start = new Point(cx + Math.Cos(baseAngle) * radius * 0.4 + drift, 
                                 cy + Math.Sin(baseAngle) * radius * 0.4);
            ctx.BeginFigure(start, false, false);
            
            // Single smooth curve segment (no jagged lightning)
            var end = new Point(cx + Math.Cos(baseAngle + 0.15) * radius + drift * 0.5,
                               cy + Math.Sin(baseAngle + 0.15) * radius);
            var cp = new Point(
                (start.X + end.X) * 0.5 + Math.Sin(t * 0.12 + trace) * 6,
                (start.Y + end.Y) * 0.5 + Math.Cos(t * 0.09 + trace) * 4);
            ctx.QuadraticBezierTo(cp, end, true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, tracePen, geometry);
    }

    /// <summary>
    /// Sparse, slow-drifting particles — minimal count, no trails, very subtle.
    /// </summary>
    private static void DrawSubtleParticles(DrawingContext dc, double w, double h, double t, double i, double cx, double cy)
    {
        var particleCount = (int)(6 * i); // Max 6 particles, very sparse
        for (int p = 0; p < particleCount; p++)
        {
            var seed = p * 137;
            
            // Very slow, small orbital motion
            var orbitRadius = 60 + Hash01(seed) * 50;
            var orbitAngle = t * 0.04 + p * 0.8; // Extremely slow rotation
            var px = cx + Math.Cos(orbitAngle) * orbitRadius;
            var py = cy + Math.Sin(orbitAngle) * orbitRadius * 0.5;
            
            // Tiny size, very low opacity
            var size = 0.8 + Hash01(seed + 1) * 0.6;
            var alpha = (byte)(18 * i); // Max 18/255 opacity
            dc.DrawEllipse(FrozenBrush(Color.FromArgb(alpha, 229, 27, 45)), null, 
                new Point(px, py), size, size);
        }
    }

    /// <summary>
    /// Barely-there core pulse — subtle breathing animation, no flash.
    /// </summary>
    private static void DrawSoftPulse(DrawingContext dc, double cx, double cy, double w, double h, double t, double i)
    {
        var pulse = Math.Sin(t * 0.18) * 0.04 + 1.0; // Very slow, tiny amplitude
        var alpha = (byte)(12 * i * pulse); // Max 12/255 opacity
        var radius = Math.Min(w, h) * 0.08 * pulse;
        
        dc.DrawEllipse(FrozenBrush(Color.FromArgb(alpha, 255, 240, 240)), null,
            new Point(cx, cy), radius, radius * 0.6);
    }

    private static void DrawBlueberryNebula(DrawingContext dc, double w, double h, double t, double i)
    {
        dc.DrawRectangle(FrozenBrush(Color.FromRgb(5, 12, 27)), null, new Rect(0, 0, w, h));
        DrawRadialGlow(dc, new Rect(w * -0.08, h * -0.12, w * 0.9, h * 0.9), Color.FromRgb(96, 165, 250), 0.34 * i);
        DrawRadialGlow(dc, new Rect(w * 0.48, h * 0.18, w * 0.75, h * 0.86), Color.FromRgb(125, 211, 252), 0.2 * i);

        var orbitPen = FrozenPen(Color.FromArgb((byte)Math.Clamp(44 * i, 0, 255), 170, 215, 255), 1);
        for (int o = 0; o < 5; o++)
        {
            dc.PushTransform(new RotateTransform(t * (3 + o) + o * 27, w * 0.5, h * 0.52));
            dc.DrawEllipse(null, orbitPen, new Point(w * 0.5, h * 0.52), w * (0.18 + o * 0.07), h * (0.08 + o * 0.045));
            dc.Pop();
        }

        for (int s = 0; s < 82; s++)
        {
            var sx = Wrap(s * 173 + t * (3 + Hash01(s) * 11), w);
            var sy = Wrap(s * 89 + Math.Sin(t * 0.45 + s) * 12, h);
            var r = 0.8 + Hash01(s + 2) * 1.8;
            dc.DrawEllipse(FrozenBrush(Color.FromArgb((byte)Math.Clamp((42 + Hash01(s) * 96) * i, 0, 255), 222, 241, 255)), null, new Point(sx, sy), r, r);
        }
    }

    private static void DrawMintTopography(DrawingContext dc, double w, double h, double t, double i)
    {
        dc.DrawRectangle(FrozenBrush(Color.FromRgb(8, 24, 19)), null, new Rect(0, 0, w, h));
        DrawRadialGlow(dc, new Rect(w * 0.12, h * 0.04, w * 0.76, h * 0.84), Color.FromRgb(157, 231, 201), 0.22 * i);

        var contourPen = FrozenPen(Color.FromArgb((byte)Math.Clamp(58 * i, 0, 255), 136, 230, 188), 1.05);
        var lightPen = FrozenPen(Color.FromArgb((byte)Math.Clamp(24 * i, 0, 255), 225, 255, 245), 0.8);
        for (int l = 0; l < 16; l++)
        {
            var y = h * (0.06 + l * 0.07) + Math.Sin(t * 0.18 + l) * 12;
            var geometry = BuildWaveGeometry(w, y, 18 + (l % 5) * 4, 0.009, t * 0.2 + l);
            dc.DrawGeometry(null, l % 4 == 0 ? lightPen : contourPen, geometry);
        }

        for (int node = 0; node < 24; node++)
        {
            var px = Wrap(node * 157 + Math.Sin(t * 0.32 + node) * 24, w);
            var py = Wrap(node * 113 + Math.Cos(t * 0.28 + node) * 24, h);
            dc.DrawEllipse(FrozenBrush(Color.FromArgb((byte)Math.Clamp(58 * i, 0, 255), 157, 231, 201)), null, new Point(px, py), 2.2, 2.2);
        }
    }

    private static void DrawContrastArchitecture(DrawingContext dc, double w, double h, double t, double i)
    {
        dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, w, h));
        var primaryPen = FrozenPen(Color.FromArgb((byte)Math.Clamp(170 * i, 0, 255), 255, 255, 255), 1.1);
        var ghostPen = FrozenPen(Color.FromArgb((byte)Math.Clamp(54 * i, 0, 255), 255, 255, 255), 1);
        var grid = 96.0;
        var offset = Wrap(t * 18, grid);
        for (var x = -grid + offset; x < w + grid; x += grid)
        {
            dc.DrawLine(ghostPen, new Point(x, 0), new Point(x, h));
        }
        for (var y = -grid + offset; y < h + grid; y += grid)
        {
            dc.DrawLine(ghostPen, new Point(0, y), new Point(w, y));
        }

        for (int block = 0; block < 9; block++)
        {
            var bw = 60 + Hash01(block) * 180;
            var bh = 26 + Hash01(block + 8) * 90;
            var bx = Wrap(block * 241 + Math.Sin(t * 0.18 + block) * 34, w);
            var by = Wrap(block * 127 - t * (10 + block), h);
            dc.DrawRectangle(null, block % 3 == 0 ? primaryPen : ghostPen, new Rect(bx, by, bw, bh));
        }
    }
}
