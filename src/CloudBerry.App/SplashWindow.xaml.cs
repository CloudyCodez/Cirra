using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CloudBerry.App.Services;
using CloudBerry.App.ViewModels;

namespace CloudBerry.App;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        SoundDesignService.PlayStartupBoot();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldNotify)
        {
            oldNotify.PropertyChanged -= OnSplashViewModelPropertyChanged;
        }

        if (e.NewValue is SplashViewModel splash)
        {
            StartupProgressBar.Value = splash.ProgressValue;
        }

        if (e.NewValue is INotifyPropertyChanged newNotify)
        {
            newNotify.PropertyChanged += OnSplashViewModelPropertyChanged;
        }
    }

    private void OnSplashViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SplashViewModel.ProgressValue) || sender is not SplashViewModel splash)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            var animation = new DoubleAnimation
            {
                From = StartupProgressBar.Value,
                To = splash.ProgressValue,
                Duration = TimeSpan.FromMilliseconds(260),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            StartupProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, animation, HandoffBehavior.SnapshotAndReplace);
        });
    }

    public Task PlayCloseTransitionAsync()
    {
        var completion = new TaskCompletionSource<object?>();
        var storyboard = new Storyboard();

        var fade = new DoubleAnimation
        {
            From = Opacity,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(fade, this);
        Storyboard.SetTargetProperty(fade, new PropertyPath(Window.OpacityProperty));
        storyboard.Children.Add(fade);

        if (RootBorder.RenderTransform is not TranslateTransform)
        {
            RootBorder.RenderTransform = new TranslateTransform();
        }

        var slide = new DoubleAnimation
        {
            From = 0,
            To = -10,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(slide, RootBorder);
        Storyboard.SetTargetProperty(slide, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
        storyboard.Children.Add(slide);

        storyboard.Completed += (_, _) => completion.TrySetResult(null);
        storyboard.Begin(this);
        return completion.Task;
    }
}
