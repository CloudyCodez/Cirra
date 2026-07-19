using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CloudBerry.App.ViewModels;

namespace CloudBerry.App;

public partial class MediaSearchWindow : Window
{
    public MediaSearchWindow(MainViewModel viewModel, string mediaKind)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;
        _mediaKind = string.IsNullOrWhiteSpace(mediaKind) ? "cover" : mediaKind.Trim().ToLowerInvariant();
        Loaded += OnLoaded;
    }

    private readonly string _mediaKind;

    private MainViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await RunUiTaskAsync(
            () => ViewModel.SearchMediaMetadataForSelectedGameAsync(_mediaKind),
            "CloudBerry couldn't load media search results.");
    }

    private async void SearchClick(object sender, RoutedEventArgs e) =>
        await RunUiTaskAsync(
            () => ViewModel.SearchMediaMetadataForSelectedGameAsync(_mediaKind),
            "CloudBerry couldn't run media search.");

    private async void SelectClick(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(
            () => ViewModel.ApplySelectedMediaSearchResultAsync(),
            "CloudBerry couldn't apply the selected artwork.");
        if (ViewModel.MediaSearchStatusMessage.StartsWith("Applied ", StringComparison.OrdinalIgnoreCase))
        {
            Close();
        }
    }

    private async void ResultDoubleClick(object sender, MouseButtonEventArgs e) =>
        await RunUiTaskAsync(
            () => ViewModel.ApplySelectedMediaSearchResultAsync(),
            "CloudBerry couldn't apply the selected artwork.");

    private async void MediaSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await RunUiTaskAsync(
                () => ViewModel.SearchMediaMetadataForSelectedGameAsync(_mediaKind),
                "CloudBerry couldn't run media search.");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down || e.Key == Key.Up)
        {
            FocusMediaResults(selectLast: e.Key == Key.Up);
            e.Handled = true;
        }
    }

    private async void MediaResultsListPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await RunUiTaskAsync(
                () => ViewModel.ApplySelectedMediaSearchResultAsync(),
                "CloudBerry couldn't apply the selected artwork.");
            if (ViewModel.MediaSearchStatusMessage.StartsWith("Applied ", StringComparison.OrdinalIgnoreCase))
            {
                Close();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OpenImageClick(object sender, RoutedEventArgs e)
    {
        var imageUrl = ViewModel.SelectedMediaSearchResult?.ImageUrl;
        OpenUrl(imageUrl);
    }

    private void OpenSourceClick(object sender, RoutedEventArgs e)
    {
        var sourceUrl = ViewModel.SelectedMediaSearchResult?.SourceUrl;
        var fallbackUrl = ViewModel.SelectedMediaSearchResult?.ImageUrl;
        OpenUrl(string.IsNullOrWhiteSpace(sourceUrl) ? fallbackUrl : sourceUrl);
    }

    private void CancelClick(object sender, RoutedEventArgs e) => Close();

    private void FocusMediaResults(bool selectLast)
    {
        if (MediaResultsList.Items.Count == 0)
        {
            return;
        }

        if (MediaResultsList.SelectedIndex < 0)
        {
            MediaResultsList.SelectedIndex = selectLast
                ? MediaResultsList.Items.Count - 1
                : 0;
        }

        MediaResultsList.UpdateLayout();
        MediaResultsList.ScrollIntoView(MediaResultsList.SelectedItem);

        if (MediaResultsList.ItemContainerGenerator.ContainerFromIndex(MediaResultsList.SelectedIndex) is ListBoxItem item)
        {
            item.Focus();
            return;
        }

        MediaResultsList.Focus();
    }

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore launch issues so the picker stays responsive.
        }
    }

    private async Task RunUiTaskAsync(Func<Task> action, string errorMessage)
    {
        try
        {
            // UI handlers surface recoverable faults instead of crashing the window.
            await action();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"{errorMessage}\n\n{exception.Message}",
                "CloudBerry", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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
}
