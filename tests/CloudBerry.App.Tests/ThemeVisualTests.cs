using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Xml.Linq;
using CloudBerry.App.Effects;

namespace CloudBerry.App.Tests;

public sealed class ThemeVisualTests
{
    [Fact]
    public void SharedInputHintsFollowTextWithoutBlockingEditing() => OnSta(() =>
    {
        var input = new TextBox { Tag = "Search your library" };
        input.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(
            new Uri("/Cirra;component/Resources/CloudBerryTheme.xaml", UriKind.Relative)));
        input.Style = (Style)input.FindResource("TextFieldStyle");
        RenderElement(input, 360, 54);
        var hint = (TextBlock)input.Template.FindName("InputHint", input);
        Assert.Equal("Search your library", hint.Text);
        Assert.Equal(Visibility.Visible, hint.Visibility);
        Assert.False(hint.IsHitTestVisible);
        input.Text = "Celeste";
        Assert.Equal(Visibility.Collapsed, hint.Visibility);
        input.Clear();
        Assert.Equal(Visibility.Visible, hint.Visibility);
    });

    public static IEnumerable<object[]> Themes => ThemeVisualProfile.All.Select(profile => new object[] { profile.Key });

    [Theory]
    [InlineData(280)]
    [InlineData(340)]
    [InlineData(540)]
    public void LibraryRowReservesWidthForTitleAndDoesNotOverlapMetadata(int width) => OnSta(() =>
    {
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml"));
        var templateXml = new XElement(document.Descendants(wpf + "DataTemplate").Single(template => template.Descendants().Any(element => (string?)element.Attribute(x + "Name") == "GameTitle")));
        templateXml.SetAttributeValue(XNamespace.Xmlns + "x", x.NamespaceName);
        templateXml.SetAttributeValue(XNamespace.Xmlns + "converters", "clr-namespace:CloudBerry.App.Converters;assembly=Cirra");
        var dictionaryXml = XElement.Parse("""
            <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:converters="clr-namespace:CloudBerry.App.Converters;assembly=Cirra">
                <converters:StringToImageSourceConverter x:Key="StringToImageSourceConverter"/>
                <BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter"/>
            </ResourceDictionary>
            """);
        templateXml.SetAttributeValue(x + "Key", "Row");
        dictionaryXml.Add(templateXml);
        var templates = (ResourceDictionary)XamlReader.Parse(dictionaryXml.ToString());
        var template = (DataTemplate)templates["Row"];
        var presenter = new ContentPresenter
        {
            ContentTemplate = template,
            Content = new { Title = "Yu-Gi-Oh! GX Tag Force 3", Platform = "Nintendo Switch", SourceBadgeLabel = "Manual", LastPlayedLabelShort = "Yesterday", DisplayCoverImagePath = "", IsFavorite = false, NeedsLaunchAttention = false }
        };
        presenter.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/Cirra;component/Resources/CloudBerryTheme.xaml", UriKind.Relative)));
        ThemeVisualProfile.Resolve("Cirra Core").ApplyMaterials((key, value) => presenter.Resources[key] = value);
        var bitmap = RenderElement(presenter, width, 92);
        var title = (TextBlock)template.FindName("GameTitle", presenter);
        var platform = (TextBlock)template.FindName("GamePlatform", presenter);
        Assert.True(title.ActualWidth >= width - 100, $"Title only has {title.ActualWidth}px at {width}px");
        var titleBottom = title.TranslatePoint(new Point(0, title.ActualHeight), presenter).Y;
        Assert.True(platform.TranslatePoint(new Point(), presenter).Y >= titleBottom);
        Assert.True(title.ActualHeight > 0);
        var preview = Environment.GetEnvironmentVariable("CLOUDBERRY_LIBRARY_ROW_GALLERY");
        if (width == 340 && !string.IsNullOrWhiteSpace(preview))
        {
            using var output = File.Create(preview);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(output);
        }
    });

    [Theory]
    [MemberData(nameof(Themes))]
    public void TextAndGlassControlsMeetContrastTargets(string key) => OnSta(() =>
    {
        var resources = new Dictionary<string, object>();
        var profile = ThemeVisualProfile.Resolve(key);
        profile.ApplyMaterials((name, value) => resources[name] = value);
        foreach (var textKey in new[] { "TextPrimaryBrush", "TextSecondaryBrush", "TextMutedBrush", "ThemePrimaryButtonTextBrush" })
        {
            var foreground = ((SolidColorBrush)resources[textKey]).Color;
            // Test the foreground/background pairs actually used by the controls.
            var surfaceKeys = textKey == "ThemePrimaryButtonTextBrush"
                ? new[] { "ThemePrimaryButtonFillBrush", "ThemePrimaryButtonHoverFillBrush", "ThemePrimaryButtonPressedFillBrush" }
                : new[] { "SurfaceBrush", "SurfaceElevatedBrush", "CardSelectedBrush", "ThemePanelFillSoftBrush", "ThemeSecondaryButtonFillBrush", "ThemeSecondaryButtonHoverFillBrush" };
            foreach (var surfaceKey in surfaceKeys)
            {
                IEnumerable<Color> backgrounds = resources[surfaceKey] is SolidColorBrush solid
                    ? new[] { solid.Color }
                    : ((GradientBrush)resources[surfaceKey]).GradientStops.Select(stop => stop.Color);
                foreach (var background in backgrounds)
                    Assert.True(Contrast(foreground, background) >= 4.5, $"{key}: {textKey} on {surfaceKey} = {Contrast(foreground, background):F2}");
            }
        }
    });

    [Theory]
    [MemberData(nameof(Themes))]
    public void SavedProfilePaletteRemainsReadableInEveryShell(string key) => OnSta(() =>
    {
        foreach (var savedProfile in ThemeVisualProfile.All)
        {
            var resources = new Dictionary<string, object>();
            ThemeVisualProfile.Resolve(key).ApplyMaterials((name, value) => resources[name] = value, savedProfile.Key);
            var foreground = ((SolidColorBrush)resources["TextSecondaryBrush"]).Color;
            foreach (var surfaceKey in new[] { "ProfileCardBaseBrush", "ProfileScoreBandBrush", "ProfileAvatarTileBrush", "ProfileBadgeFillBrush" })
            {
                IEnumerable<Color> backgrounds = resources[surfaceKey] is SolidColorBrush solid
                    ? new[] { solid.Color }
                    : ((GradientBrush)resources[surfaceKey]).GradientStops.Select(stop => stop.Color);
                foreach (var background in backgrounds)
                    Assert.True(Contrast(foreground, background) >= 4.5, $"{key} / {savedProfile.Key}: {surfaceKey}");
            }
            Assert.Equal(0d, resources["ProfileSurfaceSweepOpacity"]);
            Assert.Equal(0d, resources["ProfileBadgePulseOpacity"]);
        }
    });

    [Theory]
    [MemberData(nameof(Themes))]
    public void RendererHandlesSizesAndQualityWithoutGrowingCache(string key) => OnSta(() =>
    {
        var layer = new ThemeEffectLayer { ThemeKey = key, EffectRole = "Cloudscape" };
        foreach (var size in new[] { new Size(1280, 720), new Size(1920, 1080), new Size(3440, 1440), new Size(240, 160) })
        {
            var first = Render(layer, size, 0);
            var next = Render(layer, size, 8);
            var visiblePixels = Pixels(first).Where((_, index) => index % 4 == 3).Count(alpha => alpha > 0);
            if (key == "High Contrast")
            {
                Assert.Equal(0, visiblePixels);
                Assert.False(layer.CanAnimate);
            }
            else
            {
                Assert.True(visiblePixels > 50, $"{key} is blank at {size}.");
                if (layer.CanAnimate) Assert.False(Pixels(first).SequenceEqual(Pixels(next)), $"{key} does not move.");
                for (var frame = 0; frame < 100; frame++)
                {
                    var drawing = new DrawingVisual();
                    using var context = drawing.RenderOpen();
                    layer.RenderFrame(context, size, frame / 30d);
                }
                Assert.InRange(layer.CachedCompositionCount, 1, 2);
            }
        }
        layer.Quality = "Efficient";
        Render(layer, new Size(1280, 720), 0);
        Assert.InRange(layer.CachedCompositionCount, 0, key == "Signal Rain" ? 2 : 1);
        layer.Quality = "Off";
        Assert.False(layer.CanAnimate);
        Assert.All(Pixels(Render(layer, new Size(240, 160), 0)), value => Assert.Equal(0, value));
        layer.Quality = "Full";
        layer.IsEffectActive = false;
        Assert.All(Pixels(Render(layer, new Size(240, 160), 0)), value => Assert.Equal(0, value));
    });

    [Fact]
    public void DependencyPropertySwitchesInvalidateOldThemeAndChromeStaysStatic() => OnSta(() =>
    {
        var layer = new ThemeEffectLayer { ThemeKey = "Signal Rain" };
        var size = new Size(640, 360);
        var signal = Pixels(Render(layer, size, 0));
        layer.SetValue(ThemeEffectLayer.ThemeKeyProperty, "Verdant Glass");
        Assert.Equal(0, layer.CachedCompositionCount);
        Assert.False(signal.SequenceEqual(Pixels(Render(layer, size, 0))));
        layer.EffectRole = "Chrome";
        Assert.False(layer.CanAnimate);
        Assert.Equal(Pixels(Render(layer, size, 0)), Pixels(Render(layer, size, 60)));
        layer.Intensity = double.NaN;
        Assert.Equal(0, layer.Intensity);
        Assert.All(Pixels(Render(layer, size, 0)), value => Assert.Equal(0, value));
    });

    [Fact]
    public void MaterialSwitchOrderIsDeterministicAndProducesGallery() => OnSta(() =>
    {
        var galleryPath = Environment.GetEnvironmentVariable("CLOUDBERRY_THEME_GALLERY");
        var sheet = new DrawingVisual();
        using (var context = sheet.RenderOpen())
        {
            for (var index = 0; index < ThemeVisualProfile.All.Count; index++)
            {
                var profile = ThemeVisualProfile.All[index];
                var resources = new Dictionary<string, object>();
                foreach (var prior in ThemeVisualProfile.All.Reverse()) prior.ApplyMaterials((key, value) => resources[key] = value);
                profile.ApplyMaterials((key, value) => resources[key] = value);
                Assert.Equal(ThemeVisualProfile.ColorOf(profile.Text), ((SolidColorBrush)resources["TextPrimaryBrush"]).Color);
                Assert.All(resources.Values.OfType<Freezable>(), value => Assert.True(value.IsFrozen));
                var tile = CreateTile(profile, resources);
                var bitmap = RenderElement(tile, 480, 320);
                context.DrawImage(bitmap, new Rect(index % 3 * 480, index / 3 * 320, 480, 320));
            }
        }
        if (!string.IsNullOrWhiteSpace(galleryPath))
        {
            var bitmap = new RenderTargetBitmap(1440, 960, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(sheet);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(galleryPath))!);
            using var output = File.Create(galleryPath);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(output);
        }
    });

    private static FrameworkElement CreateTile(ThemeVisualProfile profile, Dictionary<string, object> tokens)
    {
        var root = new Grid { Background = ThemeVisualProfile.BrushOf(ThemeVisualProfile.ColorOf(profile.Background)) };
        // The preview uses the shipped WPF button templates, not approximated rectangles.
        var styles = (ResourceDictionary)Application.LoadComponent(new Uri("/Cirra;component/Resources/CloudBerryTheme.xaml", UriKind.Relative));
        root.Resources.MergedDictionaries.Add(styles);
        foreach (var (key, value) in tokens) root.Resources[key] = value;
        root.Children.Add(new ThemeEffectLayer { ThemeKey = profile.Key, EffectRole = "Card", Intensity = 1.2 });
        var content = new StackPanel { Margin = new Thickness(28), VerticalAlignment = VerticalAlignment.Top };
        content.Children.Add(new TextBlock { Text = profile.Key, FontFamily = (FontFamily)tokens["ThemeDisplayFontFamily"], FontSize = 21, FontWeight = FontWeights.SemiBold, Foreground = (Brush)tokens["TextPrimaryBrush"] });
        content.Children.Add(new TextBlock { Text = "Cirra / Horizon", FontSize = 12, Margin = new Thickness(0, 6, 0, 30), Foreground = (Brush)tokens["TextSecondaryBrush"] });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(new Button { Content = "Play", Style = (Style)styles["PrimaryButtonStyle"] });
        buttons.Children.Add(new Button { Content = "Library", Style = (Style)styles["SecondaryButtonStyle"] });
        buttons.Children.Add(new Button { Content = "Details", Style = (Style)styles["GhostButtonStyle"] });
        content.Children.Add(buttons);
        content.Children.Add(new Border
        {
            Margin = new Thickness(0, 22, 0, 0), Padding = new Thickness(20), CornerRadius = profile.PanelCorners,
            Background = (Brush)tokens["ThemePanelFillBrush"], BorderBrush = (Brush)tokens["ThemePanelBorderGradientBrush"], BorderThickness = new Thickness(1),
            Child = new TextBlock { Text = "Continue playing", FontSize = 13, Foreground = (Brush)tokens["TextPrimaryBrush"] }
        });
        root.Children.Add(content);
        return root;
    }

    private static RenderTargetBitmap RenderElement(FrameworkElement element, int width, int height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        return bitmap;
    }

    private static RenderTargetBitmap Render(ThemeEffectLayer layer, Size size, double seconds)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen()) layer.RenderFrame(context, size, seconds);
        var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    private static byte[] Pixels(BitmapSource bitmap)
    {
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        return pixels;
    }

    private static double Contrast(Color first, Color second)
    {
        static double Linear(byte component) => component / 255d <= 0.04045 ? component / 255d / 12.92 : Math.Pow((component / 255d + 0.055) / 1.055, 2.4);
        static double Luminance(Color color) => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(45)), "Theme rendering timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
