using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Net;
using System.Text;
using CloudBerry.App.Services;
using CloudBerry.App.ViewModels;
using CloudBerry.Core.Services;
using CloudBerry.App.Models;

namespace CloudBerry.App;

public partial class MainWindow : Window
{
    private const double DefaultSidebarWidth = 216;
    private const double MinimumSidebarWidth = 188;
    private const double MaximumSidebarWidth = 360;

    private readonly bool _initializeOnLoad;
    private BigPictureWindow? _bigPictureWindow;
    private bool _bigPictureWindowClosedRegistered;
    private MetadataWindow? _metadataWindow;
    private bool _mainHiddenForCloudscape;
    private int _lastNonMetadataPage;
    private readonly DispatcherTimer _homeSuggestionTimer = new();
    private readonly DispatcherTimer _socialHeartbeatTimer = new();
    private readonly AppDiagnostics _diagnostics = new();
    private readonly SoundDesignService _sound;
    private bool _startupUpdatePromptShown;
    private double _lastSidebarWidth = DefaultSidebarWidth;
    private bool _isPointerGlowVisible;

    public MainWindow()
        : this(new MainViewModel(), initializeOnLoad: true)
    {
    }

    public MainWindow(MainViewModel viewModel, bool initializeOnLoad = false)
    {
        ViewModel = viewModel;
        ApplyAppearanceTheme();
        InitializeComponent();
        DataContext = ViewModel;
        SourceInitialized += (_, _) =>
        {
            var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            hwndSource?.AddHook(WndProc);
            // Ctrl+Shift+G — global hotkey to open/focus Cloudscape from anywhere.
            RegisterHotKey(new WindowInteropHelper(this).Handle, HOTKEY_CLOUDSCAPE_ID, MOD_CONTROL | MOD_SHIFT, VK_G);
        };
        StateChanged += (_, _) => ApplySidebarLayout();
        Activated += (_, _) => ApplyWindowPerformanceState(isFocused: true);
        Deactivated += (_, _) => ApplyWindowPerformanceState(isFocused: false);
        _sound = new SoundDesignService(() => ViewModel.AudioEnabled, () => ViewModel.AudioProfile, () => ViewModel.AppearanceThemeKey);
        _initializeOnLoad = initializeOnLoad;

        if (_initializeOnLoad)
        {
            Loaded += OnLoaded;
        }

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += OnStartupUpdatePromptLoaded;
        _homeSuggestionTimer.Tick += OnHomeSuggestionTimerTick;
        _socialHeartbeatTimer.Interval = TimeSpan.FromSeconds(70);
        _socialHeartbeatTimer.Tick += async (_, _) =>
        {
            // async-void event handler boundary: PublishActiveProfilePresenceAsync already
            // catches its own network/service exceptions, but this outer lambda is still an
            // unguarded async-void tick that fires every 70s whether or not the user is at
            // the keyboard. Belt-and-suspenders guard so nothing thrown before/around the
            // await (or a future change that removes the inner try/catch) can take the app
            // down during an idle stretch.
            try
            {
                await ViewModel.PublishActiveProfilePresenceAsync();
            }
            catch (Exception ex)
            {
                _diagnostics.WriteException("social-heartbeat-timer", ex);
            }
        };
        RestartHomeSuggestionTimer();
        ApplySidebarLayout();
    }

    private void ApplyWindowPerformanceState(bool isFocused)
    {
        ViewModel.SetShellFocusState(isFocused);
        App.ApplyInteractivePerformanceMode(isFocused && !ViewModel.IsGamePerformanceMode);
    }

    private async void OnStartupUpdatePromptLoaded(object sender, RoutedEventArgs e)
    {
        if (_startupUpdatePromptShown)
        {
            return;
        }

        _startupUpdatePromptShown = true;
        await Task.Delay(1200);
        await PromptForStartupUpdateAsync();
    }

    private MainViewModel ViewModel { get; }

    // ── Win32 interop: fix maximized bounds for WindowStyle=None + WindowChrome ────────────

