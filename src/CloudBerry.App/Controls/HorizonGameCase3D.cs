using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using CloudBerry.App.ViewModels;

namespace CloudBerry.App.Controls;

/// <summary>Physical, lit game packaging rendered as a small WPF 3D model for Horizon.</summary>
public sealed class HorizonGameCase3D : Grid
{
    public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(
        nameof(Profile), typeof(GameCaseProfile), typeof(HorizonGameCase3D),
        new PropertyMetadata(null, OnVisualPropertyChanged));
    public static readonly DependencyProperty CoverImagePathProperty = DependencyProperty.Register(
        nameof(CoverImagePath), typeof(string), typeof(HorizonGameCase3D),
        new PropertyMetadata(string.Empty, OnVisualPropertyChanged));
    public static readonly DependencyProperty IsCaseFocusedProperty = DependencyProperty.Register(
        nameof(IsCaseFocused), typeof(bool), typeof(HorizonGameCase3D),
        new PropertyMetadata(false, OnFocusChanged));

    private readonly Viewport3D _viewport = new();
    private readonly ScaleTransform3D _focusScale = new();
    private readonly TranslateTransform3D _focusOffset = new();

    private static void OnFocusChanged(DependencyObject source, DependencyPropertyChangedEventArgs args)
    {
        var control = (HorizonGameCase3D)source;
        double scale = control.IsCaseFocused ? 1.06 : 1;
        control._focusScale.ScaleX = control._focusScale.ScaleY = control._focusScale.ScaleZ = scale;
        control._focusOffset.OffsetY = control.IsCaseFocused ? 0.04 : 0;
    }

    public GameCaseProfile? Profile { get => (GameCaseProfile?)GetValue(ProfileProperty); set => SetValue(ProfileProperty, value); }
    public string CoverImagePath { get => (string)GetValue(CoverImagePathProperty); set => SetValue(CoverImagePathProperty, value); }
    public bool IsCaseFocused { get => (bool)GetValue(IsCaseFocusedProperty); set => SetValue(IsCaseFocusedProperty, value); }

    public HorizonGameCase3D()
    {
        ClipToBounds = false;
        Children.Add(_viewport);
        Rebuild();
    }

    private static void OnVisualPropertyChanged(DependencyObject source, DependencyPropertyChangedEventArgs args) => ((HorizonGameCase3D)source).Rebuild();

