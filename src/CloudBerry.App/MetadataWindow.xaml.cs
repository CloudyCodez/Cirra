using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CloudBerry.App.Services;
using CloudBerry.App.ViewModels;

namespace CloudBerry.App;

public partial class MetadataWindow : Window
{
    private readonly SoundDesignService _sound;

    public MetadataWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;
        _sound = new SoundDesignService(() => ViewModel.AudioEnabled, () => ViewModel.AudioProfile, () => ViewModel.AppearanceThemeKey);

        Loaded += (_, _) => UpdateInspectorScrollBounds();
        SizeChanged += (_, _) => UpdateInspectorScrollBounds();
    }

    private MainViewModel ViewModel { get; }

    private async void SearchClick(object sender, RoutedEventArgs e) =>
        await RunUiTaskAsync(() => ViewModel.SearchMetadataAsync(), "Cirra couldn't run metadata search.");

    private async void UseSelectedGameClick(object sender, RoutedEventArgs e) =>
        await RunUiTaskAsync(() => ViewModel.SearchMetadataForSelectedGameAsync(), "Cirra couldn't search metadata for the selected game.");

    private async void FillLibraryClick(object sender, RoutedEventArgs e) =>
        await RunUiTaskAsync(() => ViewModel.DownloadMissingMetadataAsync(), "Cirra couldn't fill missing metadata right now.");

    private async void ApplyClick(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(() => ViewModel.ApplySelectedMetadataAsync(), "Cirra couldn't apply the selected metadata.");
        if (ViewModel.MetadataStatusMessage.StartsWith("Applied metadata", StringComparison.OrdinalIgnoreCase))
        {
            _sound.PlayMetadataApply();
            Close();
        }
    }

    private async void ApplyInlineClick(object sender, RoutedEventArgs e) =>
        await ApplyInlineAsync();

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private void SearchCoverMediaClick(object sender, RoutedEventArgs e) => OpenMediaSearchWindow("cover");

    private void SearchBackgroundMediaClick(object sender, RoutedEventArgs e) => OpenMediaSearchWindow("background");

    private void SearchBannerMediaClick(object sender, RoutedEventArgs e) => OpenMediaSearchWindow("banner");

    private void SearchIconMediaClick(object sender, RoutedEventArgs e) => OpenMediaSearchWindow("icon");

    private void OpenMediaSearchWindow(string mediaKind)
    {
        var window = new MediaSearchWindow(ViewModel, mediaKind)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private async void MetadataSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await RunUiTaskAsync(() => ViewModel.SearchMetadataAsync(), "Cirra couldn't run metadata search.");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down || e.Key == Key.Up)
        {
            FocusMetadataResults(selectLast: e.Key == Key.Up);
            e.Handled = true;
        }
    }

    private async void MetadataResultsListPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await RunUiTaskAsync(() => ViewModel.ApplySelectedMetadataAsync(), "Cirra couldn't apply the selected metadata.");
            if (ViewModel.MetadataStatusMessage.StartsWith("Applied metadata", StringComparison.OrdinalIgnoreCase))
            {
                _sound.PlayMetadataApply();
                Close();
            }

            e.Handled = true;
        }
    }

    private void LibraryGamesSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.SelectedGame is null)
        {
            return;
        }

        // Sync search text, media search, and status message with the newly selected game.
        ViewModel.OpenGameMetadataWorkspace(ViewModel.SelectedGame);
    }

    private async void MetadataResultsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await RunUiTaskAsync(() => ViewModel.LoadMetadataDetailsForSelectedResultAsync(), "Cirra couldn't load metadata details.");
    }

    private async Task RunUiTaskAsync(Func<Task> action, string errorMessage)
    {
        try
        {
            // UI handlers surface recoverable faults instead of crashing the dispatcher.
            await action();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"{errorMessage}\n\n{exception.Message}",
                "Cirra", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task ApplyInlineAsync()
    {
        await RunUiTaskAsync(() => ViewModel.ApplySelectedMetadataAsync(), "Cirra couldn't apply the selected metadata.");
        if (ViewModel.MetadataStatusMessage.StartsWith("Applied metadata", StringComparison.OrdinalIgnoreCase))
        {
            _sound.PlayMetadataApply();
        }
    }

    private void UpdateInspectorScrollBounds()
    {
        if (MetadataPaneInspector.ActualHeight <= 0)
        {
            return;
        }

        MediaAssetsScrollViewer.MaxHeight = Math.Max(260, MetadataPaneInspector.ActualHeight - 78);
    }

    private void FocusMetadataResults(bool selectLast)
    {
        if (MetadataResultsList.Items.Count == 0)
        {
            return;
        }

        if (MetadataResultsList.SelectedIndex < 0)
        {
            MetadataResultsList.SelectedIndex = selectLast
                ? MetadataResultsList.Items.Count - 1
                : 0;
        }

        MetadataResultsList.UpdateLayout();
        MetadataResultsList.ScrollIntoView(MetadataResultsList.SelectedItem);

        if (MetadataResultsList.ItemContainerGenerator.ContainerFromIndex(MetadataResultsList.SelectedIndex) is ListBoxItem item)
        {
            item.Focus();
            return;
        }

        MetadataResultsList.Focus();
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);
        if (e.Handled)
        {
            return;
        }

        if (TryScrollNearestViewer(e.OriginalSource as DependencyObject, e.Delta))
        {
            e.Handled = true;
        }
    }

    private static bool TryScrollNearestViewer(DependencyObject? element, int wheelDelta)
    {
        while (element is not null)
        {
            if (element is ScrollViewer scrollViewer && scrollViewer.ScrollableHeight > 0)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - wheelDelta / 3.0);
                return true;
            }

            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }

        return false;
    }

    // Lets the user pick a local image file if web searches don't surface a good result.

    private void BrowseCoverFileClick(object sender, RoutedEventArgs e)    => BrowseLocalMediaFile("cover");
    private void BrowseBackgroundFileClick(object sender, RoutedEventArgs e) => BrowseLocalMediaFile("background");
    private void BrowseBannerFileClick(object sender, RoutedEventArgs e)   => BrowseLocalMediaFile("banner");
    private void BrowseIconFileClick(object sender, RoutedEventArgs e)     => BrowseLocalMediaFile("icon");

    private void BrowseLocalMediaFile(string mediaKind)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"Select local {mediaKind} image",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif|All files|*.*"
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FileName))
        {
            return;
        }

        // Apply the local path directly; it will be cached when the user saves.
        var localPath = dialog.FileName;
        if (ViewModel.SelectedMetadataResult is not null)
        {
            switch (mediaKind)
            {
                case "background": ViewModel.SelectedMetadataResult.BackgroundImageUrl = localPath; break;
                case "banner":     ViewModel.SelectedMetadataResult.BannerImageUrl     = localPath; break;
                case "icon":       ViewModel.SelectedMetadataResult.IconImageUrl       = localPath; break;
                default:           ViewModel.SelectedMetadataResult.CoverImageUrl      = localPath; break;
            }
        }

        ViewModel.MetadataStatusMessage = $"Local {mediaKind} image staged.";
    }

}