    private const int WM_GETMINMAXINFO = 0x0024;
    private const int WM_HOTKEY        = 0x0312;
    private const int HOTKEY_CLOUDSCAPE_ID  = 0x4342; // 'CB'
    private const int MOD_CONTROL      = 0x0002;
    private const int MOD_SHIFT        = 0x0004;
    private const int VK_G             = 0x47;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && (int)wParam == HOTKEY_CLOUDSCAPE_ID)
        {
            // Ctrl+Shift+G pressed anywhere on the system — bring Cloudscape to front.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (_bigPictureWindow is { IsVisible: true })
                {
                    _bigPictureWindow.Activate();
                    _bigPictureWindow.Focus();
                }
                else
                {
                    OpenBigPicture();
                }
            }));
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            var hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMonitor, ref mi))
            {
                // ptMaxPosition is relative to the monitor's top-left in physical pixels.
                // ptMaxSize is the size of the maximized window in physical pixels.
                // Setting these to the work area eliminates the taskbar gap entirely.
                mmi.ptMaxPosition.X = mi.rcWork.Left - mi.rcMonitor.Left;
                mmi.ptMaxPosition.Y = mi.rcWork.Top  - mi.rcMonitor.Top;
                mmi.ptMaxSize.X     = mi.rcWork.Right  - mi.rcWork.Left;
                mmi.ptMaxSize.Y     = mi.rcWork.Bottom - mi.rcWork.Top;

                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_initializeOnLoad) return;
        try
        {
            await ViewModel.InitializeAsync();
            ApplyAppearanceTheme();
            ApplySidebarLayout();
            RestartHomeSuggestionTimer();
            _socialHeartbeatTimer.Start();
            _ = ViewModel.PublishActiveProfilePresenceAsync();
            _sound.PlayAppReady();
            _ = InitializeMainTheatreBrowserAsync();
            if (ViewModel.StartupView == "Cloudscape")
            {
                await Task.Delay(200); // let the window finish rendering before launching
                OpenBigPicture();
            }
            else
            {
                // Pre-warm BigPictureWindow on an idle background pass so the
                // constructor + XAML parse happen before the user ever clicks the button.
                Dispatcher.InvokeAsync(PreWarmBigPictureWindow, DispatcherPriority.Background);
            }
        }
        catch (Exception exception)
        {
            _sound.PlayError();
            MessageBox.Show(this, $"CloudBerry couldn't load the local library.\n\n{exception.Message}",
                "CloudBerry", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Window chrome ───────────────────────────────────────────────────────────────────────

    private void MinimizeClick(object sender, RoutedEventArgs e)   => WindowState = WindowState.Minimized;
    private void MaxRestoreClick(object sender, RoutedEventArgs e)  => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseClick(object sender, RoutedEventArgs e)       => Close();

    private void ToggleSidebarClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsSidebarVisible = !ViewModel.IsSidebarVisible;
        ViewModel.StatusMessage = ViewModel.IsSidebarVisible
            ? "Sidebar restored. Full shell navigation is back."
            : "Focus mode enabled. Sidebar tucked away for a roomier workspace.";
        _sound.PlaySoftClick();
        ApplySidebarLayout();
    }

    // ── Library tabs ───────────────────────────────────────────────────────────────────

    private void LibTabBrowseClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedWorkspaceTab = 0;
        _sound.PlayTabSwitch();
    }

    private void LibTabImportClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedWorkspaceTab = 1;
        _sound.PlayTabSwitch();
    }
    private async void LibTabMetadataClick(object sender, RoutedEventArgs e)
    {
        await OpenMetadataWindowForGameAsync(ViewModel.SelectedGame, search: true);
    }

    // ── Scanning ───────────────────────────────────────────────────────────────────────────

    private async void ScanFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose a folder to scan for games" };
        if (dialog.ShowDialog() != true) return;
        _sound.PlaySoftClick();
        await OpenScanPreviewAsync(dialog.FolderName, romsOnly: false);
    }

    private async void ScanRomFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose a folder to scan for ROMs" };
        if (dialog.ShowDialog() != true) return;
        _sound.PlaySoftClick();
        await OpenScanPreviewAsync(dialog.FolderName, romsOnly: true);
    }

    private async void ScanSteamLibrariesClick(object sender, RoutedEventArgs e)
    {
        var steamFolders = ViewModel.GetSteamAutoScanFolders();
        if (steamFolders.Count == 0)
        {
            _sound.PlayWarning();
            ViewModel.StatusMessage = "No Steam library folders were discovered automatically.";
            return;
        }

        _sound.PlaySoftClick();
        await OpenScanPreviewForFoldersAsync(steamFolders, romsOnly: false, scanLabel: "Steam libraries");
    }

    private async void ImportSteamGamesClick(object sender, RoutedEventArgs e)
    {
        _sound.PlaySoftClick();
        try
        {
            await ViewModel.ImportSteamGamesAsync();
        }
        catch (Exception ex)
        {
            _sound.PlayError();
            MessageBox.Show(this, $"CloudBerry couldn't import Steam games.\n\n{ex.Message}",
                "Steam Import", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ToggleLibraryViewClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsLibraryGridView = !ViewModel.IsLibraryGridView;
        _sound.PlaySoftClick();
    }

    private void GridCardClick(object sender, MouseButtonEventArgs e)
    {
        // Walk up from the clicked element to find the GameEntryViewModel data context
        var element = e.OriginalSource as DependencyObject;
        while (element != null)
        {
            if (element is FrameworkElement fe && fe.DataContext is ViewModels.GameEntryViewModel game)
            {
                ViewModel.SelectedGame = game;
                _sound.PlaySoftClick();
                return;
            }
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }
    }

    private async void SetManualPlaytimeClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedGame == null) return;
        var box = (sender as FrameworkElement)?.FindName("ManualPlaytimeBox") as System.Windows.Controls.TextBox;
        var text = box?.Text ?? ViewModel.SelectedGame.PlaytimeHoursDisplay;
        if (double.TryParse(text, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var hours) && hours >= 0)
        {
            try
            {
                await ViewModel.SetManualPlaytimeAsync((int)(hours * 60));
                _sound.PlayConfirm();
            }
            catch (Exception ex)
            {
                _sound.PlayError();
                MessageBox.Show(this, $"CloudBerry couldn't save playtime.\n\n{ex.Message}",
                    "Playtime", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private async void ScanLauncherLibrariesClick(object sender, RoutedEventArgs e)
    {
        var launcherFolders = ViewModel.GetLauncherAutoScanFolders();
        if (launcherFolders.Count == 0)
        {
            _sound.PlayWarning();
            ViewModel.StatusMessage = "No launcher library folders were discovered automatically.";
            return;
        }

        _sound.PlaySoftClick();
        await OpenScanPreviewForFoldersAsync(launcherFolders, romsOnly: false, scanLabel: "launcher libraries");
    }

    private async void ScanAllLibrariesClick(object sender, RoutedEventArgs e)
    {
        var folders = ViewModel.GetAllAutoScanFolders();
        if (folders.Count == 0)
        {
            _sound.PlayWarning();
            ViewModel.StatusMessage = "No Steam or launcher library folders were discovered automatically.";
            return;
        }

        _sound.PlaySoftClick();
        await OpenScanPreviewForFoldersAsync(folders, romsOnly: false, scanLabel: "all discovered libraries");
    }

    private async Task OpenScanPreviewAsync(string folderPath, bool romsOnly)
    {
        try
        {
            var candidates = await ViewModel.GetScanCandidatesAsync(folderPath, romsOnly);

            if (candidates.Count == 0)
            {
                _sound.PlayWarning();
                ViewModel.StatusMessage = $"No {(romsOnly ? "ROMs" : "games")} found in {folderPath}.";
                return;
            }

            var previewWindow = new ScanPreviewWindow(ViewModel, candidates) { Owner = this };
            var confirmed = previewWindow.ShowDialog();

            if (confirmed == true && previewWindow.ConfirmedCandidates.Count > 0)
            {
                await ViewModel.ImportCandidatesFromPreviewAsync(previewWindow.ConfirmedCandidates);
                _sound.PlaySuccess();
            }
            else if (confirmed != true)
            {
                _sound.PlaySoftClick();
                ViewModel.StatusMessage = "Scan cancelled — nothing imported.";
            }
        }
        catch (Exception ex)
        {
            _sound.PlayError();
            ShowError($"CloudBerry couldn't scan that folder.\n\n{ex.Message}", "Scan failed");
        }
    }

    private async Task OpenScanPreviewForFoldersAsync(IReadOnlyList<string> folderPaths, bool romsOnly, string scanLabel)
    {
        try
        {
            var merged = new List<ImportCandidateViewModel>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var folderPath in folderPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                var candidates = await ViewModel.GetScanCandidatesAsync(folderPath, romsOnly);
                foreach (var candidate in candidates)
                {
                    if (seenPaths.Add(candidate.InstallPath))
                    {
                        merged.Add(candidate);
                    }
                }
            }

            if (merged.Count == 0)
            {
                ViewModel.StatusMessage = $"No {(romsOnly ? "ROMs" : "games")} were found in {scanLabel}.";
                return;
            }

            var previewWindow = new ScanPreviewWindow(ViewModel, merged) { Owner = this };
            var confirmed = previewWindow.ShowDialog();

            if (confirmed == true && previewWindow.ConfirmedCandidates.Count > 0)
            {
                await ViewModel.ImportCandidatesFromPreviewAsync(previewWindow.ConfirmedCandidates);
            }
            else if (confirmed != true)
            {
                ViewModel.StatusMessage = "Scan cancelled — nothing imported.";
            }
        }
        catch (Exception ex)
        {
            ShowError($"CloudBerry couldn't finish scanning {scanLabel}.\n\n{ex.Message}", "Scan failed");
        }
    }

    private async void QuickAddClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose game executables, shortcuts, or ROMs",
            Multiselect = true,
            Filter = "Supported launch targets|*.exe;*.lnk;*.bat;*.cmd;*.url;*.iso;*.cue;*.chd;*.bin;*.cso;*.rvz;*.wbfs;*.nes;*.sfc;*.smc;*.gba;*.gb;*.gbc;*.n64;*.z64;*.v64;*.nds;*.3ds;*.cia;*.gcm;*.gcz;*.wad;*.pbp;*.psx;*.m3u;*.zip;*.7z|All files|*.*"
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            await ViewModel.AddManualGamesAsync(dialog.FileNames);
            _sound.PlaySuccess();
        }
        catch (Exception ex)
        {
            _sound.PlayError();
            ShowError($"CloudBerry couldn't add those files.\n\n{ex.Message}", "Add failed");
        }
    }

    private void BrowseExecutableClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedGame is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Choose a launch target",
            Filter = "Supported launch targets|*.exe;*.lnk;*.bat;*.cmd;*.url;*.iso;*.cue;*.chd;*.bin;*.cso;*.rvz;*.wbfs;*.nes;*.sfc;*.smc;*.gba;*.gb;*.gbc;*.n64;*.z64;*.v64;*.nds;*.3ds;*.cia;*.gcm;*.gcz;*.wad;*.pbp;*.psx;*.m3u;*.zip;*.7z|All files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            ViewModel.SelectedGame.InstallPath = dialog.FileName;
        }
    }

    // ── Import ──────────────────────────────────────────────────────────────────────────────

    private async void ImportSelectedClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ImportSelectedAsync();
        _sound.PlaySuccess();
    }
    private async void SaveSelectedGameClick(object sender, RoutedEventArgs e)     => await ViewModel.SaveSelectedGameAsync();
    private async void LaunchSelectedGameClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.LaunchSelectedGameAsync();
        _sound.PlayLaunchHandoff();
    }

    private async void ToggleFavoriteClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ToggleFavoriteAsync();
        _sound.PlayConfirm();
    }

    private void PickRandomFilteredGameClick(object sender, RoutedEventArgs e)
    {
        ViewModel.PickRandomGameFromCurrentView();
        _sound.PlaySoftClick();
        if (ViewModel.SelectedGame is not null)
        {
            GameLibraryList.ScrollIntoView(ViewModel.SelectedGame);
        }
    }

    private async void RemoveSelectedGameClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedGame is null) return;
        var result = MessageBox.Show(this, $"Remove {ViewModel.SelectedGame.Title} from CloudBerry?",
            "Remove game", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        await ViewModel.RemoveSelectedGameAsync();
        _sound.PlayWarning();
    }

    // ── Undo-remove state ──────────────────────────────────────────────────
    private List<GameEntryViewModel>? _pendingRemoveGames;
    private CancellationTokenSource? _removeCts;
    private DispatcherTimer? _removeCountdownTimer;
    private int _removeCountdownSeconds;

    private async void RemoveSelectedGamesClick(object sender, RoutedEventArgs e)
    {
        await RemoveSelectedGamesWithPromptAsync();
    }

    private async Task RemoveSelectedGamesWithPromptAsync()
    {
        var selected = GetSelectedLibraryGames();
        if (selected.Count == 0) return;

        // Cancel any in-flight removal from a previous click
        _removeCts?.Cancel();
        _removeCountdownTimer?.Stop();
        _pendingRemoveGames = selected;

        var label = selected.Count == 1 ? $"Removing \u201c{selected[0].Title}\u201d" : $"Removing {selected.Count} games";
        ShowRemoveUndoToast(label);

        _removeCts = new CancellationTokenSource();
        var token = _removeCts.Token;

        try
        {
            await Task.Delay(5000, token);
        }
        catch (OperationCanceledException)
        {
            // User hit Undo — don't delete
            HideActionToast();
            _sound.PlaySoftClick();
            ViewModel.StatusMessage = "Removal cancelled.";
            return;
        }

        // Timer elapsed — commit the delete
        HideActionToast();
        await ViewModel.RemoveGamesAsync(selected);
        _sound.PlayWarning();
        ViewModel.StatusMessage = selected.Count == 1
            ? $"Removed \u201c{selected[0].Title}\u201d from CloudBerry."
            : $"Removed {selected.Count} games from CloudBerry.";
    }

    private void ShowRemoveUndoToast(string label)
    {
        ActionToastLabel.Text = "REMOVING";
        ActionToastGlyph.Text = "\uE74D"; // trash
        // Repurpose the StatusMessage line to show the game title + countdown
        _removeCountdownSeconds = 5;
        UpdateRemoveToastMessage(label);

        // Tick the countdown every second
        _removeCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _removeCountdownTimer.Tick += (_, _) =>
        {
            _removeCountdownSeconds--;
            if (_removeCountdownSeconds <= 0) { _removeCountdownTimer.Stop(); return; }
            UpdateRemoveToastMessage(label);
        };
        _removeCountdownTimer.Start();

        // Show the toast
        ActionToast.Visibility = Visibility.Visible;
        ActionToast.IsHitTestVisible = true;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        ActionToast.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
        ActionToastLift.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(-20, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
    }

    private void UpdateRemoveToastMessage(string label)
    {
        // Reuse the second text line (StatusMessage binding) to show countdown
        // The toast's lower TextBlock binds StatusMessage — temporarily override it
        ViewModel.StatusMessage = $"{label} \u2014 {_removeCountdownSeconds}s  \u00b7  Click Undo to cancel";
    }

    private void HideActionToast()
    {
        _removeCountdownTimer?.Stop();
        ActionToast.IsHitTestVisible = false;
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fade = new DoubleAnimation(ActionToast.Opacity, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease };
        fade.Completed += (_, _) => ActionToast.Visibility = Visibility.Collapsed;
        ActionToast.BeginAnimation(OpacityProperty, fade);
    }

    private void UndoRemoveClick(object sender, RoutedEventArgs e)
    {
        _removeCts?.Cancel();
    }

    private async void FavoriteSelectedGamesClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SetFavoriteForGamesAsync(GetSelectedLibraryGames(), true);
        _sound.PlayConfirm();
    }

    private async void UnfavoriteSelectedGamesClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SetFavoriteForGamesAsync(GetSelectedLibraryGames(), false);
        _sound.PlaySoftClick();
    }

    private async void MarkSelectedGamesInstalledClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SetInstalledForGamesAsync(GetSelectedLibraryGames(), true);
        _sound.PlayConfirm();
    }

    private async void MarkSelectedGamesNotInstalledClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SetInstalledForGamesAsync(GetSelectedLibraryGames(), false);
        _sound.PlaySoftClick();
    }

    private List<GameEntryViewModel> GetSelectedLibraryGames()
    {
        var selected = GameLibraryList.SelectedItems
            .OfType<GameEntryViewModel>()
            .DistinctBy(game => game.Id)
            .ToList();

        if (selected.Count == 0 && ViewModel.SelectedGame is not null)
        {
            selected.Add(ViewModel.SelectedGame);
        }

        return selected;
    }

    // ── Star ratings ────────────────────────────────────────────────────────────────────────

    private async void RateStar1Click(object sender, RoutedEventArgs e)  => await ViewModel.SetUserRatingAsync(1);
    private async void RateStar2Click(object sender, RoutedEventArgs e)  => await ViewModel.SetUserRatingAsync(2);
    private async void RateStar3Click(object sender, RoutedEventArgs e)  => await ViewModel.SetUserRatingAsync(3);
    private async void RateStar4Click(object sender, RoutedEventArgs e)  => await ViewModel.SetUserRatingAsync(4);
    private async void RateStar5Click(object sender, RoutedEventArgs e)  => await ViewModel.SetUserRatingAsync(5);
    private async void RateClearClick(object sender, RoutedEventArgs e)  => await ViewModel.SetUserRatingAsync(null);

    // ── Import candidates ──────────────────────────────────────────────────────────────────

    private void SelectSuggestedCandidatesClick(object sender, RoutedEventArgs e) => ViewModel.SelectSuggestedCandidates();
    private void SelectAllCandidatesClick(object sender, RoutedEventArgs e)       => ViewModel.SelectAllCandidates();
    private void ClearImportReviewClick(object sender, RoutedEventArgs e)         => ViewModel.ClearImportReview();
    private void ToggleHomeCustomizeClick(object sender, RoutedEventArgs e)       => ViewModel.ToggleHomeCustomize();

    // ── Settings page category nav ──────────────────────────────────────
    private void SelectSettingsProfileClick(object sender, RoutedEventArgs e)    { ViewModel.SelectedSettingsCategoryIndex = 0; _sound.PlaySoftClick(); }
    private void SelectSettingsAppearanceClick(object sender, RoutedEventArgs e) { ViewModel.SelectedSettingsCategoryIndex = 1; _sound.PlaySoftClick(); }
    private void SelectSettingsLibraryClick(object sender, RoutedEventArgs e)    { ViewModel.SelectedSettingsCategoryIndex = 2; _sound.PlaySoftClick(); }
    private void SelectSettingsEmulatorsClick(object sender, RoutedEventArgs e)  { ViewModel.SelectedSettingsCategoryIndex = 3; _sound.PlaySoftClick(); }
    private void SelectSettingsUpdatesClick(object sender, RoutedEventArgs e)    { ViewModel.SelectedSettingsCategoryIndex = 4; _sound.PlaySoftClick(); }
    private void SetLibraryListViewClick(object sender, RoutedEventArgs e)        => ViewModel.IsLibraryGridView = false;
    private void SetLibraryGridViewClick(object sender, RoutedEventArgs e)        => ViewModel.IsLibraryGridView = true;

    // ── Metadata ─────────────────────────────────────────────────────────────────────────────

    private async void SearchMetadataClick(object sender, RoutedEventArgs e)
    {
        OpenMetadataWindow();
        await ViewModel.SearchMetadataAsync();
        _sound.PlaySoftClick();
    }
    private async void SearchSelectedGameMetadataClick(object sender, RoutedEventArgs e)
    {
        await OpenMetadataWindowForGameAsync(ViewModel.SelectedGame, search: true);
    }

    private async void RefreshSelectedGameMetadataClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshSelectedGameMetadataAsync();
        _sound.PlaySoftClick();
    }

    private async void LibraryMetadataSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await ViewModel.SearchMetadataAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down || e.Key == Key.Up)
        {
            FocusMetadataResultsList(LibraryMetadataResultsList, selectLast: e.Key == Key.Up);
            e.Handled = true;
        }
    }

    private async void MetadataCenterSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await ViewModel.SearchMetadataAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down || e.Key == Key.Up)
        {
            FocusMetadataResultsList(MetadataCenterResultsList, selectLast: e.Key == Key.Up);
            e.Handled = true;
        }
    }

    private async void MetadataResultsListPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await ViewModel.ApplySelectedMetadataAsync();
            e.Handled = true;
        }
    }
    private async void ApplyMetadataClick(object sender, RoutedEventArgs e)
    {
        OpenMetadataWindow();
        await ViewModel.ApplySelectedMetadataAsync();
        _sound.PlaySuccess();
    }

    private async void SaveMetadataSettingsClick(object sender, RoutedEventArgs e)
    {
        OpenMetadataWindow();
        await ViewModel.SaveMetadataSettingsAsync();
    }
    private async void SaveSettingsClick(object sender, RoutedEventArgs e)              => await ViewModel.SaveSettingsAsync();
    private void CreateProfileClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CreateProfileFromCurrentSetup();
        _sound.PlaySoftClick();
    }

    private async void SaveProfileClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveSelectedProfileCard();
        await ViewModel.PublishActiveProfilePresenceAsync();
        _sound.PlaySuccess();
    }

    private async void ApplyProfileClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ActivateSelectedProfile();
        ApplyAppearanceTheme();
        await ViewModel.PublishActiveProfilePresenceAsync();
        _sound.PlayTabSwitch();
    }

    private void DeleteProfileClick(object sender, RoutedEventArgs e)
    {
        ViewModel.DeleteSelectedProfile();
        ApplyAppearanceTheme();
        _sound.PlaySoftClick();
    }

    private void MatchProfileThemeClick(object sender, RoutedEventArgs e)
    {
        ViewModel.MatchProfileThemeToCurrentShell();
        ApplyAppearanceTheme();
        _sound.PlaySoftClick();
    }
    private async void PublishProfileOnlineClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.PublishActiveProfilePresenceAsync();
        _sound.PlaySuccess();
    }

    private void CopyCloudCodeClick(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ViewModel.ActiveProfileCloudCode);
        ViewModel.SocialStatusMessage = $"Copied {ViewModel.ActiveProfileCloudCode}.";
        _sound.PlaySoftClick();
    }

    private void CopyFriendCardClick(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ViewModel.ActiveFriendCode);
        ViewModel.SocialStatusMessage = "Copied the active friend card to the clipboard.";
        _sound.PlaySoftClick();
    }

    private async void SearchFriendCloudCodeClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SearchFriendByCloudCodeAsync();
        _sound.PlaySoftClick();
    }

    private void PasteFriendCardClick(object sender, RoutedEventArgs e)
    {
        if (Clipboard.ContainsText())
        {
            ViewModel.FriendImportCode = Clipboard.GetText();
            ViewModel.SocialStatusMessage = "Pasted a friend card from the clipboard.";
            _sound.PlaySoftClick();
        }
    }

    private void ImportFriendCardClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ImportFriendFromCode();
        _sound.PlaySuccess();
    }

    private async void RefreshFriendClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshSelectedFriendAsync();
        _sound.PlaySoftClick();
    }

    private void RemoveFriendClick(object sender, RoutedEventArgs e)
    {
        ViewModel.RemoveSelectedFriend();
        _sound.PlaySoftClick();
    }
    private async void RunProductReadinessCheckClick(object sender, RoutedEventArgs e)  => await ViewModel.RunProductReadinessCheckAsync();
    private async void PrepareProductTestPassClick(object sender, RoutedEventArgs e)    => await ViewModel.PrepareProductTestPassAsync();
    private async void RepairLibraryClick(object sender, RoutedEventArgs e)             => await ViewModel.RepairLibraryAsync();
    private async void RemoveDuplicatesClick(object sender, RoutedEventArgs e)          => await ViewModel.RemoveDuplicateLibraryEntriesAsync();
    private async void BackupLibraryClick(object sender, RoutedEventArgs e)             => await ViewModel.CreateLibraryBackupAsync();
    private async void ExportLibraryClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export CloudBerry library snapshot",
            FileName = $"cloudberry-library-{DateTime.Now:yyyyMMdd-HHmm}.json",
            Filter = "CloudBerry library snapshot (*.json)|*.json|All files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            await ViewModel.ExportLibrarySnapshotAsync(dialog.FileName);
        }
    }
    private void OpenStorageFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.OpenStorageFolder();
        }
        catch (Exception ex)
        {
            ShowError($"CloudBerry couldn't open the storage folder.\n\n{ex.Message}", "Open folder failed");
        }
    }

    private void OpenDiagnosticsFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.OpenDiagnosticsFolder();
        }
        catch (Exception ex)
        {
            ShowError($"CloudBerry couldn't open the log folder.\n\n{ex.Message}", "Open folder failed");
        }
    }
    private void OpenInstallFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.OpenInstallFolder();
        }
        catch (Exception ex)
        {
            ShowError($"CloudBerry couldn't open the install folder.\n\n{ex.Message}", "Open folder failed");
        }
    }

    private void OpenMediaCacheFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.OpenMediaCacheFolder();
        }
        catch (Exception ex)
        {
            ShowError($"CloudBerry couldn't open the media cache folder.\n\n{ex.Message}", "Open folder failed");
        }
    }

    private void OpenUpdatePackageFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.OpenLocalUpdatePackageFolder();
        }
        catch (Exception ex)
        {
            ShowError($"CloudBerry couldn't open the update package folder.\n\n{ex.Message}", "Open folder failed");
        }
    }

    private void RefreshUpdatePackageStatusClick(object sender, RoutedEventArgs e)
    {
        ViewModel.StatusMessage = ViewModel.RefreshLocalUpdatePackageStatus();
        _sound.PlaySoftClick();
    }

    private async void CheckForAppUpdatesClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.CheckForAppUpdatesAsync(silent: false);
        _sound.PlaySoftClick();
    }

    private async void DownloadLatestUpdateClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.DownloadLatestUpdateAsync(silent: false);
        _sound.PlaySoftClick();
    }

    private async Task PromptForStartupUpdateAsync()
    {
        if (!ViewModel.AutoCheckForUpdates)
        {
            return;
        }

        try
        {
            await ViewModel.CheckForAppUpdatesAsync(silent: true, allowAutoDownload: false);
            if (!ViewModel.HasAvailableUpdate)
            {
                return;
            }

            _sound.PlaySoftClick();
            var result = MessageBox.Show(
                this,
                $"CloudBerry {ViewModel.AvailableUpdateVersion} is available.\n\nUpdate now? CloudBerry will download the update, close, install it, and reopen automatically.",
                "CloudBerry update available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes)
            {
                ViewModel.StatusMessage = $"Skipped CloudBerry {ViewModel.AvailableUpdateVersion} for now.";
                return;
            }

            await ViewModel.CreateLibraryBackupAsync();
			await ViewModel.DownloadLatestUpdateAsync(silent: false, forceInstallOnExit: true);
			if (!ViewModel.IsUpdateInstallQueued)
			{
				_sound.PlayWarning();
				return;
			}

			if (!ViewModel.TryLaunchQueuedUpdateNow())
			{
				_sound.PlayWarning();
				return;
			}

			_sound.PlayLaunchHandoff();
			Application.Current.Shutdown();
		}
		catch (Exception exception)
		{
            _sound.PlayError();
            ShowError($"CloudBerry couldn't prepare the update.\n\n{exception.Message}", "Update failed");
        }
    }

    private async void LaunchLocalUpdatePackageClick(object sender, RoutedEventArgs e)
    {
        var packagePath = ViewModel.LocalUpdatePackagePath;
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            ViewModel.StatusMessage = ViewModel.RefreshLocalUpdatePackageStatus();
            _sound.PlayWarning();
            return;
        }

        var result = MessageBox.Show(this,
            "CloudBerry will create a fresh backup, then launch the staged update installer.\n\nContinue?",
            "Launch local update",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await ViewModel.CreateLibraryBackupAsync();
            Process.Start(new ProcessStartInfo
            {
                FileName = packagePath,
                UseShellExecute = true
            });
            ViewModel.StatusMessage = $"Launched local update package: {Path.GetFileName(packagePath)}";
            _sound.PlayLaunchHandoff();
        }
        catch (Exception ex)
        {
            _sound.PlayError();
            ShowError($"CloudBerry couldn't launch the staged update package.\n\n{ex.Message}", "Update handoff failed");
        }
    }
    private async void RefreshCompanionToolsClick(object sender, RoutedEventArgs e)     => await ViewModel.RefreshCompanionToolsAsync();
    private async void PolishLibraryTitlesClick(object sender, RoutedEventArgs e)       => await ViewModel.PolishLibraryTitlesWithCompanionToolsAsync();
    private async void RefreshCompanionInsightsClick(object sender, RoutedEventArgs e)  => await ViewModel.RefreshCompanionInsightsAsync();
    private async void DownloadMissingMetadataClick(object sender, RoutedEventArgs e)   => await ViewModel.DownloadMissingMetadataAsync();
    private async void CleanMissingLibraryClick(object sender, RoutedEventArgs e)
    {
        var missingGames = ViewModel.GetMissingLibraryEntries();
        if (missingGames.Count == 0)
        {
            _sound.PlaySoftClick();
            ViewModel.StatusMessage = "Library cleanup finished. No missing launch targets were found.";
            return;
        }

        var previewWindow = new CleanMissingPreviewWindow(ViewModel, missingGames) { Owner = this };
        var confirmed = previewWindow.ShowDialog();

        if (confirmed == true && previewWindow.ConfirmedGames.Count > 0)
        {
            await ViewModel.RemoveGamesAsync(previewWindow.ConfirmedGames);
            _sound.PlayWarning();
            ViewModel.StatusMessage = previewWindow.ConfirmedGames.Count == 1
                ? $"Library cleanup removed \u201c{previewWindow.ConfirmedGames[0].Title}\u201d."
                : $"Library cleanup removed {previewWindow.ConfirmedGames.Count} missing entries.";
        }
        else if (confirmed != true)
        {
            _sound.PlaySoftClick();
            ViewModel.StatusMessage = "Clean missing entries cancelled \u2014 nothing removed.";
        }
        else
        {
            ViewModel.StatusMessage = "No entries were selected for removal.";
        }
    }

    private void NewCollectionClick(object sender, RoutedEventArgs e)
    {
        var editorWindow = new CollectionEditorWindow(ViewModel, existingCollection: null) { Owner = this };
        var confirmed = editorWindow.ShowDialog();

        _sound.PlaySoftClick();
        if (confirmed == true)
        {
            var newest = ViewModel.UserCollections.LastOrDefault();
            ViewModel.StatusMessage = $"Created collection \u201c{newest?.Name ?? "New Collection"}\u201d.";
        }
        else
        {
            ViewModel.StatusMessage = "New collection cancelled.";
        }
    }

    private void EditCollectionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CollectionOption option } || string.IsNullOrWhiteSpace(option.CollectionId))
        {
            return;
        }

        var collection = ViewModel.UserCollections.FirstOrDefault(item => item.Id == option.CollectionId);
        if (collection == null)
        {
            return;
        }

        var editorWindow = new CollectionEditorWindow(ViewModel, collection) { Owner = this };
        var confirmed = editorWindow.ShowDialog();

        _sound.PlaySoftClick();
        ViewModel.StatusMessage = confirmed == true
            ? $"Updated collection \u201c{collection.Name}\u201d."
            : "Collection edit cancelled.";
    }

    private void ClearFiltersClick(object sender, RoutedEventArgs e)                    => ViewModel.ClearLibraryFilters();
    private void QuickThemeCloudBerryClick(object sender, RoutedEventArgs e)            => ViewModel.AppearanceThemeKey = "CloudBerry Dark";
    private void QuickThemeMatrixClick(object sender, RoutedEventArgs e)                => ViewModel.AppearanceThemeKey = "Neon Matrix";
    private void QuickThemeGlitchClick(object sender, RoutedEventArgs e)                => ViewModel.AppearanceThemeKey = "Retro Glitch";
    private void QuickThemeMidnightClick(object sender, RoutedEventArgs e)              => ViewModel.AppearanceThemeKey = "Blueberry Midnight";
    private void QuickThemeNierClick(object sender, RoutedEventArgs e)                  => ViewModel.AppearanceThemeKey = "NieR Automata";
    private void QuickThemeWutheringClick(object sender, RoutedEventArgs e)             => ViewModel.AppearanceThemeKey = "Wuthering Waves";
    private void QuickThemeBlackFlashClick(object sender, RoutedEventArgs e)            => ViewModel.AppearanceThemeKey = "Black Flash";
    private async void OpenMetadataWindowClick(object sender, RoutedEventArgs e)
    {
        await OpenMetadataWindowForGameAsync(ViewModel.SelectedGame, search: true);
    }

    // ── Achievements ──────────────────────────────────────────────────────────────────────

    private async void SearchAchievementGameClick(object sender, RoutedEventArgs e)
        => await ViewModel.FocusAchievementGameByTitleAsync(AchievementGameSearchBox.Text);

    private async void AchievementGameSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        await ViewModel.FocusAchievementGameByTitleAsync(AchievementGameSearchBox.Text);
        e.Handled = true;
    }

    private async void ScanSelectedAchievementsLocalClick(object sender, RoutedEventArgs e)
        => await ViewModel.ScanSelectedGameAchievementsFromDiskAsync();

    private async void SyncSelectedAchievementsClick(object sender, RoutedEventArgs e)  => await ViewModel.SyncSelectedGameAchievementsAsync();
    private async void SyncMissingAchievementsClick(object sender, RoutedEventArgs e)   => await ViewModel.SyncGamesMissingAchievementsAsync();

    // ── Shell navigation ─────────────────────────────────────────────────────────────────

    private void OpenSelectedGameFolderClick(object sender, RoutedEventArgs e)       => ViewModel.OpenSelectedGameFolder();
    private void OpenSelectedGameWebsiteClick(object sender, RoutedEventArgs e)      => ViewModel.OpenSelectedGameWebsite();
    private void OpenSelectedGameMetadataPageClick(object sender, RoutedEventArgs e) => ViewModel.OpenSelectedGameMetadataPage();
    private async void OpenSelectedGameMetadataWorkspaceClick(object sender, RoutedEventArgs e)
    {
        var targetGame = ViewModel.SelectedGame ?? ViewModel.ContinuePlayingGame ?? ViewModel.RecommendedGame ?? ViewModel.RandomBacklogGame;
        await OpenMetadataWindowForGameAsync(targetGame, search: targetGame is not null);
    }
    private void OpenSelectedGameAchievementsPageClick(object sender, RoutedEventArgs e)
    {
        var targetGame = ViewModel.SelectedGame ?? ViewModel.ContinuePlayingGame ?? ViewModel.RecommendedGame ?? ViewModel.RandomBacklogGame;
        ViewModel.OpenGameAchievementsPage(targetGame);
        _sound.PlayMenuOpen();
    }

    private async void LaunchContinuePlayingClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.LaunchGameAsync(ViewModel.ContinuePlayingGame);
        _sound.PlayLaunchHandoff();
    }
    private void RevealContinuePlayingClick(object sender, RoutedEventArgs e)       => ViewModel.RevealGameInLibrary(ViewModel.ContinuePlayingGame);
    private async void LaunchRecommendedGameClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.LaunchGameAsync(ViewModel.RecommendedGame);
        _sound.PlayLaunchHandoff();
    }
    private async void OpenRecommendedMetadataClick(object sender, RoutedEventArgs e)
    {
        await OpenMetadataWindowForGameAsync(ViewModel.RecommendedGame, search: true);
    }
    private void RevealRecommendedGameClick(object sender, RoutedEventArgs e)       => ViewModel.RevealGameInLibrary(ViewModel.RecommendedGame);
    private async void LaunchBacklogPickClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.LaunchGameAsync(ViewModel.RandomBacklogGame);
        _sound.PlayLaunchHandoff();
    }
    private async void OpenBacklogPickMetadataClick(object sender, RoutedEventArgs e)
    {
        await OpenMetadataWindowForGameAsync(ViewModel.RandomBacklogGame, search: true);
    }
    private void ShuffleBacklogPickClick(object sender, RoutedEventArgs e)
    {
        ViewModel.PickAnotherRandomBacklogGame();
        _sound.PlaySoftClick();
    }
    private void RevealBacklogPickClick(object sender, RoutedEventArgs e)           => ViewModel.RevealGameInLibrary(ViewModel.RandomBacklogGame);
    private void OpenBigPictureClick(object sender, RoutedEventArgs e)              => OpenBigPicture();

    private void OpenYearWrappedClick(object sender, RoutedEventArgs e)
    {
        var win = new WrappedWindow(ViewModel) { Owner = this };
        win.ShowDialog();
        _sound.PlaySoftClick();
    }

    // ── Play Patterns (DIFFERENTIATORS.md #1 — "Session replay timeline") ───────────
    // All-time view of the same WrappedWindow: time-of-day / day-of-week / session-length
    // charts, available year-round instead of only inside the once-a-year Wrapped recap.
    private void OpenPlayPatternsClick(object sender, RoutedEventArgs e)
    {
        var win = new WrappedWindow(ViewModel, allTime: true) { Owner = this };
        win.ShowDialog();
        _sound.PlaySoftClick();
    }

    // ── Emulator browse buttons ───────────────────────────────────────────────────────────

    private void BrowseEmulatorNesClick(object sender, RoutedEventArgs e)        => ViewModel.EmulatorNes       = BrowseEmulator() ?? ViewModel.EmulatorNes;
    private void BrowseEmulatorSnesClick(object sender, RoutedEventArgs e)       => ViewModel.EmulatorSnes      = BrowseEmulator() ?? ViewModel.EmulatorSnes;
    private void BrowseEmulatorN64Click(object sender, RoutedEventArgs e)        => ViewModel.EmulatorN64       = BrowseEmulator() ?? ViewModel.EmulatorN64;
    private void BrowseEmulatorGbClick(object sender, RoutedEventArgs e)         => ViewModel.EmulatorGb        = BrowseEmulator() ?? ViewModel.EmulatorGb;
    private void BrowseEmulatorGbaClick(object sender, RoutedEventArgs e)        => ViewModel.EmulatorGba       = BrowseEmulator() ?? ViewModel.EmulatorGba;
    private void BrowseEmulatorNdsClick(object sender, RoutedEventArgs e)        => ViewModel.EmulatorNds       = BrowseEmulator() ?? ViewModel.EmulatorNds;
    private void BrowseEmulator3dsClick(object sender, RoutedEventArgs e)        => ViewModel.Emulator3ds       = BrowseEmulator() ?? ViewModel.Emulator3ds;
    private void BrowseEmulatorGameCubeClick(object sender, RoutedEventArgs e)   => ViewModel.EmulatorGameCube  = BrowseEmulator() ?? ViewModel.EmulatorGameCube;
    private void BrowseEmulatorWiiClick(object sender, RoutedEventArgs e)        => ViewModel.EmulatorWii       = BrowseEmulator() ?? ViewModel.EmulatorWii;
    private void BrowseEmulatorPs1Click(object sender, RoutedEventArgs e)        => ViewModel.EmulatorPs1       = BrowseEmulator() ?? ViewModel.EmulatorPs1;
    private void BrowseEmulatorPs2Click(object sender, RoutedEventArgs e)        => ViewModel.EmulatorPs2       = BrowseEmulator() ?? ViewModel.EmulatorPs2;
    private void BrowseEmulatorPs3Click(object sender, RoutedEventArgs e)        => ViewModel.EmulatorPs3       = BrowseEmulator() ?? ViewModel.EmulatorPs3;
    private void BrowseEmulatorPspClick(object sender, RoutedEventArgs e)        => ViewModel.EmulatorPsp       = BrowseEmulator() ?? ViewModel.EmulatorPsp;
    private void BrowseEmulatorMegaDriveClick(object sender, RoutedEventArgs e)  => ViewModel.EmulatorMegaDrive = BrowseEmulator() ?? ViewModel.EmulatorMegaDrive;
    private void BrowseEmulatorDreamcastClick(object sender, RoutedEventArgs e)  => ViewModel.EmulatorDreamcast = BrowseEmulator() ?? ViewModel.EmulatorDreamcast;
    private void BrowseEmulatorSaturnClick(object sender, RoutedEventArgs e)     => ViewModel.EmulatorSaturn    = BrowseEmulator() ?? ViewModel.EmulatorSaturn;
    private void BrowseEmulatorWiiUClick(object sender, RoutedEventArgs e)       => ViewModel.EmulatorWiiU      = BrowseEmulator() ?? ViewModel.EmulatorWiiU;
    private void BrowseEmulatorSwitchClick(object sender, RoutedEventArgs e)     => ViewModel.EmulatorSwitch    = BrowseEmulator() ?? ViewModel.EmulatorSwitch;
    private void BrowseEmulatorXboxClick(object sender, RoutedEventArgs e)       => ViewModel.EmulatorXbox      = BrowseEmulator() ?? ViewModel.EmulatorXbox;
    private void BrowseEmulatorXbox360Click(object sender, RoutedEventArgs e)    => ViewModel.EmulatorXbox360   = BrowseEmulator() ?? ViewModel.EmulatorXbox360;
    private void BrowseEmulatorDefaultClick(object sender, RoutedEventArgs e)    => ViewModel.EmulatorDefault   = BrowseEmulator() ?? ViewModel.EmulatorDefault;
    private void BrowseRetroArchClick(object sender, RoutedEventArgs e)          => ViewModel.RetroArchPath     = BrowseEmulator("Select retroarch.exe") ?? ViewModel.RetroArchPath;
    private void ScanInstalledEmulatorsClick(object sender, RoutedEventArgs e)   => ViewModel.ScanInstalledEmulators();
    private async void ApplyDetectedEmulatorMappingsClick(object sender, RoutedEventArgs e) => await ViewModel.ApplyDetectedEmulatorMappingsAsync();
    private void DetectRetroArchCoresClick(object sender, RoutedEventArgs e)     => ViewModel.DetectRetroArchCores();
    private async void ApplyRetroArchSuggestionsClick(object sender, RoutedEventArgs e) => await ViewModel.ApplyRetroArchSuggestionsAsync();
    private void OpenRetroArchCoreCenterClick(object sender, RoutedEventArgs e)
    {
        var window = new RetroArchCoreCenterWindow(ViewModel)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    /// <summary>Opens a file picker for an emulator .exe and returns the chosen path, or null on cancel.</summary>
    private string? BrowseEmulator(string title = "Select emulator executable")
    {
        var dialog = new OpenFileDialog
        {
            Title  = title,
            Filter = "Executables (*.exe)|*.exe|All files|*.*"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    // ── Emulation Station platform cards ──────────────────────────────────────────────────

    private void BrowseEmulatorCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EmulatorPlatformCardViewModel card })
        {
            var chosen = BrowseEmulator($"Select emulator for {card.Platform}");
            if (chosen != null)
            {
                card.Path = chosen;
                _sound.PlaySoftClick();
            }
        }
    }

    private void ClearEmulatorCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EmulatorPlatformCardViewModel card })
        {
            card.Clear();
            _sound.PlaySoftClick();
        }
    }

    private void ApplyDetectedEmulatorCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EmulatorPlatformCardViewModel card })
        {
            card.ApplyDetectedPath();
            _sound.PlaySoftClick();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    private void OnTabControlSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource == sender && sender is TabControl tabControl)
        {
            var presenter = FindVisualChild<ContentPresenter>(tabControl, string.Empty);
            if (presenter != null)
            {
                var themeKey = ViewModel?.AppearanceThemeKey;
                var isNierOrContrast = string.Equals(themeKey, "NieR Automata", StringComparison.Ordinal) ||
                                       string.Equals(themeKey, "High Contrast", StringComparison.Ordinal);

                if (isNierOrContrast)
                {
                    presenter.Opacity = 1.0;
                    if (presenter.RenderTransform is TranslateTransform oldTrans)
                    {
                        oldTrans.X = 0;
                        oldTrans.Y = 0;
                    }
                    return;
                }

                var animOpacity = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = ResolveShellMotionPack().FadeDuration,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                var transform = EnsureWritableTranslateTransform(presenter);

                var animSlide = new DoubleAnimation
                {
                    From = ResolveShellMotionPack().SlideDistance,
                    To = 0.0,
                    Duration = ResolveShellMotionPack().SlideDuration,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                presenter.BeginAnimation(UIElement.OpacityProperty, animOpacity);
                transform.BeginAnimation(TranslateTransform.XProperty, animSlide);
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent, string name) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild && (string.IsNullOrEmpty(name) || (child is FrameworkElement fe && fe.Name == name)))
            {
                return typedChild;
            }
            var childOfChild = FindVisualChild<T>(child, name);
            if (childOfChild != null)
            {
                return childOfChild;
            }
        }
        return null;
    }

    private (Duration FadeDuration, Duration SlideDuration, double SlideDistance) ResolveShellMotionPack()
    {
        return ViewModel.AppearanceThemeKey switch
        {
            "NieR Automata" => (new Duration(TimeSpan.FromMilliseconds(180)), new Duration(TimeSpan.FromMilliseconds(210)), 8),
            "Wuthering Waves" => (new Duration(TimeSpan.FromMilliseconds(260)), new Duration(TimeSpan.FromMilliseconds(320)), 16),
            "Neon Matrix" => (new Duration(TimeSpan.FromMilliseconds(200)), new Duration(TimeSpan.FromMilliseconds(240)), 12),
            "Retro Glitch" => (new Duration(TimeSpan.FromMilliseconds(190)), new Duration(TimeSpan.FromMilliseconds(220)), 10),
            _ => (new Duration(TimeSpan.FromMilliseconds(240)), new Duration(TimeSpan.FromMilliseconds(280)), 14)
        };
    }

    private async void GameListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.SelectedGame is not null)
        {
            await ViewModel.LaunchSelectedGameAsync();
            _sound.PlayLaunchHandoff();
        }
    }

    private void GameListPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var current = e.OriginalSource as DependencyObject;
        while (current is not null && current is not ListBoxItem)
        {
            current = VisualTreeHelper.GetParent(current);
        }

        if (current is ListBoxItem item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private async void WindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            OpenBigPicture();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.F:
                    ViewModel.SelectedPrimaryPage = 1;
                    ViewModel.SelectedWorkspaceTab = 0;
                    LibrarySearchBox.Focus();
                    LibrarySearchBox.SelectAll();
                    e.Handled = true;
                    return;
                case Key.B:
                    ViewModel.IsSidebarVisible = !ViewModel.IsSidebarVisible;
                    ApplySidebarLayout();
                    e.Handled = true;
                    return;
                case Key.D1:
                case Key.NumPad1:
                    ViewModel.SelectedPrimaryPage = 0;
                    e.Handled = true;
                    return;
                case Key.D2:
                case Key.NumPad2:
                    ViewModel.SelectedPrimaryPage = 1;
                    e.Handled = true;
                    return;
                case Key.D3:
                case Key.NumPad3:
                    await OpenMetadataWindowForGameAsync(ViewModel.SelectedGame, search: true);
                    e.Handled = true;
                    return;
                case Key.D4:
                case Key.NumPad4:
                    ViewModel.SelectedPrimaryPage = 3;
                    e.Handled = true;
                    return;
                case Key.D5:
                case Key.NumPad5:
                    ViewModel.SelectedPrimaryPage = 4; // Theatre
                    e.Handled = true;
                    return;
                case Key.D6:
                case Key.NumPad6:
                    ViewModel.SelectedPrimaryPage = 5; // Friends
                    e.Handled = true;
                    return;
                case Key.D7:
                case Key.NumPad7:
                    ViewModel.SelectedPrimaryPage = 6; // Settings
                    e.Handled = true;
                    return;
            }
        }

        if (e.Key == Key.Enter &&
            ViewModel.SelectedPrimaryPage == 1 &&
            ViewModel.SelectedWorkspaceTab == 0 &&
            e.OriginalSource is not TextBox)
        {
            await ViewModel.LaunchSelectedGameAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete &&
            ViewModel.SelectedPrimaryPage == 1 &&
            ViewModel.SelectedWorkspaceTab == 0 &&
            e.OriginalSource is not TextBox)
        {
            await RemoveSelectedGamesWithPromptAsync();
            e.Handled = true;
        }
    }

    private static void FocusMetadataResultsList(ListBox listBox, bool selectLast)
    {
        if (listBox.Items.Count == 0)
        {
            return;
        }

        if (listBox.SelectedIndex < 0)
        {
            listBox.SelectedIndex = selectLast ? listBox.Items.Count - 1 : 0;
        }

        listBox.UpdateLayout();
        listBox.ScrollIntoView(listBox.SelectedItem);

        if (listBox.ItemContainerGenerator.ContainerFromIndex(listBox.SelectedIndex) is ListBoxItem item)
        {
            item.Focus();
            return;
        }

        listBox.Focus();
    }

    protected override void OnClosed(EventArgs e)
    {
        UnregisterHotKey(new WindowInteropHelper(this).Handle, HOTKEY_CLOUDSCAPE_ID);

        if (_bigPictureWindow is not null)
        {
            _bigPictureWindow.Closed -= OnBigPictureWindowClosed;
            _bigPictureWindow.Close();
            _bigPictureWindow = null;
        }

        if (_metadataWindow is not null)
        {
            _metadataWindow.Closed -= OnMetadataWindowClosed;
            _metadataWindow.Close();
            _metadataWindow = null;
        }

        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _homeSuggestionTimer.Stop();
        _homeSuggestionTimer.Tick -= OnHomeSuggestionTimerTick;

        base.OnClosed(e);
    }

    private void OpenMetadataWindow()
    {
        if (_metadataWindow is not null)
        {
            _metadataWindow.Activate();
            _sound.PlayMenuOpen();
            return;
        }

        _metadataWindow = new MetadataWindow(ViewModel)
        {
            Owner = this
        };
        _metadataWindow.Closed += OnMetadataWindowClosed;
        _metadataWindow.Show();
        _metadataWindow.Activate();
        _sound.PlayMenuOpen();
    }

    public Task OpenMetadataWindowForGameFromCloudscapeAsync(GameEntryViewModel? game)
        => OpenMetadataWindowForGameAsync(game, search: true);

    private async Task OpenMetadataWindowForGameAsync(GameEntryViewModel? game, bool search)
    {
        if (game is not null)
        {
            ViewModel.OpenGameMetadataWorkspace(game);
        }

        OpenMetadataWindow();

        if (search && game is not null)
        {
            await ViewModel.SearchMetadataForSelectedGameAsync();
        }
    }

    private void OpenFriendsPageClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedPrimaryPage = 5;
        _sound.PlayTabSwitch();
    }

    private void OnMetadataWindowClosed(object? sender, EventArgs e)
    {
        if (_metadataWindow is null)
        {
            return;
        }

        _metadataWindow.Closed -= OnMetadataWindowClosed;
        _metadataWindow = null;
        Activate();
    }

    private void PreWarmBigPictureWindow()
    {
        if (_bigPictureWindow is not null) return;
        try
        {
            _bigPictureWindow = new BigPictureWindow(ViewModel) { Owner = this };
            _bigPictureWindow.Closed += OnBigPictureWindowClosed;
            _bigPictureWindowClosedRegistered = true;
        }
        catch { /* silent — OpenBigPicture will create it on demand if this fails */ }
    }

    private async void OpenBigPicture()
    {
        if (_bigPictureWindow is { IsVisible: true })
        {
            _bigPictureWindow.Activate();
            return;
        }

        try
        {
            ShowCloudscapeLoadingOverlay();
        }
        catch { /* overlay is cosmetic; continue even if it fails */ }

        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

        if (ViewModel.SelectedGame is null)
        {
            ViewModel.SelectedGame = ViewModel.BigPictureHeroGame;
        }

        if (_bigPictureWindow is null)
        {
            // Pre-warm didn't run yet (e.g. user clicked very fast) — create now.
            // Short delay so the loading overlay has time to paint before the freeze.
            await Task.Delay(60);
            try
            {
                _bigPictureWindow = new BigPictureWindow(ViewModel) { Owner = this };
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException is not null
                    ? $"\n\nInner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace?.Split('\n').FirstOrDefault()}"
                    : string.Empty;
                MessageBox.Show(
                    $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace?.Split('\n').Take(6).Aggregate((a, b) => a + "\n" + b)}{inner}",
                    "Cloudscape Init Crash — Report This",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _mainHiddenForCloudscape = false;
                HideCloudscapeLoadingOverlay();
                return;
            }
        }

        if (!_bigPictureWindowClosedRegistered)
        {
            _bigPictureWindow.Closed += OnBigPictureWindowClosed;
            _bigPictureWindowClosedRegistered = true;
        }

        _mainHiddenForCloudscape = true;
        Hide();
        _bigPictureWindow.Show();
        _bigPictureWindow.Activate();
        HideCloudscapeLoadingOverlay();
        _sound.PlayMenuOpen();
    }

    private void ShowCloudscapeLoadingOverlay()
    {
        CloudscapeLoadingOverlay.Visibility = Visibility.Visible;
        CloudscapeLoadingOverlay.IsHitTestVisible = true;
        CloudscapeLoadingOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void HideCloudscapeLoadingOverlay()
    {
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) =>
        {
            CloudscapeLoadingOverlay.IsHitTestVisible = false;
            CloudscapeLoadingOverlay.Visibility = Visibility.Collapsed;
        };
        CloudscapeLoadingOverlay.BeginAnimation(OpacityProperty, fade);
    }

    private void OnBigPictureWindowClosed(object? sender, EventArgs e)
    {
        if (_bigPictureWindow is null) return;

        _bigPictureWindow.Closed -= OnBigPictureWindowClosed;
        _bigPictureWindow = null;
        _bigPictureWindowClosedRegistered = false;

        // Guarantee the cursor is restored when returning from Cloudscape,
        // regardless of where the BigPictureWindow left things.
        Mouse.OverrideCursor = null;

        try
        {
            if (_mainHiddenForCloudscape)
            {
                _mainHiddenForCloudscape = false;
                Show();
                WindowState = WindowState.Maximized;
            }

            Activate();
        }
        catch (InvalidOperationException)
        {
            // Window is closing or in an invalid state (e.g. app is shutting down
            // while Big Picture is open) — suppress and let shutdown proceed.
        }

        // Re-warm immediately so the next open is instant too.
        Dispatcher.InvokeAsync(PreWarmBigPictureWindow, DispatcherPriority.Background);
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.AppearanceThemeKey))
        {
            ApplyAppearanceTheme();
        }

        if (e.PropertyName is nameof(MainViewModel.ActiveProfile) or nameof(MainViewModel.ActiveProfileThemeLabel))
        {
            ApplyAppearanceTheme();
            AnimateSelectionSurface(SidebarProfileCard, fromY: 10, targetOpacity: 1);
            AnimateSelectionSurface(HomeProfileHeroCard, fromY: 10, targetOpacity: 1);
        }

        if (e.PropertyName is nameof(MainViewModel.IsSidebarVisible))
        {
            ApplySidebarLayout();
        }

        if (e.PropertyName is nameof(MainViewModel.HomeRotationSeconds))
        {
            RestartHomeSuggestionTimer();
        }

        if (e.PropertyName is nameof(MainViewModel.IsGamePerformanceMode))
        {
            App.ApplyInteractivePerformanceMode(IsActive && !ViewModel.IsGamePerformanceMode);
        }

        if (e.PropertyName is nameof(MainViewModel.SelectedGame))
        {
            AnimateSelectionSurface(LibraryHeroShell, fromY: 12, targetOpacity: 1);
            AnimateSelectionSurface(LibraryHeroCoverShell, fromY: 10, targetOpacity: 1);
            AnimateSelectionSurface(HomeSpotlightCard, fromY: 8, targetOpacity: 1);
        }

        if (e.PropertyName is nameof(MainViewModel.ContinuePlayingGame))
        {
            AnimateSelectionSurface(ContinuePlayingCard, fromY: 8, targetOpacity: 1);
        }

        if (e.PropertyName is nameof(MainViewModel.RandomBacklogGame))
        {
            AnimateSelectionSurface(RotatingPickCard, fromY: 8, targetOpacity: 1);
        }

        if (e.PropertyName is nameof(MainViewModel.RecommendedGame))
        {
            AnimateSelectionSurface(SmartPickCard, fromY: 8, targetOpacity: 1);
        }

        if (e.PropertyName is nameof(MainViewModel.StatusMessage))
        {
            if (ViewModel.StatusMessage.StartsWith("Applied metadata", StringComparison.OrdinalIgnoreCase))
            {
                _sound.PlayMetadataApply();
                ShowStatusToast("METADATA UPDATED", "\uE8FB");
            }
            else if (ViewModel.StatusMessage.StartsWith("Applied ", StringComparison.OrdinalIgnoreCase) &&
                     ViewModel.StatusMessage.Contains(" art ", StringComparison.OrdinalIgnoreCase))
            {
                _sound.PlayMetadataApply();
                ShowStatusToast("ARTWORK UPDATED", "\uEB9F");
            }
            else if (ViewModel.StatusMessage.StartsWith("Achievement unlocked", StringComparison.OrdinalIgnoreCase))
            {
                _sound.PlayAchievementUnlock();
                ShowStatusToast("ACHIEVEMENT UNLOCKED", "\uE735");
            }
            else if (ViewModel.StatusMessage.StartsWith("Launched ", StringComparison.OrdinalIgnoreCase))
            {
                ShowStatusToast("LAUNCHING", "\uE768");
            }
            else if (ViewModel.StatusMessage.Contains("profile card", StringComparison.OrdinalIgnoreCase) ||
                     ViewModel.StatusMessage.Contains("is now active", StringComparison.OrdinalIgnoreCase) ||
                     ViewModel.StatusMessage.Contains("visible online", StringComparison.OrdinalIgnoreCase))
            {
                ShowStatusToast("PROFILE UPDATED", "\uE77B");
            }
            else if ((ViewModel.StatusMessage.Contains("staged", StringComparison.OrdinalIgnoreCase) ||
                      ViewModel.StatusMessage.Contains("downloaded", StringComparison.OrdinalIgnoreCase)) &&
                     ViewModel.StatusMessage.Contains("update", StringComparison.OrdinalIgnoreCase))
            {
                ShowStatusToast("UPDATE READY", "\uE895");
            }
            else if (ViewModel.StatusMessage.Contains("favorite", StringComparison.OrdinalIgnoreCase))
            {
                ShowStatusToast("FAVORITE UPDATED", "\uE734");
            }
            else if (ViewModel.StatusMessage.Contains("backup", StringComparison.OrdinalIgnoreCase) ||
                     ViewModel.StatusMessage.Contains("export", StringComparison.OrdinalIgnoreCase))
            {
                ShowStatusToast("LIBRARY SAVED", "\uE74E");
            }
            else if (ViewModel.StatusMessage.Contains("cleanup removed", StringComparison.OrdinalIgnoreCase) ||
                     ViewModel.StatusMessage.Contains("repair complete", StringComparison.OrdinalIgnoreCase))
            {
                ShowStatusToast("LIBRARY CLEANED", "\uE74D");
            }
        }

        if (e.PropertyName is nameof(MainViewModel.SocialStatusMessage))
        {
            var message = ViewModel.SocialStatusMessage ?? string.Empty;
            if (message.Contains("visible online", StringComparison.OrdinalIgnoreCase))
            {
                ShowStatusToast("PROFILE ONLINE", "\uE77B");
            }
            else if (message.Contains("added", StringComparison.OrdinalIgnoreCase) ||
                     message.Contains("Updated ", StringComparison.OrdinalIgnoreCase))
            {
                ShowStatusToast("FRIEND UPDATED", "\uE716");
            }
            else if (message.Contains("removed", StringComparison.OrdinalIgnoreCase))
            {
                ShowStatusToast("FRIEND REMOVED", "\uE74D");
            }
            else if (message.Contains("Copied", StringComparison.OrdinalIgnoreCase))
            {
                ShowStatusToast("COPIED", "\uE8C8");
            }
        }

        if (e.PropertyName is nameof(MainViewModel.SelectedPrimaryPage))
        {
            if (ViewModel.SelectedPrimaryPage == 2)
            {
                ViewModel.SelectedPrimaryPage = _lastNonMetadataPage == 2 ? 1 : _lastNonMetadataPage;
                await OpenMetadataWindowForGameAsync(ViewModel.SelectedGame, search: true);
                return;
            }

            _lastNonMetadataPage = ViewModel.SelectedPrimaryPage;
            ApplyBlackFlashPageArtworkResources(string.Equals(ViewModel.AppearanceThemeKey, "Black Flash", StringComparison.Ordinal));
            ApplyTheatreChrome(ViewModel.SelectedPrimaryPage == 4);
        }
    }

    private void ApplyBlackFlashPageArtworkResources(bool isBlackFlashTheme)
    {
        var activePage = isBlackFlashTheme ? ViewModel.SelectedPrimaryPage : -1;
        SetResource("BlackFlashHomeArtOpacity", activePage == 0 ? 0.86d : 0d);
        SetResource("BlackFlashLibraryArtOpacity", activePage == 1 ? 0.78d : 0d);
        SetResource("BlackFlashAchievementsArtOpacity", activePage == 3 ? 0.74d : 0d);
        SetResource("BlackFlashFriendsArtOpacity", activePage == 4 ? 0.72d : 0d);
        SetResource("BlackFlashSettingsArtOpacity", activePage == 5 ? 0.68d : 0d);
        SetResource("BlackFlashSidePanelArtOpacity", activePage is 1 or 3 ? 0.26d : 0.14d);
    }

    private void OnHomeSuggestionTimerTick(object? sender, EventArgs e)
    {
        // This fires unattended while the user is idling on the Home page - any exception
        // here previously had nowhere safe to land (see App.OnDispatcherUnhandledException,
        // which only whitelists two specific InvalidOperationException messages), so a
        // transient failure mid-rotation could take down the whole app with no user action
        // to correlate it to. Guarded the same way ThemeEffectLayer.OnRender guards its
        // per-frame work: log and skip this tick, try again next interval.
        try
        {
            if (ViewModel.SelectedPrimaryPage == 0)
            {
                ViewModel.RotateHomeSuggestion();
            }
        }
        catch (Exception ex)
        {
            _diagnostics.WriteException("home-suggestion-timer", ex);
        }
    }

    private void RootPointerMove(object sender, MouseEventArgs e)
    {
        if (!ViewModel.VisualEffectsEnabled)
        {
            return;
        }

        var point = e.GetPosition(RootShell);
        MovePointerGlow(point, visible: true);
    }

    private void RootPointerLeave(object sender, MouseEventArgs e)
    {
        _isPointerGlowVisible = false;
        PointerGlow.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void RootPointerDown(object sender, MouseButtonEventArgs e)
    {
        if (!ViewModel.VisualEffectsEnabled || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        var point = e.GetPosition(RootShell);
        PulsePointerClick(point);
    }

    private void MovePointerGlow(Point point, bool visible)
    {
        var width = PointerGlow.ActualWidth > 0 ? PointerGlow.ActualWidth : PointerGlow.Width;
        var height = PointerGlow.ActualHeight > 0 ? PointerGlow.ActualHeight : PointerGlow.Height;
        PointerGlowTransform.X = point.X - (width / 2);
        PointerGlowTransform.Y = point.Y - (height / 2);
        var targetOpacity = visible
            ? Math.Clamp(0.18 + ViewModel.EffectiveVisualEffectIntensity * 0.12, 0.18, 0.38)
            : 0;

        if (!visible)
        {
            _isPointerGlowVisible = false;
            PointerGlow.BeginAnimation(UIElement.OpacityProperty, null);
            PointerGlow.Opacity = 0;
            return;
        }

        if (_isPointerGlowVisible)
        {
            PointerGlow.BeginAnimation(UIElement.OpacityProperty, null);
            PointerGlow.Opacity = targetOpacity;
            return;
        }

        _isPointerGlowVisible = true;
        PointerGlow.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(targetOpacity, TimeSpan.FromMilliseconds(70))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void PulsePointerClick(Point point)
    {
        PointerClickPulseTransform.X = point.X - (PointerClickPulse.Width / 2);
        PointerClickPulseTransform.Y = point.Y - (PointerClickPulse.Height / 2);
        PointerClickPulseScale.ScaleX = 0.65;
        PointerClickPulseScale.ScaleY = 0.65;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var pulseOpacity = Math.Clamp(0.48 + ViewModel.EffectiveVisualEffectIntensity * 0.22, 0.5, 0.86);
        PointerClickPulse.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(pulseOpacity, 0, TimeSpan.FromMilliseconds(360)) { EasingFunction = ease });
        PointerClickPulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.65, 2.35, TimeSpan.FromMilliseconds(360)) { EasingFunction = ease });
        PointerClickPulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.65, 2.35, TimeSpan.FromMilliseconds(360)) { EasingFunction = ease });
    }

    private void ShowStatusToast(string label, string glyph)
    {
        if (!ViewModel.VisualEffectsEnabled)
        {
            return;
        }

        ActionToastLabel.Text = label;
        ActionToastGlyph.Text = glyph;

        var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        var easeIn = new SineEase { EasingMode = EasingMode.EaseIn };
        var fade = new DoubleAnimationUsingKeyFrames();
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))) { EasingFunction = easeOut });
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.0))));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.35))) { EasingFunction = easeIn });

        var lift = new DoubleAnimationUsingKeyFrames();
        lift.KeyFrames.Add(new EasingDoubleKeyFrame(-12, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        lift.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))) { EasingFunction = easeOut });
        lift.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.0))));
        lift.KeyFrames.Add(new EasingDoubleKeyFrame(-8, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.35))) { EasingFunction = easeIn });

        ActionToast.BeginAnimation(UIElement.OpacityProperty, fade);
        ActionToastLift.BeginAnimation(TranslateTransform.YProperty, lift);
    }

    private static void AnimateSelectionSurface(FrameworkElement? element, double fromY, double targetOpacity)
    {
        if (element is null)
        {
            return;
        }

        var translate = EnsureWritableTranslateTransform(element);

        var fade = new DoubleAnimation
        {
            From = 0.55,
            To = targetOpacity,
            Duration = TimeSpan.FromMilliseconds(260),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var slide = new DoubleAnimation
        {
            From = fromY,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(320),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        element.BeginAnimation(UIElement.OpacityProperty, fade);
        translate.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    private static TranslateTransform EnsureWritableTranslateTransform(FrameworkElement element)
    {
        switch (element.RenderTransform)
        {
            case TranslateTransform translate when !translate.IsFrozen:
                return translate;
            case TransformGroup group:
            {
                if (group.IsFrozen)
                {
                    group = group.Clone();
                    element.RenderTransform = group;
                }

                var existingTranslate = group.Children.OfType<TranslateTransform>().FirstOrDefault();
                if (existingTranslate is { IsFrozen: false })
                {
                    return existingTranslate;
                }

                if (existingTranslate is { IsFrozen: true })
                {
                    var index = group.Children.IndexOf(existingTranslate);
                    var replacement = new TranslateTransform(existingTranslate.X, existingTranslate.Y);
                    group.Children[index] = replacement;
                    return replacement;
                }

                var appended = new TranslateTransform();
                group.Children.Add(appended);
                return appended;
            }
            case Transform transform when transform != Transform.Identity:
            {
                var group = new TransformGroup();
                group.Children.Add(transform.IsFrozen ? transform.Clone() : transform);
                var translate = new TranslateTransform();
                group.Children.Add(translate);
                element.RenderTransform = group;
                return translate;
            }
            default:
            {
                var translate = new TranslateTransform();
                element.RenderTransform = translate;
                return translate;
            }
        }
    }

    private void RestartHomeSuggestionTimer()
    {
        _homeSuggestionTimer.Stop();
        if (ViewModel.HomeRotationSeconds <= 0)
        {
            return;
        }

        _homeSuggestionTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(ViewModel.HomeRotationSeconds, 5, 300));
        _homeSuggestionTimer.Start();
    }

    private void ApplySidebarLayout()
    {
        if (!IsLoaded)
        {
            return;
        }

        var showSidebar = ViewModel.IsSidebarVisible;
        if (showSidebar)
        {
            SidebarColumn.MinWidth = MinimumSidebarWidth;
            SidebarColumn.MaxWidth = MaximumSidebarWidth;
            var currentWidth = SidebarColumn.Width.IsAbsolute && SidebarColumn.Width.Value > 0
                ? SidebarColumn.Width.Value
                : _lastSidebarWidth;
            SidebarColumn.Width = new GridLength(Math.Clamp(currentWidth, MinimumSidebarWidth, MaximumSidebarWidth));
            SidebarSeparatorColumn.Width = new GridLength(8);
        }
        else
        {
            if (SidebarRail.ActualWidth >= MinimumSidebarWidth)
            {
                _lastSidebarWidth = Math.Clamp(SidebarRail.ActualWidth, MinimumSidebarWidth, MaximumSidebarWidth);
            }

            SidebarColumn.MinWidth = 0;
            SidebarColumn.MaxWidth = 0;
            SidebarColumn.Width = new GridLength(0);
            SidebarSeparatorColumn.Width = new GridLength(0);
        }

        var canResizeSidebar = showSidebar && WindowState != WindowState.Maximized;
        SidebarRail.Visibility = showSidebar ? Visibility.Visible : Visibility.Collapsed;
        SidebarSeparator.Visibility = showSidebar ? Visibility.Visible : Visibility.Collapsed;
        SidebarResizeGrip.Visibility = canResizeSidebar ? Visibility.Visible : Visibility.Collapsed;
        BladeNavigationHost.Visibility = showSidebar ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SidebarRailSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ViewModel.IsSidebarVisible && e.NewSize.Width >= MinimumSidebarWidth)
        {
            _lastSidebarWidth = Math.Clamp(e.NewSize.Width, MinimumSidebarWidth, MaximumSidebarWidth);
        }
    }

    private void ApplyAppearanceTheme()
    {
        var themeKey = ViewModel.AppearanceThemeKey;
        var profileThemeKey = string.IsNullOrWhiteSpace(ViewModel.ActiveProfileThemeLabel)
            ? themeKey
            : ViewModel.ActiveProfileThemeLabel;
        var palette = themeKey switch
        {
            "Blueberry Midnight" => new ThemePalette("#07101F", "#030915", "#0D1B32", "#152A48", "#60A5FA", "#1C3356", "#9CC9FF"),
            "Mint Cloud" => new ThemePalette("#081713", "#06110F", "#0E211D", "#183831", "#2DB880", "#0D342A", "#9DE7C9"),
            "High Contrast" => new ThemePalette("#000000", "#000000", "#0B0B0B", "#181818", "#FFFFFF", "#2A2A2A", "#FFFFFF"),
            "Neon Matrix" => new ThemePalette("#040F0B", "#030A07", "#0A1812", "#13261E", "#46FF8A", "#0D3E24", "#D8FFE9"),
            "Retro Glitch" => new ThemePalette("#0E0A1B", "#080515", "#171128", "#231A3B", "#FF4FD0", "#321044", "#EFE6FF"),
            "NieR Automata" => new ThemePalette("#D8D0BF", "#0E0D0B", "#D9D1BF", "#E2D9C7", "#2B2721", "#C9BCA3", "#F3EBD9", "#2A261F"),
            "Wuthering Waves" => new ThemePalette("#061320", "#030810", "#091B28", "#10293A", "#72F6E8", "#0E4351", "#E8FFFA", "#EAFDF8"),
            "Black Flash" => new ThemePalette("#050204", "#010101", "#0A0506", "#18080B", "#E51B2D", "#2A0508", "#FFF1F2", "#FFF6F6"),
            _ => new ThemePalette("#090D1A", "#060A14", "#0F1623", "#172030", "#E04469", "#2E0F1C", "#E2EAF8")
        };

        SetBrush("AppBackgroundBrush", palette.AppBackground);
        SetBrush("SidebarBrush", palette.Sidebar);
        SetBrush("SurfaceBrush", palette.Surface);
        SetBrush("SurfaceElevatedBrush", palette.Elevated);
        SetBrush("BerryBrush", palette.Accent);
        SetBrush("BerryHoverBrush", palette.AccentText);
        SetBrush("BerryPressedBrush", "#C8395E");
        SetBrush("BerryDimBrush", palette.AccentDim);
        SetBrush("TextPrimaryBrush", palette.PrimaryText);

        var isNierTheme = string.Equals(themeKey, "NieR Automata", StringComparison.Ordinal);
        var isWutheringTheme = string.Equals(themeKey, "Wuthering Waves", StringComparison.Ordinal);
        var isBlueberryTheme = string.Equals(themeKey, "Blueberry Midnight", StringComparison.Ordinal);
        var isMintTheme = string.Equals(themeKey, "Mint Cloud", StringComparison.Ordinal);
        var isHighContrastTheme = string.Equals(themeKey, "High Contrast", StringComparison.Ordinal);
        var isMatrixTheme = string.Equals(themeKey, "Neon Matrix", StringComparison.Ordinal);
        var isGlitchTheme = string.Equals(themeKey, "Retro Glitch", StringComparison.Ordinal);
        var isBlackFlashTheme = string.Equals(themeKey, "Black Flash", StringComparison.Ordinal);
        SetResource("NierArtworkOpacity", isNierTheme ? 0.74d : 0d);
        SetResource("WutheringArtworkOpacity", isWutheringTheme ? 0.56d : 0d);
        SetResource("BlackFlashArtworkOpacity", isBlackFlashTheme ? 0.42d : 0d);
        ApplyBlackFlashPageArtworkResources(isBlackFlashTheme);
        SetResource("NierDeletionEffectOpacity", isNierTheme ? 0.22d : 0d);
        SetResource("ThemeShellOverlayOpacity", isNierTheme ? 0.42d : isWutheringTheme ? 0.54d : isBlackFlashTheme ? 0.44d : isMatrixTheme ? 0.74d : isGlitchTheme ? 0.72d : 0.82d);
        SetResource("ThemeBodyFontFamily", new FontFamily(themeKey switch
        {
        "NieR Automata" => "Yu Gothic UI, Segoe UI, Calibri", 
        "Neon Matrix" => "Cascadia Mono, Consolas, Segoe UI", 
        "Retro Glitch" => "Bahnschrift, Segoe UI Variable Text, Segoe UI", 
        "Wuthering Waves" => "Segoe UI Variable Text, Microsoft YaHei UI, Segoe UI", 
        "Black Flash" => "Yu Gothic UI, Segoe UI Variable Text, Segoe UI", 
        "Mint Cloud" => "Calibri, Segoe UI", 
            "Blueberry Midnight" => "Candara, Segoe UI", 
            "High Contrast" => "Segoe UI, Arial", 
            _ => "Corbel, Segoe UI Variable Text, Segoe UI"
        }));
        SetResource("ThemeDisplayFontFamily", new FontFamily(themeKey switch
        {
        "NieR Automata" => "Yu Gothic UI Semibold, Segoe UI Semibold, Segoe UI", 
        "Neon Matrix" => "Cascadia Mono SemiBold, Consolas, Segoe UI Semibold", 
        "Retro Glitch" => "Bahnschrift SemiBold, Segoe UI Black, Segoe UI", 
            "Wuthering Waves" => "Bahnschrift SemiBold, Segoe UI Semibold, Segoe UI", 
            "Black Flash" => "Bahnschrift SemiBold, Yu Gothic UI Semibold, Segoe UI Semibold", 
            "Mint Cloud" => "Calibri Light, Calibri, Segoe UI Semibold", 
            "Blueberry Midnight" => "Candara, Segoe UI Semibold", 
            "High Contrast" => "Segoe UI Semibold, Segoe UI, Arial", 
            _ => "Corbel, Segoe UI Semibold, Segoe UI"
        }));

        var squareTheme = isNierTheme || isHighContrastTheme;
        var controlRadius = squareTheme ? 0 : isMatrixTheme ? 5 : isBlackFlashTheme ? 6 : isWutheringTheme ? 12 : 9;
        var panelRadius = squareTheme ? 0 : isMatrixTheme ? 8 : isBlackFlashTheme ? 10 : isWutheringTheme ? 18 : 14;
        SetResource("ThemeCornerRadiusControl", new CornerRadius(controlRadius));
        SetResource("ThemeCornerRadiusPanel", new CornerRadius(panelRadius));
        SetResource("ThemeCornerRadiusLarge", new CornerRadius(squareTheme ? 0 : isBlackFlashTheme ? 12 : isWutheringTheme ? 20 : 12));
        SetResource("ThemeCornerRadiusPill", new CornerRadius(squareTheme ? 0 : 999));
        SetResource("GlassButtonCornerRadius", new CornerRadius(isHighContrastTheme ? 4 : isNierTheme ? 12 : isBlackFlashTheme ? 14 : isWutheringTheme ? 20 : isMatrixTheme ? 16 : 18));
        SetResource("GlassChipCornerRadius", new CornerRadius(isHighContrastTheme ? 4 : 999));
        SetResource("GlassButtonBorderThickness", new Thickness(isHighContrastTheme ? 1.4 : isBlackFlashTheme ? 1.2 : 1));
        SetResource("GlassButtonFillBrush", BuildLinearBrush("#5EFFFFFF", "#2412D7FF", "#16000000", diagonal: true));
        SetResource("GlassButtonHoverFillBrush", BuildLinearBrush("#82FFFFFF", "#3220DFFF", "#1E000000", diagonal: true));
        SetResource("GlassButtonPressedFillBrush", BuildLinearBrush("#36FFFFFF", "#2012A8FF", "#2A000000", diagonal: true));
        SetResource("GlassButtonBorderBrush", BuildLinearBrush("#B8FFFFFF", "#5C8EDBFF", "#30FFFFFF", diagonal: true));
        SetResource("GlassChipFillBrush", BuildLinearBrush("#36FFFFFF", "#1812D7FF", "#12000000", diagonal: true));
        SetResource("GlassChipActiveFillBrush", BuildLinearBrush("#58FFFFFF", "#2620DFFF", "#18000000", diagonal: true));
        SetResource("GlassChipBorderBrush", BuildLinearBrush("#8FFFFFFF", "#588EDBFF", "#2AFFFFFF", diagonal: true));
        SetResource("ThemeBorderThickness", new Thickness(isNierTheme ? 1.2 : 1));
        SetResource("ThemeShellAtmosphereBrush", BuildLinearBrush("#14000000", "#08000000", "#00000000", diagonal: true));
        SetResource("ThemeShellPatternBrush", new SolidColorBrush(Colors.Transparent));
        SetResource("ThemeSidebarOverlayBrush", BuildVerticalBrush("#20000000", "#08000000", "#00000000"));
        SetResource("ThemeSidebarPatternBrush", new SolidColorBrush(Colors.Transparent));
        SetResource("ThemeSweepBrush", BuildLinearBrush("#66FFFFFF", "#22FFFFFF", "#00FFFFFF"));
        SetResource("ThemeShellPatternOpacity", 0.035d);
        SetResource("ThemeSidebarPatternOpacity", 0.04d);
        SetResource("ThemeSweepOpacity", 0.04d);
        SetResource("ThemeRainOverlayOpacity", 0.055d);
        SetResource("ThemeSweepOverlayOpacity", 0.055d);
        SetResource("ThemeShellOverlayOpacity", 0.86d);
        SetResource("ThemePointerGlowCenterColor", (Color)ColorConverter.ConvertFromString("#34FFFFFF"));
        SetResource("ThemePointerGlowMidColor", (Color)ColorConverter.ConvertFromString("#225DA6FF"));
        SetResource("ThemePointerClickColor", (Color)ColorConverter.ConvertFromString("#AA5DA6FF"));

        if (isNierTheme)
        {
            SetBrush("AppBackgroundBrush", "#CCC4AE");
            SetBrush("SidebarBrush", "#17130F");
            SetBrush("SurfaceBrush", "#C8D9D1BF");
            SetBrush("SurfaceElevatedBrush", "#D0E2D9C7");
            SetBrush("SurfaceHighBrush", "#D8CEC6B4");
            SetBrush("CardBrush", "#CDE7DECC");
            SetBrush("CardHoverBrush", "#DDEDE4D2");
            SetBrush("CardSelectedBrush", "#E8F0E7D6");
            SetBrush("TextPrimaryBrush", "#1E1810");
            SetBrush("TextSecondaryBrush", "#5E5648");
            SetBrush("TextMutedBrush", "#7D7563");
            SetBrush("TextOnDarkBrush", "#F3EBD9");
            SetBrush("ErrorBrush", "#7A3018");
            SetBrush("ErrorHoverBrush", "#8A3C20");
            SetBrush("ErrorDimBrush", "#E2D8CC");
            SetBrush("CloudBrush", "#B9AD8B");
            SetBrush("CloudDimBrush", "#2B261D");
            SetBrush("MintBrush", "#A5A682");
            SetBrush("MintDimBrush", "#232518");
            SetResource("MintGlowColor", (Color)ColorConverter.ConvertFromString("#A5A682")); // A2: theme-safe glow to match MintBrush
            SetBrush("GoldBrush", "#D7C27C");
            SetBrush("GoldDimBrush", "#2F2818");
            SetBrush("NeoCyanBrush", "#C8B98E");
            SetBrush("NeoBerryBrush", "#9D8B66");
            SetBrush("BorderBrush", "#625946");
            SetBrush("BorderSubtleBrush", "#4F4839");
            SetBrush("GlassPanelBorderBrush", "#6F6654");
            SetBrush("GlassPanelHoverBorderBrush", "#92876E");
            SetBrush("ThemePanelFillBrush", "#BDEFE5D2");
            SetBrush("ThemePanelFillSoftBrush", "#AEEFE6D4");
            SetBrush("ThemePanelEdgeBrush", "#6E6452");
            SetBrush("ThemePanelShadowBrush", "#120E0A");
            SetResource("ThemePanelShadowColor", (Color)ColorConverter.ConvertFromString("#120E0A"));
            SetResource("ThemePrimaryButtonGlowColor", (Color)ColorConverter.ConvertFromString("#00000000"));
            SetResource("ThemePanelBorderGradientBrush", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6E6452")));
            SetResource("ThemeAtmosphereBlobBrush", new SolidColorBrush(Colors.Transparent));
            SetResource("ThemeAtmosphereBlobSecondaryBrush", new SolidColorBrush(Colors.Transparent));
            SetBrush("ThemeDeckFrameBrush", "#CFE2D7C2");
            SetBrush("ThemeDeckFrameBorderBrush", "#8A7E66");
            SetBrush("ThemeNavItemBackgroundBrush", "#D8D0BF");
            SetBrush("ThemeNavItemHoverBackgroundBrush", "#E4DBC8");
            SetBrush("ThemeNavItemSelectedBackgroundBrush", "#F1E8D5");
            SetBrush("ThemeNavItemBorderBrush", "#7E7461");
            SetBrush("ThemeNavItemHoverBorderBrush", "#9B8F76");
            SetBrush("ThemeNavItemSelectedBorderBrush", "#4F4637");
            SetBrush("ThemeNavItemTopEdgeBrush", "#70FFFFFF");
            SetBrush("ThemePillFillBrush", "#D8D0BF");
            SetBrush("ThemePillHoverFillBrush", "#E4DBC8");
            SetBrush("ThemePillSelectedFillBrush", "#F1E8D5");
            SetBrush("ThemePillBorderBrush", "#817764");
            SetBrush("ThemePillHoverBorderBrush", "#A3977D");
            SetBrush("ThemePillSelectedBorderBrush", "#4F4637");
            SetBrush("ThemePillLabelBrush", "#4E4638");
            SetBrush("ThemePillLabelActiveBrush", "#2F2A22");
            SetBrush("ThemePrimaryButtonTextBrush", "#F3EBD9");
            SetBrush("ThemePrimaryButtonFillBrush", "#27231D");
            SetBrush("ThemePrimaryButtonHoverFillBrush", "#373229");
            SetBrush("ThemePrimaryButtonPressedFillBrush", "#1D1A16");
            SetBrush("ThemeSecondaryButtonTextBrush", "#3A3328");
            SetBrush("ThemeSecondaryButtonFillBrush", "#E0D8C7");
            SetBrush("ThemeSecondaryButtonHoverFillBrush", "#ECE3D2");
            SetBrush("ThemeSecondaryButtonPressedFillBrush", "#D0C6B3");
            SetBrush("ThemeSecondaryButtonBorderBrush", "#8D816B");
            SetBrush("ThemeGhostButtonHoverFillBrush", "#D7CCBA");
            SetBrush("ThemeGhostButtonPressedFillBrush", "#CBBFA9");
            SetBrush("ThemeGhostButtonBorderBrush", "#7A705E");
            SetBrush("ThemeGhostButtonHoverBorderBrush", "#A09379");
            SetResource("HeroOverlayBrush", BuildVerticalBrush("#18D8D0BF", "#66D8D0BF", "#92D8D0BF"));
            SetResource("NostalgiaAtmosphereBrush", BuildLinearBrush("#10D8D0BF", "#08CEC4AE", "#18D8D0BF", diagonal: true));
            SetResource("AuroraVeilBrush", BuildLinearBrush("#08000000", "#04000000", "#00000000"));
            SetResource("GlitchNeonBrush", BuildLinearBrush("#8A4F4637", "#AA5E5548", "#8A4F4637"));
            SetResource("BladeGlassBrush", BuildLinearBrush("#E8E0CF", "#E1D8C5", "#DDD3C0", diagonal: true));
            SetResource("BladeActiveBrush", BuildLinearBrush("#8F6F6452", "#BF6F6452", "#8F6F6452"));
            SetResource("MatrixRainBrush", new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)));
            SetResource("NostalgiaScanlineBrush", BuildNierDotGridBrush());
            SetResource("ThemeShellAtmosphereBrush", BuildVerticalBrush("#10E4DAC0", "#04D6CCB4", "#00D6CCB4"));
            SetResource("ThemeShellPatternBrush", BuildNierDotGridBrush());
            SetResource("ThemeSidebarOverlayBrush", BuildVerticalBrush("#3817130F", "#1217130F", "#0017130F"));
            SetResource("ThemeSidebarPatternBrush", BuildCadenceLineBrush("#1A8A7E66", "#0C4F4637"));
            SetResource("ThemeSweepBrush", BuildLinearBrush("#505E5548", "#205E5548", "#005E5548"));
            SetResource("ThemeShellPatternOpacity", 0.032d);
            SetResource("ThemeSidebarPatternOpacity", 0.035d);
            SetResource("ThemeSweepOpacity", 0.025d);
            SetResource("ThemeShellOverlayOpacity", 0.42d);
            SetResource("NierArtworkOpacity", 0.74d);
            SetResource("NierDeletionEffectOpacity", 0.22d);
            SetResource("ThemePointerGlowCenterColor", (Color)ColorConverter.ConvertFromString("#28F3EBD9"));
            SetResource("ThemePointerGlowMidColor", (Color)ColorConverter.ConvertFromString("#28D7C27C"));
            SetResource("ThemePointerClickColor", (Color)ColorConverter.ConvertFromString("#AAD7C27C"));

            SetResource("GlassButtonFillBrush", BuildLinearBrush("#72F3EBD9", "#28D7C27C", "#1427231D", diagonal: true));
            SetResource("GlassButtonHoverFillBrush", BuildLinearBrush("#96FFF6E4", "#36D7C27C", "#1D373229", diagonal: true));
            SetResource("GlassButtonPressedFillBrush", BuildLinearBrush("#48D8D0BF", "#2AC8B98E", "#301D1A16", diagonal: true));
            SetResource("GlassButtonBorderBrush", BuildLinearBrush("#B8FFF8E8", "#7DD7C27C", "#405E5548", diagonal: true));
            SetResource("GlassChipFillBrush", BuildLinearBrush("#46F3EBD9", "#22D7C27C", "#0F27231D", diagonal: true));
            SetResource("GlassChipActiveFillBrush", BuildLinearBrush("#62FFF6E4", "#30D7C27C", "#1627231D", diagonal: true));
            SetResource("GlassChipBorderBrush", BuildLinearBrush("#98FFF8E8", "#62D7C27C", "#305E5548", diagonal: true));
            SetResource("GlassSheenBrush", BuildVerticalBrush("#2AFFFFFF", "#0EF3EBD9", "#00FFFFFF"));

            // Authentic NieR replicant save data deletion drifting glitch effect in C#
            if (TryFindResourceValue(Application.Current.Resources, "NierDeletionBrush", out var rawNierBrush) && rawNierBrush is DrawingBrush staticNierBrush)
            {
                var animatedNierBrush = staticNierBrush.Clone();
                var trans = new TranslateTransform();
                
                var animX = new DoubleAnimation 
                { 
                    From = 0, 
                    To = -72, 
                    Duration = TimeSpan.FromSeconds(8), 
                    RepeatBehavior = RepeatBehavior.Forever 
                };
                var animY = new DoubleAnimation 
                { 
                    From = 0, 
                    To = 28, 
                    Duration = TimeSpan.FromSeconds(16), 
                    RepeatBehavior = RepeatBehavior.Forever 
                };
                
                trans.BeginAnimation(TranslateTransform.XProperty, animX);
                trans.BeginAnimation(TranslateTransform.YProperty, animY);
                animatedNierBrush.Transform = trans;
                
                SetResource("NierDeletionBrush", animatedNierBrush);
            }
        }
        else if (isWutheringTheme)
        {
            SetBrush("AppBackgroundBrush", "#081520");
            SetBrush("SidebarBrush", "#07111A");
            SetBrush("SurfaceBrush", "#B5091B28");
            SetBrush("SurfaceElevatedBrush", "#C510293A");
            SetBrush("SurfaceHighBrush", "#D018364A");
            SetBrush("CardBrush", "#C80D2232");
            SetBrush("CardHoverBrush", "#D8123049");
            SetBrush("CardSelectedBrush", "#E8173A54");
            SetBrush("TextSecondaryBrush", "#A9D7DF");
            SetBrush("TextMutedBrush", "#5E8792");
            SetBrush("CloudBrush", "#7CCBFF");
            SetBrush("CloudDimBrush", "#0B2A3F");
            SetBrush("MintBrush", "#72F6E8");
            SetBrush("MintDimBrush", "#0B3B3D");
            SetResource("MintGlowColor", (Color)ColorConverter.ConvertFromString("#72F6E8")); // A2: theme-safe glow to match MintBrush
            SetBrush("GoldBrush", "#D9C98A");
            SetBrush("GoldDimBrush", "#342E14");
            SetBrush("NeoCyanBrush", "#99FFF1");
            SetBrush("NeoBerryBrush", "#5DA6FF");
            SetBrush("BorderBrush", "#25566A");
            SetBrush("BorderSubtleBrush", "#143445");
            SetBrush("GlassPanelBorderBrush", "#4BA6B6");
            SetBrush("GlassPanelHoverBorderBrush", "#9AF8F0");
            SetResource("ThemePanelFillBrush", BuildGlassBackingBrush("#B20A1B28", "#82122B3B"));
            SetResource("ThemePanelFillSoftBrush", BuildGlassBackingBrush("#96122B3B", "#761C3A4F"));
            SetBrush("ThemePanelEdgeBrush", "#3E8AA0");
            SetBrush("ThemePanelShadowBrush", "#02070C");
            SetResource("ThemePanelShadowColor", (Color)ColorConverter.ConvertFromString("#72F6E8"));
            SetResource("ThemePrimaryButtonGlowColor", (Color)ColorConverter.ConvertFromString("#72F6E8"));
            SetResource("ThemePanelBorderGradientBrush", BuildGlassBorderBrush("#72F6E8"));
            SetResource("ThemeAtmosphereBlobBrush", new SolidColorBrush(Color.FromArgb(42, 114, 246, 232)));
            SetResource("ThemeAtmosphereBlobSecondaryBrush", new SolidColorBrush(Color.FromArgb(34, 93, 166, 255)));
            SetBrush("ThemeDeckFrameBrush", "#44152E42");
            SetBrush("ThemeDeckFrameBorderBrush", "#3E8AA0");
            SetBrush("ThemeNavItemBackgroundBrush", "#24112638");
            SetBrush("ThemeNavItemHoverBackgroundBrush", "#2F193D55");
            SetBrush("ThemeNavItemSelectedBackgroundBrush", "#3A1C4D64");
            SetBrush("ThemeNavItemBorderBrush", "#346D83");
            SetBrush("ThemeNavItemHoverBorderBrush", "#72F6E8");
            SetBrush("ThemeNavItemSelectedBorderBrush", "#D9C98A");
            SetBrush("ThemeNavItemTopEdgeBrush", "#82EFFFF6");
            SetBrush("ThemePillFillBrush", "#20112A3A");
            SetBrush("ThemePillHoverFillBrush", "#2A17384D");
            SetBrush("ThemePillSelectedFillBrush", "#34204F64");
            SetBrush("ThemePillBorderBrush", "#346D83");
            SetBrush("ThemePillHoverBorderBrush", "#72F6E8");
            SetBrush("ThemePillSelectedBorderBrush", "#D9C98A");
            SetBrush("ThemePillLabelBrush", "#A9D7DF");
            SetBrush("ThemePillLabelActiveBrush", "#FFFFFF");
            SetBrush("ThemePrimaryButtonTextBrush", "#031018");
            SetResource("ThemePrimaryButtonFillBrush", BuildButtonGradient("#72F6E8", "#1FBDAD"));
            SetResource("ThemePrimaryButtonHoverFillBrush", BuildButtonGradient("#A2FFF5", "#34D3C4"));
            SetResource("ThemePrimaryButtonPressedFillBrush", BuildButtonGradient("#48D7CC", "#199B8F"));
            SetBrush("ThemeSecondaryButtonTextBrush", "#EAFDF8");
            SetBrush("ThemeSecondaryButtonFillBrush", "#10293A");
            SetBrush("ThemeSecondaryButtonHoverFillBrush", "#17384D");
            SetBrush("ThemeSecondaryButtonPressedFillBrush", "#0A1E2C");
            SetBrush("ThemeSecondaryButtonBorderBrush", "#3E8AA0");
            SetBrush("ThemeGhostButtonHoverFillBrush", "#182F43");
            SetBrush("ThemeGhostButtonPressedFillBrush", "#203D56");
            SetBrush("ThemeGhostButtonBorderBrush", "#25566A");
            SetBrush("ThemeGhostButtonHoverBorderBrush", "#72F6E8");
            SetResource("HeroOverlayBrush", BuildVerticalBrush("#70061320", "#92091B28", "#AE061320"));
            SetResource("NostalgiaAtmosphereBrush", BuildLinearBrush("#2862E5FF", "#186B5CFF", "#22FF6EA8", diagonal: true));
            SetResource("AuroraVeilBrush", BuildLinearBrush("#2A72F6E8", "#1A5DA6FF", "#18D9C98A", diagonal: true));
            SetResource("GlitchNeonBrush", BuildLinearBrush("#CC72F6E8", "#AA7CCBFF", "#CCD9C98A"));
            SetResource("BladeGlassBrush", BuildLinearBrush("#66193B52", "#5521425C", "#442D2450", diagonal: true));
            SetResource("BladeActiveBrush", BuildLinearBrush("#8872F6E8", "#665DA6FF", "#66D9C98A"));
            SetResource("NostalgiaScanlineBrush", BuildCadenceLineBrush("#1672F6E8", "#0ED9C98A"));
            SetResource("MatrixRainBrush", new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)));
            SetResource("ThemeShellAtmosphereBrush", BuildLinearBrush("#260F2A3C", "#14385C76", "#08201A31", diagonal: true));
            SetResource("ThemeShellPatternBrush", BuildCadenceLineBrush("#1872F6E8", "#0ED9C98A"));
            SetResource("ThemeSidebarOverlayBrush", BuildVerticalBrush("#66071320", "#28071320", "#00071320"));
            SetResource("ThemeSidebarPatternBrush", BuildCadenceLineBrush("#1E72F6E8", "#127CCBFF"));
            SetResource("ThemeSweepBrush", BuildLinearBrush("#78D9C98A", "#4072F6E8", "#0072F6E8"));
            SetResource("ThemeShellPatternOpacity", 0.075d);
            SetResource("ThemeSidebarPatternOpacity", 0.06d);
            SetResource("ThemeSweepOpacity", 0.05d);
            SetResource("ThemePointerGlowCenterColor", (Color)ColorConverter.ConvertFromString("#3AFFFFFF"));
            SetResource("ThemePointerGlowMidColor", (Color)ColorConverter.ConvertFromString("#3672F6E8"));
            SetResource("ThemePointerClickColor", (Color)ColorConverter.ConvertFromString("#CC72F6E8"));

            // Wuthering Waves shifting holographic elegant glass sheen (Changli vermilion/gold, Yinlin violet/teal)
            var wuwaSheen = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            wuwaSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#3A72F6E8"), 0.0)); // Teal
            wuwaSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#15785DF5"), 0.35)); // Yinlin Violet
            wuwaSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1DFFC43A"), 0.7)); // Changli Gold
            wuwaSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#2DFF7A5A"), 1.0)); // Changli Vermilion
            SetResource("GlassSheenBrush", wuwaSheen);
        }
        else
        {
            SetBrush("AppBackgroundBrush", "#0A1220");
            SetBrush("SidebarBrush", "#070C14");
            // Default PS5-style glass sheen for other/standard themes
            var defaultSheen = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            defaultSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#24FFFFFF"), 0.0));
            defaultSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0CFFFFFF"), 0.44));
            defaultSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00FFFFFF"), 0.45));
            defaultSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00000000"), 1.0));
            SetResource("GlassSheenBrush", defaultSheen);

            SetBrush("SurfaceHighBrush", "#1C2840");
            SetBrush("CardBrush", "#141E2E");
            SetBrush("CardHoverBrush", "#1C2A40");
            SetBrush("CardSelectedBrush", "#1E2F4A");
            SetBrush("TextSecondaryBrush", "#7A90B8");
            SetBrush("TextMutedBrush", "#3E5070");
            SetBrush("CloudBrush", "#4F8FE0");
            SetBrush("CloudDimBrush", "#102040");
            SetBrush("MintBrush", "#2DB880");
            SetBrush("MintDimBrush", "#0A2820");
            SetResource("MintGlowColor", (Color)ColorConverter.ConvertFromString("#2DB880")); // A2: theme-safe glow to match MintBrush (also default for Blueberry/Mint/HighContrast/Matrix/Glitch unless overridden)
            SetBrush("GoldBrush", "#F2A325");
            SetBrush("GoldDimBrush", "#301F08");
            SetBrush("NeoCyanBrush", "#00F0FF");
            SetBrush("NeoBerryBrush", "#D92662");
            SetBrush("BorderBrush", "#243248");
            SetBrush("BorderSubtleBrush", "#182236");
            SetBrush("GlassPanelBorderBrush", "#4A5D83");
            SetBrush("GlassPanelHoverBorderBrush", "#6D86B3");
            SetResource("ThemePanelFillBrush", BuildGlassBackingBrush("#CC101827", "#9A152236"));
            SetResource("ThemePanelFillSoftBrush", BuildGlassBackingBrush("#B5162236", "#80121A2B"));
            SetBrush("ThemePanelEdgeBrush", "#22395A");
            SetBrush("ThemePanelShadowBrush", "#02050C");
            SetResource("ThemePanelShadowColor", (Color)ColorConverter.ConvertFromString("#E04469"));
            SetResource("ThemePrimaryButtonGlowColor", (Color)ColorConverter.ConvertFromString("#E04469"));
            SetResource("ThemePanelBorderGradientBrush", BuildGlassBorderBrush("#E04469"));
            SetResource("ThemeAtmosphereBlobBrush", new SolidColorBrush(Color.FromArgb(36, 224, 68, 105)));
            SetResource("ThemeAtmosphereBlobSecondaryBrush", new SolidColorBrush(Color.FromArgb(24, 46, 15, 28)));
            SetBrush("ThemeDeckFrameBrush", "#2215263C");
            SetBrush("ThemeDeckFrameBorderBrush", "#4A5D83");
            SetBrush("ThemeNavItemBackgroundBrush", "#241A2A41");
            SetBrush("ThemeNavItemHoverBackgroundBrush", "#2A22354D");
            SetBrush("ThemeNavItemSelectedBackgroundBrush", "#2F283C58");
            SetBrush("ThemeNavItemBorderBrush", "#455B80");
            SetBrush("ThemeNavItemHoverBorderBrush", "#6384B5");
            SetBrush("ThemeNavItemSelectedBorderBrush", "#00F0FF");
            SetBrush("ThemeNavItemTopEdgeBrush", "#80FFFFFF");
            SetBrush("ThemePillFillBrush", "#1A16263C");
            SetBrush("ThemePillHoverFillBrush", "#24314A");
            SetBrush("ThemePillSelectedFillBrush", "#2A3148");
            SetBrush("ThemePillBorderBrush", "#43577A");
            SetBrush("ThemePillHoverBorderBrush", "#6B88B5");
            SetBrush("ThemePillSelectedBorderBrush", "#00F0FF");
            SetBrush("ThemePillLabelBrush", "#9DB4D9");
            SetBrush("ThemePillLabelActiveBrush", "#E6F7FF");
            SetBrush("ThemePrimaryButtonTextBrush", "#FFFFFF");
            SetResource("ThemePrimaryButtonFillBrush", BuildButtonGradient("#E04469", "#9C1F3E"));
            SetResource("ThemePrimaryButtonHoverFillBrush", BuildButtonGradient("#F05579", "#BE2F51"));
            SetResource("ThemePrimaryButtonPressedFillBrush", BuildButtonGradient("#C8395E", "#82152F"));
            SetBrush("ThemeSecondaryButtonTextBrush", "#E2EAF8");
            SetBrush("ThemeSecondaryButtonFillBrush", "#172030");
            SetBrush("ThemeSecondaryButtonHoverFillBrush", "#1C2840");
            SetBrush("ThemeSecondaryButtonPressedFillBrush", "#141E2E");
            SetBrush("ThemeSecondaryButtonBorderBrush", "#25374F");
            SetBrush("ThemeGhostButtonHoverFillBrush", "#172030");
            SetBrush("ThemeGhostButtonPressedFillBrush", "#1C2840");
            SetBrush("ThemeGhostButtonBorderBrush", "#243248");
            SetBrush("ThemeGhostButtonHoverBorderBrush", "#4F8FE0");
            SetResource("HeroOverlayBrush", BuildVerticalBrush("#C6090D1A", "#E20F1623", "#F4090D1A"));
            SetResource("NostalgiaAtmosphereBrush", BuildLinearBrush("#1A4F8FE0", "#16E04469", "#102DB880", diagonal: true));
            SetResource("AuroraVeilBrush", BuildLinearBrush("#2A4F8FE0", "#1AE04469", "#0A2DB880"));
            SetResource("GlitchNeonBrush", BuildLinearBrush("#CC67FFE2", "#CC93FF5F", "#CC58B7FF"));
            SetResource("BladeGlassBrush", BuildLinearBrush("#4421374C", "#34121A2B", "#3A15243A", diagonal: true));
            SetResource("BladeActiveBrush", BuildLinearBrush("#62D92662", "#5800F0FF", "#56D92662"));
            SetResource("NostalgiaScanlineBrush", BuildScanlineBrush());
            SetResource("MatrixRainBrush", BuildMatrixRainBrush());
            SetResource("ThemeShellAtmosphereBrush", BuildLinearBrush("#180F1623", "#10172030", "#08090D1A", diagonal: true));
            SetResource("ThemeShellPatternBrush", BuildCadenceLineBrush("#166384B5", "#10E04469"));
            SetResource("ThemeSidebarOverlayBrush", BuildVerticalBrush("#34070C14", "#12070C14", "#00070C14"));
            SetResource("ThemeSidebarPatternBrush", BuildCadenceLineBrush("#144F8FE0", "#0E243248"));
            SetResource("ThemeSweepBrush", BuildLinearBrush("#66E04469", "#2658B7FF", "#002DB880"));
            SetResource("ThemeShellPatternOpacity", 0.05d);
            SetResource("ThemeSidebarPatternOpacity", 0.045d);
            SetResource("ThemeSweepOpacity", 0.045d);

            if (isBlueberryTheme)
            {
                SetBrush("AppBackgroundBrush", "#08111D");
                SetBrush("SidebarBrush", "#060B13");
                SetBrush("ThemePanelEdgeBrush", "#315A86");
                SetBrush("ThemeDeckFrameBorderBrush", "#416B9A");
                SetResource("ThemePanelFillBrush", BuildGlassBackingBrush("#CC07101F", "#A00D1B32"));
                SetResource("ThemePanelFillSoftBrush", BuildGlassBackingBrush("#B50D1B32", "#80152A48"));
                SetResource("ThemePanelShadowColor", (Color)ColorConverter.ConvertFromString("#60A5FA"));
                SetResource("ThemePrimaryButtonGlowColor", (Color)ColorConverter.ConvertFromString("#60A5FA"));
                SetResource("ThemePanelBorderGradientBrush", BuildGlassBorderBrush("#60A5FA"));
                SetResource("ThemeAtmosphereBlobBrush", new SolidColorBrush(Color.FromArgb(37, 96, 165, 250)));
                SetResource("ThemeAtmosphereBlobSecondaryBrush", new SolidColorBrush(Color.FromArgb(24, 28, 51, 86)));
                SetResource("ThemePrimaryButtonFillBrush", BuildButtonGradient("#60A5FA", "#1D4ED8"));
                SetResource("ThemePrimaryButtonHoverFillBrush", BuildButtonGradient("#8FC3FF", "#3B82F6"));
                SetResource("ThemePrimaryButtonPressedFillBrush", BuildButtonGradient("#3E86D4", "#172554"));
                SetBrush("ThemePrimaryButtonTextBrush", "#06101F");
                SetBrush("ThemeNavItemBackgroundBrush", "#0F1A2A");
                SetBrush("ThemeNavItemHoverBackgroundBrush", "#1C2A40");
                SetBrush("ThemeNavItemSelectedBackgroundBrush", "#25374F");
                SetBrush("ThemeNavItemBorderBrush", "#25374F");
                SetBrush("ThemeNavItemHoverBorderBrush", "#60A5FA");
                SetBrush("ThemeNavItemSelectedBorderBrush", "#9CC9FF");
                SetBrush("ThemePillFillBrush", "#10192A");
                SetBrush("ThemePillHoverFillBrush", "#1C2A40");
                SetBrush("ThemePillSelectedFillBrush", "#25374F");
                SetBrush("ThemePillBorderBrush", "#25374F");
                SetBrush("ThemePillHoverBorderBrush", "#60A5FA");
                SetBrush("ThemePillSelectedBorderBrush", "#9CC9FF");
                SetBrush("ThemePillLabelBrush", "#7A90B8");
                SetBrush("ThemePillLabelActiveBrush", "#9CC9FF");
                SetResource("AuroraVeilBrush", BuildLinearBrush("#263B82F6", "#184F9EE8", "#101D4ED8", diagonal: true));
                SetResource("GlitchNeonBrush", BuildLinearBrush("#AA60A5FA", "#887DD3FC", "#AA38BDF8"));
                SetResource("BladeActiveBrush", BuildLinearBrush("#8060A5FA", "#504F46E5", "#4438BDF8"));
                SetResource("HeroOverlayBrush", BuildVerticalBrush("#CC07101F", "#E20D1B32", "#F407101F"));
                SetResource("NostalgiaAtmosphereBrush", BuildLinearBrush("#243B82F6", "#144F9EE8", "#101D4ED8", diagonal: true));
                SetResource("ThemeShellAtmosphereBrush", BuildLinearBrush("#1A173252", "#10204066", "#06101F", diagonal: true));
                SetResource("ThemeShellPatternBrush", BuildCadenceLineBrush("#145FA9FF", "#0E7DD3FC"));
                SetResource("ThemeSidebarOverlayBrush", BuildVerticalBrush("#4A060B13", "#16060B13", "#00060B13"));
                SetResource("ThemeSidebarPatternBrush", BuildCadenceLineBrush("#1660A5FA", "#0C315A86"));
                SetResource("ThemeSweepBrush", BuildLinearBrush("#6860A5FA", "#307DD3FC", "#0038BDF8"));
                SetResource("ThemeShellPatternOpacity", 0.055d);
                SetResource("ThemeSidebarPatternOpacity", 0.05d);
                SetResource("ThemeSweepOpacity", 0.045d);

                // Blueberry sapphire glass sheen — cool white highlight with blue mid-band
                var blueberrySheen = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
                blueberrySheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#2E7DD3FC"), 0.0));
                blueberrySheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#16FFFFFF"), 0.38));
                blueberrySheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00FFFFFF"), 0.44));
                blueberrySheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0C60A5FA"), 1.0));
                SetResource("GlassSheenBrush", blueberrySheen);
                SetResource("ThemePointerGlowCenterColor", (Color)ColorConverter.ConvertFromString("#287DD3FC"));
                SetResource("ThemePointerGlowMidColor", (Color)ColorConverter.ConvertFromString("#3060A5FA"));
                SetResource("ThemePointerClickColor", (Color)ColorConverter.ConvertFromString("#B060A5FA"));
            }
            else if (isMintTheme)
            {
                SetBrush("AppBackgroundBrush", "#071510");
                SetBrush("SidebarBrush", "#05100C");
                SetBrush("ThemePanelEdgeBrush", "#246E5B");
                SetBrush("ThemeDeckFrameBorderBrush", "#2DB880");
                SetResource("ThemePanelFillBrush", BuildGlassBackingBrush("#CC081713", "#A00E211D"));
                SetResource("ThemePanelFillSoftBrush", BuildGlassBackingBrush("#B50E211D", "#80183831"));
                SetResource("ThemePanelShadowColor", (Color)ColorConverter.ConvertFromString("#2DB880"));
                SetResource("ThemePrimaryButtonGlowColor", (Color)ColorConverter.ConvertFromString("#2DB880"));
                SetResource("ThemePanelBorderGradientBrush", BuildGlassBorderBrush("#2DB880"));
                SetResource("ThemeAtmosphereBlobBrush", new SolidColorBrush(Color.FromArgb(34, 45, 184, 128)));
                SetResource("ThemeAtmosphereBlobSecondaryBrush", new SolidColorBrush(Color.FromArgb(24, 13, 52, 42)));
                SetResource("ThemePrimaryButtonFillBrush", BuildButtonGradient("#2DB880", "#0F764F"));
                SetResource("ThemePrimaryButtonHoverFillBrush", BuildButtonGradient("#52D5A0", "#159565"));
                SetResource("ThemePrimaryButtonPressedFillBrush", BuildButtonGradient("#159565", "#064E3B"));
                SetBrush("ThemePrimaryButtonTextBrush", "#02130E");
                SetBrush("ThemeNavItemBackgroundBrush", "#0A1F1A");
                SetBrush("ThemeNavItemHoverBackgroundBrush", "#14372F");
                SetBrush("ThemeNavItemSelectedBackgroundBrush", "#1A463B");
                SetBrush("ThemeNavItemBorderBrush", "#1C4E41");
                SetBrush("ThemeNavItemHoverBorderBrush", "#2DB880");
                SetBrush("ThemeNavItemSelectedBorderBrush", "#9DE7C9");
                SetBrush("ThemePillFillBrush", "#0A1E1A");
                SetBrush("ThemePillHoverFillBrush", "#14372F");
                SetBrush("ThemePillSelectedFillBrush", "#1A463B");
                SetBrush("ThemePillBorderBrush", "#1C4E41");
                SetBrush("ThemePillHoverBorderBrush", "#2DB880");
                SetBrush("ThemePillSelectedBorderBrush", "#9DE7C9");
                SetBrush("ThemePillLabelBrush", "#5E8E81");
                SetBrush("ThemePillLabelActiveBrush", "#9DE7C9");
                SetResource("AuroraVeilBrush", BuildLinearBrush("#242DB880", "#184FD1A5", "#102AC79A", diagonal: true));
                SetResource("GlitchNeonBrush", BuildLinearBrush("#AA2DB880", "#887EE7C2", "#AA27D1A5"));
                SetResource("MatrixRainBrush", new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)));
                SetResource("HeroOverlayBrush", BuildVerticalBrush("#CC081713", "#E20E211D", "#F4081713"));
                SetResource("NostalgiaAtmosphereBrush", BuildLinearBrush("#202DB880", "#144FD1A5", "#102AC79A", diagonal: true));
                SetResource("ThemeShellAtmosphereBrush", BuildLinearBrush("#180E211D", "#0E183831", "#06081713", diagonal: true));
                SetResource("ThemeShellPatternBrush", BuildCadenceLineBrush("#142DB880", "#0E9DE7C9"));
                SetResource("ThemeSidebarOverlayBrush", BuildVerticalBrush("#4205100C", "#1505100C", "#0005100C"));
                SetResource("ThemeSidebarPatternBrush", BuildCadenceLineBrush("#162DB880", "#0E1C4E41"));
                SetResource("ThemeSweepBrush", BuildLinearBrush("#622DB880", "#289DE7C9", "#0027D1A5"));
                SetResource("ThemeShellPatternOpacity", 0.05d);
                SetResource("ThemeSidebarPatternOpacity", 0.05d);
                SetResource("ThemeSweepOpacity", 0.04d);

                // Mint emerald glass sheen — cool teal highlight with green mid-band
                var mintSheen = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
                mintSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#289DE7C9"), 0.0));
                mintSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#14FFFFFF"), 0.38));
                mintSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00FFFFFF"), 0.44));
                mintSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0A2DB880"), 1.0));
                SetResource("GlassSheenBrush", mintSheen);
                SetResource("ThemePointerGlowCenterColor", (Color)ColorConverter.ConvertFromString("#269DE7C9"));
                SetResource("ThemePointerGlowMidColor", (Color)ColorConverter.ConvertFromString("#2C2DB880"));
                SetResource("ThemePointerClickColor", (Color)ColorConverter.ConvertFromString("#AA2DB880"));
            }
            else if (isHighContrastTheme)
            {
                SetBrush("AppBackgroundBrush", "#000000");
                SetBrush("SidebarBrush", "#000000");
                SetResource("ThemeCornerRadiusControl", new CornerRadius(0));
                SetResource("ThemeCornerRadiusPanel", new CornerRadius(0));
                SetResource("ThemeCornerRadiusLarge", new CornerRadius(0));
                SetResource("ThemeCornerRadiusPill", new CornerRadius(0));
                SetBrush("BorderBrush", "#FFFFFF");
                SetBrush("BorderSubtleBrush", "#7A7A7A");
                SetBrush("TextSecondaryBrush", "#E6E6E6");
                SetBrush("TextMutedBrush", "#B8B8B8");
                SetResource("ThemePanelFillBrush", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0000000")));
                SetResource("ThemePanelFillSoftBrush", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0000000")));
                SetBrush("ThemePanelEdgeBrush", "#FFFFFF");
                SetBrush("ThemePrimaryButtonTextBrush", "#000000");
                SetResource("ThemePrimaryButtonFillBrush", new SolidColorBrush(Colors.White));
                SetResource("ThemePrimaryButtonHoverFillBrush", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DADADA")));
                SetResource("ThemePrimaryButtonPressedFillBrush", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AFAFAF")));
                SetBrush("ThemeNavItemBackgroundBrush", "#000000");
                SetBrush("ThemeNavItemHoverBackgroundBrush", "#222222");
                SetBrush("ThemeNavItemSelectedBackgroundBrush", "#FFFFFF");
                SetBrush("ThemeNavItemBorderBrush", "#7A7A7A");
                SetBrush("ThemeNavItemHoverBorderBrush", "#FFFFFF");
                SetBrush("ThemeNavItemSelectedBorderBrush", "#FFFFFF");
                SetBrush("ThemePillFillBrush", "#000000");
                SetBrush("ThemePillHoverFillBrush", "#222222");
                SetBrush("ThemePillSelectedFillBrush", "#FFFFFF");
                SetBrush("ThemePillBorderBrush", "#7A7A7A");
                SetBrush("ThemePillHoverBorderBrush", "#FFFFFF");
                SetBrush("ThemePillSelectedBorderBrush", "#FFFFFF");
                SetBrush("ThemePillLabelBrush", "#E6E6E6");
                SetBrush("ThemePillLabelActiveBrush", "#000000");
                SetResource("ThemePanelShadowColor", Colors.Transparent);
                SetResource("ThemePrimaryButtonGlowColor", Colors.Transparent);
                SetResource("ThemePanelBorderGradientBrush", new SolidColorBrush(Colors.White));
                SetResource("ThemeAtmosphereBlobBrush", new SolidColorBrush(Colors.Transparent));
                SetResource("ThemeAtmosphereBlobSecondaryBrush", new SolidColorBrush(Colors.Transparent));
                SetResource("AuroraVeilBrush", BuildLinearBrush("#00000000", "#00000000", "#00000000"));
                SetResource("GlitchNeonBrush", BuildLinearBrush("#66FFFFFF", "#44FFFFFF", "#66FFFFFF"));
                SetResource("MatrixRainBrush", new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)));
                SetResource("HeroOverlayBrush", BuildVerticalBrush("#F0000000", "#F0000000", "#F0000000"));
                SetResource("NostalgiaAtmosphereBrush", BuildLinearBrush("#00000000", "#00000000", "#00000000"));
                SetResource("ThemeShellAtmosphereBrush", new SolidColorBrush(Colors.Transparent));
                SetResource("ThemeShellPatternBrush", new SolidColorBrush(Colors.Transparent));
                SetResource("ThemeSidebarOverlayBrush", BuildVerticalBrush("#18000000", "#08000000", "#00000000"));
                SetResource("ThemeSidebarPatternBrush", new SolidColorBrush(Colors.Transparent));
                SetResource("ThemeSweepBrush", new SolidColorBrush(Colors.White));
                SetResource("ThemeShellPatternOpacity", 0d);
                SetResource("ThemeSidebarPatternOpacity", 0d);
                SetResource("ThemeSweepOpacity", 0.02d);

                // High Contrast: pure white sheen for maximum clarity
                var hcSheen = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
                hcSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#40FFFFFF"), 0.0));
                hcSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#10FFFFFF"), 0.44));
                hcSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00FFFFFF"), 0.45));
                hcSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00000000"), 1.0));
                SetResource("GlassSheenBrush", hcSheen);
            }
            else if (isMatrixTheme)
            {
                SetBrush("AppBackgroundBrush", "#030B07");
                SetBrush("SidebarBrush", "#020805");
                SetBrush("ThemePanelEdgeBrush", "#25784A");
                SetBrush("ThemeDeckFrameBorderBrush", "#46FF8A");
                SetResource("ThemePanelFillBrush", BuildGlassBackingBrush("#CC040F0B", "#A00A1812"));
                SetResource("ThemePanelFillSoftBrush", BuildGlassBackingBrush("#B50A1812", "#8013261E"));
                SetResource("ThemePanelShadowColor", (Color)ColorConverter.ConvertFromString("#46FF8A"));
                SetResource("ThemePrimaryButtonGlowColor", (Color)ColorConverter.ConvertFromString("#46FF8A"));
                SetResource("ThemePanelBorderGradientBrush", BuildGlassBorderBrush("#46FF8A"));
                SetResource("ThemeAtmosphereBlobBrush", new SolidColorBrush(Color.FromArgb(32, 70, 255, 138)));
                SetResource("ThemeAtmosphereBlobSecondaryBrush", new SolidColorBrush(Color.FromArgb(21, 13, 62, 36)));
                SetResource("ThemePrimaryButtonFillBrush", BuildButtonGradient("#46FF8A", "#0D9A4C"));
                SetResource("ThemePrimaryButtonHoverFillBrush", BuildButtonGradient("#80FFB0", "#20CC65"));
                SetResource("ThemePrimaryButtonPressedFillBrush", BuildButtonGradient("#20CC65", "#056630"));
                SetBrush("ThemePrimaryButtonTextBrush", "#011007");
                SetBrush("ThemeNavItemBackgroundBrush", "#05140F");
                SetBrush("ThemeNavItemHoverBackgroundBrush", "#0A281E");
                SetBrush("ThemeNavItemSelectedBackgroundBrush", "#103C2D");
                SetBrush("ThemeNavItemBorderBrush", "#124231");
                SetBrush("ThemeNavItemHoverBorderBrush", "#46FF8A");
                SetBrush("ThemeNavItemSelectedBorderBrush", "#46FF8A");
                SetBrush("ThemePillFillBrush", "#05130E");
                SetBrush("ThemePillHoverFillBrush", "#0A281F");
                SetBrush("ThemePillSelectedFillBrush", "#103C2D");
                SetBrush("ThemePillBorderBrush", "#124231");
                SetBrush("ThemePillHoverBorderBrush", "#46FF8A");
                SetBrush("ThemePillSelectedBorderBrush", "#46FF8A");
                SetBrush("ThemePillLabelBrush", "#25784A");
                SetBrush("ThemePillLabelActiveBrush", "#D8FFE9");
                SetResource("AuroraVeilBrush", BuildLinearBrush("#1834FF8A", "#1012E66E", "#0811FFB0", diagonal: true));
                SetResource("GlitchNeonBrush", BuildLinearBrush("#CC46FF8A", "#AA00F0FF", "#CC9AFFC9"));
                SetResource("BladeActiveBrush", BuildLinearBrush("#8046FF8A", "#502DB880", "#5000F0FF"));
                SetResource("HeroOverlayBrush", BuildVerticalBrush("#D8040F0B", "#E60A1812", "#F3040F0B"));
                SetResource("NostalgiaAtmosphereBrush", BuildLinearBrush("#2334FF8A", "#1200E676", "#0C9AFFC9", diagonal: true));

                // Neon Matrix phosphor green terminal glass sheen
                var matrixSheen = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1)
                };
                matrixSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#3A46FF8A"), 0.0));
                matrixSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0CFFFFFF"), 0.44));
                matrixSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00FFFFFF"), 0.45));
                matrixSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1500F0FF"), 1.0));
                SetResource("GlassSheenBrush", matrixSheen);

                // The live ThemeEffectLayer owns the motion. These frozen brushes add cheap depth
                // without starting extra render clocks on the shell and sidebar.
                var matrixRainBrush = BuildMatrixRainBrush();

                SetResource("MatrixRainBrush", matrixRainBrush);
                SetResource("ThemeShellAtmosphereBrush", BuildLinearBrush("#16040F0B", "#0A0A1812", "#00030B07", diagonal: true));
                SetResource("ThemeShellPatternBrush", matrixRainBrush);
                SetResource("ThemeSidebarOverlayBrush", BuildVerticalBrush("#30020805", "#12020805", "#00020805"));
                SetResource("ThemeSidebarPatternBrush", matrixRainBrush);
                SetResource("ThemeSweepBrush", BuildLinearBrush("#7846FF8A", "#3600F0FF", "#0046FF8A"));
                SetResource("ThemeShellPatternOpacity", 0.13d);
                SetResource("ThemeSidebarPatternOpacity", 0.15d);
                SetResource("ThemeSweepOpacity", 0.075d);
                SetResource("ThemeRainOverlayOpacity", 0.085d);
                SetResource("ThemeSweepOverlayOpacity", 0.085d);
                SetResource("ThemeShellOverlayOpacity", 0.7d);
                SetResource("ThemePointerGlowCenterColor", (Color)ColorConverter.ConvertFromString("#2646FF8A"));
                SetResource("ThemePointerGlowMidColor", (Color)ColorConverter.ConvertFromString("#3A46FF8A"));
                SetResource("ThemePointerClickColor", (Color)ColorConverter.ConvertFromString("#CC46FF8A"));
            }
            else if (isGlitchTheme)
            {
                SetBrush("AppBackgroundBrush", "#0B0715");
                SetBrush("SidebarBrush", "#080511");
                SetBrush("ThemePanelEdgeBrush", "#744D9B");
                SetBrush("ThemeDeckFrameBorderBrush", "#FF4FD0");
                SetResource("ThemePanelFillBrush", BuildGlassBackingBrush("#CC0E0A1B", "#A0171128"));
                SetResource("ThemePanelFillSoftBrush", BuildGlassBackingBrush("#B5171128", "#80231A3B"));
                SetResource("ThemePanelShadowColor", (Color)ColorConverter.ConvertFromString("#FF4FD0"));
                SetResource("ThemePrimaryButtonGlowColor", (Color)ColorConverter.ConvertFromString("#FF4FD0"));
                SetResource("ThemePanelBorderGradientBrush", BuildGlassBorderBrush("#FF4FD0"));
                SetResource("ThemeAtmosphereBlobBrush", new SolidColorBrush(Color.FromArgb(40, 255, 79, 208)));
                SetResource("ThemeAtmosphereBlobSecondaryBrush", new SolidColorBrush(Color.FromArgb(32, 88, 183, 255)));
                SetResource("ThemePrimaryButtonFillBrush", BuildButtonGradient("#FF4FD0", "#00F0FF"));
                SetResource("ThemePrimaryButtonHoverFillBrush", BuildButtonGradient("#FF80DD", "#58B7FF"));
                SetResource("ThemePrimaryButtonPressedFillBrush", BuildButtonGradient("#D936A8", "#00A8B5"));
                SetBrush("ThemePrimaryButtonTextBrush", "#120416");
                SetBrush("ThemeNavItemBackgroundBrush", "#140F24");
                SetBrush("ThemeNavItemHoverBackgroundBrush", "#251B45");
                SetBrush("ThemeNavItemSelectedBackgroundBrush", "#352760");
                SetBrush("ThemeNavItemBorderBrush", "#3A2B68");
                SetBrush("ThemeNavItemHoverBorderBrush", "#FF4FD0");
                SetBrush("ThemeNavItemSelectedBorderBrush", "#FF4FD0");
                SetBrush("ThemePillFillBrush", "#140E24");
                SetBrush("ThemePillHoverFillBrush", "#251B45");
                SetBrush("ThemePillSelectedFillBrush", "#352760");
                SetBrush("ThemePillBorderBrush", "#3A2B68");
                SetBrush("ThemePillHoverBorderBrush", "#FF4FD0");
                SetBrush("ThemePillSelectedBorderBrush", "#FF4FD0");
                SetBrush("ThemePillLabelBrush", "#744D9B");
                SetBrush("ThemePillLabelActiveBrush", "#EFE6FF");
                SetResource("AuroraVeilBrush", BuildLinearBrush("#25FF4FD0", "#1B58B7FF", "#16FFE45F", diagonal: true));
                SetResource("GlitchNeonBrush", BuildLinearBrush("#CCFF4FD0", "#AA58B7FF", "#CCFFE45F"));
                SetResource("BladeActiveBrush", BuildLinearBrush("#80FF4FD0", "#5058B7FF", "#50FFE45F"));
                SetResource("HeroOverlayBrush", BuildVerticalBrush("#DA0E0A1B", "#E8171128", "#F40E0A1B"));
                SetResource("NostalgiaAtmosphereBrush", BuildLinearBrush("#24FF4FD0", "#1858B7FF", "#10FFE45F", diagonal: true));
                SetResource("ThemePointerGlowCenterColor", (Color)ColorConverter.ConvertFromString("#2DFFFFFF"));
                SetResource("ThemePointerGlowMidColor", (Color)ColorConverter.ConvertFromString("#36FF4FD0"));
                SetResource("ThemePointerClickColor", (Color)ColorConverter.ConvertFromString("#CCFF4FD0"));

                // Retro Glitch cyberpunk hot pink-to-cyan outrun glass sheen
                var glitchSheen = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1)
                };
                glitchSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#33FF4FD0"), 0.0));
                glitchSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0CFFFFFF"), 0.44));
                glitchSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00FFFFFF"), 0.45));
                glitchSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1158B7FF"), 1.0));
                SetResource("GlassSheenBrush", glitchSheen);

                // Retro Glitch animated neon landscape grid scrolling vertically
                var gridBrush = BuildSynthwaveGridBrush();
                var trans = new TranslateTransform();
                var animY = new DoubleAnimation 
                { 
                    From = 0, 
                    To = 40, 
                    Duration = TimeSpan.FromSeconds(4), 
                    RepeatBehavior = RepeatBehavior.Forever 
                };
                trans.BeginAnimation(TranslateTransform.YProperty, animY);
                gridBrush.Transform = trans;
                SetResource("MatrixRainBrush", gridBrush);
                SetResource("ThemeShellAtmosphereBrush", BuildLinearBrush("#180E0A1B", "#10171128", "#080A0715", diagonal: true));
                SetResource("ThemeShellPatternBrush", gridBrush);
                SetResource("ThemeSidebarOverlayBrush", BuildVerticalBrush("#30080511", "#14080511", "#00080511"));
                SetResource("ThemeSidebarPatternBrush", BuildCadenceLineBrush("#18FF4FD0", "#1458B7FF"));
                SetResource("ThemeSweepBrush", BuildLinearBrush("#76FF4FD0", "#3458B7FF", "#00FFE45F"));
                SetResource("ThemeShellPatternOpacity", 0.085d);
                SetResource("ThemeSidebarPatternOpacity", 0.065d);
                SetResource("ThemeSweepOpacity", 0.055d);
            }
            else if (isBlackFlashTheme)
            {
                // ── Core surfaces — Yuji-black with red impact depth ──────────────────
                SetBrush("AppBackgroundBrush",         "#030507");
                SetBrush("SidebarBrush",               "#010203");
                SetBrush("SurfaceBrush",               "#C0080506");
                SetBrush("SurfaceElevatedBrush",       "#D00F0608");
                SetBrush("SurfaceHighBrush",           "#DD1A090D");
                SetBrush("CardBrush",                  "#C80A0506");
                SetBrush("CardHoverBrush",             "#D916080B");
                SetBrush("CardSelectedBrush",          "#EA26070D");

                // ── Text — near-white with lavender tint ─────────────────────────────
                SetBrush("TextPrimaryBrush",           "#FFF6F6");
                SetBrush("TextSecondaryBrush",         "#F2B6B9");
                SetBrush("TextMutedBrush",             "#9F5B60");
                SetBrush("TextOnDarkBrush",            "#FFF6F6");

                // ── Borders — deep cursed-energy violet ──────────────────────────────
                SetBrush("BorderBrush",                "#5E1218");
                SetBrush("BorderSubtleBrush",          "#2A080B");
                SetBrush("GlassPanelBorderBrush",      "#A81724");
                SetBrush("GlassPanelHoverBorderBrush", "#FF4755");

                // ── Accent — Black Flash red / impact white ──────────────────────────
                SetBrush("BerryBrush",                 "#E51B2D");
                SetBrush("BerryHoverBrush",            "#FF4252");
                SetBrush("BerryPressedBrush",          "#A80012");
                SetBrush("BerryDimBrush",              "#260508");
                SetBrush("BerryMutedBrush",            "#180305");

                // ── Secondary accents ─────────────────────────────────────────────────
                SetBrush("CloudBrush",                 "#FFCDD2");
                SetBrush("CloudDimBrush",              "#2A0508");
                SetBrush("MintBrush",                  "#FF5A66");
                SetBrush("MintDimBrush",               "#230407");
                SetResource("MintGlowColor", (Color)ColorConverter.ConvertFromString("#FF5A66")); // A2: theme-safe glow to match MintBrush
                SetBrush("GoldBrush",                  "#FFF1F2");
                SetBrush("GoldDimBrush",               "#2F080C");
                SetBrush("NeoCyanBrush",               "#FFB3B8");
                SetBrush("NeoBerryBrush",              "#C40018");
                SetBrush("WarningBrush",               "#FFB199");

                // ── Errors ────────────────────────────────────────────────────────────
                SetBrush("ErrorBrush",                 "#CC2040");
                SetBrush("ErrorHoverBrush",            "#E02855");
                SetBrush("ErrorDimBrush",              "#1A0410");

                // ── Panels ────────────────────────────────────────────────────────────
                SetResource("ThemePanelFillBrush",     BuildGlassBackingBrush("#D0080506", "#AA17070A"));
                SetResource("ThemePanelFillSoftBrush", BuildGlassBackingBrush("#B0120608", "#801F070B"));
                SetBrush("ThemePanelEdgeBrush",        "#A81724");
                SetBrush("ThemePanelShadowBrush",      "#010103");
                SetResource("ThemePanelShadowColor",   (Color)ColorConverter.ConvertFromString("#010103"));
                SetResource("ThemePrimaryButtonGlowColor", (Color)ColorConverter.ConvertFromString("#E51B2D"));
                SetResource("ThemePanelBorderGradientBrush", BuildGlassBorderBrush("#E51B2D"));

                // ── Atmosphere blobs — red impact radial halos ───────────────────────
                SetResource("ThemeAtmosphereBlobBrush",          new SolidColorBrush(Color.FromArgb(64, 229, 27, 45)));
                SetResource("ThemeAtmosphereBlobSecondaryBrush", new SolidColorBrush(Color.FromArgb(34, 255, 246, 246)));

                // ── Deck frame ────────────────────────────────────────────────────────
                SetBrush("ThemeDeckFrameBrush",        "#CC080506");
                SetBrush("ThemeDeckFrameBorderBrush",  "#E51B2D");

                // ── Nav items ─────────────────────────────────────────────────────────
                SetBrush("ThemeNavItemBackgroundBrush",         "#0A0506");
                SetBrush("ThemeNavItemHoverBackgroundBrush",    "#1A070A");
                SetBrush("ThemeNavItemSelectedBackgroundBrush", "#2A070D");
                SetBrush("ThemeNavItemBorderBrush",             "#4A0E14");
                SetBrush("ThemeNavItemHoverBorderBrush",        "#E51B2D");
                SetBrush("ThemeNavItemSelectedBorderBrush",     "#FF4252");
                SetBrush("ThemeNavItemTopEdgeBrush",            "#44FFF6F6");

                // ── Pills ─────────────────────────────────────────────────────────────
                SetBrush("ThemePillFillBrush",           "#0A0506");
                SetBrush("ThemePillHoverFillBrush",      "#1A070A");
                SetBrush("ThemePillSelectedFillBrush",   "#2A070D");
                SetBrush("ThemePillBorderBrush",         "#4A0E14");
                SetBrush("ThemePillHoverBorderBrush",    "#E51B2D");
                SetBrush("ThemePillSelectedBorderBrush", "#FF4252");
                SetBrush("ThemePillLabelBrush",          "#9F5B60");
                SetBrush("ThemePillLabelActiveBrush",    "#FFF6F6");

                // ── Buttons ───────────────────────────────────────────────────────────
                SetResource("ThemePrimaryButtonFillBrush",        BuildButtonGradient("#E51B2D", "#6F000B"));
                SetResource("ThemePrimaryButtonHoverFillBrush",   BuildButtonGradient("#FF4252", "#A80012"));
                SetResource("ThemePrimaryButtonPressedFillBrush", BuildButtonGradient("#A80012", "#4E0007"));
                SetBrush("ThemePrimaryButtonTextBrush",           "#FFF6F6");
                SetBrush("ThemeSecondaryButtonTextBrush",         "#FFCDD2");
                SetBrush("ThemeSecondaryButtonFillBrush",         "#1808050A");
                SetBrush("ThemeSecondaryButtonHoverFillBrush",    "#2611070A");
                SetBrush("ThemeSecondaryButtonPressedFillBrush",  "#10050406");
                SetBrush("ThemeSecondaryButtonBorderBrush",       "#6F1018");
                SetBrush("ThemeGhostButtonHoverFillBrush",        "#1A070A");
                SetBrush("ThemeGhostButtonPressedFillBrush",      "#100405");
                SetBrush("ThemeGhostButtonBorderBrush",           "#4A0E14");
                SetBrush("ThemeGhostButtonHoverBorderBrush",      "#E51B2D");

                // ── Shell overlays ────────────────────────────────────────────────────
                SetResource("HeroOverlayBrush",           BuildVerticalBrush("#22030507", "#6A030507", "#A0030507"));
                SetResource("NostalgiaAtmosphereBrush",   BuildLinearBrush("#24E51B2D", "#12000000", "#18FFF6F6", diagonal: true));
                SetResource("AuroraVeilBrush",            BuildLinearBrush("#26E51B2D", "#12000000", "#08000000"));
                SetResource("GlitchNeonBrush",            BuildLinearBrush("#CCE51B2D", "#AAFFF6F6", "#CC6F000B"));
                SetResource("BladeGlassBrush",            BuildLinearBrush("#180506", "#080203", "#180506", diagonal: true));
                SetResource("BladeActiveBrush",           BuildLinearBrush("#88E51B2D", "#58FFF6F6", "#806F000B"));
                SetResource("MatrixRainBrush",            new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)));

                // ── Shell atmosphere / pattern / sweep ────────────────────────────────
                SetResource("ThemeShellAtmosphereBrush",  BuildVerticalBrush("#24E51B2D", "#10000000", "#00000000"));
                SetResource("ThemeShellPatternBrush",     new SolidColorBrush(Colors.Transparent));
                SetResource("ThemeSidebarOverlayBrush",   BuildVerticalBrush("#40010203", "#18010203", "#00010203"));
                SetResource("ThemeSidebarPatternBrush",   BuildCadenceLineBrush("#24E51B2D", "#12FFF6F6"));
                SetResource("ThemeSweepBrush",            BuildLinearBrush("#88E51B2D", "#44FFF6F6", "#00E51B2D"));
                SetResource("ThemeShellPatternOpacity",   0.028d);
                SetResource("ThemeSidebarPatternOpacity", 0.038d);
                SetResource("ThemeSweepOpacity",          0.048d);
                SetResource("ThemeShellOverlayOpacity",   0.58d);

                // ── Pointer / glow ────────────────────────────────────────────────────
                SetResource("ThemePointerGlowCenterColor", (Color)ColorConverter.ConvertFromString("#34FFF6F6"));
                SetResource("ThemePointerGlowMidColor",    (Color)ColorConverter.ConvertFromString("#44E51B2D"));
                SetResource("ThemePointerClickColor",      (Color)ColorConverter.ConvertFromString("#CCE51B2D"));

                // ── Glass sheen — impact-white to blood-red top reflection ───────────
                var bfSheen = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
                bfSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#38FFFFFF"), 0.0));
                bfSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#18E51B2D"), 0.36));
                bfSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00FFFFFF"), 0.44));
                bfSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00000000"), 0.45));
                bfSheen.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0A6F000B"), 1.0));
                SetResource("GlassSheenBrush", bfSheen);

                // ── NostalgiaScanlineBrush — subtle cursed-energy cross-hatch ────────
                SetResource("NostalgiaScanlineBrush", BuildCadenceLineBrush("#18E51B2D", "#08FFF6F6"));
            }
        }

        ApplyGlassControlPalette(themeKey);
        ApplyProfileVisualPack(profileThemeKey, themeKey);
    }

    private static void ApplyGlassControlPalette(string themeKey)
    {
        var palette = themeKey switch
        {
            "NieR Automata" => ("#72F3EBD9", "#28D7C27C", "#1427231D", "#96FFF6E4", "#36D7C27C", "#1D373229", "#B8FFF8E8", "#7DD7C27C", "#405E5548"),
            "Wuthering Waves" => ("#6872F6E8", "#245DA6FF", "#14071320", "#8AFFFFFF", "#3472F6E8", "#20153A50", "#BCFFFFFF", "#8072F6E8", "#38D9C98A"),
            "Neon Matrix" => ("#5A46FF8A", "#2400F0FF", "#10020805", "#7846FF8A", "#3400F0FF", "#18020805", "#A846FF8A", "#6600F0FF", "#2646FF8A"),
            "Retro Glitch" => ("#58FF4FD0", "#2458B7FF", "#12080511", "#78FF80DD", "#3058B7FF", "#1C0E0A1B", "#AAFF4FD0", "#7058B7FF", "#36FFE45F"),
            "Blueberry Midnight" => ("#5860A5FA", "#227DD3FC", "#12060913", "#787DD3FC", "#3060A5FA", "#1C030915", "#AAFFFFFF", "#7260A5FA", "#387DD3FC"),
            "Mint Cloud" => ("#5A9DE7C9", "#242DB880", "#1006110F", "#789DE7C9", "#342DB880", "#18081713", "#AAE8FFF5", "#682DB880", "#389DE7C9"),
            "High Contrast" => ("#18FFFFFF", "#0CFFFFFF", "#00000000", "#30FFFFFF", "#18FFFFFF", "#05000000", "#FFFFFFFF", "#FFFFFFFF", "#FFFFFFFF"),
            "Black Flash" => ("#68E51B2D", "#28FFF6F6", "#18010203", "#88FF4252", "#34E51B2D", "#2218080A", "#AAFFF6F6", "#78E51B2D", "#40FFF6F6"),
            _ => ("#5EFFFFFF", "#2412D7FF", "#16000000", "#82FFFFFF", "#3220DFFF", "#1E000000", "#B8FFFFFF", "#5C8EDBFF", "#30FFFFFF")
        };

        SetResource("GlassButtonFillBrush", BuildLinearBrush(palette.Item1, palette.Item2, palette.Item3, diagonal: true));
        SetResource("GlassButtonHoverFillBrush", BuildLinearBrush(palette.Item4, palette.Item5, palette.Item6, diagonal: true));
        SetResource("GlassButtonPressedFillBrush", BuildLinearBrush("#36FFFFFF", palette.Item2, "#2A000000", diagonal: true));
        SetResource("GlassButtonBorderBrush", BuildLinearBrush(palette.Item7, palette.Item8, palette.Item9, diagonal: true));
        SetResource("GlassChipFillBrush", BuildLinearBrush(WithAlpha(palette.Item1, "34"), WithAlpha(palette.Item2, "18"), WithAlpha(palette.Item3, "10"), diagonal: true));
        SetResource("GlassChipActiveFillBrush", BuildLinearBrush(WithAlpha(palette.Item4, "58"), WithAlpha(palette.Item5, "26"), WithAlpha(palette.Item6, "18"), diagonal: true));
        SetResource("GlassChipBorderBrush", BuildLinearBrush(WithAlpha(palette.Item7, "8F"), WithAlpha(palette.Item8, "58"), WithAlpha(palette.Item9, "2A"), diagonal: true));
    }

    private static string WithAlpha(string color, string alpha)
    {
        var trimmed = color.TrimStart('#');
        return trimmed.Length == 8
            ? $"#{alpha}{trimmed[2..]}"
            : $"#{alpha}{trimmed}";
    }

    private static void ApplyProfileVisualPack(string profileThemeKey, string shellThemeKey)
    {
        SetResource("ProfileSurfaceAtmosphereBrush", BuildLinearBrush("#1CFFFFFF", "#0AFFFFFF", "#00000000", diagonal: true));
        SetResource("ProfileSurfacePatternBrush", new SolidColorBrush(Colors.Transparent));
        SetResource("ProfileSurfaceSweepBrush", BuildLinearBrush("#4CFFFFFF", "#12FFFFFF", "#00FFFFFF"));
        SetResource("ProfileCardBaseBrush", BuildLinearBrush("#E7151A23", "#BE243044", "#D5090B10", diagonal: true));
        SetResource("ProfileScoreBandBrush", BuildLinearBrush("#1FFFFFFF", "#3312D7FF", "#11FFFFFF"));
        SetResource("ProfileAvatarTileBrush", BuildLinearBrush("#36FFFFFF", "#2212D7FF", "#12000000", diagonal: true));
        SetBrush("ProfileBadgeFillBrush", "#18000000");
        SetBrush("ProfileBadgeBorderBrush", "#40FFFFFF");
        SetBrush("ProfileBadgeGlowBrush", "#55FFFFFF");
        SetResource("ProfileSurfaceAtmosphereOpacity", 0.74d);
        SetResource("ProfileSurfacePatternOpacity", 0.10d);
        SetResource("ProfileSurfaceSweepOpacity", 0.14d);
        SetResource("ProfileBadgePulseOpacity", 0.16d);

        switch (profileThemeKey)
        {
            case "Neon Matrix":
                SetResource("ProfileSurfaceAtmosphereBrush", BuildLinearBrush("#1A0D2D17", "#0E071A10", "#00040F0B", diagonal: true));
                SetResource("ProfileSurfacePatternBrush", BuildMatrixRainBrush());
                SetResource("ProfileSurfaceSweepBrush", BuildLinearBrush("#8846FF8A", "#2800F0FF", "#0046FF8A"));
                SetResource("ProfileCardBaseBrush", BuildLinearBrush("#E604160C", "#B80A281E", "#E0020805", diagonal: true));
                SetResource("ProfileScoreBandBrush", BuildLinearBrush("#2420CC65", "#1834FF8A", "#0800F0FF"));
                SetResource("ProfileAvatarTileBrush", BuildLinearBrush("#3434FF8A", "#1600F0FF", "#06020805", diagonal: true));
                SetBrush("ProfileBadgeFillBrush", "#12051108");
                SetBrush("ProfileBadgeBorderBrush", "#5846FF8A");
                SetBrush("ProfileBadgeGlowBrush", "#6646FF8A");
                SetResource("ProfileSurfaceAtmosphereOpacity", 0.86d);
                SetResource("ProfileSurfacePatternOpacity", 0.26d);
                SetResource("ProfileSurfaceSweepOpacity", 0.18d);
                SetResource("ProfileBadgePulseOpacity", 0.22d);
                break;
            case "NieR Automata":
                SetResource("ProfileSurfaceAtmosphereBrush", BuildVerticalBrush("#24EEE4CF", "#10D8D0BF", "#00D8D0BF"));
                SetResource("ProfileSurfacePatternBrush", BuildNierDotGridBrush());
                SetResource("ProfileSurfaceSweepBrush", BuildLinearBrush("#406E6452", "#166E6452", "#006E6452"));
                SetResource("ProfileCardBaseBrush", BuildLinearBrush("#F4EEE4CF", "#E8D8D0BF", "#DCC8BFAE", diagonal: true));
                SetResource("ProfileScoreBandBrush", BuildLinearBrush("#32F9F0DC", "#20B9AD8B", "#08F3EBDA"));
                SetResource("ProfileAvatarTileBrush", BuildLinearBrush("#50F3EBDA", "#24C8B98E", "#10F3EBDA", diagonal: true));
                SetBrush("ProfileBadgeFillBrush", "#24F3EBDA");
                SetBrush("ProfileBadgeBorderBrush", "#726E6452");
                SetBrush("ProfileBadgeGlowBrush", "#4C8A7E66");
                SetResource("ProfileSurfaceAtmosphereOpacity", 0.72d);
                SetResource("ProfileSurfacePatternOpacity", 0.12d);
                SetResource("ProfileSurfaceSweepOpacity", 0.08d);
                SetResource("ProfileBadgePulseOpacity", 0.11d);
                break;
            case "Wuthering Waves":
                SetResource("ProfileSurfaceAtmosphereBrush", BuildLinearBrush("#2A0F2B39", "#163A5C76", "#06101E2A", diagonal: true));
                SetResource("ProfileSurfacePatternBrush", BuildWaveLineBrush());
                SetResource("ProfileSurfaceSweepBrush", BuildLinearBrush("#78D9C98A", "#4472F6E8", "#0065B5FF"));
                SetResource("ProfileCardBaseBrush", BuildLinearBrush("#E20A1B28", "#B7163950", "#DC07111A", diagonal: true));
                SetResource("ProfileScoreBandBrush", BuildLinearBrush("#2472F6E8", "#185DA6FF", "#10D9C98A"));
                SetResource("ProfileAvatarTileBrush", BuildLinearBrush("#3672F6E8", "#1E5DA6FF", "#10071320", diagonal: true));
                SetBrush("ProfileBadgeFillBrush", "#1A102B3B");
                SetBrush("ProfileBadgeBorderBrush", "#4E72F6E8");
                SetBrush("ProfileBadgeGlowBrush", "#5C72F6E8");
                SetResource("ProfileSurfaceAtmosphereOpacity", 0.84d);
                SetResource("ProfileSurfacePatternOpacity", 0.16d);
                SetResource("ProfileSurfaceSweepOpacity", 0.18d);
                SetResource("ProfileBadgePulseOpacity", 0.20d);
                break;
            case "Retro Glitch":
                SetResource("ProfileSurfaceAtmosphereBrush", BuildLinearBrush("#240E0A1B", "#12171128", "#060A0715", diagonal: true));
                SetResource("ProfileSurfacePatternBrush", BuildSynthwaveGridBrush());
                SetResource("ProfileSurfaceSweepBrush", BuildLinearBrush("#80FF4FD0", "#3A58B7FF", "#00FFE45F"));
                SetResource("ProfileCardBaseBrush", BuildLinearBrush("#E60E0A1B", "#BC231A3B", "#E0080511", diagonal: true));
                SetResource("ProfileScoreBandBrush", BuildLinearBrush("#26FF4FD0", "#1758B7FF", "#10FFE45F"));
                SetResource("ProfileAvatarTileBrush", BuildLinearBrush("#34FF4FD0", "#2058B7FF", "#10080511", diagonal: true));
                SetBrush("ProfileBadgeFillBrush", "#1C170D24");
                SetBrush("ProfileBadgeBorderBrush", "#56FF4FD0");
                SetBrush("ProfileBadgeGlowBrush", "#62FF4FD0");
                SetResource("ProfileSurfaceAtmosphereOpacity", 0.84d);
                SetResource("ProfileSurfacePatternOpacity", 0.15d);
                SetResource("ProfileSurfaceSweepOpacity", 0.19d);
                SetResource("ProfileBadgePulseOpacity", 0.22d);
                break;
            case "Blueberry Midnight":
                SetResource("ProfileSurfaceAtmosphereBrush", BuildLinearBrush("#220C1830", "#10173152", "#0409151F", diagonal: true));
                SetResource("ProfileSurfacePatternBrush", BuildCadenceLineBrush("#1660A5FA", "#0E7DD3FC"));
                SetResource("ProfileSurfaceSweepBrush", BuildLinearBrush("#7260A5FA", "#347DD3FC", "#0038BDF8"));
                SetResource("ProfileCardBaseBrush", BuildLinearBrush("#E707101F", "#B8152A48", "#DC030915", diagonal: true));
                SetResource("ProfileScoreBandBrush", BuildLinearBrush("#2460A5FA", "#167DD3FC", "#0AFFFFFF"));
                SetResource("ProfileAvatarTileBrush", BuildLinearBrush("#3460A5FA", "#181D4ED8", "#08060913", diagonal: true));
                SetBrush("ProfileBadgeFillBrush", "#180E1930");
                SetBrush("ProfileBadgeBorderBrush", "#4860A5FA");
                SetBrush("ProfileBadgeGlowBrush", "#5060A5FA");
                SetResource("ProfileSurfaceAtmosphereOpacity", 0.8d);
                SetResource("ProfileSurfacePatternOpacity", 0.12d);
                SetResource("ProfileSurfaceSweepOpacity", 0.16d);
                SetResource("ProfileBadgePulseOpacity", 0.18d);
                break;
            case "Mint Cloud":
                SetResource("ProfileSurfaceAtmosphereBrush", BuildLinearBrush("#1E0A1A15", "#10183831", "#04081713", diagonal: true));
                SetResource("ProfileSurfacePatternBrush", BuildCadenceLineBrush("#142DB880", "#0E9DE7C9"));
                SetResource("ProfileSurfaceSweepBrush", BuildLinearBrush("#682DB880", "#289DE7C9", "#0027D1A5"));
                SetResource("ProfileCardBaseBrush", BuildLinearBrush("#E7081713", "#B8183831", "#DC06110F", diagonal: true));
                SetResource("ProfileScoreBandBrush", BuildLinearBrush("#242DB880", "#149DE7C9", "#08081713"));
                SetResource("ProfileAvatarTileBrush", BuildLinearBrush("#342DB880", "#1827D1A5", "#0806110F", diagonal: true));
                SetBrush("ProfileBadgeFillBrush", "#140C1F1A");
                SetBrush("ProfileBadgeBorderBrush", "#472DB880");
                SetBrush("ProfileBadgeGlowBrush", "#502DB880");
                SetResource("ProfileSurfaceAtmosphereOpacity", 0.78d);
                SetResource("ProfileSurfacePatternOpacity", 0.12d);
                SetResource("ProfileSurfaceSweepOpacity", 0.15d);
                SetResource("ProfileBadgePulseOpacity", 0.17d);
                break;
            case "High Contrast":
                SetResource("ProfileSurfaceAtmosphereBrush", new SolidColorBrush(Colors.Transparent));
                SetResource("ProfileSurfacePatternBrush", new SolidColorBrush(Colors.Transparent));
                SetResource("ProfileSurfaceSweepBrush", new SolidColorBrush(Colors.White));
                SetResource("ProfileCardBaseBrush", new SolidColorBrush(Colors.Black));
                SetResource("ProfileScoreBandBrush", new SolidColorBrush(Colors.Black));
                SetResource("ProfileAvatarTileBrush", new SolidColorBrush(Colors.Black));
                SetBrush("ProfileBadgeFillBrush", "#000000");
                SetBrush("ProfileBadgeBorderBrush", "#FFFFFF");
                SetBrush("ProfileBadgeGlowBrush", "#40FFFFFF");
                SetResource("ProfileSurfaceAtmosphereOpacity", 0d);
                SetResource("ProfileSurfacePatternOpacity", 0d);
                SetResource("ProfileSurfaceSweepOpacity", 0.05d);
                SetResource("ProfileBadgePulseOpacity", 0.10d);
                break;
            default:
                if (string.Equals(shellThemeKey, "Wuthering Waves", StringComparison.Ordinal))
                {
                    SetResource("ProfileSurfaceAtmosphereBrush", BuildLinearBrush("#220F2B39", "#123A5C76", "#06091520", diagonal: true));
                    SetResource("ProfileSurfacePatternBrush", BuildWaveLineBrush());
                    SetResource("ProfileSurfaceSweepBrush", BuildLinearBrush("#68D9C98A", "#3265B5FF", "#0072F6E8"));
                    SetResource("ProfileCardBaseBrush", BuildLinearBrush("#DA0A1B28", "#AA163950", "#D207111A", diagonal: true));
                    SetResource("ProfileScoreBandBrush", BuildLinearBrush("#2072F6E8", "#145DA6FF", "#0AD9C98A"));
                    SetResource("ProfileAvatarTileBrush", BuildLinearBrush("#2E72F6E8", "#185DA6FF", "#0C071320", diagonal: true));
                    SetBrush("ProfileBadgeFillBrush", "#1A112A3A");
                    SetBrush("ProfileBadgeBorderBrush", "#4A72F6E8");
                    SetBrush("ProfileBadgeGlowBrush", "#5472F6E8");
                    SetResource("ProfileSurfacePatternOpacity", 0.12d);
                    SetResource("ProfileSurfaceSweepOpacity", 0.15d);
                    SetResource("ProfileBadgePulseOpacity", 0.17d);
                }
                break;
        }
    }

    private static void SetBrush(string key, string color)
    {
        var targetColor = (Color)ColorConverter.ConvertFromString(color);
        if (TryFindResourceValue(Application.Current.Resources, key, out var existing) &&
            existing is SolidColorBrush solidBrush &&
            !solidBrush.IsFrozen)
        {
            solidBrush.Color = targetColor;
            return;
        }

        SetResource(key, new SolidColorBrush(targetColor));
    }

    private static void SetResource(string key, object value)
    {
        if (!ReplaceResourceValue(Application.Current.Resources, key, value))
        {
            Application.Current.Resources[key] = value;
        }
    }

    private static bool ReplaceResourceValue(ResourceDictionary dictionary, string key, object value)
    {
        if (dictionary.Contains(key))
        {
            if (TryMutateResource(dictionary[key], value))
            {
                return true;
            }

            dictionary[key] = value;
            return true;
        }

        foreach (var mergedDictionary in dictionary.MergedDictionaries)
        {
            if (ReplaceResourceValue(mergedDictionary, key, value))
                return true;
        }

        return false;
    }

    private static bool TryFindResourceValue(ResourceDictionary dictionary, string key, out object? value)
    {
        if (dictionary.Contains(key))
        {
            value = dictionary[key];
            return true;
        }

        foreach (var mergedDictionary in dictionary.MergedDictionaries)
        {
            if (TryFindResourceValue(mergedDictionary, key, out value))
            {
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool TryMutateResource(object current, object next)
    {
        if (current is Freezable { IsFrozen: true })
        {
            return false;
        }

        if (current is SolidColorBrush currentSolid && next is SolidColorBrush nextSolid)
        {
            try
            {
                currentSolid.Color = nextSolid.Color;
                currentSolid.Opacity = nextSolid.Opacity;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        if (current is LinearGradientBrush currentGradient && next is LinearGradientBrush nextGradient)
        {
            try
            {
                currentGradient.StartPoint = nextGradient.StartPoint;
                currentGradient.EndPoint = nextGradient.EndPoint;
                currentGradient.MappingMode = nextGradient.MappingMode;
                currentGradient.SpreadMethod = nextGradient.SpreadMethod;
                currentGradient.ColorInterpolationMode = nextGradient.ColorInterpolationMode;
                currentGradient.GradientStops.Clear();
                foreach (var stop in nextGradient.GradientStops)
                {
                    currentGradient.GradientStops.Add(new GradientStop(stop.Color, stop.Offset));
                }

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        return false;
    }

    private static LinearGradientBrush BuildLinearBrush(string start, string middle, string end, bool diagonal = false)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = diagonal ? new Point(0, 0) : new Point(0, 0),
            EndPoint = diagonal ? new Point(1, 1) : new Point(1, 0)
        };
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(start), 0));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(middle), 0.5));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(end), 1));
        return brush;
    }

    private static LinearGradientBrush BuildVerticalBrush(string start, string middle, string end)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(start), 0));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(middle), 0.55));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(end), 1));
        return brush;
    }

    private static LinearGradientBrush BuildGlassBorderBrush(string accentHex)
    {
        var accentColor = (Color)ColorConverter.ConvertFromString(accentHex);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(120, 255, 255, 255), 0.0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(45, accentColor.R, accentColor.G, accentColor.B), 0.45));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(20, 0, 0, 0), 1.0));
        return brush;
    }

    private static LinearGradientBrush BuildGlassBackingBrush(string baseColorHex, string secondaryColorHex)
    {
        var baseColor = (Color)ColorConverter.ConvertFromString(baseColorHex);
        var secondaryColor = (Color)ColorConverter.ConvertFromString(secondaryColorHex);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        brush.GradientStops.Add(new GradientStop(baseColor, 0.0));
        brush.GradientStops.Add(new GradientStop(secondaryColor, 1.0));
        return brush;
    }

    private static LinearGradientBrush BuildButtonGradient(string startHex, string endHex)
    {
        var startColor = (Color)ColorConverter.ConvertFromString(startHex);
        var endColor = (Color)ColorConverter.ConvertFromString(endHex);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0)
        };
        brush.GradientStops.Add(new GradientStop(startColor, 0.0));
        brush.GradientStops.Add(new GradientStop(endColor, 1.0));
        return brush;
    }

    private static DrawingBrush BuildScanlineBrush()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12000000")), null, Geometry.Parse("M0,0 L1,0 1,1 0,1 Z")));
        group.Children.Add(new GeometryDrawing(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#04000000")), null, Geometry.Parse("M0,2 L1,2 1,3 0,3 Z")));
        return new DrawingBrush(group)
        {
            Viewport = new Rect(0, 0, 1, 3),
            ViewportUnits = BrushMappingMode.Absolute,
            TileMode = TileMode.Tile,
            Stretch = Stretch.Fill
        };
    }

    private static DrawingBrush BuildNierDotGridBrush()
    {
        var group = new DrawingGroup();
        var dot = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A5E5548"));
        group.Children.Add(new GeometryDrawing(dot, null, Geometry.Parse("M1,1 A1,1 0 1 1 1.1,1 Z")));
        group.Children.Add(new GeometryDrawing(dot, null, Geometry.Parse("M7,4 A0.8,0.8 0 1 1 7.1,4 Z")));
        group.Children.Add(new GeometryDrawing(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#104A453B")), null, Geometry.Parse("M0,0 L12,0 12,0.4 0,0.4 Z")));
        return new DrawingBrush(group)
        {
            Viewport = new Rect(0, 0, 12, 12),
            ViewportUnits = BrushMappingMode.Absolute,
            TileMode = TileMode.Tile,
            Stretch = Stretch.Fill
        };
    }

    private static DrawingBrush BuildCadenceLineBrush(string primaryHex, string secondaryHex)
    {
        var primary = new SolidColorBrush((Color)ColorConverter.ConvertFromString(primaryHex));
        var secondary = new SolidColorBrush((Color)ColorConverter.ConvertFromString(secondaryHex));
        var group = new DrawingGroup();

        group.Children.Add(new GeometryDrawing(primary, null, Geometry.Parse("M0,8 L28,8 28,9.2 0,9.2 Z")));
        group.Children.Add(new GeometryDrawing(primary, null, Geometry.Parse("M22,0 L24,0 44,20 42,20 Z")));
        group.Children.Add(new GeometryDrawing(primary, null, Geometry.Parse("M8,36 L10,36 30,56 28,56 Z")));

        group.Children.Add(new GeometryDrawing(secondary, null, Geometry.Parse("M34,14 L56,14 56,15 34,15 Z")));
        group.Children.Add(new GeometryDrawing(secondary, null, Geometry.Parse("M0,30 L16,30 16,31 0,31 Z")));
        group.Children.Add(new GeometryDrawing(secondary, null, Geometry.Parse("M40,40 L56,40 56,41 40,41 Z")));

        group.Children.Add(new GeometryDrawing(secondary, null, Geometry.Parse("M6,4 L7,4 7,20 6,20 Z")));
        group.Children.Add(new GeometryDrawing(primary, null, Geometry.Parse("M48,24 L49,24 49,52 48,52 Z")));

        return new DrawingBrush(group)
        {
            Viewport = new Rect(0, 0, 56, 56),
            ViewportUnits = BrushMappingMode.Absolute,
            TileMode = TileMode.Tile,
            Stretch = Stretch.Fill
        };
    }

    private static DrawingBrush BuildMatrixRainBrush()
    {
        var group = new DrawingGroup();
        var culture = CultureInfo.InvariantCulture;
        var typeface = new Typeface(new FontFamily("Cascadia Mono, Consolas, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        const string glyphs = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZアイウエオカキクケコサシスセソ";

        var safeGlyphs = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ" + glyphs[..0];
        var columnCount = 12;
        var columnSpacing = 13.5;
        var rowHeight = 14.5;
        var viewportWidth = 168d;
        var viewportHeight = 240d;

        for (var column = 0; column < columnCount; column++)
        {
            var x = 6 + (column * columnSpacing);
            var leadRow = (column * 3) % 10;
            var trailLength = 8 + (column % 4);

            var trailBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(column % 2 == 0 ? "#0F46FF8A" : "#1430C86B"));
            group.Children.Add(new GeometryDrawing(
                trailBrush,
                null,
                new RectangleGeometry(new Rect(x + 3.5, leadRow * rowHeight, 1.2, (trailLength + 2) * rowHeight))));

            for (var row = 0; row < trailLength; row++)
            {
                var glyphIndex = (column * 11 + row * 7) % safeGlyphs.Length;
                var glyph = safeGlyphs[glyphIndex].ToString();
                var y = (leadRow + row) * rowHeight;
                var fontSize = row == 0 ? 12.5 : row < 3 ? 11.5 : 10.5;
                var brushHex = row switch
                {
                    0 => "#E6F8FFF1",
                    1 => "#AA88FFC0",
                    2 => "#7046FF8A",
                    3 => "#4028D46E",
                    _ => "#18209A4C"
                };

                AppendMatrixGlyph(group, glyph, x, y, fontSize, brushHex, typeface, culture);
            }

            // Add a few off-column ghost glyphs so the pattern feels less tiled and more cinematic.
            var ghostGlyph = safeGlyphs[(column * 5 + 17) % safeGlyphs.Length].ToString();
            AppendMatrixGlyph(group, ghostGlyph, x + 5, ((leadRow + trailLength + 2) % 14) * rowHeight, 9.5, "#1230C86B", typeface, culture);
        }

        var brush = new DrawingBrush(group)
        {
            Viewport = new Rect(0, 0, viewportWidth, viewportHeight),
            ViewportUnits = BrushMappingMode.Absolute,
            TileMode = TileMode.Tile,
            Stretch = Stretch.Fill
        };
        brush.Freeze();
        return brush;
    }

    private static void AppendMatrixGlyph(DrawingGroup group, string glyph, double x, double y, double fontSize, string colorHex, Typeface typeface, CultureInfo culture)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
        brush.Freeze();
        var formatted = new FormattedText(
            glyph,
            culture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            brush,
            1.0);
        var geometry = formatted.BuildGeometry(new Point(x, y));
        geometry.Freeze();
        group.Children.Add(new GeometryDrawing(brush, null, geometry));
    }

    private static DrawingBrush BuildSynthwaveGridBrush()
    {
        var group = new DrawingGroup();
        var pinkBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#33FF4FD0"));
        var cyanBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1100F0FF"));
        
        // Vertical grid lines (neon cyan)
        group.Children.Add(new GeometryDrawing(cyanBrush, null, Geometry.Parse("M0,0 L1.5,0 1.5,40 0,40 Z")));
        group.Children.Add(new GeometryDrawing(cyanBrush, null, Geometry.Parse("M20,0 L21.5,0 21.5,40 20,40 Z")));
        
        // Horizontal grid lines (neon pink)
        group.Children.Add(new GeometryDrawing(pinkBrush, null, Geometry.Parse("M0,0 L40,0 40,1.5 0,1.5 Z")));
        group.Children.Add(new GeometryDrawing(pinkBrush, null, Geometry.Parse("M0,20 L40,20 40,21.5 0,21.5 Z")));
        
        return new DrawingBrush(group)
        {
            Viewport = new Rect(0, 0, 40, 40),
            ViewportUnits = BrushMappingMode.Absolute,
            TileMode = TileMode.Tile,
            Stretch = Stretch.Fill
        };
    }

    private static DrawingBrush BuildWaveLineBrush()
    {
        var group = new DrawingGroup();
        var line = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#168DFFF4"));
        group.Children.Add(new GeometryDrawing(line, null, Geometry.Parse("M0,0 L1,0 1,1 0,1 Z")));
        group.Children.Add(new GeometryDrawing(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#08D9C98A")), null, Geometry.Parse("M0,5 L1,5 1,6 0,6 Z")));
        return new DrawingBrush(group)
        {
            Viewport = new Rect(0, 0, 1, 6),
            ViewportUnits = BrushMappingMode.Absolute,
            TileMode = TileMode.Tile,
            Stretch = Stretch.Fill
        };
    }

    private sealed record ThemePalette(
        string AppBackground,
        string Sidebar,
        string Surface,
        string Elevated,
        string Accent,
        string AccentDim,
        string AccentText,
        string PrimaryText = "#E2EAF8");

    private void ShowError(string message, string title = "CloudBerry") =>
        MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    // ── Mouse wheel bubbling ─────────────────────────────────────────────────────────────────
    // WPF ListBoxes contain an internal ScrollViewer that eats wheel events even when the
    // list sits inside an outer ScrollViewer (e.g. Home page, Achievements page, detail panel).
    // We intercept PreviewMouseWheel in the tunneling phase: if the source is inside a ListBox
    // that in turn sits inside a ScrollViewer, redirect the scroll to that outer ScrollViewer.
    // ListBoxes that ARE the primary scroll container (GameLibraryList) have no outer
    // ScrollViewer so they fall through and keep handling the event themselves.
    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);
        if (e.Handled) return;

        var element = e.OriginalSource as DependencyObject;
        ScrollViewer? outerSv = null;
        bool insideListBox = false;

        while (element is not null)
        {
            // We encounter the ListBox's INTERNAL ScrollViewer before the ListBox itself
            // (child → parent walk), so only start looking for an outer ScrollViewer
            // once we've passed through the ListBox element.
            if (element is ListBox)
            {
                insideListBox = true;
            }
            else if (insideListBox && element is ScrollViewer sv && sv.ScrollableHeight > 0)
            {
                outerSv = sv;
                break;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        if (outerSv is null) return;

        outerSv.ScrollToVerticalOffset(outerSv.VerticalOffset - e.Delta / 3.0);
        e.Handled = true;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Theatre (PAGE 4)
    // ─────────────────────────────────────────────────────────────────────

    private async Task InitializeMainTheatreBrowserAsync()
    {
        try
        {
            if (MainTheatreBrowser != null && MainTheatreBrowser.CoreWebView2 == null)
                await MainTheatreBrowser.EnsureCoreWebView2Async();
        }
        catch
        {
            // WebView2 runtime not installed; Theatre page will show empty.
        }
    }

    private void MainTheatreBrowserInitCompleted(object? sender,
        Microsoft.Web.WebView2.Core.CoreWebView2InitializationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            NavigateMainTheatreBrowser();
            MainTheatreBrowser.CoreWebView2.ContainsFullScreenElementChanged += MainTheatreFullScreenChanged;
        }
    }

    private void MainTheatreFullScreenChanged(object? sender, object e)
    {
        Dispatcher.Invoke(() =>
        {
            var isFullScreen = MainTheatreBrowser.CoreWebView2.ContainsFullScreenElement;
            if (isFullScreen)
            {
                _preTheatreFullscreenState = WindowState;
                WindowStyle   = WindowStyle.None;
                WindowState   = WindowState.Normal; // must go Normal before Maximized when style changes
                WindowState   = WindowState.Maximized;
                TitleBarHost.Visibility        = Visibility.Collapsed;
                BladeNavigationHost.Visibility = Visibility.Collapsed;
                StatusBarHost.Visibility       = Visibility.Collapsed;
                SidebarRail.Visibility         = Visibility.Collapsed;
                SidebarColumn.MinWidth         = 0;
                SidebarColumn.MaxWidth         = 0;
                SidebarColumn.Width            = new GridLength(0);
                SidebarSeparatorColumn.Width   = new GridLength(0);
                SidebarSeparator.Visibility    = Visibility.Collapsed;
            }
            else
            {
                WindowStyle = WindowStyle.None; // keep our custom chrome
                WindowState = _preTheatreFullscreenState;
                TitleBarHost.Visibility  = Visibility.Visible;
                StatusBarHost.Visibility = Visibility.Visible;
                // Restore normal Theatre chrome (no sidebar, no blade nav)
                ApplyTheatreChrome(isTheatre: true);
            }
        });
    }

    private WindowState _preTheatreFullscreenState = WindowState.Normal;

    /// <summary>
    /// Hides the left sidebar, blade navigation, and status bar when on the Theatre page
    /// so the browser fills the full content area edge-to-edge. Restores them on other pages.
    /// </summary>
    private void ApplyTheatreChrome(bool isTheatre)
    {
        if (!IsLoaded) return;

        if (isTheatre)
        {
            // Collapse left sidebar completely
            _lastSidebarWidth = SidebarColumn.Width.IsAbsolute && SidebarColumn.Width.Value > 0
                ? SidebarColumn.Width.Value
                : _lastSidebarWidth;
            SidebarColumn.MinWidth    = 0;
            SidebarColumn.MaxWidth    = 0;
            SidebarColumn.Width          = new GridLength(0);
            SidebarSeparatorColumn.Width = new GridLength(0);
            SidebarRail.Visibility       = Visibility.Collapsed;
            SidebarSeparator.Visibility  = Visibility.Collapsed;
            SidebarResizeGrip.Visibility = Visibility.Collapsed;
            // Collapse blade nav and status bar
            BladeNavigationHost.Visibility = Visibility.Collapsed;
            StatusBarHost.Visibility       = Visibility.Collapsed;
        }
        else
        {
            // Restore all chrome hidden on Theatre entry
            TitleBarHost.Visibility  = Visibility.Visible;
            StatusBarHost.Visibility = Visibility.Visible;
            ApplySidebarLayout(); // restores sidebar + blade nav correctly
        }
    }

    private void NavigateMainTheatreBrowser()
    {
        if (!IsLoaded || MainTheatreBrowser?.CoreWebView2 == null) return;
        try
        {
            if (!string.IsNullOrWhiteSpace(ViewModel.TheatreBrowserHtml))
            {
                MainTheatreBrowser.NavigateToString(ViewModel.TheatreBrowserHtml);
                return;
            }
            if (string.IsNullOrWhiteSpace(ViewModel.TheatreBrowserUrl) ||
                string.Equals(ViewModel.TheatreBrowserUrl, "about:blank", StringComparison.OrdinalIgnoreCase))
            {
                MainTheatreBrowser.NavigateToString(BuildTheatreHomeHtml());
                return;
            }
            MainTheatreBrowser.Source = new Uri(ViewModel.TheatreBrowserUrl);
        }
        catch { /* navigation failures are surfaced via TheatreStatusMessage */ }
    }

    /// <summary>
    /// Builds Theatre's home/new-tab page: a real HTML page with a native Google search
    /// form and tiles linking straight to the player's saved sites. Both work with a plain
    /// WebView2.NavigateToString call, no host-object/JS interop needed, since a GET form
    /// and an anchor tag both just trigger a normal top-level navigation in the browser.
    /// </summary>
    private string BuildTheatreHomeHtml()
    {
        var tiles = new StringBuilder();
        foreach (var site in ViewModel.SavedTheatreSites)
        {
            var name = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(site.DisplayName) ? site.Url : site.DisplayName);
            var url = WebUtility.HtmlEncode(site.Url);
            tiles.Append($"<a class=\"tile\" href=\"{url}\">{name}</a>");
        }

        var tilesSection = tiles.Length > 0
            ? $"<div class=\"eyebrow\" style=\"margin-top:36px;\">Saved sites</div><div class=\"tiles\">{tiles}</div>"
            : "<p style=\"margin-top:20px;\">Save a site with the &#x2605; Save Site button and it'll show up here.</p>";

        return $$"""
            <!doctype html><html><head><meta http-equiv="X-UA-Compatible" content="IE=edge">
            <style>
            html,body{width:100%;height:100%;margin:0;overflow:auto;
            background:radial-gradient(circle at 18% 18%,rgba(83,255,196,.18),transparent 36%),
            linear-gradient(135deg,#06120d 0%,#071b13 48%,#020806 100%);
            color:#c8f7df;font-family:"Segoe UI",Arial,sans-serif;}
            .shell{box-sizing:border-box;min-height:100%;padding:34px;
            border:1px solid rgba(96,255,194,.26);
            box-shadow:inset 0 0 80px rgba(20,255,153,.08);}
            .eyebrow{color:rgba(111,190,255,.78);font-size:11px;font-weight:700;
            letter-spacing:.16em;text-transform:uppercase;}
            h1{margin:10px 0 8px;font-size:34px;line-height:1.05;}
            p{max-width:620px;color:rgba(203,230,220,.78);font-size:14px;line-height:1.55;}
            form{margin-top:22px;max-width:560px;display:flex;gap:8px;}
            input[type=text]{flex:1;padding:12px 14px;border-radius:8px;border:1px solid rgba(96,255,194,.3);
            background:rgba(255,255,255,.04);color:#eafff4;font-size:14px;outline:none;}
            input[type=text]::placeholder{color:rgba(203,230,220,.45);}
            button{padding:12px 20px;border-radius:8px;border:none;background:#53ffc4;color:#04140d;
            font-weight:700;font-size:13px;cursor:pointer;}
            .tiles{display:flex;flex-wrap:wrap;gap:10px;margin-top:14px;}
            .tile{display:inline-block;padding:10px 16px;border-radius:8px;
            border:1px solid rgba(96,255,194,.25);background:rgba(255,255,255,.03);
            color:#c8f7df;text-decoration:none;font-size:13px;max-width:220px;
            overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}
            .tile:hover{border-color:rgba(96,255,194,.6);background:rgba(83,255,196,.08);}
            </style></head><body>
            <div class="shell">
            <div class="eyebrow">Cloud's Theatre</div>
            <h1>Type a site, or search the web.</h1>
            <p>Use the address bar above, or search right here - both take direct URLs and plain search terms.</p>
            <form action="https://www.google.com/search" method="get">
            <input type="text" name="q" placeholder="Search Google, or type a URL above" autofocus>
            <button type="submit">Search</button>
            </form>
            {{tilesSection}}
            </div></body></html>
            """;
    }

    private void MainTheatreAddressKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Return or Key.Enter)
        {
            ViewModel.NavigateTheatreAddress();
            _sound.PlaySoftClick();
            e.Handled = true;
        }
    }

    private void ExitTheatreClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedPrimaryPage = 1; // back to library
        _sound.PlaySoftClick();
    }

    private void MainTheatreGoClick(object sender, RoutedEventArgs e)
    {
        ViewModel.NavigateTheatreAddress();
        _sound.PlaySoftClick();
    }

    private void MainTheatreBackClick(object sender, RoutedEventArgs e)
    {
        if (MainTheatreBrowser.CanGoBack)
        {
            MainTheatreBrowser.GoBack();
            _sound.PlaySoftClick();
        }
    }

    private void MainTheatreForwardClick(object sender, RoutedEventArgs e)
    {
        if (MainTheatreBrowser.CanGoForward)
        {
            MainTheatreBrowser.GoForward();
            _sound.PlaySoftClick();
        }
    }

    private void MainTheatreRefreshClick(object sender, RoutedEventArgs e)
    {
        MainTheatreBrowser.CoreWebView2?.Reload();
        _sound.PlaySoftClick();
    }

    private void MainTheatreHomeClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetTheatrePlayer();
        MainTheatreBrowser.NavigateToString(BuildTheatreHomeHtml());
        _sound.PlaySoftClick();
    }

    private void MainTheatreAddSiteClick(object sender, RoutedEventArgs e)
    {
        ViewModel.AddCurrentSiteToSaved();
        _sound.PlaySoftClick();
    }

    private void MainTheatreRemoveSiteClick(object sender, RoutedEventArgs e)
    {
        ViewModel.RemoveSavedSite(ViewModel.SelectedSavedSite);
        _sound.PlaySoftClick();
    }

    private void MainTheatreSavedSiteClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: TheatreSavedSiteViewModel site })
        {
            ViewModel.NavigateToSavedSite(site);
            _sound.PlaySoftClick();
        }
    }

    private void EnterMainTheatreWatchClick(object sender, RoutedEventArgs e)
    {
        ViewModel.EnterTheatreWatchMode();
        _sound.PlayMenuOpen();
    }

    private void ExitMainTheatreWatchClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ExitTheatreWatchMode();
        _sound.PlayMenuOpen();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Add to Collection (Library context menu)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the "Add to Collection" flyout every time it opens so it always reflects the
    /// current selection and the latest set of user collections. "New Collection..." always
    /// leads, followed by a checkable entry per existing collection (checked when every
    /// selected game already belongs to it).
    /// </summary>
    private void AddToCollectionSubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
        {
            return;
        }

        menuItem.Items.Clear();

        var selectedGames = GameLibraryList.SelectedItems.Cast<GameEntryViewModel>().ToList();
        if (selectedGames.Count == 0 && ViewModel.SelectedGame != null)
        {
            selectedGames.Add(ViewModel.SelectedGame);
        }

        var newCollectionItem = new MenuItem { Header = "New Collection..." };
        newCollectionItem.Click += (_, _) => OpenCollectionEditorForNewCollection(selectedGames);
        menuItem.Items.Add(newCollectionItem);

        if (ViewModel.UserCollections.Count == 0)
        {
            return;
        }

        menuItem.Items.Add(new Separator());

        foreach (var collection in ViewModel.UserCollections.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            var alreadyInCollection = selectedGames.Count > 0
                && selectedGames.All(game => ViewModel.IsGameInCollection(game, collection.Id));

            var collectionItem = new MenuItem
            {
                Header = collection.Name,
                IsCheckable = true,
                IsChecked = alreadyInCollection
            };
            collectionItem.Click += (_, _) =>
            {
                if (selectedGames.Count == 0)
                {
                    return;
                }

                if (alreadyInCollection)
                {
                    ViewModel.RemoveGamesFromCollection(collection.Id, selectedGames);
                }
                else
                {
                    ViewModel.AddGamesToCollection(collection.Id, selectedGames);
                }
                _sound.PlaySoftClick();
            };
            menuItem.Items.Add(collectionItem);
        }
    }

    /// <summary>Opens the Collection editor pre-seeded with whichever games were selected when "New Collection..." was clicked.</summary>
    private void OpenCollectionEditorForNewCollection(List<GameEntryViewModel> preselectedGames)
    {
        var editor = new CollectionEditorWindow(ViewModel, null) { Owner = this };
        if (preselectedGames.Count > 0 && editor.DataContext is CollectionEditorViewModel editorViewModel)
        {
            var preselectedIds = preselectedGames.Select(g => g.Id).ToHashSet();
            foreach (var entry in editorViewModel.Entries.Where(entry => preselectedIds.Contains(entry.Game.Id)))
            {
                entry.IsSelected = true;
            }
        }

        editor.ShowDialog();
        _sound.PlaySoftClick();
    }
}
