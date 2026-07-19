using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CloudBerry.Core.Models;

namespace CloudBerry.App.Services;

/// <summary>
/// Xbox/PSN-style toast that slides in from the bottom-right of the primary screen
/// when an achievement is unlocked in real time.
///
///   ┌──────────────────────────────────────────────────┐
///   │ [icon]  Achievement Unlocked          +25G        │
///   │         "Settled In"                             │
///   │         "Accumulate 30 minutes of playtime."     │
///   └──────────────────────────────────────────────────┘
///
/// The window slides in with cubic-ease-out, stays for <see cref="DisplaySeconds"/>
/// seconds, then slides back out. Clicking dismisses it immediately.
/// </summary>
internal sealed class AchievementPopWindow : Window
{
    private const double PopWidth       = 440;
    private const double PopHeight      = 104;
    private const double EdgeMargin     = 24;
    private int    DisplaySeconds = 5;
    private string PopCorner = "BottomRight";

    private readonly AchievementUnlockEvent _event;
    private readonly DispatcherTimer _dismissTimer;
    private bool _dismissing;

    public AchievementPopWindow(AchievementUnlockEvent ev, Window? owner, int displaySeconds = 5, string popCorner = "BottomRight")
    {
        _event = ev;
        DisplaySeconds = Math.Clamp(displaySeconds, 2, 10);
        PopCorner = popCorner ?? "BottomRight";

        // ── Window properties ─────────────────────────────────────────────
        WindowStyle         = WindowStyle.None;
        AllowsTransparency  = true;
        Background          = Brushes.Transparent;
        Topmost             = true;
        ShowInTaskbar       = false;
        IsHitTestVisible    = true;
        ResizeMode          = ResizeMode.NoResize;
        Width               = PopWidth;
        Height              = PopHeight;
        Opacity             = 0;

        PositionOnScreen();
        Content = BuildVisual();

        _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(DisplaySeconds) };
        _dismissTimer.Tick += (_, _) =>
        {
            _dismissTimer.Stop();
            SlideOut();
        };