    private void Rebuild()
    {
        var profile = Profile ?? GameCaseProfile.Resolve(null);

        _viewport.Children.Clear();
        _viewport.Camera = new PerspectiveCamera(new Point3D(0, 0.12, 4.8), new Vector3D(0, -0.12, -4.8), new Vector3D(0, 1, 0), 27);
        var scene = new Model3DGroup();
        scene.Children.Add(new AmbientLight(Color.FromRgb(118, 124, 132)));
        scene.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-0.7, -0.8, -1)));
        scene.Children.Add(new DirectionalLight(WithOpacity(profile.Header, 0.45), new Vector3D(1, 0.4, -0.6)));

        double height = Math.Min(2.25, 1.65 * profile.Height / profile.Width);
        double width = height * profile.Width / profile.Height;
        double depth = profile.IsCartridge ? 0.24 : profile.IsDisc ? 0.16 : 0.11;
        var shell = Brush(profile.Shell);
        var darkShell = Brush(Darken(profile.Shell, 0.54));
        var lightShell = Brush(Lighten(profile.Shell, 0.24));
        AddBox(scene, width, height, depth, shell, darkShell, lightShell);

        double inset = profile.IsCartridge ? 0.06 : 0.055;
        double headerHeight = profile.IsCartridge ? 0.08 : 0.15;
        AddQuad(scene, -width / 2 + inset, -height / 2 + inset, width / 2 - inset, height / 2 - headerHeight, depth / 2 + 0.004, CoverMaterial(profile, width - 2 * inset, height - headerHeight - inset));
        AddQuad(scene, -width / 2 + inset, height / 2 - headerHeight, width / 2 - inset, height / 2 - inset, depth / 2 + 0.006, HeaderMaterial(profile));
        AddQuad(scene, -width / 2 + inset * 1.5, -height / 2 + inset * 1.5, width / 2 - inset * 1.5, height / 2 - inset * 1.5, depth / 2 + 0.009,
            new SpecularMaterial(Brush("#95FFFFFF"), 22));

        var transform = new Transform3DGroup();
        transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), -12)));
        transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), -2)));
        transform.Children.Add(_focusScale);
        transform.Children.Add(_focusOffset);
        scene.Transform = transform;
        _viewport.Children.Add(new ModelVisual3D { Content = scene });
    }

    private Material CoverMaterial(GameCaseProfile profile, double width, double height)
    {
        ImageSource? image = LoadCover(CoverImagePath);
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            dc.DrawRectangle(Brush(profile.Shell), null, new Rect(0, 0, width, height));
            if (image is not null && image.Width > 0 && image.Height > 0)
            {
                double scale = Math.Min(width / image.Width, height / image.Height);
                double imageWidth = image.Width * scale, imageHeight = image.Height * scale;
                dc.DrawImage(image, new Rect((width - imageWidth) / 2, (height - imageHeight) / 2, imageWidth, imageHeight));
            }
        }
        Brush brush = new DrawingBrush(drawing);
        if (brush.CanFreeze) brush.Freeze();
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(brush));
        material.Children.Add(new SpecularMaterial(Brush("#70FFFFFF"), 17));
        return material;
    }

    private static Material HeaderMaterial(GameCaseProfile profile)
    {
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            dc.DrawRectangle(Brush(profile.Header), null, new Rect(0, 0, 480, 32));
            var text = new FormattedText(profile.Label, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 22, Brush(profile.HeaderText), 1);
            text.MaxTextWidth = 468;
            text.TextAlignment = TextAlignment.Center;
            dc.DrawText(text, new Point(6, 1));
        }
        drawing.Freeze();
        return new DiffuseMaterial(new DrawingBrush(drawing));
    }

    private static ImageSource? LoadCover(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Uri.TryCreate(path, UriKind.Absolute, out var uri)) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = uri;
            image.DecodePixelHeight = 900;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch { return null; }
    }

    private static void AddBox(Model3DGroup scene, double width, double height, double depth, Brush front, Brush side, Brush top)
    {
        double x = width / 2, y = height / 2, z = depth / 2;
        AddQuad(scene, -x, -y, x, y, z, new DiffuseMaterial(front));
        AddFace(scene, new(x, -y, z), new(x, -y, -z), new(x, y, -z), new(x, y, z), new DiffuseMaterial(side));
        AddFace(scene, new(-x, y, z), new(x, y, z), new(x, y, -z), new(-x, y, -z), new DiffuseMaterial(top));
        AddFace(scene, new(-x, -y, z), new(-x, -y, -z), new(x, -y, -z), new(x, -y, z), new DiffuseMaterial(side));
        AddFace(scene, new(-x, -y, z), new(-x, y, z), new(-x, y, -z), new(-x, -y, -z), new DiffuseMaterial(side));
        AddFace(scene, new(-x, -y, -z), new(-x, y, -z), new(x, y, -z), new(x, -y, -z), new DiffuseMaterial(side));
    }

    private static void AddQuad(Model3DGroup scene, double left, double bottom, double right, double top, double z, Material material)
    {
        AddFace(scene, new(left, bottom, z), new(right, bottom, z), new(right, top, z), new(left, top, z), material);
    }

    private static void AddFace(Model3DGroup scene, Point3D first, Point3D second, Point3D third, Point3D fourth, Material material)
    {
        var mesh = new MeshGeometry3D { Positions = new Point3DCollection { first, second, third, fourth }, TextureCoordinates = new PointCollection { new(0, 1), new(1, 1), new(1, 0), new(0, 0) }, TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 } };
        scene.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
    }

    private static SolidColorBrush Brush(string hex) => new(ColorFrom(hex));
    private static Color ColorFrom(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    private static Color WithOpacity(string hex, double opacity) { var color = ColorFrom(hex); color.A = (byte)(255 * opacity); return color; }
    private static string Darken(string hex, double factor) { var c = ColorFrom(hex); return $"#{(byte)(c.R * factor):X2}{(byte)(c.G * factor):X2}{(byte)(c.B * factor):X2}"; }
    private static string Lighten(string hex, double amount) { var c = ColorFrom(hex); return $"#{(byte)(c.R + (255 - c.R) * amount):X2}{(byte)(c.G + (255 - c.G) * amount):X2}{(byte)(c.B + (255 - c.B) * amount):X2}"; }
}
