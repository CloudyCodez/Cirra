using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CloudBerry.App.ViewModels;
using CloudBerry.App.Services;
using CloudBerry.Core.Models;
using CloudBerry.Core.Services;

namespace CloudBerry.App;

public partial class GuideReaderWindow : Window
{
    private readonly GameGuidebook _guidebook;
    private readonly CancellationTokenSource _lifetime = new();
    private PdfSpreadRenderer? _renderer;
    private bool _opening;
    private bool _closed;
    private bool _rendering;
    private bool _singlePage;
    private uint _spreadStart = 1;

    public GuideReaderWindow(GameEntryViewModel game, GameGuidebook guidebook, bool immersive = false)
    {
        InitializeComponent();
        _guidebook = guidebook;
        GuideTitleText.Text = string.IsNullOrWhiteSpace(guidebook.Title) ? "Game guide" : guidebook.Title;
        GuideContextText.Text = $"{game.Title}  /  {guidebook.SourceLabel}  /  Original PDF";
        SourceButton.Visibility = TryGetSourceUri(out _) ? Visibility.Visible : Visibility.Collapsed;
        Title = GuideTitleText.Text + " - Cirra Guide Reader";
        if (immersive)
        {
            WindowState = WindowState.Maximized;
        }

        Loaded += ReaderLoaded;
        PreviewKeyDown += ReaderKeyDown;
        Closed += (_, _) =>
        {
            _closed = true;
            _lifetime.Cancel();
            _renderer?.Dispose();
        };
    }

    private async void ReaderLoaded(object sender, RoutedEventArgs e)
    {
        if (_opening || _closed) return;
        _opening = true;
        RetryButton.Visibility = Visibility.Collapsed;
        ReaderStatusPanel.Visibility = Visibility.Visible;
        SetReaderEnabled(false);
        try
        {
            var progress = new Progress<string>(message => { if (!_closed) ReaderStatusText.Text = message; });
            string path = await new GuideDocumentService().PrepareAsync(_guidebook.PdfLocation, progress, _lifetime.Token);
            if (_closed) return;
            ReaderStatusText.Text = "Preparing the first pages...";
            _renderer?.Dispose();
            _renderer = await PdfSpreadRenderer.OpenAsync(path);
            if (_closed) { _renderer.Dispose(); return; }
            _spreadStart = 1;
            _singlePage = _renderer.HasWidePages;
            FacingPagesToggle.IsChecked = !_singlePage;
            await ShowSpreadAsync();
        }
        catch (OperationCanceledException) when (_closed) { }
        catch (Exception ex)
        {
            if (_closed) return;
            ReaderStatusText.Text = "Cirra couldn't open this PDF as a page-turn book. " + ex.Message;
            RetryButton.Visibility = Visibility.Visible;
        }
        finally { _opening = false; }
    }

    private async Task ShowSpreadAsync()
    {
        if (_renderer is null || _closed || _rendering) return;
        _rendering = true;
        try
        {
        SetReaderEnabled(false);
        ReaderStatusPanel.Visibility = Visibility.Visible;
        ReaderStatusText.Text = "Rendering original pages...";
        uint height = (uint)Math.Max(900, Math.Min(1800, BookStage.ActualHeight * 1.6));
        uint left = _singlePage || _spreadStart == 1 ? 0 : _spreadStart;
        uint right = _singlePage ? _spreadStart : _spreadStart == 1 ? 1 : _spreadStart + 1;
        BitmapSource? leftImage = left > 0 ? await _renderer.RenderPageAsync(left, height, _lifetime.Token) : null;
        BitmapSource? rightImage = await _renderer.RenderPageAsync(right, height, _lifetime.Token);
        if (_closed) return;
        LeftPageImage.Source = leftImage;
        RightPageImage.Source = rightImage;
        LeftPageFrame.Visibility = leftImage is null ? Visibility.Hidden : Visibility.Visible;
        RightPageFrame.Visibility = rightImage is null ? Visibility.Hidden : Visibility.Visible;
        Grid.SetColumn(RightPageFrame, _singlePage ? 0 : 2);
        Grid.SetColumnSpan(RightPageFrame, _singlePage ? 3 : 1);
        PageCounterText.Text = _singlePage || _spreadStart == 1
            ? $"Page {right} of {_renderer.PageCount}"
            : rightImage is null ? $"Page {left} of {_renderer.PageCount}" : $"Pages {left}-{right} of {_renderer.PageCount}";
        ReaderStatusPanel.Visibility = Visibility.Collapsed;
        PageJumpInput.Text = _spreadStart.ToString();
        SetReaderEnabled(true);
        }
        catch (OperationCanceledException) when (_closed) { }
        catch (Exception ex)
        {
            if (_closed) return;
            ReaderStatusText.Text = "Cirra couldn't render these pages. " + ex.Message;
            RetryButton.Visibility = Visibility.Visible;
            ReaderStatusPanel.Visibility = Visibility.Visible;
            SetReaderEnabled(true);
        }
        finally { _rendering = false; }
    }

