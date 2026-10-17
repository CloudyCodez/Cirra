using System.Windows;
using Microsoft.Win32;
using CloudBerry.App.ViewModels;

namespace CloudBerry.App;

public partial class RetroArchCoreCenterWindow : Window
{
    private readonly MainViewModel _viewModel;

    public RetroArchCoreCenterWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.DetectRetroArchCores();
    }

    private void RefreshClick(object sender, RoutedEventArgs e)
    {
        _viewModel.DetectRetroArchCores();
    }

    /// <summary>
    /// Direct escape hatch for when auto-detection can't find RetroArch at all, or found a stale
    /// install (e.g. it moved drives since a platform's core mapping was last applied). Sets
    /// RetroArchPath and immediately re-runs detection against the newly chosen exe, rather than
    /// requiring a separate Refresh Cores click afterward.
    /// </summary>
    private void BrowseRetroArchExeClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select retroarch.exe",
            Filter = "retroarch.exe|retroarch.exe|Executables (*.exe)|*.exe|All files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.RetroArchPath = dialog.FileName;
            _viewModel.DetectRetroArchCores();
        }
    }

    private async void ApplyMappingsClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.ApplyRetroArchSuggestionsAsync();
        _viewModel.DetectRetroArchCores();
    }
}
