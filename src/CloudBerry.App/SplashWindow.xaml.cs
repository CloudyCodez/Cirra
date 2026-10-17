using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CloudBerry.App.Services;
using CloudBerry.App.ViewModels;

namespace CloudBerry.App;

public partial class SplashWindow : Window
{
    private readonly bool _motion;
    private bool _closed;
    private DispatcherOperation? _pendingUpdate;

    public SplashWindow(bool horizon = false, bool motionEnabled = true)
    {
        InitializeComponent();
        _motion = motionEnabled && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
        Atmosphere.IsEffectActive = _motion;
        if (horizon)
        {
            Title = "Cirra Horizon";
            BrandTitleText.Text = "Horizon";
            BrandSubtitleText.Text = "Cirra / Your full-screen library";
            StartupProgressBar.Visibility = ProgressLabel.Visibility = Visibility.Collapsed;
            HorizonBusyBar.Visibility = Visibility.Visible;
            CancelEntryButton.Visibility = Visibility.Visible;
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key != Key.Escape) return;
                e.Handled = true;
                Close();
            };
            if (!_motion) HorizonBusyBar.IsIndeterminate = false;
        }
        Loaded += (_, _) =>
        {
            if (!horizon) SoundDesignService.PlayStartupBoot();
            if (_motion) BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            else Opacity = 1;
        };
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is INotifyPropertyChanged previous) previous.PropertyChanged -= ProgressChanged;
            if (e.NewValue is INotifyPropertyChanged current) current.PropertyChanged += ProgressChanged;
        };
        Closed += (_, _) =>
        {
            _closed = true;
            _pendingUpdate?.Abort();
            if (DataContext is INotifyPropertyChanged current) current.PropertyChanged -= ProgressChanged;
        };
    }

    private void ProgressChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_closed || e.PropertyName != nameof(SplashViewModel.ProgressValue) ||
            _pendingUpdate?.Status == DispatcherOperationStatus.Pending) return;
        _pendingUpdate = Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            if (_closed || DataContext is not SplashViewModel model) return;
            double current = StartupProgressBar.Value;
            double target = Math.Max(current, model.ProgressValue);
            StartupProgressBar.BeginAnimation(RangeBase.ValueProperty, null);
            StartupProgressBar.Value = target;
            if (_motion)
                StartupProgressBar.BeginAnimation(RangeBase.ValueProperty, new DoubleAnimation(current, target, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop
                });
        }));
    }

    private void CancelEntryClick(object sender, RoutedEventArgs e) => Close();

    public async Task PlayCloseTransitionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_closed || !_motion) return;
        BeginAnimation(OpacityProperty, new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(220)));
        await Task.Delay(230, cancellationToken);
    }
}
