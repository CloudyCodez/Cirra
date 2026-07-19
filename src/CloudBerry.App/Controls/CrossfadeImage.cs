using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CloudBerry.App.Motion;
using CloudBerry.App.Services;

namespace CloudBerry.App.Controls;

/// <summary>
/// Premium crossfade image control — v2.0 elegance overhaul.
/// Provides buttery-smooth transitions with subtle scale dynamics for a professional feel.
/// Uses centralized MotionTokens for consistent timing and easing.
/// </summary>
public sealed class CrossfadeImage : Grid
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(ImageSource),
        typeof(CrossfadeImage),
        new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
        nameof(Stretch),
        typeof(Stretch),
        typeof(CrossfadeImage),
        new PropertyMetadata(Stretch.UniformToFill, OnStretchChanged));

    private readonly Image _front;
    private readonly Image _back;
    private readonly ScaleTransform _frontScale;
    private readonly ScaleTransform _backScale;
    private bool _frontIsLive = true;

    public CrossfadeImage()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;

        _frontScale = new ScaleTransform(1, 1);
        _backScale = new ScaleTransform(1, 1);

        _back = CreateImage(_backScale);
        _front = CreateImage(_frontScale);
        _front.Opacity = 1;
        _back.Opacity = 0;

        Children.Add(_back);
        Children.Add(_front);
    }

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public Stretch Stretch
    {
        get => (Stretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    private static Image CreateImage(ScaleTransform scaleTransform)
    {
        var image = new Image
        {
            Stretch = Stretch.UniformToFill,
            Opacity = 0,
            // BitmapCache lets the compositor reuse the GPU texture during crossfade.
            CacheMode = new BitmapCache { SnapsToDevicePixels = true },
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = scaleTransform
        };

        // Fant gives the same perceptual quality as HighQuality but is GPU-composited
        // rather than CPU-filtered, which matters during crossfade animations.
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.Fant);
        return image;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var childConstraint = new Size(
            double.IsInfinity(constraint.Width) ? 0 : constraint.Width,
            double.IsInfinity(constraint.Height) ? 0 : constraint.Height);

        foreach (UIElement child in Children)
        {
            child.Measure(childConstraint);
        }

        return new Size(
            double.IsInfinity(constraint.Width) ? 0 : constraint.Width,
            double.IsInfinity(constraint.Height) ? 0 : constraint.Height);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        foreach (UIElement child in Children)
        {
            child.Arrange(new Rect(arrangeSize));
        }

        return arrangeSize;
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((CrossfadeImage)d).CrossfadeTo((ImageSource?)e.NewValue);
    }

    private static void OnStretchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (CrossfadeImage)d;
        var stretch = (Stretch)e.NewValue;
        control._front.Stretch = stretch;
        control._back.Stretch = stretch;
    }

    private void CrossfadeTo(ImageSource? source)
    {
        var live = _frontIsLive ? _front : _back;
        var liveScale = _frontIsLive ? _frontScale : _backScale;
        if (ReferenceEquals(live.Source, source))
        {
            return;
        }

        var next = _frontIsLive ? _back : _front;
        var nextScale = _frontIsLive ? _backScale : _frontScale;
        
        // Prepare next image: start slightly scaled up for dynamic entry
        next.Source = source;
        next.Opacity = 0;
        nextScale.ScaleX = 1.025;
        nextScale.ScaleY = 1.025;

        // Animate next image in: fade + scale down to normal
        next.BeginAnimation(OpacityProperty, new DoubleAnimation(1, MotionTokens.Crossfade)
        {
            EasingFunction = MotionTokens.PremiumOut
        });
        nextScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.025, 1.0, MotionTokens.Crossfade)
        {
            EasingFunction = MotionTokens.PremiumOut
        });
        nextScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.025, 1.0, MotionTokens.Crossfade)
        {
            EasingFunction = MotionTokens.PremiumOut
        });

        // Animate live image out: fade + slight scale down for cohesive exit
        live.BeginAnimation(OpacityProperty, new DoubleAnimation(0, MotionTokens.Crossfade)
        {
            EasingFunction = MotionTokens.SoftOut
        });
        liveScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, 0.985, MotionTokens.Crossfade)
        {
            EasingFunction = MotionTokens.SoftOut
        });
        liveScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, 0.985, MotionTokens.Crossfade)
        {
            EasingFunction = MotionTokens.SoftOut
        });

        _frontIsLive = !_frontIsLive;
    }
}