        Loaded += (_, _) => SlideIn();
    }

    // ── Layout ────────────────────────────────────────────────────────────

    private UIElement BuildVisual()
    {
        var outerStack = new StackPanel();

        var card = new Border
        {
            CornerRadius    = new CornerRadius(12, 12, 0, 0),
            BorderThickness = new Thickness(1, 1, 1, 0),
            Padding         = new Thickness(14, 11, 16, 11),
            Effect          = new DropShadowEffect
            {
                BlurRadius  = 28,
                ShadowDepth = 8,
                Opacity     = 0.55,
                Color       = Colors.Black
            },
            Cursor = System.Windows.Input.Cursors.Hand
        };
        card.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "BorderSubtleBrush");

        card.MouseLeftButtonUp += (_, _) => SlideOut();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(66) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // ── Icon ──────────────────────────────────────────────────────────
        var iconBorder = new Border
        {
            Width               = 56,
            Height              = 56,
            CornerRadius        = new CornerRadius(6),
            VerticalAlignment   = VerticalAlignment.Center
        };
        iconBorder.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");

        iconBorder.Child = TryLoadIcon(_event.IconUrl) ?? BuildFallbackGlyph();
        Grid.SetColumn(iconBorder, 0);
        grid.Children.Add(iconBorder);

        // ── Text ──────────────────────────────────────────────────────────
        var text = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(12, 0, 8, 0)
        };

        // Game title eyebrow
        if (!string.IsNullOrWhiteSpace(_event.GameTitle))
        {
            var gameEyebrow = new TextBlock
            {
                Text         = _event.GameTitle.ToUpperInvariant(),
                FontSize     = 9,
                FontWeight   = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin       = new Thickness(0, 0, 0, 1)
            };
            gameEyebrow.SetResourceReference(TextBlock.ForegroundProperty, "BerryBrush");
            text.Children.Add(gameEyebrow);
        }

        var unlockedLabel = new TextBlock
        {
            Text            = "Achievement Unlocked",
            FontSize        = 10,
            FontWeight      = FontWeights.SemiBold,
            TextTrimming    = TextTrimming.CharacterEllipsis
        };
        unlockedLabel.SetResourceReference(TextBlock.ForegroundProperty, "GoldBrush");
        text.Children.Add(unlockedLabel);

        var achievementTitle = new TextBlock
        {
            Text         = _event.AchievementTitle,
            FontSize     = 14,
            FontWeight   = FontWeights.Bold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin       = new Thickness(0, 2, 0, 2)
        };
        achievementTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        text.Children.Add(achievementTitle);

        if (!string.IsNullOrWhiteSpace(_event.Description))
        {
            var descriptionText = new TextBlock
            {
                Text         = Truncate(_event.Description, 62),
                FontSize     = 11,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            descriptionText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            text.Children.Add(descriptionText);
        }

        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        // ── Score badge ───────────────────────────────────────────────────
        if (_event.GamerScore > 0)
        {
            var badge = new Border
            {
                CornerRadius      = new CornerRadius(4),
                BorderThickness   = new Thickness(1),
                Padding           = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
            badge.SetResourceReference(Border.BorderBrushProperty, "GoldBrush");

            var badgeText = new TextBlock
            {
                Text       = _event.ScoreLabel,
                FontSize   = 13,
                FontWeight = FontWeights.Bold
            };
            badgeText.SetResourceReference(TextBlock.ForegroundProperty, "GoldBrush");
            badge.Child = badgeText;

            Grid.SetColumn(badge, 2);
            grid.Children.Add(badge);
        }

        card.Child = grid;
        outerStack.Children.Add(card);

        // ── Drain bar — animates from full width to 0 over DisplaySeconds ────
        var drainTrack = new Border
        {
            Height          = 3,
            CornerRadius    = new CornerRadius(0, 0, 12, 12),
            BorderThickness = new Thickness(1, 0, 1, 1),
            ClipToBounds    = true
        };
        drainTrack.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
        drainTrack.SetResourceReference(Border.BorderBrushProperty, "BorderSubtleBrush");

        var drainFill = new Border
        {
            Height              = 3,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width               = PopWidth
        };
        drainFill.SetResourceReference(Border.BackgroundProperty, "GoldBrush");
        drainTrack.Child = drainFill;
        outerStack.Children.Add(drainTrack);

        // Wire the drain animation after layout so ActualWidth is known
        outerStack.Loaded += (_, _) =>
        {
            drainFill.Width = PopWidth;
            drainFill.BeginAnimation(FrameworkElement.WidthProperty,
                new DoubleAnimation(PopWidth, 0,
                    new Duration(TimeSpan.FromSeconds(DisplaySeconds))));
        };

        var wrapper = new Border
        {
            Child = outerStack,
            Effect = new DropShadowEffect
            {
                BlurRadius  = 32,
                ShadowDepth = 0,
                Opacity     = 0.45,
                Color       = Colors.Black
            }
        };
        return wrapper;
    }

    private static UIElement? TryLoadIcon(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource           = new Uri(url, UriKind.RelativeOrAbsolute);
            bmp.CacheOption         = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth    = 56;
            bmp.EndInit();

            var img = new Image { Source = bmp, Stretch = Stretch.UniformToFill };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            return img;
        }
        catch
        {
            return null;
        }
    }

    private static UIElement BuildFallbackGlyph()
    {
        var glyph = new TextBlock
        {
            Text                = "\uE73A", // trophy/ribbon glyph
            FontFamily          = new FontFamily("Segoe MDL2 Assets"),
            FontSize            = 28,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center
        };
        glyph.SetResourceReference(TextBlock.ForegroundProperty, "GoldBrush");
        return glyph;
    }

    private static string Truncate(string s, int max) =>
        s.Length > max ? s[..(max - 1)] + "…" : s;

    // ── Animation ─────────────────────────────────────────────────────────

    private void SlideIn()
    {
        var translate = new TranslateTransform(0, PopHeight + EdgeMargin + 8);
        ((FrameworkElement)Content).RenderTransform = translate;

        translate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(PopHeight + EdgeMargin + 8, 0,
                new Duration(TimeSpan.FromMilliseconds(340)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

        BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200))));

        _dismissTimer.Start();
    }

    private void SlideOut()
    {
        if (_dismissing)
        {
            return;
        }

        _dismissing = true;
        _dismissTimer.Stop();

        var translate = ((FrameworkElement)Content).RenderTransform as TranslateTransform
                        ?? new TranslateTransform();
        ((FrameworkElement)Content).RenderTransform = translate;

        translate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(0, PopHeight + EdgeMargin + 8,
                new Duration(TimeSpan.FromMilliseconds(260)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            });

        var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(220)));
        fadeOut.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fadeOut);
    }

    // ── Position ──────────────────────────────────────────────────────────

    private void PositionOnScreen()
    {
        // WPF SystemParameters gives us the work area in device-independent pixels
        var workArea = SystemParameters.WorkArea;
        switch (PopCorner?.ToLowerInvariant())
        {
            case "topleft":
                Left = workArea.Left + EdgeMargin;
                Top  = workArea.Top  + EdgeMargin;
                break;
            case "topright":
                Left = workArea.Right - PopWidth - EdgeMargin;
                Top  = workArea.Top   + EdgeMargin;
                break;
            case "bottomleft":
                Left = workArea.Left + EdgeMargin;
                Top  = workArea.Bottom - PopHeight - EdgeMargin;
                break;
            default: // BottomRight
                Left = workArea.Right  - PopWidth  - EdgeMargin;
                Top  = workArea.Bottom - PopHeight - EdgeMargin;
                break;
        }
    }
}
