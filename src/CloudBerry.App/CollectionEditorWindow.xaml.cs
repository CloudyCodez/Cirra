using System.Windows;
using System.Windows.Input;
using CloudBerry.App.ViewModels;
using CloudBerry.Core.Models;

namespace CloudBerry.App;

/// <summary>
/// Create-or-edit window for a manual, user-curated collection: name the collection, then
/// check off exactly which games belong. Nothing auto-sorts — the player owns the list.
/// </summary>
public partial class CollectionEditorWindow : Window
{
    private readonly MainViewModel _mainViewModel;
    private readonly CollectionEditorViewModel _viewModel;

    public CollectionEditorWindow(MainViewModel mainViewModel, CloudBerryCollection? existingCollection)
    {
        InitializeComponent();
        _mainViewModel = mainViewModel;
        _viewModel = new CollectionEditorViewModel(mainViewModel, existingCollection);
        DataContext = _viewModel;

        HeaderTitleText.Text = _viewModel.WindowTitle;
        ConfirmButton.Content = _viewModel.ConfirmButtonLabel;
        Title = $"CloudBerry — {_viewModel.WindowTitle}";

        UpdateSelectionCount();
        foreach (var entry in _viewModel.Entries)
        {
            entry.PropertyChanged += (_, _) => UpdateSelectionCount();
        }

        NameTextBox.Focus();
        NameTextBox.SelectAll();
    }

    /// <summary>True once the player confirms create/save; false on cancel or delete.</summary>
    public bool WasConfirmed { get; private set; }

    private void UpdateSelectionCount()
    {
        var count = _viewModel.SelectedCount;
        SelectionCountText.Text = count == 1 ? "1 game selected" : $"{count} games selected";
    }

    private void EntryCardClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SelectableGameEntryViewModel entry })
        {
            entry.IsSelected = !entry.IsSelected;
        }
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void DeleteCollectionClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ExistingCollectionId is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Delete \u201c{_viewModel.Name}\u201d? This only removes the collection \u2014 your games are untouched.",
            "Delete Collection",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        _mainViewModel.DeleteCollection(_viewModel.ExistingCollectionId);
        WasConfirmed = false;
        DialogResult = false;
        Close();
    }

    private void ConfirmClick(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsNameValid)
        {
            MessageBox.Show(this, "Give your collection a name first.", "Name Required", MessageBoxButton.OK, MessageBoxImage.Information);
            NameTextBox.Focus();
            return;
        }

        var gameIds = _viewModel.BuildSelectedGameIds();

        if (_viewModel.ExistingCollectionId is null)
        {
            _mainViewModel.CreateCollection(_viewModel.Name, gameIds);
        }
        else
        {
            _mainViewModel.UpdateCollection(_viewModel.ExistingCollectionId, _viewModel.Name, gameIds);
        }

        WasConfirmed = true;
        DialogResult = true;
        Close();
    }
}
