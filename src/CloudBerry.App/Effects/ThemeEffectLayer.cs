using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CloudBerry.App.Effects;

/// <summary>Role-aware ambient artwork shared by the desktop, Horizon, and previews.</summary>
public sealed class ThemeEffectLayer : FrameworkElement
{
    public static readonly DependencyProperty ThemeKeyProperty = DependencyProperty.Register(
        nameof(ThemeKey), typeof(string), typeof(ThemeEffectLayer),
        new FrameworkPropertyMetadata("Cirra Core", FrameworkPropertyMetadataOptions.AffectsRender, OnConfigurationChanged));
    public static readonly DependencyProperty IntensityProperty = DependencyProperty.Register(
        nameof(Intensity), typeof(double), typeof(ThemeEffectLayer),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender, OnConfigurationChanged, CoerceIntensity));
    public static readonly DependencyProperty IsEffectActiveProperty = DependencyProperty.Register(
        nameof(IsEffectActive), typeof(bool), typeof(ThemeEffectLayer),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender, OnConfigurationChanged));
    public static readonly DependencyProperty EffectRoleProperty = DependencyProperty.Register(
        nameof(EffectRole), typeof(string), typeof(ThemeEffectLayer),
        new FrameworkPropertyMetadata("Shell", FrameworkPropertyMetadataOptions.AffectsRender, OnConfigurationChanged));
    public static readonly DependencyProperty QualityProperty = DependencyProperty.Register(
        nameof(Quality), typeof(string), typeof(ThemeEffectLayer),
        new FrameworkPropertyMetadata("Full", FrameworkPropertyMetadataOptions.AffectsRender, OnConfigurationChanged));

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<string, DrawingGroup> _compositions = new();
    private ThemeVisualProfile _profile = ThemeVisualProfile.Resolve(null);
    private Pen? _highlightPen;
    private Window? _host;
    private TimeSpan _lastFrame;
    private Size _cachedSize;
    private double _sceneTime;
    private double _pointerX, _pointerY, _targetX, _targetY;
    private bool _subscribed;

    public string ThemeKey { get => (string)GetValue(ThemeKeyProperty); set => SetValue(ThemeKeyProperty, value); }
    public double Intensity { get => (double)GetValue(IntensityProperty); set => SetValue(IntensityProperty, value); }
    public bool IsEffectActive { get => (bool)GetValue(IsEffectActiveProperty); set => SetValue(IsEffectActiveProperty, value); }
    public string EffectRole { get => (string)GetValue(EffectRoleProperty); set => SetValue(EffectRoleProperty, value); }
    public string Quality { get => (string)GetValue(QualityProperty); set => SetValue(QualityProperty, value); }

    internal bool CanAnimate => IsEffectActive && Intensity > 0 && Quality != "Off" &&
        !_profile.IsHighContrast && SystemParameters.ClientAreaAnimation && EffectRole is not ("Chrome" or "Sidebar");
    internal int CachedCompositionCount => _compositions.Count;

    public ThemeEffectLayer()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        Loaded += (_, _) => UpdateSubscription();
        Unloaded += (_, _) => Stop();
        IsVisibleChanged += (_, _) => UpdateSubscription();
    }

    private static object CoerceIntensity(DependencyObject d, object value) =>
        double.IsFinite((double)value) ? Math.Clamp((double)value, 0, 1.6) : 0d;

    private static void OnConfigurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var layer = (ThemeEffectLayer)d;
        layer._profile = ThemeVisualProfile.Resolve(layer.ThemeKey);
        layer._compositions.Clear();
        layer._highlightPen = null;
        layer._pointerX = layer._pointerY = layer._targetX = layer._targetY = 0;
        layer.UpdateSubscription();
    }

    private void UpdateSubscription()
    {
        Stop();
        if (!IsLoaded || !IsVisible || !CanAnimate) return;
        _host = Window.GetWindow(this);
        if (_host is not null)
        {
            _host.PreviewMouseMove += OnPointerMove;
            _host.MouseLeave += OnPointerLeave;
        }
        _lastFrame = _clock.Elapsed;
        CompositionTarget.Rendering += OnRendering;
        _subscribed = true;
    }

    private void Stop()
    {
        if (_subscribed) CompositionTarget.Rendering -= OnRendering;
        _subscribed = false;
        if (_host is not null)
        {
            _host.PreviewMouseMove -= OnPointerMove;
            _host.MouseLeave -= OnPointerLeave;
            _host = null;
        }
    }

    private void OnPointerMove(object sender, MouseEventArgs e)
    {
        if (ActualWidth <= 1 || ActualHeight <= 1 || Quality == "Efficient") return;
        var point = e.GetPosition(this);
        _targetX = Math.Clamp(point.X / ActualWidth - 0.5, -0.5, 0.5) * _profile.Parallax;
        _targetY = Math.Clamp(point.Y / ActualHeight - 0.5, -0.5, 0.5) * _profile.Parallax;
    }

    private void OnPointerLeave(object sender, MouseEventArgs e) => _targetX = _targetY = 0;

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!CanAnimate) { UpdateSubscription(); InvalidateVisual(); return; }
        var now = _clock.Elapsed;
        var delta = now - _lastFrame;
        if (_host is { WindowState: WindowState.Minimized }) { _lastFrame = now; return; }
        if (delta.TotalMilliseconds < (Quality == "Efficient" ? 66 : 33)) return;
        _lastFrame = now;
        // Active time pauses while hidden/minimized, avoiding a jump on return.
        var seconds = Math.Min(delta.TotalSeconds, 0.1);
        _sceneTime += seconds;
        var smoothing = 1 - Math.Exp(-seconds * 5);
        _pointerX += (_targetX - _pointerX) * smoothing;
        _pointerY += (_targetY - _pointerY) * smoothing;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        RenderFrame(dc, RenderSize, CanAnimate ? _sceneTime : 0);
    }

    internal void RenderFrame(DrawingContext dc, Size size, double seconds)
    {
        if (size.Width <= 1 || size.Height <= 1 || !IsEffectActive || Intensity <= 0 || Quality == "Off" || _profile.IsHighContrast) return;
        if (_cachedSize != size) { _compositions.Clear(); _cachedSize = size; }
        var chrome = EffectRole == "Chrome";
        var compact = EffectRole is "Sidebar" or "Card" || size.Height < 160;
        var time = CanAnimate ? seconds * _profile.Tempo : 0;
        var layers = Quality == "Efficient" || compact || chrome || _profile.Key == "Signal Rain" ? 1 : 2;
        dc.PushClip(new RectangleGeometry(new Rect(size)));
        try
        {
            for (var layer = 0; layer < layers; layer++)
            {
                var drawing = GetComposition(layer, compact, chrome);
                var travel = compact ? 0.25 : 1d;
                var (horizontal, vertical) = _profile.Key switch
                {
                    "Verdant Glass" => (0.008, 0.018),
                    "Tidal Glass" => (0.018, 0.012),
                    "Impact Noir" => (0.012, 0.012),
                    "Signal Rain" => (0d, 0.006),
                    "Archive Ivory" => (0.004, 0.004),
                    _ => (0.012, 0.009)
                };
                var dx = chrome ? 0 : Math.Sin(time * 0.30 + layer) * horizontal * travel + _pointerX / size.Width;
                var dy = chrome ? 0 : Math.Sin(time * 0.23 + layer * 2) * vertical * travel + _pointerY / size.Height;
                var strength = (EffectRole == "Cloudscape" ? 0.9 : EffectRole == "Card" ? 1 : 0.7) * Intensity;
                dc.PushOpacity(Math.Clamp(strength * (0.93 + 0.07 * Math.Cos(time * 0.12 + layer)), 0, 1));
                dc.PushTransform(new MatrixTransform(size.Width / 1000, 0, 0, size.Height / 600, dx * size.Width, dy * size.Height));
                var angle = _profile.Key switch
                {
                    "Midnight Observatory" => Math.Sin(time * 0.18) * (layer == 0 ? 6 : -6),
                    "Archive Ivory" => Math.Sin(time * 0.06) * 0.5,
                    "Tidal Glass" => Math.Sin(time * 0.24 + layer) * 1.8,
                    _ => 0
                };
                dc.PushTransform(new RotateTransform(angle, layer == 0 ? 900 : 70, layer == 0 ? 80 : 560));
                try { dc.DrawDrawing(drawing); }
                finally { dc.Pop(); dc.Pop(); dc.Pop(); }
            }
            if (_profile.Key == "Signal Rain" && !chrome) DrawSignalHeads(dc, size, time, compact);
            if (CanAnimate && Quality == "Full" && !compact && !chrome && _profile.Key != "Signal Rain")
                DrawTravelingHighlights(dc, size, time);
        }
        finally { dc.Pop(); }
    }

    private DrawingGroup GetComposition(int layer, bool compact, bool chrome)
    {
        var key = $"{layer}:{compact}:{chrome}";
        if (_compositions.TryGetValue(key, out var cached)) return cached;
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            if (chrome)
                dc.DrawLine(MakePen(_profile.Accent, 0.28, 1), new Point(0, 590), new Point(1000, 590));
            else
            {
                if (Quality == "Full") DrawMaterialLight(dc, layer, compact);
                switch (_profile.Key)
                {
                    case "Signal Rain": DrawSignal(dc, compact); break;
                    case "Midnight Observatory": DrawObservatory(dc, layer, compact); break;
                    case "Verdant Glass": DrawContours(dc, layer, compact); break;
                    case "Archive Ivory": DrawArchive(dc, layer, compact); break;
                    case "Tidal Glass": DrawTidal(dc, layer, compact); break;
                    case "Arcade Static": DrawArcade(dc, layer, compact); break;
                    case "Impact Noir": DrawFacets(dc, layer); break;
                    default: DrawCirrus(dc, layer); break;
                }
            }
        }
        group.Freeze();
        _compositions.Add(key, group);
        return group;
    }

    private void DrawTravelingHighlights(DrawingContext dc, Size size, double time)
    {
        _highlightPen ??= MakePen(_profile.SecondaryAccent, 0.55, 1.1);
        dc.PushTransform(new ScaleTransform(size.Width / 1000, size.Height / 600));
        try
        {
            for (int index = 0; index < 3; index++)
            {
                double phase = (time / 24 + index / 3d) % 1;
                double fade = Math.Pow(Math.Sin(phase * Math.PI), 2) * Math.Min(Intensity, 1);
                dc.PushOpacity(fade);
                try
                {
                    if (_profile.Key is "Midnight Observatory" or "Archive Ivory")
                    {
                        double angle = phase * Math.PI * 2;
                        double radius = 62 + index * 26;
                        var point = new Point(910 + Math.Cos(angle) * radius, 65 + Math.Sin(angle) * radius * 0.65);
                        dc.DrawLine(_highlightPen, new Point(point.X - 2, point.Y), new Point(point.X + 2, point.Y));
                        dc.DrawLine(_highlightPen, new Point(point.X, point.Y - 2), new Point(point.X, point.Y + 2));
                    }
                    else if (_profile.Key is "Verdant Glass" or "Arcade Static" or "Impact Noir")
                    {
                        double y = phase * 660 - 30;
                        double x = 950 - index * 22 + Math.Sin(y / 125) * 18;
                        dc.DrawLine(_highlightPen, new Point(x, y), new Point(x + 1, y + 14));
                    }
                    else
                    {
                        double x = phase * 1100 - 50;
                        double y = (_profile.Key == "Tidal Glass" ? 555 : 40) + Math.Sin(x / 180 + index * 0.2) * 15 + index * 7;
                        dc.DrawLine(_highlightPen, new Point(x, y), new Point(x + 16, y + Math.Cos(x / 180 + index * 0.2) * 1.3));
                    }
                }
                finally { dc.Pop(); }
            }
        }
        finally { dc.Pop(); }
    }

    private static SolidColorBrush Brush(string color, double opacity)
    {
        var value = ThemeVisualProfile.ColorOf(color);
        value.A = (byte)Math.Clamp(opacity * 255, 0, 255);
        return ThemeVisualProfile.BrushOf(value);
    }

    private static Pen MakePen(string color, double opacity, double width)
    {
        var pen = new Pen(Brush(color, opacity), width);
        pen.Freeze();
        return pen;
    }

    private static StreamGeometry Curve(Point start, Point control1, Point control2, Point end)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(start, false, false);
            context.BezierTo(control1, control2, end, true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    // Broad optical illumination is baked into each cached composition. There is
    // no BlurEffect, particle allocation or new gradient on the frame path.
    private void DrawMaterialLight(DrawingContext dc, int layer, bool compact)
    {
        if (_profile.IsHighContrast) return;
        var (center, radiusX, radiusY, strength) = _profile.Key switch
        {
            "Midnight Observatory" => (new Point(910, 60), 320d, 210d, 0.13),
            "Verdant Glass" => (new Point(960, 350), 270d, 430d, 0.13),
            "Signal Rain" => (new Point(40, 320), 100d, 420d, 0.11),
            "Archive Ivory" => (new Point(860, 130), 300d, 300d, 0.055),
            "Tidal Glass" => (new Point(730, 580), 560d, 190d, 0.16),
            "Arcade Static" => (new Point(840, 510), 400d, 210d, 0.14),
            "Impact Noir" => (new Point(920, 200), 180d, 410d, 0.15),
            _ => (new Point(740, 15), 610d, 180d, 0.14)
        };
        if (layer == 1) center = new Point(1000 - center.X, 600 - center.Y);
        var color = ThemeVisualProfile.ColorOf(layer == 0 ? _profile.Accent : _profile.SecondaryAccent);
        var light = new RadialGradientBrush();
        light.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * strength * (compact ? 0.7 : 1)), color.R, color.G, color.B), 0));
        light.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * strength * 0.28), color.R, color.G, color.B), 0.48));
        light.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
        light.Freeze();
        dc.DrawEllipse(light, null, center, radiusX, radiusY);
    }

    private void DrawCirrus(DrawingContext dc, int layer)
    {
        for (var n = 0; n < 3; n++)
        {
            var y = 75 + n * 18 + layer * 350;
            var curve = Curve(new(-60, y), new(230, y - 170), new(520, y + 100), new(1080, y - 65));
            dc.DrawGeometry(null, MakePen(layer == 0 ? _profile.Accent : _profile.SecondaryAccent, 0.045, 14 - n * 3), curve);
            dc.DrawGeometry(null, MakePen(_profile.Accent, 0.25 - n * 0.045, 0.7), curve);
        }
    }

    private void DrawContours(DrawingContext dc, int layer, bool compact)
    {
        for (var n = 0; n < (compact ? 4 : 7); n++)
        {
            var x = 780 + n * 25;
            var geometry = Curve(new(x, -30), new(x - 260, 190), new(x + 220, 350), new(x - 70, 640));
            if (layer == 1) geometry = Curve(new(-60, 450 + n * 20), new(100, 320), new(180, 680), new(510, 650));
            dc.DrawGeometry(null, MakePen(n == 3 ? _profile.SecondaryAccent : _profile.Accent, n == 3 ? 0.23 : 0.14, n == 3 ? 1 : 0.6), geometry);
        }
    }

    private void DrawTidal(DrawingContext dc, int layer, bool compact)
    {
        for (var n = 0; n < (compact ? 3 : 5); n++)
        {
            var y = 490 + n * 20 - layer * 510;
            var geometry = Curve(new(-50, y), new(260, y - 180), new(780, y + 120), new(1050, y - 95));
            dc.DrawGeometry(null, MakePen(_profile.Accent, 0.035, 22), geometry);
            dc.DrawGeometry(null, MakePen(_profile.Accent, 0.28, 0.8), geometry);
            dc.DrawGeometry(null, MakePen(_profile.SecondaryAccent, 0.12, 2.2),
                Curve(new(-50, y + 3), new(260, y - 173), new(780, y + 126), new(1050, y - 92)));
        }
    }

    private void DrawObservatory(DrawingContext dc, int layer, bool compact)
    {
        var center = new Point(layer == 0 ? 910 : 70, layer == 0 ? 60 : 560);
        if (!compact && Quality == "Full")
        {
            var constellation = new[] { new Point(700, 42), new Point(755, 96), new Point(826, 74), new Point(864, 138) };
            for (var index = 1; index < constellation.Length; index++)
                dc.DrawLine(MakePen(_profile.SecondaryAccent, 0.10, 0.65), constellation[index - 1], constellation[index]);
            foreach (var star in constellation)
                dc.DrawEllipse(Brush(_profile.Text, 0.42), null, star, 1.1, 1.1);
        }
        for (var n = 0; n < (compact ? 2 : 4); n++)
        {
            var radius = 130 + n * 44;
            dc.DrawEllipse(null, MakePen(_profile.Accent, 0.17, 0.6), center, radius, radius * 0.66);
            var angle = n * 1.9 + layer;
            var point = new Point(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius * 0.66);
            dc.DrawEllipse(Brush(_profile.SecondaryAccent, 0.55), null, point, 1.4, 1.4);
        }
        for (var n = 0; n < (compact ? 10 : 28); n++)
        {
            var point = new Point(Hash(n + layer * 61) * 1000, Hash(n + 203) * 600);
            if (point.X > 220 && point.X < 740 && point.Y > 140 && point.Y < 480) continue;
            dc.DrawEllipse(Brush(_profile.Text, 0.18 + Hash(n + 35) * 0.16), null, point, 0.65, 0.65);
        }
    }

    private void DrawSignal(DrawingContext dc, bool compact)
    {
        for (var n = 0; n < (compact ? 5 : 10); n++)
        {
            var x = n < 5 ? 36 + n * 28 : 850 + (n - 5) * 28;
            dc.DrawLine(MakePen(_profile.Accent, 0.10, 0.55), new(x, -30), new(x, 630));
            for (var tick = 0; tick < 12; tick++)
            {
                var y = Hash(tick * 13 + n) * 600;
                dc.DrawLine(MakePen(tick % 3 == 0 ? _profile.SecondaryAccent : _profile.Accent, 0.22, 1), new(x, y), new(x + 4 + Hash(tick + n) * 7, y));
            }
        }
    }

    private void DrawSignalHeads(DrawingContext dc, Size size, double time, bool compact)
    {
        if (!_compositions.TryGetValue("heads", out var head))
        {
            head = new DrawingGroup();
            using (var context = head.Open())
            {
                var accent = ThemeVisualProfile.ColorOf(_profile.Accent);
                var gradient = ThemeVisualProfile.Gradient(Colors.Transparent, Color.FromArgb(30, accent.R, accent.G, accent.B), Color.FromArgb(150, accent.R, accent.G, accent.B));
                context.DrawRectangle(gradient, null, new Rect(0, 0, 1, 32));
            }
            head.Freeze();
            _compositions.Add("heads", head);
        }
        for (var n = 0; n < (compact ? 3 : 6); n++)
        {
            var x = (n < 3 ? 36 + n * 56 : 850 + (n - 3) * 56) / 1000d * size.Width;
            var cycle = (time * 0.025 + Hash(n + 93)) % 1;
            dc.PushOpacity(Math.Sin(cycle * Math.PI) * Intensity * 0.6);
            dc.PushTransform(new TranslateTransform(x, cycle * (size.Height + 64) - 32));
            dc.DrawDrawing(head);
            dc.Pop(); dc.Pop();
        }
    }

    private void DrawArchive(DrawingContext dc, int layer, bool compact)
    {
        var x = layer == 0 ? 860 : 70;
        var y = layer == 0 ? 130 : 490;
        for (var n = 0; n < (compact ? 2 : 4); n++)
        {
            var radius = 56 + n * 28;
            dc.DrawEllipse(null, MakePen(_profile.Accent, 0.17, 0.6), new(x, y), radius, radius);
        }
        dc.DrawLine(MakePen(_profile.SecondaryAccent, 0.23, 0.65), new(x - 200, y), new(x + 200, y));
        dc.DrawLine(MakePen(_profile.Accent, 0.16, 0.65), new(x, y - 200), new(x, y + 200));
        for (var tick = 0; tick < 16; tick++)
        {
            var a = tick * Math.PI / 8;
            dc.DrawLine(MakePen(_profile.Accent, 0.32, 0.7), new(x + Math.Cos(a) * 138, y + Math.Sin(a) * 138), new(x + Math.Cos(a) * 145, y + Math.Sin(a) * 145));
        }
    }

    private void DrawArcade(DrawingContext dc, int layer, bool compact)
    {
        if (layer == 0 && !compact)
        {
            // A shallow cabinet horizon gives Arcade a spatial vocabulary of its own.
            var floorPen = MakePen(_profile.SecondaryAccent, 0.12, 0.7);
            for (var lane = -5; lane <= 5; lane++)
                dc.DrawLine(floorPen, new(760 + lane * 13, 470), new(760 + lane * 120, 630));
            for (var row = 0; row < 6; row++)
            {
                var y = 474 + Math.Pow(row / 5d, 1.8) * 156;
                dc.DrawLine(floorPen, new(460, y), new(1080, y));
            }
        }
        for (var n = 0; n < (compact ? 3 : 5); n++)
        {
            var offset = n * 28;
            var geometry = Curve(new(730 + offset, -30), new(950 + offset, 200), new(530 + offset, 480), new(760 + offset, 660));
            if (layer == 1) geometry = Curve(new(-70, 430 + offset), new(100, 400 + offset), new(170, 590 + offset), new(330, 650 + offset));
            dc.DrawGeometry(null, MakePen(n % 2 == 0 ? _profile.Accent : _profile.SecondaryAccent, 0.035, 11), geometry);
            dc.DrawGeometry(null, MakePen(n % 2 == 0 ? _profile.Accent : _profile.SecondaryAccent, 0.26, 1), geometry);
        }
    }

    private void DrawFacets(DrawingContext dc, int layer)
    {
        for (var n = 0; n < 3; n++)
        {
            var x = 720 + n * 70;
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(layer == 0 ? x : -100, -40), true, true);
                context.LineTo(new Point(layer == 0 ? x + 130 : 220, -40), true, false);
                context.LineTo(new Point(layer == 0 ? x - 120 : 60, 640), true, false);
                context.LineTo(new Point(layer == 0 ? x - 145 : -100, 640), true, false);
            }
            geometry.Freeze();
            dc.DrawGeometry(Brush(_profile.Accent, 0.018 + n * 0.008), MakePen(n == 1 ? _profile.SecondaryAccent : _profile.Accent, 0.16, 0.65), geometry);
        }
    }

    private static double Hash(int seed)
    {
        var value = Math.Sin(seed * 12.9898 + 4.21) * 43758.5453;
        return value - Math.Floor(value);
    }
}
