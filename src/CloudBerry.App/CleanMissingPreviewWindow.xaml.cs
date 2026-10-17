using System.Windows;
using CloudBerry.App.ViewModels;

namespace CloudBerry.App;

/// <summary>
/// Shows every detected missing-launch-target library entry with a checkbox so the player
/// can confirm exactly what gets removed, instead of a blind Yes/No prompt.
/// </summary>
public partial class CleanMissingPreviewWindow : Window
{
    private readonly CleanMissingPreviewViewModel _viewModel;

    public CleanMissingPreviewWindow(MainViewModel mainViewModel, IReadOnlyList<GameEntryViewModel> missingGames)
    {
        InitializeComponent();
        _viewModel = new CleanMissingPreviewViewModel(mainViewModel, missingGames);
        DataContext = _viewModel;

        SummaryText.Text = missingGames.Count == 1
            ? "Cirra found 1 library entry whose launch file or folder no longer exists. Uncheck anything you want to keep, then confirm removal."
            : $"Cirra found {missingGames.Count} library entries whose launch files or folders no longer exist. Uncheck anything you want to keep, then confirm removal.";

        UpdateSelectionCount();
        foreach (var entry in _viewModel.Entries)
        {
            entry.PropertyChanged += (_, _) => UpdateSelectionCount();
        }
    }

    /// <summary>Games the player confirmed for removal. Empty unless DialogResult is true.</summary>
    public IReadOnlyList<GameEntryViewModel> ConfirmedGames { get; private set; } = [];

    private void UpdateSelectionCount()
    {
        var selected = _viewModel.Entries.Count(entry => entry.IsSelected);
        SelectionCountText.Text = $"{selected} of {_viewModel.Entries.Count} selected for removal";
    }

    private void SelectAllClick(object sender, RoutedEventArgs e)
    {
        foreach (var entry in _viewModel.Entries)
        {
            entry.IsSelected = true;
        }
    }

    private void SelectNoneClick(object sender, RoutedEventArgs e)
    {
        foreach (var entry in _viewModel.Entries)
        {
            entry.IsSelected = false;
        }
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void RemoveSelectedClick(object sender, RoutedEventArgs e)
    {
        ConfirmedGames = _viewModel.BuildSelectedGames();
        DialogResult = true;
        Close();
    }
}
