using System.Windows;
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

    private async void ApplyMappingsClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.ApplyRetroArchSuggestionsAsync();
        _viewModel.DetectRetroArchCores();
    }
}
