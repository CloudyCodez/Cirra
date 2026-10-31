using System.Windows;
using System.Windows.Input;
using CloudBerry.App.ViewModels;
using CloudBerry.Core.Models;

namespace CloudBerry.App;

public partial class ScanPreviewWindow : Window
{
    private ScanPreviewViewModel ViewModel { get; }

    public ScanPreviewWindow(
        MainViewModel mainVm,
        IReadOnlyList<ImportCandidateViewModel> candidates,
        ScanReport? report = null)
    {
        ViewModel = new ScanPreviewViewModel(mainVm, candidates, report);
        InitializeComponent();
        DataContext = ViewModel;
    }

    public IReadOnlyList<ImportCandidateViewModel> ConfirmedCandidates { get; private set; } =
        Array.Empty<ImportCandidateViewModel>();

    private async void SearchMetadataClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SearchMetadataAsync();
    }

    private async void ApplyMetadataClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplySelectedMetadataAsync();
    }

    private async void AutoPrepareSelectedClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.AutoPrepareSelectedAsync();
    }

    private async void MetadataSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await ViewModel.SearchMetadataAsync();
            e.Handled = true;
        }
    }

    private async void SearchMediaClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SearchMediaAsync();
    }

    private void ApplyMediaClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplySelectedMedia();
    }

    private void CandidateCheckBoxChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.NotifyCandidateSelectionChanged();
    }

    private void SelectSuggestedClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectSuggested();
    }

    private void SelectAllClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectAll();
    }

    private void ClearClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearSelection();
    }

    private void ImportClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanImport) return;
        ConfirmedCandidates = ViewModel.BuildConfirmedCandidates();
        DialogResult = true;
        Close();
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