    private uint LastSpread => _renderer is null || _renderer.PageCount <= 1
        ? 1 : _singlePage ? _renderer.PageCount : _renderer.PageCount - _renderer.PageCount % 2;

    private async void FacingPagesChanged(object sender, RoutedEventArgs e)
    {
        if (_renderer is null || _opening || _rendering || _closed) return;
        _singlePage = FacingPagesToggle.IsChecked != true;
        if (!_singlePage && _spreadStart > 1) _spreadStart -= _spreadStart % 2;
        await ShowSpreadAsync();
    }

    private void SetReaderEnabled(bool enabled)
    {
        uint count = _renderer?.PageCount ?? 0;
        FacingPagesToggle.IsEnabled = enabled;
        PageJumpInput.IsEnabled = enabled;
        PageJumpButton.IsEnabled = enabled;
        FirstSpreadButton.IsEnabled = enabled && _spreadStart > 1;
        PreviousSpreadButton.IsEnabled = enabled && _spreadStart > 1;
        NextSpreadButton.IsEnabled = enabled && count > 0 && _spreadStart < LastSpread;
        LastSpreadButton.IsEnabled = enabled && _spreadStart < LastSpread;
    }

    private async void FirstSpreadClick(object sender, RoutedEventArgs e)
    {
        if (_rendering || _opening || _closed) return;
        _spreadStart = 1;
        await ShowSpreadAsync();
    }
    private async void LastSpreadClick(object sender, RoutedEventArgs e)
    {
        if (_renderer is null || _rendering || _opening || _closed) return;
        _spreadStart = LastSpread;
        await ShowSpreadAsync();
    }
    private async void PreviousSpreadClick(object sender, RoutedEventArgs e)
    {
        if (_rendering || _opening || _closed || _spreadStart == 1) return;
        _spreadStart = _singlePage ? _spreadStart - 1 : _spreadStart <= 2 ? 1 : _spreadStart - 2;
        await ShowSpreadAsync();
    }
    private async void NextSpreadClick(object sender, RoutedEventArgs e)
    {
        if (_renderer is null || _rendering || _opening || _closed || _spreadStart >= LastSpread) return;
        _spreadStart = _singlePage ? _spreadStart + 1 : _spreadStart == 1 ? 2 : Math.Min(_spreadStart + 2, LastSpread);
        await ShowSpreadAsync();
    }

    private async void PageJumpClick(object sender, RoutedEventArgs e)
    {
        if (_renderer is null || _opening || _rendering || _closed) return;
        if (!uint.TryParse(PageJumpInput.Text, out uint page) || page < 1 || page > _renderer.PageCount)
        {
            PageJumpInput.Text = _spreadStart.ToString();
            PageJumpInput.SelectAll();
            return;
        }
        _spreadStart = !_singlePage && page > 1 ? page - page % 2 : page;
        await ShowSpreadAsync();
    }

    private void PageJumpKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        PageJumpClick(sender, e);
    }

    private void ReaderKeyDown(object sender, KeyEventArgs e)
    {
        if (_renderer is null || _opening || _rendering || e.Handled || Keyboard.FocusedElement is TextBox) return;
        if (e.Key is Key.Right or Key.PageDown) { e.Handled = true; NextSpreadClick(sender, new RoutedEventArgs()); }
        if (e.Key is Key.Left or Key.PageUp) { e.Handled = true; PreviousSpreadClick(sender, new RoutedEventArgs()); }
    }

    private void TitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OpenSourceClick(object sender, RoutedEventArgs e)
    {
        if (TryGetSourceUri(out var source))
        {
            try { Process.Start(new ProcessStartInfo(source.AbsoluteUri) { UseShellExecute = true }); }
            catch (Exception ex)
            {
                ReaderStatusPanel.Visibility = Visibility.Visible;
                ReaderStatusText.Text = "The source page couldn't be opened. " + ex.Message;
                RetryButton.Visibility = Visibility.Visible;
            }
        }
    }

    private bool TryGetSourceUri([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Uri? source) =>
        Uri.TryCreate(_guidebook.SourcePageUrl, UriKind.Absolute, out source) && source.Scheme is "https" or "http";

    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeClick(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized
        ? WindowState.Normal
        : WindowState.Maximized;

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
