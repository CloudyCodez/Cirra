using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using CloudBerry.Core.Models;
using CloudBerry.Core.Services;

namespace CloudBerry.App;

public partial class InputLabWindow : Window, INotifyPropertyChanged
{
    private readonly InputLabService _inputLab = new();
    private readonly DispatcherTimer _pollTimer;
    private double _driftThreshold = 0.12;

    public InputLabWindow()
    {
        InitializeComponent();
        DataContext = this;
        foreach (var snapshot in _inputLab.SampleAll(DriftThreshold))
        {
            Controllers.Add(snapshot);
        }

        _pollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _pollTimer.Tick += PollControllers;
        _pollTimer.Start();
        Closed += (_, _) => _pollTimer.Stop();
    }

    public ObservableCollection<ControllerSnapshot> Controllers { get; } = [];

    public double DriftThreshold
    {
        get => _driftThreshold;
        set
        {
            var normalized = Math.Clamp(value, 0.04, 0.30);
            if (Math.Abs(_driftThreshold - normalized) < 0.0001) return;
            _driftThreshold = normalized;
            OnPropertyChanged();
            RefreshControllers();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void PollControllers(object? sender, EventArgs e) => RefreshControllers();

    private void RefreshControllers()
    {
        var snapshots = _inputLab.SampleAll(DriftThreshold);
        for (var index = 0; index < snapshots.Count; index++)
        {
            if (index < Controllers.Count)
            {
                Controllers[index] = snapshots[index];
            }
            else
            {
                Controllers.Add(snapshots[index]);
            }
        }
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
