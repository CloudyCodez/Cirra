using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CloudBerry.App.Services;
using CloudBerry.App.ViewModels;

namespace CloudBerry.App;

public partial class BigPictureWindow : Window
{
    private const double ShelfDragThreshold = 22;
    private readonly SoundDesignService _sound;
    private readonly DispatcherTimer _trailerPreviewTimer;
    private Point? _shelfDragStartPoint;
    private double _shelfDragStartOffset;
    private ScrollViewer? _activeShelfScrollViewer;
    private ListBox? _activeShelfListBox;
    private GameEntryViewModel? _pendingShelfClickGame;
    private GameEntryViewModel? _pendingTrailerPreviewGame;
    private GameEntryViewModel? _activeTrailerPreviewGame;
    private FrameworkElement? _pendingTrailerPreviewElement;
    private bool _shelfDragMoved;
    private bool _shelfWasDoubleClick; // set in DragStart on double-click; tells DragEnd to skip OpenDetailOverlay
    private bool _trailerFrameHookRegistered;
    private HttpListener? _trailerServer;
    private int _trailerServerPort;
    private Task<CoreWebView2Environment>? _trailerBrowserEnvironmentTask;
    private bool _isCloudscapePointerGlowVisible;
    // Momentum scrolling fields
    private double _shelfVelocityX;
    private Point? _shelfPrevDragPoint;
    private DispatcherTimer? _shelfMomentumTimer;
    // Vertical scroll momentum fields
    private double _verticalScrollVelocity;
    private DispatcherTimer? _verticalMomentumTimer;
    private ScrollViewer? _lastVerticalScrollViewer;
    // Hero auto-advance
    private DispatcherTimer? _heroAdvanceTimer;
    // Live session timer — refreshes LiveSessionLabel on the running game every 30s
    private DispatcherTimer? _liveSessionTimer;
    // Cursor auto-hide after inactivity
    private DispatcherTimer? _cursorHideTimer;
    private bool _cursorHidden;
    // Gamepad navigation
    private DispatcherTimer? _gamepadTimer;
    private DispatcherTimer? _homeScrollTimer;
    private double _homeScrollTarget;
    // Tab transition direction tracking (-1 = backward, +1 = forward)
    private int _lastCloudscapeTabIndex = 0;
    // Detail overlay animation state
    private bool _isDetailOpen = false;

    public BigPictureWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;
        SourceInitialized += (_, _) => ApplyWorkAreaBounds();
        StateChanged += (_, _) => ApplyWorkAreaBounds();
        Activated += (_, _) => ApplyWindowPerformanceState(isFocused: true);
        Deactivated += (_, _) => { ApplyWindowPerformanceState(isFocused: false); StopTrailerPreview(); };
        _sound = new SoundDesignService(() => ViewModel.AudioEnabled, () => ViewModel.AudioProfile, () => ViewModel.AppearanceThemeKey);
        _trailerPreviewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
        _trailerPreviewTimer.Tick += TrailerPreviewTimerTick;
        // Single persistent vertical momentum timer — created once, reused every scroll.
        _verticalMomentumTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _verticalMomentumTimer.Tick += VerticalMomentumTick;
        // Seed the first hero background so selection changes can crossfade cleanly.
        UpdateHeroBg(HeroBgA, ViewModel.BigPictureHeroGame?.DisplayBackgroundImagePath);
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _ = InitializeTheatreBrowserAsync();
        StartTrailerServer();
        StartHeroAdvanceTimer();
        StartLiveSessionTimer();
        // Cursor hide: hide cursor after 3 seconds of inactivity in Cloudscape.
        // The timer is NOT started here — it starts in OnActivated, so a pre-warmed
        // window that is never shown never hides the cursor in CloudBerry.
        _cursorHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _cursorHideTimer.Tick += (_, _) =>
        {
            _cursorHideTimer!.Stop();
            _cursorHidden = true;
            Mouse.OverrideCursor = Cursors.None;
        };
        MouseMove += OnBigPictureMouseMove;
        PreviewKeyDown += (_, _) => ShowCursor();
        // Start the hide timer only once the window is actually visible and active.
        Activated += OnFirstActivated;
        // Re-initialize achievement pop service with this window so toasts appear on
        // the correct screen while Cloudscape is open.
        Loaded += (_, _) => InitCloudscapeAchievementPop();
        // Gamepad navigation poll — 60fps DirectInput XInput wrapper.
        StartGamepadTimer();
    }

    /// <summary>
    /// Starts a tiny localhost HTTP server that serves an HTML page containing a YouTube/Vimeo
    /// iframe. This is the only reliable way to get YouTube embed playback in WebView2 without
    /// Error 153: YouTube's embed validator requires the iframe to be hosted on a real HTTP
    /// origin. data: URIs, NavigateToString, and NavigateWithWebResourceRequest all fail because
    /// they either have no origin or a non-HTTP origin. localhost:PORT is a real HTTP origin that
    /// YouTube's embed player accepts.
    /// </summary>
    private void StartTrailerServer()
    {
        try
        {
            // Find a free port.
            var listener = new HttpListener();
            // Try a stable port first; fall back to the OS picking one.
            int port = FindFreePort(19854);
            _trailerServerPort = port;
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();
            _trailerServer = listener;

            // Handle requests on a background thread — never blocks the UI.
            Thread serverThread = new Thread(() =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var ctx = listener.GetContext();
                        ThreadPool.QueueUserWorkItem(_ => HandleTrailerRequest(ctx));
                    }
                    catch (HttpListenerException) { break; } // listener stopped
                    catch { /* swallow */ }
                }
            })
            {
                IsBackground = true,
                Name = "TrailerPreviewServer"
            };
            serverThread.Start();
        }
        catch
        {
            // If the server fails to start, ShowCardTrailerPreview will fall through to fallback.
        }
    }

    private static int FindFreePort(int preferred)
    {
        try
        {
            var test = new HttpListener();
            test.Prefixes.Add($"http://localhost:{preferred}/");
            test.Start();
            test.Stop();
            return preferred;
        }
        catch
        {
            // Preferred port in use; let the OS pick one via TcpListener trick.
            var tcp = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            int port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            tcp.Stop();
            return port;
        }
    }

    private static void HandleTrailerRequest(HttpListenerContext ctx)
    {
        try
        {
            var req = ctx.Request;
            var resp = ctx.Response;

            if (req.HttpMethod != "GET" || req.Url?.AbsolutePath != "/trailer")
            {
                resp.StatusCode = 404;
                resp.Close();
                return;
            }

            var embedUrl = req.QueryString["embed"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(embedUrl))
            {
                resp.StatusCode = 400;
                resp.Close();
                return;
            }

            var videoId = string.Empty;
            if (Uri.TryCreate(embedUrl, UriKind.Absolute, out var embedUri))
            {
                var path = embedUri.AbsolutePath;
                var idx = path.LastIndexOf('/');
                if (idx >= 0 && idx < path.Length - 1)
                    videoId = path[(idx + 1)..];
            }

            string html;
            if (!string.IsNullOrWhiteSpace(videoId) && !embedUrl.Contains("vimeo"))
            {
                // YouTube IFrame API: same-page JS object so unMute() works without
                // cross-origin restrictions.
                //
                // The secret weapon for clean playback: pointer-events:none on the iframe.
                // YouTube ONLY renders the pause button, title card, and ESRB splash in
                // response to mouseover/mouseenter events on the player element. With
                // pointer-events:none the iframe never receives those events, so all
                // hover-triggered overlays simply never appear. Zero CSS injection needed.
                //
                // The .shield div above the iframe is a transparent hit-blocker that
                // catches any stray pointer events that somehow reach the page.
                var sb = new StringBuilder();
                sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");
                sb.Append("<style>");
                sb.Append("*{margin:0;padding:0;box-sizing:border-box}");
                sb.Append("html,body{width:100%;height:100%;background:#000;overflow:hidden;position:relative}");
                sb.Append("#p{position:absolute;top:0;left:0;width:100%;height:100%}");
                sb.Append("#p iframe{position:absolute!important;top:0!important;left:0!important;");
                sb.Append("width:100%!important;height:100%!important;border:none!important;");
                sb.Append("pointer-events:none!important}");
                sb.Append(".shield{position:absolute;top:0;left:0;width:100%;height:100%;");
                sb.Append("z-index:10;background:transparent;cursor:default}");
                sb.Append("</style></head><body>");
                sb.Append("<div id='p'></div>");
                sb.Append("<div class='shield'></div>");
                sb.Append("<script src='https://www.youtube.com/iframe_api'></script>");
                sb.Append("<script>");
                sb.Append("var player;");
                sb.Append("function onYouTubeIframeAPIReady(){");
                sb.Append("  player=new YT.Player('p',{");
                sb.Append("    videoId:'").Append(videoId).Append("',");
                sb.Append("    playerVars:{autoplay:1,mute:1,controls:0,rel:0,playsinline:1,");
                sb.Append("               iv_load_policy:3,modestbranding:1,disablekb:1,fs:0,");
                sb.Append("               cc_load_policy:0,showinfo:0,enablejsapi:1},");
                sb.Append("    events:{");
                sb.Append("      onReady:function(e){e.target.playVideo()},");
                sb.Append("      onStateChange:function(e){");
                sb.Append("        if(e.data===1){e.target.unMute();e.target.setVolume(88)}");
                sb.Append("      }");
                sb.Append("    }");
                sb.Append("  })");
                sb.Append("}");
                sb.Append("window._unmute=function(){if(player&&player.unMute){player.unMute();player.setVolume(88);player.playVideo()}}");
                sb.Append("</script>");
                sb.Append("</body></html>");
                html = sb.ToString();
            }
            else
            {
                // Vimeo or unknown: plain iframe with shield
                var safeUrl = HtmlEncode(embedUrl);
                html = "<!DOCTYPE html><html><head><meta charset='utf-8'>" +
                       "<style>*{margin:0;padding:0;box-sizing:border-box}" +
                       "html,body{width:100%;height:100%;background:#000;overflow:hidden}" +
                       ".wrap{position:relative;width:100%;height:100%}" +
                       "iframe{position:absolute;top:0;left:0;width:100%;height:100%;border:none;pointer-events:none}" +
                       ".shield{position:absolute;top:0;left:0;width:100%;height:100%;z-index:10}" +
                       "</style></head><body>" +
                       "<div class='wrap'>" +
                       "<iframe src='" + safeUrl + "' allow='autoplay; encrypted-media'></iframe>" +
                       "<div class='shield'></div>" +
                       "</div></body></html>";
            }

            var bytes = Encoding.UTF8.GetBytes(html);
            resp.ContentType = "text/html; charset=utf-8";
            resp.ContentLength64 = bytes.Length;
            resp.StatusCode = 200;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.Close();
        }
        catch
        {
            try { ctx.Response.Close(); } catch { }
        }
    }

    private static string HtmlEncode(string value) =>
        value.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");

    private async Task InitializeTheatreBrowserAsync()
    {
        try
        {
            if (CloudTheatreBrowser != null && CloudTheatreBrowser.CoreWebView2 == null)
            {
                await CloudTheatreBrowser.EnsureCoreWebView2Async();
            }
        }
        catch
        {
            // WebView2 runtime might not be installed; ignore for now.
        }

        // Pre-warm the card trailer preview browser so it is ready before first hover.
        try
        {
            if (CardTrailerPreviewBrowser != null && CardTrailerPreviewBrowser.CoreWebView2 == null)
            {
                // Create an environment with --autoplay-policy=no-user-gesture-required
                // so muted autoplay immediately unmutes via postMessage once playing.
                // The environment-creation task is cached in _trailerBrowserEnvironmentTask so
                // ShowCardTrailerPreview reuses the same in-flight/completed task instead of
                // calling EnsureCoreWebView2Async a second time with a second environment
                // (WebView2 throws if that happens while the first init is still running).
                var options = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
                _trailerBrowserEnvironmentTask = CoreWebView2Environment.CreateAsync(null, null, options);
                var env = await _trailerBrowserEnvironmentTask;
                await CardTrailerPreviewBrowser.EnsureCoreWebView2Async(env);
                CardTrailerPreviewBrowser.CoreWebView2!.Settings.AreDevToolsEnabled = false;
                CardTrailerPreviewBrowser.CoreWebView2!.Settings.IsStatusBarEnabled = false;
                RegisterTrailerFrameHook();
            }
        }
        catch
        {
            // Preview browser is optional; ignore init failures.
        }
    }

    private MainViewModel ViewModel { get; }

    private void ApplyWindowPerformanceState(bool isFocused)
    {
        ViewModel.SetShellFocusState(isFocused);
        App.ApplyInteractivePerformanceMode(isFocused && !ViewModel.IsGamePerformanceMode);
    }

    private void ApplyWorkAreaBounds()
    {
        var area = SystemParameters.WorkArea;
        MaxWidth = area.Width;
        MaxHeight = area.Height;

        if (WindowState == WindowState.Maximized)
        {
            Left = area.Left;
            Top = area.Top;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.BigPictureHeroGame))
            CrossfadeHeroBg();

        if (e.PropertyName is nameof(MainViewModel.ActiveProfile) or nameof(MainViewModel.ActiveProfileThemeLabel))
            PulseProfileCard();

        if (e.PropertyName is nameof(MainViewModel.IsGamePerformanceMode))
            App.ApplyInteractivePerformanceMode(IsActive && !ViewModel.IsGamePerformanceMode);

        if (e.PropertyName is nameof(MainViewModel.TheatreBrowserUrl))
            NavigateCloudTheatreBrowser();

        if (e.PropertyName is nameof(MainViewModel.SelectedPrimaryPage))
            OnPageChanged();

        // Now Playing bar — show/hide when a session starts or ends.
        if (e.PropertyName is nameof(MainViewModel.IsRunning) or nameof(MainViewModel.RunningGameTitle))
            UpdateNowPlayingBar();

        // Detail overlay — close automatically if the detail game is cleared from the VM.
        if (e.PropertyName is nameof(MainViewModel.CloudscapeDetailGame) && ViewModel.CloudscapeDetailGame == null)
            CloseDetailOverlay();
    }

    private void OnPageChanged()
    {
        // Kill any active trailer immediately when the page changes —
        // prevents trailers bleeding across tabs.
        StopTrailerPreview();

        // Track direction for tab slide transition (used by UI if wired).
        int newTab = ViewModel.SelectedPrimaryPage;
        _lastCloudscapeTabIndex = newTab;

        var onTheatre = ViewModel.SelectedPrimaryPage == 4 && ViewModel.IsCloudTheatreUnlocked;
        // Hero panel: hidden on Theatre tab, visible everywhere else
        CloudscapeHeroShell.Visibility = onTheatre ? Visibility.Collapsed : Visibility.Visible;
        // Nav bar: restore visibility when leaving Theatre (watch mode may have hidden it)
        if (!onTheatre)
        {
            CloudscapeTopNav.Visibility = Visibility.Visible;
            // Also exit watch mode cleanly
            if (ViewModel.IsTheatreWatchMode)
                ViewModel.ExitTheatreWatchMode();
        }

        // Animate shelves in when landing on the Home tab.
        if (ViewModel.SelectedPrimaryPage == 0)
        {
            // Defer until layout is complete so ItemContainerGenerator is ready.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(AnimateShelvesFadeIn));
            ResetHeroAdvanceTimer();
        }
    }

    private void NavigateCloudTheatreBrowser()
    {
        if (!IsLoaded || CloudTheatreBrowser == null || CloudTheatreBrowser.CoreWebView2 == null)
        {
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(ViewModel.TheatreBrowserHtml))
            {
                CloudTheatreBrowser.NavigateToString(ViewModel.TheatreBrowserHtml);
                return;
            }

            if (string.IsNullOrWhiteSpace(ViewModel.TheatreBrowserUrl) ||
                string.Equals(ViewModel.TheatreBrowserUrl, "about:blank", StringComparison.OrdinalIgnoreCase))
            {
                LoadCloudTheatrePlaceholder();
                return;
            }

            CloudTheatreBrowser.Source = new Uri(ViewModel.TheatreBrowserUrl);
        }
        catch
        {
            // The status text already reports source/search failures; browser navigation can fail quietly.
        }
    }

    private void CloudTheatreBrowser_InitCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2InitializationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            NavigateCloudTheatreBrowser();
            CloudTheatreBrowser.CoreWebView2!.ContainsFullScreenElementChanged += CloudTheatreFullScreenChanged;
        }
    }

    private void CloudTheatreFullScreenChanged(object? sender, object e)
    {
        // BigPicture is always a true fullscreen window (WindowStyle=None, Maximized).
        // When the site exits native fullscreen via Escape, WebView2 shrinks its viewport
        // unless we keep WindowState = Maximized. Re-assert it here to prevent jank.
        Dispatcher.Invoke(() =>
        {
            if (!CloudTheatreBrowser.CoreWebView2!.ContainsFullScreenElement)
            {
                // Site exited fullscreen — keep our window fully maximized
                WindowState = WindowState.Maximized;
            }
        });
    }

    private void LoadCloudTheatrePlaceholder()
    {
        const string placeholderHtml = """
<!doctype html>
<html>
<head>
<meta http-equiv="X-UA-Compatible" content="IE=edge" />
<style>
html, body {
    width: 100%;
    height: 100%;
    margin: 0;
    overflow: hidden;
    background:
        radial-gradient(circle at 18% 18%, rgba(83, 255, 196, .18), transparent 36%),
        linear-gradient(135deg, #06120d 0%, #071b13 48%, #020806 100%);
    color: #c8f7df;
    font-family: "Segoe UI", Arial, sans-serif;
}
.shell {
    box-sizing: border-box;
    height: 100%;
    padding: 34px;
    border: 1px solid rgba(96, 255, 194, .26);
    box-shadow: inset 0 0 80px rgba(20, 255, 153, .08);
}
.eyebrow {
    color: rgba(111, 190, 255, .78);
    font-size: 11px;
    font-weight: 700;
    letter-spacing: .16em;
    text-transform: uppercase;
}
h1 {
    margin: 10px 0 8px;
    font-size: 34px;
    line-height: 1.05;
}
p {
    max-width: 620px;
    color: rgba(203, 230, 220, .78);
    font-size: 14px;
    line-height: 1.55;
}
.status {
    position: absolute;
    left: 34px;
    bottom: 30px;
    display: inline-block;
    padding: 10px 14px;
    border: 1px solid rgba(96, 255, 194, .28);
    border-radius: 12px;
    background: rgba(255,255,255,.05);
    color: rgba(203, 247, 223, .9);
}
</style>
</head>
<body>
<div class="shell">
    <div class="eyebrow">Cloud's Theatre</div>
    <h1>Choose a source or search to begin.</h1>
    <p>The player will load the selected free host inside Cloudscape. Add hosts from the left rail, search from the command bar, or load a category page.</p>
    <div class="status">Ready for media</div>
</div>
</body>
</html>
""";

        CloudTheatreBrowser.NavigateToString(placeholderHtml);
    }

    private void CrossfadeHeroBg()
    {
        var path = ViewModel.BigPictureHeroGame?.DisplayBackgroundImagePath;
        UpdateHeroBg(HeroBgB, path);

        var motion = ResolveThemeMotionPack();
        var dur = new Duration(TimeSpan.FromMilliseconds(motion.HeroCrossfadeMs));
        var easing = new QuarticEase { EasingMode = EasingMode.EaseOut };
        var fadeIn  = new DoubleAnimation(0, motion.BackgroundOpacity, dur) { EasingFunction = easing };
        var fadeOut = new DoubleAnimation(motion.BackgroundOpacity, 0, dur);

        fadeIn.Completed += (_, _) =>
        {
            HeroBgA.Source  = HeroBgB.Source;
            HeroBgA.Opacity = motion.BackgroundOpacity;
            HeroBgB.Opacity = 0;
        };

        HeroBgB.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        HeroBgA.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    private static void UpdateHeroBg(System.Windows.Controls.Image img, string? path)
    {
        if (string.IsNullOrEmpty(path)) { img.Source = null; return; }
        try { img.Source = new BitmapImage(new Uri(path, UriKind.Absolute)); }
        catch { img.Source = null; }
    }
    // ─────────────────────────────────────────────────────────────────────
    private async void LaunchHeroGameClick(object sender, RoutedEventArgs e)
    {
        await PlayLaunchHandoffAnimationAsync();
        await RunUiTaskAsync(
            () => ViewModel.LaunchGameAsync(ViewModel.BigPictureHeroGame),
            "CloudBerry couldn't launch that game from Big Picture.");
        _sound.PlayLaunchHandoff();
    }

    private void BrowseHeroGameClick(object sender, RoutedEventArgs e)
    {
        ViewModel.RevealGameInLibrary(ViewModel.BigPictureHeroGame);
    }

    private void OpenHeroAchievementsClick(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenGameAchievementsPage(ViewModel.BigPictureHeroGame);
        _sound.PlayMenuOpen();
    }

    private void OpenHeroWebsiteClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedGame = ViewModel.BigPictureHeroGame;
        ViewModel.OpenSelectedGameWebsite();
        _sound.PlaySoftClick();
    }

    private void OpenHeroTrailerClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedGame = ViewModel.BigPictureHeroGame;
        ViewModel.OpenSelectedGameTrailer();
        _sound.PlaySoftClick();
    }

    private async void BigPictureScanFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose a folder to scan for games" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await RunUiTaskAsync(
            () => ViewModel.ScanFolderAsync(dialog.FolderName),
            "CloudBerry couldn't complete the folder scan.");
        ViewModel.SelectedPrimaryPage = 1;
    }

    private async void BigPictureFillMetadataClick(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(
            () => ViewModel.DownloadMissingMetadataAsync(),
            "CloudBerry couldn't fill missing metadata.");
    }

    private async void BigPictureSyncAchievementsClick(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(
            () => ViewModel.SyncGamesMissingAchievementsAsync(),
            "CloudBerry couldn't sync missing achievements.");
    }

    private void OpenDesktopManagerClick(object sender, RoutedEventArgs e)
    {
        if (Owner is Window owner)
        {
            owner.Activate();
        }

        Close();
    }

    private bool _isNetflixMode = false;

    private void NetflixModeToggleClick(object sender, RoutedEventArgs e)
    {
        _isNetflixMode = !_isNetflixMode;
        ViewModel.IsCloudscapeCompact = _isNetflixMode;

        // ── Opacity ──────────────────────────────────────────────────────────
        // When collapsing: fade the hero OUT immediately so it vanishes cleanly
        // as the row shrinks. Don't touch Visibility — it's data-bound.
        // When expanding: start at opacity 0 (already 0 from the collapse), let
        // the row open first, then fade in once the space exists.
        var easeOut = new PowerEase { EasingMode = EasingMode.EaseOut, Power = 5 };
        if (_isNetflixMode)
        {
            // Collapsing — fade out quickly, then let the row shrink.
            CloudscapeHeroShell.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(CloudscapeHeroShell.Opacity, 0,
                    new Duration(TimeSpan.FromMilliseconds(120)))
                { EasingFunction = easeOut });
        }
        else
        {
            // Expanding — hold at 0 for the first half of the row animation,
            // then fade in over the second half so content appears into space
            // that already exists rather than fighting a shrinking clip.
            CloudscapeHeroShell.BeginAnimation(UIElement.OpacityProperty, null);
            CloudscapeHeroShell.Opacity = 0;
            CloudscapeHeroShell.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1,
                    new Duration(TimeSpan.FromMilliseconds(180)))
                {
                    BeginTime = TimeSpan.FromMilliseconds(140),
                    EasingFunction = easeOut
                });
        }

        // ── Row height ───────────────────────────────────────────────────────
        // Animate the grid row height with a DispatcherTimer (WPF has no
        // built-in GridLengthAnimation).  Quintic ease-out is used throughout.
        double fromHeight = _isNetflixMode ? 240 : 0;
        double toHeight   = _isNetflixMode ? 0   : 240;
        double elapsed    = 0;
        const double durationMs = 280;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8) };
        timer.Tick += (s, _) =>
        {
            elapsed += 8;
            double t    = Math.Min(elapsed / durationMs, 1.0);
            double ease = 1 - Math.Pow(1 - t, 5); // quintic ease-out
            HeroRow.Height = new GridLength(fromHeight + (toHeight - fromHeight) * ease);
            if (t >= 1.0)
            {
                HeroRow.Height = new GridLength(toHeight);
                ((DispatcherTimer)s!).Stop();
            }
        };
        timer.Start();

        LibraryFilterPanel.Visibility = _isNetflixMode ? Visibility.Collapsed : Visibility.Visible;
        NetflixToggleIcon.Text  = _isNetflixMode ? "\uE8AA" : "\uE8A9";
        NetflixToggleLabel.Text = _isNetflixMode ? "Expand"  : "Compact";
    }

    private void CloseBigPictureClick(object sender, RoutedEventArgs e)
    {
        _sound.PlayMenuOpen();
        Close();
    }

    private void ShelfSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox || listBox.SelectedItem is not GameEntryViewModel game)
        {
            return;
        }

        if (ReferenceEquals(_activeShelfListBox, listBox) && _shelfDragStartPoint is not null)
        {
            return;
        }

        ViewModel.SelectedGame = game;
        listBox.ScrollIntoView(game);
        CenterShelfSelection(listBox, game);
        PulseShelfSelection(listBox, game);
        PulseHeroShell();
        _sound.PlayFocusMove();
        ResetHeroAdvanceTimer(); // user is actively navigating — reset the idle rotate
    }

    private void GameCardMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            element.DataContext is not GameEntryViewModel game ||
            !game.HasTrailer)
        {
            // No trailer — if we were showing one for a different card, stop it.
            StopTrailerPreview();
            return;
        }

        // If the same card is still hovered (e.g. mouse re-entered after a child element
        // briefly took focus), don't restart the timer or flicker the preview.
        if (ReferenceEquals(game, _pendingTrailerPreviewGame) ||
            ReferenceEquals(game, _activeTrailerPreviewGame))
        {
            return;
        }

        // Different card — cancel pending work and hide any active preview,
        // but DO NOT navigate the WebView2 to blank yet (avoid flash).
        _trailerPreviewTimer.Stop();
        _pendingTrailerPreviewGame = game;
        _pendingTrailerPreviewElement = element;

        // Hide the overlay immediately so it doesn't linger over the wrong card,
        // but keep the browser warm — we'll navigate it in the tick handler.
        if (_activeTrailerPreviewGame != null && !ReferenceEquals(_activeTrailerPreviewGame, game))
        {
            CardTrailerPreviewHost.Visibility = Visibility.Collapsed;
            CardTrailerPreviewHost.Opacity = 0;
            _activeTrailerPreviewGame = null;
        }

        _trailerPreviewTimer.Start();
    }

    private void GameCardMouseLeave(object sender, MouseEventArgs e)
    {
        // Case 1: mouse moved onto the preview overlay — keep playing.
        var pos = e.GetPosition(CardTrailerPreviewHost);
        if (CardTrailerPreviewHost.Visibility == Visibility.Visible &&
            pos.X >= 0 && pos.Y >= 0 &&
            pos.X <= CardTrailerPreviewHost.ActualWidth &&
            pos.Y <= CardTrailerPreviewHost.ActualHeight)
        {
            return;
        }

        // Case 2: the card scrolled away from under the cursor (vertical drag / momentum).
        // GetPosition returns the cursor relative to the card element — if it's outside the
        // card bounds the card has moved away even if no real MouseLeave fired cleanly.
        if (sender is FrameworkElement card)
        {
            var cardPos = e.GetPosition(card);
            if (cardPos.X >= 0 && cardPos.Y >= 0 &&
                cardPos.X <= card.ActualWidth &&
                cardPos.Y <= card.ActualHeight)
            {
                // Cursor is still physically over the card — don't stop.
                return;
            }
        }

        StopTrailerPreview();
    }

    private void TrailerPreviewTimerTick(object? sender, EventArgs e)
    {
        _trailerPreviewTimer.Stop();
        if (_pendingTrailerPreviewGame is not { HasTrailer: true } game)
            return;

        _activeTrailerPreviewGame = game;
        // Do NOT set IsTrailerPreviewActive here — that triggers the dead MediaElement
        // binding on every card and causes the visible flicker. The WebView2 overlay
        // handles its own visibility independently.
        ShowCardTrailerPreview(game);
    }

    /// <summary>
    /// Registers the FrameCreated hook exactly once. Subsequent calls are no-ops.
    /// When YouTube's iframe is created inside the trailer WebView2, we inject CSS
    /// directly into that frame's document — bypassing the cross-origin wall — to
    /// kill every YouTube overlay element before they can ever render.
    /// </summary>
    private void RegisterTrailerFrameHook()
    {
        if (_trailerFrameHookRegistered || CardTrailerPreviewBrowser?.CoreWebView2 == null) return;
        _trailerFrameHookRegistered = true;
        CardTrailerPreviewBrowser.CoreWebView2.FrameCreated += OnTrailerChildFrameCreated;
    }

    private static void OnTrailerChildFrameCreated(object? sender, CoreWebView2FrameCreatedEventArgs e)
    {
        // CoreWebView2Frame.AddScriptToExecuteOnDocumentCreatedAsync is main-frame only.
        // Instead: hook NavigationCompleted on the child frame and inject via ExecuteScriptAsync.
        // A <style> tag appended to <head> is global — it applies to every selector match
        // past, present, and future — so injecting after nav-complete still kills every
        // YouTube overlay element the moment it is created by the player JS.
        var frame = e.Frame;
        frame.NavigationCompleted += async (_, args) =>
        {
            if (!args.IsSuccess) return;
            try
            {
                await frame.ExecuteScriptAsync(
                    "(function(){" +
                    "var s=document.createElement('style');" +
                    "s.textContent=" +
                    "'.ytp-chrome-top,.ytp-chrome-bottom,.ytp-title,.ytp-title-text,.ytp-title-expanded," +
                    ".ytp-watermark,.ytp-pause-overlay,.ytp-bezel,.ytp-bezel-text," +
                    ".ytp-cued-thumbnail-overlay,.ytp-ce-element,.ytp-endscreen-content," +
                    ".ytp-gradient-top,.ytp-gradient-bottom,.ytp-contextmenu," +
                    ".ytp-content-rating,.ytp-feature-list-container," +
                    ".ytp-cards-teaser,.ytp-iv-video-content" +
                    "{display:none!important;opacity:0!important;visibility:hidden!important}';" +
                    "(document.head||document.documentElement).appendChild(s)" +
                    "})()");
            }
            catch { /* frame may be torn down during the await; safe to ignore */ }
        };
    }

    private void StopTrailerPreview()
    {
        _trailerPreviewTimer.Stop();
        _pendingTrailerPreviewGame = null;
        _pendingTrailerPreviewElement = null;
        _activeTrailerPreviewGame = null;

        // Fade overlay out smoothly instead of snapping to hidden.
        CardTrailerPreviewHost.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(CardTrailerPreviewHost.Opacity, 0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            });
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            CardTrailerPreviewHost.Visibility = Visibility.Collapsed;
            CardTrailerPreviewFallback.Visibility = Visibility.Collapsed;
        }));

        // Fade audio out then navigate to blank — avoids abrupt cut.
        try
        {
            if (CardTrailerPreviewBrowser.CoreWebView2 != null)
            {
                // Ramp volume to 0 over 200ms via JS, then navigate away.
                _ = CardTrailerPreviewBrowser.CoreWebView2.ExecuteScriptAsync(
                    "(function(){" +
                    "  if(window.player&&player.setVolume){" +
                    "    var v=88,t=setInterval(function(){" +
                    "      v=Math.max(0,v-12);player.setVolume(v);" +
                    "      if(v<=0)clearInterval(t);" +
                    "    },20);" +
                    "  }" +
                    "})()");
                // Navigate to blank after the fade (220ms)
                var nav = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
                nav.Tick += (s, _) =>
                {
                    ((DispatcherTimer)s!).Stop();
                    try
                    {
                        if (CardTrailerPreviewBrowser.CoreWebView2 != null)
                            CardTrailerPreviewBrowser.NavigateToString(
                                "<!doctype html><html><head><meta charset='utf-8'></head>" +
                                "<body style='margin:0;background:#050b12;overflow:hidden'></body></html>");
                    }
                    catch { }
                };
                nav.Start();
            }
        }
        catch
        {
            // Browser shutdown during window close; ignore.
        }
    }

    private async void ShowCardTrailerPreview(GameEntryViewModel game)
    {
        if (_pendingTrailerPreviewElement is null || !game.HasEmbeddableTrailer)
            return;

        // Position the overlay over the card.
        var origin = _pendingTrailerPreviewElement.TranslatePoint(new Point(0, 0), CloudscapeRoot);
        var w = Math.Max(180, _pendingTrailerPreviewElement.ActualWidth);
        var h = Math.Max(240, _pendingTrailerPreviewElement.ActualHeight);
        CardTrailerPreviewHost.Width  = w;
        CardTrailerPreviewHost.Height = h;
        CardTrailerPreviewHost.Margin = new Thickness(origin.X, origin.Y, 0, 0);

        try
        {
            CardTrailerPreviewFallback.Visibility = Visibility.Collapsed;

            if (CardTrailerPreviewBrowser.CoreWebView2 == null)
            {
                // Reuse the pre-warm task if one is already running (or finished) instead of
                // starting a second EnsureCoreWebView2Async on this control — calling it twice
                // with two different environments throws, which used to silently kill the
                // very first trailer hover of a session before pre-warm had finished.
                if (_trailerBrowserEnvironmentTask == null)
                {
                    var options = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
                    _trailerBrowserEnvironmentTask = CoreWebView2Environment.CreateAsync(null, null, options);
                }
                var env = await _trailerBrowserEnvironmentTask;

                // Guard: user may have moused off while we awaited the environment.
                if (!ReferenceEquals(game, _pendingTrailerPreviewGame))
                    return;

                if (CardTrailerPreviewBrowser.CoreWebView2 == null)
                {
                    await CardTrailerPreviewBrowser.EnsureCoreWebView2Async(env);
                }

                // Guard again after the second await.
                if (!ReferenceEquals(game, _pendingTrailerPreviewGame))
                    return;

                CardTrailerPreviewBrowser.CoreWebView2!.Settings.AreDevToolsEnabled  = false;
                CardTrailerPreviewBrowser.CoreWebView2!.Settings.IsStatusBarEnabled   = false;
                RegisterTrailerFrameHook();
            }

            var embedUrl = game.TrailerEmbedUrl;
            if (string.IsNullOrWhiteSpace(embedUrl))
            {
                CardTrailerPreviewHost.Visibility = Visibility.Visible;
                CardTrailerPreviewHost.Opacity = 1;
                CardTrailerPreviewFallback.Visibility = Visibility.Visible;
                return;
            }

            // Show the overlay now — the WebView2 is ready and we're committed to this card.
            CardTrailerPreviewHost.Visibility = Visibility.Visible;
            CardTrailerPreviewHost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

            if (_trailerServer == null || !_trailerServer.IsListening)
            {
                CardTrailerPreviewBrowser.CoreWebView2!.Navigate(embedUrl);
            }
            else
            {
                var pageUrl = $"http://localhost:{_trailerServerPort}/trailer?embed={Uri.EscapeDataString(embedUrl)}";
                CardTrailerPreviewBrowser.CoreWebView2!.Navigate(pageUrl);

                EventHandler<CoreWebView2NavigationCompletedEventArgs>? navHandler = null;
                navHandler = async (s, ev) =>
                {
                    CardTrailerPreviewBrowser.CoreWebView2!.NavigationCompleted -= navHandler;
                    if (!ev.IsSuccess) return;
                    await Task.Delay(500);
                    if (CardTrailerPreviewBrowser.CoreWebView2 == null) return;
                    await CardTrailerPreviewBrowser.CoreWebView2.ExecuteScriptAsync(
                        "if(window._unmute)window._unmute();");
                };
                CardTrailerPreviewBrowser.CoreWebView2!.NavigationCompleted += navHandler;
            }
        }
        catch
        {
            CardTrailerPreviewHost.Visibility = Visibility.Visible;
            CardTrailerPreviewHost.Opacity = 1;
            CardTrailerPreviewFallback.Visibility = Visibility.Visible;
        }
    }

    // ── Shelf "See All" ──────────────────────────────────────────────────────
    private void SeeAllShelfClick(object sender, RoutedEventArgs e)
    {
        // The button's Tag is the shelf Title — match it to a library category or jump to All Games.
        var tag = (sender as FrameworkElement)?.Tag as string ?? string.Empty;

        // Try to find a matching BigPictureLibraryCategory by Title
        var match = ViewModel.BigPictureLibraryCategories
            .FirstOrDefault(c => c.Title.Equals(tag, StringComparison.OrdinalIgnoreCase));

        if (match != null)
            ViewModel.SelectedBigPictureLibraryCategory = match;
        else
            ViewModel.SelectedBigPictureLibraryCategory =
                ViewModel.BigPictureLibraryCategories.FirstOrDefault();

        ViewModel.SelectedPrimaryPage = 1; // jump to Library tab
        _sound.PlaySoftClick();
    }

    // ── Achievement filter buttons ─────────────────────────────────────────
    private void AchievementFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string filter) return;
        if (ViewModel.SelectedGame is null || AchievementItemsControl is null) return;

        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(AchievementItemsControl.ItemsSource);
        if (view is null) return;

        view.Filter = filter switch
        {
            "unlocked" => (obj) => obj is CloudBerry.App.ViewModels.AchievementEntryViewModel a && a.IsUnlocked,
            "locked"   => (obj) => obj is CloudBerry.App.ViewModels.AchievementEntryViewModel a && !a.IsUnlocked,
            _          => null
        };

        _sound.PlaySoftClick();
    }

    // ── Achievement game row click ──────────────────────────────────────────────
    private void AchievementGameRowClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is GameEntryViewModel game)
        {
            ViewModel.SelectedGame = game;
            _sound.PlaySoftClick();
        }
    }

    // ── BigPicture library sort pills ────────────────────────────────────────
    private void BigPictureLibrarySortClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string key)
        {
            ViewModel.BigPictureLibrarySortKey = key;
            _sound.PlaySoftClick();
        }
    }

    // ── BigPicture search ───────────────────────────────────────────────────
    private void BigPictureSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ViewModel.BigPictureSearchText = string.Empty;
            BigPictureSearchBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            e.Handled = true;
        }
    }

    private void BigPictureSearchClearClick(object sender, RoutedEventArgs e)
    {
        ViewModel.BigPictureSearchText = string.Empty;
        BigPictureSearchBox.Focus();
    }

    // ── Card context menu ────────────────────────────────────────────────────
    // Helper: get the GameEntryViewModel from a context menu item click,
    // which fires on the ContextMenu's PlacementTarget (the card Grid).
    private static GameEntryViewModel? GameFromContextSender(object sender)
    {
        if (sender is MenuItem mi &&
            mi.CommandParameter is GameEntryViewModel g) return g;
        // Walk up: the ContextMenu.PlacementTarget is the card Grid
        if (sender is MenuItem menuItem)
        {
            var ctx = menuItem.GetValue(ContextMenuService.PlacementTargetProperty);
            if (ctx is FrameworkElement fe && fe.DataContext is GameEntryViewModel gvm)
                return gvm;
            if (menuItem.Parent is ContextMenu cm &&
                cm.PlacementTarget is FrameworkElement target &&
                target.DataContext is GameEntryViewModel gvm2)
                return gvm2;
        }
        return null;
    }

    private async void CardContextLaunchClick(object sender, RoutedEventArgs e)
    {
        var game = GameFromContextSender(sender);
        if (game == null) return;
        await RunUiTaskAsync(() => ViewModel.LaunchGameAsync(game), "CloudBerry couldn't launch that game.");
        _sound.PlayLaunchHandoff();
    }

    private async void CardContextFavoriteClick(object sender, RoutedEventArgs e)
    {
        var game = GameFromContextSender(sender);
        if (game == null) return;
        await RunUiTaskAsync(
            () => ViewModel.SetFavoriteForGamesAsync(new[] { game }, !game.IsFavorite),
            "CloudBerry couldn't update favorite status.");
        _sound.PlaySoftClick();
    }

    private async void CardContextMarkPlayingClick(object sender, RoutedEventArgs e)
    {
        var game = GameFromContextSender(sender);
        if (game == null) return;
        game.Status = "Playing";
        await RunUiTaskAsync(() => ViewModel.SaveSelectedGameAsync(), "CloudBerry couldn't save game status.");
        _sound.PlaySoftClick();
    }

    private async void CardContextMarkCompletedClick(object sender, RoutedEventArgs e)
    {
        var game = GameFromContextSender(sender);
        if (game == null) return;
        game.Status = "Completed";
        await RunUiTaskAsync(() => ViewModel.SaveSelectedGameAsync(), "CloudBerry couldn't save game status.");
        _sound.PlaySoftClick();
    }

    private void CardContextDetailsClick(object sender, RoutedEventArgs e)
    {
        var game = GameFromContextSender(sender);
        if (game == null) return;
        ViewModel.SelectedGame = game;
        ViewModel.OpenGameMetadataWorkspace(game);
        _sound.PlayMenuOpen();
    }

    private void CardContextOpenLibraryClick(object sender, RoutedEventArgs e)
    {
        var game = GameFromContextSender(sender);
        if (game == null) return;
        ViewModel.RevealGameInLibrary(game);
        _sound.PlaySoftClick();
    }

    private async void CardContextRemoveClick(object sender, RoutedEventArgs e)
    {
        var game = GameFromContextSender(sender);
        if (game == null) return;
        var result = MessageBox.Show(this,
            $"Remove \u201c{game.Title}\u201d from your library? This will not delete any files.",
            "CloudBerry", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        await RunUiTaskAsync(
            () => ViewModel.RemoveGamesAsync(new[] { game }),
            "CloudBerry couldn't remove that game.");
        _sound.PlaySoftClick();
    }

    private async void ShelfMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Double-click is fully handled by ShelfPointerDragStart (ClickCount > 1 path).
        // This handler exists only as a safety net for keyboards/accessibility; mark
        // handled so it doesn't race with the drag-start launch.
        e.Handled = true;
    }

    private async void CompactPlayBtnClick(object sender, MouseButtonEventArgs e)
    {
        // Walk up to find the DataContext — the Border sits inside a DataTemplate
        // whose DataContext is the GameEntryViewModel for that card.
        var game = (sender as FrameworkElement)?.DataContext as GameEntryViewModel
                ?? FindAncestor<ListBoxItem>((DependencyObject)sender)?.DataContext as GameEntryViewModel;
        if (game == null) return;

        e.Handled = true; // prevent the click from also selecting/deselecting the card
        await RunUiTaskAsync(
            () => ViewModel.LaunchGameAsync(game),
            "CloudBerry couldn't launch that game.");
        _sound.PlayLaunchHandoff();
    }

    private async void ShelfPointerDragStart(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        var scrollViewer = FindDescendantScrollViewer(listBox);
        if (scrollViewer is null)
        {
            return;
        }

        _activeShelfScrollViewer = scrollViewer;
        _activeShelfListBox = listBox;
        _shelfDragStartPoint = e.GetPosition(this);
        _shelfDragStartOffset = scrollViewer.HorizontalOffset;
        _pendingShelfClickGame = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as GameEntryViewModel;
        _shelfDragMoved = false;
        _shelfVelocityX = 0;
        _shelfPrevDragPoint = null;
        _shelfMomentumTimer?.Stop();
        _shelfWasDoubleClick = false;
        e.Handled = true;

        if (e.ClickCount > 1 && _pendingShelfClickGame is not null)
        {
            _shelfWasDoubleClick = true;
            listBox.SelectedItem = _pendingShelfClickGame;
            await RunUiTaskAsync(
                () => ViewModel.LaunchGameAsync(_pendingShelfClickGame),
                "CloudBerry couldn't launch the selected game.");
            _sound.PlayLaunchHandoff();
            ReleaseShelfDrag(listBox);
        }
    }

    private void ShelfPointerDragMove(object sender, MouseEventArgs e)
    {
        if (sender is not ListBox listBox ||
            _shelfDragStartPoint is null ||
            e.LeftButton != MouseButtonState.Pressed ||
            !ReferenceEquals(_activeShelfListBox, listBox))
        {
            return;
        }

        var currentPoint = e.GetPosition(this);
        var deltaX = currentPoint.X - _shelfDragStartPoint.Value.X;
        if (!_shelfDragMoved && Math.Abs(deltaX) <= ShelfDragThreshold)
        {
            return;
        }

        if (!_shelfDragMoved)
        {
            _shelfDragMoved = true;
            _activeShelfScrollViewer ??= FindDescendantScrollViewer(listBox);
            _shelfDragStartOffset = _activeShelfScrollViewer?.HorizontalOffset ?? 0;
            _shelfPrevDragPoint = currentPoint;
            _shelfVelocityX = 0;
            listBox.CaptureMouse();
            SetDragCueVisible(true);
            // Immediately kill any trailer preview — the shelf is being dragged.
            StopTrailerPreview();
        }

        if (_activeShelfScrollViewer is not null)
        {
            // Track instantaneous velocity for momentum (heavier weighting on recent movement)
            if (_shelfPrevDragPoint.HasValue)
            {
                _shelfVelocityX = (_shelfPrevDragPoint.Value.X - currentPoint.X) * 0.75 + _shelfVelocityX * 0.25;
            }
            _shelfPrevDragPoint = currentPoint;

            _activeShelfScrollViewer.ScrollToHorizontalOffset(_shelfDragStartOffset - deltaX);
            e.Handled = true;
        }
    }

    private void ShelfPointerDragEnd(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        var handledDrag = _shelfDragMoved;
        var pendingClickGame = _pendingShelfClickGame;
        var scrollViewer = _activeShelfScrollViewer;
        var velocity = _shelfVelocityX;

        ReleaseShelfDrag(listBox);

        // Launch momentum scroll if flicked fast enough
        if (handledDrag && scrollViewer is not null && Math.Abs(velocity) > 1.2)
        {
            _shelfMomentumTimer?.Stop();
            double v = velocity * 1.1; // slight boost at release for a snappier feel
            _shelfMomentumTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _shelfMomentumTimer.Tick += (s, _) =>
            {
                v *= 0.91; // softer friction — scrolls further on a flick
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + v);
                if (Math.Abs(v) < 0.4)
                    ((DispatcherTimer)s!).Stop();
            };
            _shelfMomentumTimer.Start();
        }

        if (handledDrag)
        {
            e.Handled = true;
            return;
        }

        if (pendingClickGame is not null && !_shelfWasDoubleClick)
        {
            listBox.SelectedItem = pendingClickGame;
            listBox.Focus();
            // Single click opens the full-screen detail overlay.
            OpenDetailOverlay(pendingClickGame);
            e.Handled = true;
        }
    }

    private void ShelfPointerDragLost(object sender, MouseEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            ReleaseShelfDrag(listBox);
        }
    }

    private void ShelfPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // The ListBox's internal ScrollViewer swallows wheel events when the cursor
        // is over a card. We intercept here and apply the same momentum impulse that
        // OnPreviewMouseWheel uses, bypassing the routed event system entirely so
        // the momentum state is updated correctly.
        if (sender is not DependencyObject source) return;

        // Always kill any active trailer preview — the card is scrolling away.
        StopTrailerPreview();

        // Walk up past the ListBox to find the outer vertically-scrollable ScrollViewer.
        var element = VisualTreeHelper.GetParent(source);
        while (element is not null)
        {
            if (element is ScrollViewer sv && sv.ScrollableHeight > 0)
            {
                // Mirror OnPreviewMouseWheel's impulse logic exactly.
                double impulse = -e.Delta / 3.0;

                if (Math.Sign(impulse) == Math.Sign(_verticalScrollVelocity))
                    _verticalScrollVelocity += impulse * 0.40;
                else
                    _verticalScrollVelocity = impulse;

                _verticalScrollVelocity = Math.Clamp(_verticalScrollVelocity, -1400, 1400);
                sv.ScrollToVerticalOffset(sv.VerticalOffset + impulse);
                _lastVerticalScrollViewer = sv;

                if (!_verticalMomentumTimer!.IsEnabled)
                    _verticalMomentumTimer.Start();

                e.Handled = true;
                return;
            }
            element = VisualTreeHelper.GetParent(element);
        }
    }

    private void ReleaseShelfDrag(ListBox listBox)
    {
        if (listBox.IsMouseCaptured)
        {
            listBox.ReleaseMouseCapture();
        }

        _shelfDragStartPoint = null;
        _activeShelfScrollViewer = null;
        _activeShelfListBox = null;
        _pendingShelfClickGame = null;
        _shelfDragMoved = false;
        _shelfWasDoubleClick = false;
        _shelfPrevDragPoint = null;
        SetDragCueVisible(false);
    }

    private async void WindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // ── Hero shelf arrow navigation ───────────────────────────────────────
        // Left/Right arrow keys when on the Home tab move hero selection through
        // the shelf, giving full couch-mode keyboard nav without needing a mouse.
        if (e.Key is Key.Left or Key.Right &&
            ViewModel.SelectedPrimaryPage == 0 &&
            e.OriginalSource is not TextBox)
        {
            var games = ViewModel.Games;
            var current = ViewModel.BigPictureHeroGame;
            if (games.Count > 0 && current != null)
            {
                var idx = games.IndexOf(current);
                if (idx >= 0)
                {
                    var next = e.Key == Key.Right
                        ? games[(idx + 1) % games.Count]
                        : games[(idx - 1 + games.Count) % games.Count];
                    ViewModel.SelectedGame = next;
                    _sound.PlayFocusMove();
                    ResetHeroAdvanceTimer(); // reset idle timer on manual nav
                    e.Handled = true;
                    return;
                }
            }
        }
        if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && ViewModel.SelectedPrimaryPage == 3)
        {
            ViewModel.UnlockCloudTheatre();
            _sound.PlaySoftClick();
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                // If the detail overlay is open, close it instead of closing the window.
                if (_isDetailOpen)
                {
                    CloseDetailOverlay();
                    e.Handled = true;
                    return;
                }
                Close();
                e.Handled = true;
                return;
            case Key.F11:
                // On Theatre tab: F11 toggles watch mode (full player) instead of closing
                if (ViewModel.SelectedPrimaryPage == 4 && ViewModel.IsCloudTheatreUnlocked)
                {
                    if (ViewModel.IsTheatreWatchMode)
                    {
                        ExitTheatreWatchModeClick(this, new RoutedEventArgs());
                    }
                    else
                    {
                        EnterTheatreWatchModeClick(this, new RoutedEventArgs());
                    }
                    e.Handled = true;
                    return;
                }
                Close();
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
                ViewModel.SelectedPrimaryPage = 2;
                e.Handled = true;
                return;
            case Key.D4:
            case Key.NumPad4:
                ViewModel.SelectedPrimaryPage = 3;
                e.Handled = true;
                return;
            case Key.D5:
            case Key.NumPad5:
                if (ViewModel.IsCloudTheatreUnlocked)
                {
                    ViewModel.SelectedPrimaryPage = 4;
                    e.Handled = true;
                    return;
                }

                break;
        }

        if (e.Key == Key.Enter && e.OriginalSource is not TextBox)
        {
            await RunUiTaskAsync(
                () => ViewModel.LaunchGameAsync(ViewModel.SelectedGame ?? ViewModel.BigPictureHeroGame),
                "CloudBerry couldn't launch the selected game.");
            _sound.PlayLaunchHandoff();
            e.Handled = true;
        }
    }

    // SearchCloudTheatreClick, LoadTheatreCategoryClick, LoadSelectedTheatreResultClick,
    // OpenSelectedTheatreResultExternalClick — removed with the old category/source system.

    private void NavigateTheatreAddressClick(object sender, RoutedEventArgs e)
    {
        ViewModel.NavigateTheatreAddress();
        _sound.PlaySoftClick();
    }

    private void TheatreBackClick(object sender, RoutedEventArgs e)
    {
        if (CloudTheatreBrowser.CanGoBack)
        {
            CloudTheatreBrowser.GoBack();
            _sound.PlaySoftClick();
        }
    }

    private void TheatreForwardClick(object sender, RoutedEventArgs e)
    {
        if (CloudTheatreBrowser.CanGoForward)
        {
            CloudTheatreBrowser.GoForward();
            _sound.PlaySoftClick();
        }
    }

    private void TheatreRefreshClick(object sender, RoutedEventArgs e)
    {
        CloudTheatreBrowser.CoreWebView2?.Reload();
        _sound.PlaySoftClick();
    }

    private void TheatreHomeClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetTheatrePlayer();
        LoadCloudTheatrePlaceholder();
        _sound.PlaySoftClick();
    }

    private void EnterTheatreWatchModeClick(object sender, RoutedEventArgs e)
    {
        ViewModel.EnterTheatreWatchMode();
        // Hide nav bar and hero so the player fills the entire window
        CloudscapeTopNav.Visibility = Visibility.Collapsed;
        CloudscapeHeroShell.Visibility = Visibility.Collapsed;
        _sound.PlayMenuOpen();
    }

    private void ExitTheatreWatchModeClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ExitTheatreWatchMode();
        // Restore nav bar; hero stays hidden on Theatre tab
        CloudscapeTopNav.Visibility = Visibility.Visible;
        _sound.PlayMenuOpen();
    }

    private void TheatreAddressBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return || e.Key == Key.Enter)
        {
            ViewModel.NavigateTheatreAddress();
            _sound.PlaySoftClick();
            e.Handled = true;
        }
    }

    private void TheatreSaveSiteClick(object sender, RoutedEventArgs e)
    {
        ViewModel.AddSavedTheatreSite();
        _sound.PlayConfirm();
    }

    private void TheatreToggleSidebarClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsTheatreSidebarOpen = !ViewModel.IsTheatreSidebarOpen;
        _sound.PlaySoftClick();
    }

    private void SavedSiteSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && ViewModel.SelectedSavedSite != null)
        {
            ViewModel.NavigateToSavedSite(ViewModel.SelectedSavedSite);
            _sound.PlaySoftClick();
        }
    }

    private void RemoveSavedSiteClick(object sender, RoutedEventArgs e)
    {
        ViewModel.RemoveSelectedSavedSite();
        _sound.PlaySoftClick();
    }

    private void AddTheatreSourceClick(object sender, RoutedEventArgs e)
    {
        ViewModel.AddSavedTheatreSite();
        _sound.PlaySoftClick();
    }

    private void RemoveTheatreSourceClick(object sender, RoutedEventArgs e)
    {
        ViewModel.RemoveSelectedSavedSite();
        _sound.PlaySoftClick();
    }

    private void OnFirstActivated(object? sender, EventArgs e)
    {
        // Unhook immediately — we only need to start the timer once on first show.
        Activated -= OnFirstActivated;
        // Now the window is actually visible; safe to start the idle-hide timer.
        _cursorHideTimer?.Start();
    }

    private void OnBigPictureMouseMove(object sender, MouseEventArgs e)
    {
        ShowCursor();
    }

    private void ShowCursor()
    {
        if (_cursorHidden)
        {
            Mouse.OverrideCursor = null;
            _cursorHidden = false;
        }
        _cursorHideTimer?.Stop();
        _cursorHideTimer?.Start();
    }

    /// <summary>
    /// Fades the full-screen black overlay in (0→1 over 380ms), plays the launch sound,
    /// then fades back out (1→0 over 280ms) so the window is clean if the game exits quickly.
    /// </summary>
    private async Task PlayLaunchHandoffAnimationAsync()
    {
        var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(380)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        LaunchHandoffOverlay.BeginAnimation(OpacityProperty, fadeIn);
        await Task.Delay(380);
        // Fade back out after a beat so if game closes quickly the overlay is gone
        var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(280)))
        {
            BeginTime = TimeSpan.FromMilliseconds(600)
        };
        LaunchHandoffOverlay.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void HeroParallaxMouseMove(object sender, MouseEventArgs e)
    {
        var motion = ResolveThemeMotionPack();
        if (motion.ParallaxBackground <= 0.01 && motion.ParallaxForeground <= 0.01)
        {
            return;
        }

        var position = e.GetPosition(CloudscapeHeroShell);
        var width = Math.Max(1, CloudscapeHeroShell.ActualWidth);
        var height = Math.Max(1, CloudscapeHeroShell.ActualHeight);
        var normalizedX = (position.X / width) - 0.5;
        var normalizedY = (position.Y / height) - 0.5;

        HeroBgATransform.X = -normalizedX * motion.ParallaxBackground;
        HeroBgATransform.Y = -normalizedY * motion.ParallaxBackground;
        HeroBgBTransform.X = -normalizedX * motion.ParallaxBackground;
        HeroBgBTransform.Y = -normalizedY * motion.ParallaxBackground;
        CloudscapeHeroCoverParallax.X = normalizedX * motion.ParallaxForeground;
        CloudscapeHeroCoverParallax.Y = normalizedY * motion.ParallaxForeground;
    }

    private void HeroParallaxMouseLeave(object sender, MouseEventArgs e)
    {
        ResetParallax(HeroBgATransform);
        ResetParallax(HeroBgBTransform);
        ResetParallax(CloudscapeHeroCoverParallax);
    }

    private void CloudscapePointerMove(object sender, MouseEventArgs e)
    {
        // If a trailer is active, check every move whether the cursor has left the source card.
        // This catches leaving via hero panel, nav bar, empty background, etc. — anything that
        // doesn't reliably fire MouseLeave on the card element itself.
        if (_pendingTrailerPreviewElement != null || _activeTrailerPreviewGame != null)
        {
            var sourceElement = _pendingTrailerPreviewElement;
            if (sourceElement != null)
            {
                var cardPos = e.GetPosition(sourceElement);
                bool stillOver = cardPos.X >= 0 && cardPos.Y >= 0 &&
                                 cardPos.X <= sourceElement.ActualWidth &&
                                 cardPos.Y <= sourceElement.ActualHeight;

                // Also allow cursor to be over the preview overlay itself.
                var overlayPos = e.GetPosition(CardTrailerPreviewHost);
                bool overOverlay = CardTrailerPreviewHost.Visibility == Visibility.Visible &&
                                   overlayPos.X >= 0 && overlayPos.Y >= 0 &&
                                   overlayPos.X <= CardTrailerPreviewHost.ActualWidth &&
                                   overlayPos.Y <= CardTrailerPreviewHost.ActualHeight;

                if (!stillOver && !overOverlay)
                    StopTrailerPreview();
            }
        }

        if (!ViewModel.VisualEffectsEnabled)
        {
            return;
        }

        var point = e.GetPosition(CloudscapeRoot);
        var width = CloudscapePointerGlow.ActualWidth > 0 ? CloudscapePointerGlow.ActualWidth : CloudscapePointerGlow.Width;
        var height = CloudscapePointerGlow.ActualHeight > 0 ? CloudscapePointerGlow.ActualHeight : CloudscapePointerGlow.Height;
        CloudscapePointerGlowTransform.X = point.X - (width / 2);
        CloudscapePointerGlowTransform.Y = point.Y - (height / 2);
        var glowOpacity = Math.Clamp(0.18 + ViewModel.EffectiveVisualEffectIntensity * 0.12, 0.18, 0.38);

        if (_isCloudscapePointerGlowVisible)
        {
            CloudscapePointerGlow.BeginAnimation(UIElement.OpacityProperty, null);
            CloudscapePointerGlow.Opacity = glowOpacity;
            return;
        }

        _isCloudscapePointerGlowVisible = true;
        CloudscapePointerGlow.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(glowOpacity, TimeSpan.FromMilliseconds(60))
        {
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void CloudscapePointerLeave(object sender, MouseEventArgs e)
    {
        _isCloudscapePointerGlowVisible = false;
        CloudscapePointerGlow.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void CloudscapePointerDown(object sender, MouseButtonEventArgs e)
    {
        if (!ViewModel.VisualEffectsEnabled || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        var point = e.GetPosition(CloudscapeRoot);
        CloudscapeClickPulseTransform.X = point.X - (CloudscapeClickPulse.Width / 2);
        CloudscapeClickPulseTransform.Y = point.Y - (CloudscapeClickPulse.Height / 2);
        CloudscapeClickPulseScale.ScaleX = 0.7;
        CloudscapeClickPulseScale.ScaleY = 0.7;

        var ease = new QuarticEase { EasingMode = EasingMode.EaseOut };
        var pulseOpacity = Math.Clamp(0.56 + ViewModel.EffectiveVisualEffectIntensity * 0.24, 0.58, 0.92);
        CloudscapeClickPulse.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(pulseOpacity, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease });
        CloudscapeClickPulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.7, 2.65, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease });
        CloudscapeClickPulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.7, 2.65, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease });
    }

    private void SetDragCueVisible(bool visible)
    {
        CloudscapeDragCue.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(visible ? 0.94 : 0, TimeSpan.FromMilliseconds(visible ? 100 : 180))
        {
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        });
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);
        if (e.Handled) return;

        var element = e.OriginalSource as System.Windows.DependencyObject;
        ScrollViewer? outerSv = null;
        bool insideListBox = false;

        // Walk up the visual tree to find the outer page ScrollViewer.
        // The ListBox has its own internal ScrollViewer (horizontal shelf scroller) —
        // we need to skip that one and find the next vertically scrollable one above it.
        bool skippedListBoxScroller = false;
        while (element is not null)
        {
            if (element is ListBox)
                insideListBox = true;

            if (element is ScrollViewer sv)
            {
                if (insideListBox && !skippedListBoxScroller)
                {
                    // This is the ListBox's own horizontal scroller — skip it.
                    skippedListBoxScroller = true;
                }
                else if (sv.ScrollableHeight > 0)
                {
                    outerSv = sv;
                    break;
                }
            }

            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }

        if (outerSv is null) return;

        // Each wheel notch is 120 delta units. Convert to pixels with a comfortable multiplier.
        double impulse = -e.Delta / 3.6;

        // Accumulate: new impulses in the same direction stack up (flick acceleration),
        // direction reversals cut velocity immediately.
        if (Math.Sign(impulse) == Math.Sign(_verticalScrollVelocity))
            _verticalScrollVelocity += impulse * 0.32;  // same direction — build momentum
        else
            _verticalScrollVelocity = impulse;           // direction change — snap to new direction

        // Clamp so a hard spin can't send the page flying off screen.
        _verticalScrollVelocity = Math.Clamp(_verticalScrollVelocity, -1200, 1200);

        // Apply the immediate impulse so the first tick feels instant.
        outerSv.ScrollToVerticalOffset(outerSv.VerticalOffset + impulse);
        _lastVerticalScrollViewer = outerSv;

        // Stop any active trailer — the card is scrolling away.
        StopTrailerPreview();

        // Start the momentum timer if not already running.
        if (!_verticalMomentumTimer!.IsEnabled)
            _verticalMomentumTimer.Start();

        e.Handled = true;
    }

    private void VerticalMomentumTick(object? sender, EventArgs e)
    {
        if (_lastVerticalScrollViewer is null)
        {
            _verticalMomentumTimer!.Stop();
            return;
        }

        // Exponential decay — 0.93 per frame at 60fps gives ~2.5 seconds of natural coast.
        // Feels like a phone scroll: snappy start, graceful stop.
        _verticalScrollVelocity *= 0.93;

        _lastVerticalScrollViewer.ScrollToVerticalOffset(
            _lastVerticalScrollViewer.VerticalOffset + _verticalScrollVelocity);

        if (Math.Abs(_verticalScrollVelocity) < 0.5)
        {
            _verticalScrollVelocity = 0;
            _lastVerticalScrollViewer = null;
            _verticalMomentumTimer!.Stop();
        }
    }

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }

            var nested = FindDescendantScrollViewer(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static T? FindAncestor<T>(DependencyObject? root)
        where T : DependencyObject
    {
        while (root is not null)
        {
            if (root is T match)
            {
                return match;
            }

            root = VisualTreeHelper.GetParent(root);
        }

        return null;
    }

    private void CenterShelfSelection(ListBox listBox, GameEntryViewModel game)
    {
        var scrollViewer = FindDescendantScrollViewer(listBox);
        if (scrollViewer is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (listBox.ItemContainerGenerator.ContainerFromItem(game) is not FrameworkElement container)
            {
                return;
            }

            var transform = container.TransformToAncestor(scrollViewer);
            var position = transform.Transform(new Point(0, 0));
            var targetOffset = scrollViewer.HorizontalOffset + position.X - ((scrollViewer.ViewportWidth - container.ActualWidth) / 2);
            var maxOffset = Math.Max(0, scrollViewer.ExtentWidth - scrollViewer.ViewportWidth);
            scrollViewer.ScrollToHorizontalOffset(Math.Clamp(targetOffset, 0, maxOffset));
        }));
    }

    private static void PulseShelfSelection(ListBox listBox, GameEntryViewModel game)
    {
        if (listBox.ItemContainerGenerator.ContainerFromItem(game) is not ListBoxItem container)
        {
            return;
        }

        container.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = EnsureWritableScaleTransform(container);

        var easing = new QuarticEase { EasingMode = EasingMode.EaseOut };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.022, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = easing });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.022, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = easing });
    }

    private void PulseHeroShell()
    {
        CloudscapeHeroShell.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = EnsureWritableScaleTransform(CloudscapeHeroShell);

        var easing = new QuarticEase { EasingMode = EasingMode.EaseOut };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.012, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = easing });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.012, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = easing });
    }

    private void PulseProfileCard()
    {
        CloudscapeProfileCard.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = EnsureWritableScaleTransform(CloudscapeProfileCard);

        var easing = new QuarticEase { EasingMode = EasingMode.EaseOut };
        CloudscapeProfileCard.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.78, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = easing });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.022, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = easing });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.022, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = easing });
    }

    // ── Hero auto-advance ──────────────────────────────────────────────────────
    // Rotates the hero game every 12 seconds when the user is idle on the Home tab.
    // Resets whenever the user manually selects or navigates.
    private void StartHeroAdvanceTimer()
    {
        _heroAdvanceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        _heroAdvanceTimer.Tick += (_, _) =>
        {
            if (ViewModel.SelectedPrimaryPage != 0 || ViewModel.Games.Count < 2)
                return;
            var games = ViewModel.Games;
            var current = ViewModel.BigPictureHeroGame;
            var idx = current != null ? games.IndexOf(current) : -1;
            var next = games[(idx + 1) % games.Count];
            ViewModel.SelectedGame = next;
        };
        _heroAdvanceTimer.Start();
    }

    private void ResetHeroAdvanceTimer()
    {
        _heroAdvanceTimer?.Stop();
        _heroAdvanceTimer?.Start();
    }

    private void StartLiveSessionTimer()
    {
        _liveSessionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _liveSessionTimer.Tick += (_, _) =>
        {
            // Tick every 30s and refresh LiveSessionLabel on all running games.
            // This keeps the hero "Playing — X min" counter current without
            // requiring GameEntryViewModel to hold its own timer.
            foreach (var game in ViewModel.Games.Where(g => g.IsRunning))
                game.RefreshLiveSessionLabel();
        };
        _liveSessionTimer.Start();
    }

    // ── Shelf slide-in stagger — called once when the Home page becomes active ──
    private void AnimateShelvesFadeIn()
    {
        // Walk the visual tree to find all shelf StackPanels (the DataTemplate root)
        // and animate each one in with a slight vertical offset stagger.
        var host = FindDescendant<ItemsControl>(CloudscapeRoot,
            ic => ic.ItemsSource == ViewModel.BigPictureHomeShelves);
        if (host == null) return;

        int shelfIndex = 0;
        foreach (var item in host.Items)
        {
            if (host.ItemContainerGenerator.ContainerFromItem(item) is not FrameworkElement container)
                continue;

            var delay = shelfIndex * 55; // 55ms stagger between shelves
            var tt = new TranslateTransform(0, 22);
            container.RenderTransform = tt;
            container.Opacity = 0;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delay) };
            timer.Tick += (s, _) =>
            {
                ((DispatcherTimer)s!).Stop();
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                var dur = TimeSpan.FromMilliseconds(280);
                container.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0, 1, dur) { EasingFunction = ease });
                tt.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(22, 0, dur) { EasingFunction = ease });
            };
            timer.Start();
            shelfIndex++;
        }
    }

    private static T? FindDescendant<T>(DependencyObject root, Func<T, bool>? predicate = null)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match && (predicate == null || predicate(match)))
                return match;
            var nested = FindDescendant(child, predicate);
            if (nested != null) return nested;
        }
        return null;
    }

    protected override void OnClosed(EventArgs e)
    {
        // Detach shared view-model subscriptions when Big Picture closes.
        // Stop the hide timer FIRST so its tick can't race and re-hide after we restore.
        _cursorHideTimer?.Stop();
        _cursorHidden = false;
        Mouse.OverrideCursor = null;
        _heroAdvanceTimer?.Stop();
        _liveSessionTimer?.Stop();
        _shelfMomentumTimer?.Stop();
        _gamepadTimer?.Stop();
        _trailerServer?.Stop();
        // Close any open detail overlay without animation (window is going away).
        ViewModel.CloseCloudscapeDetail();
        // Restore achievement pop service to the main window.
        ViewModel.ReinitAchievementPopToMainWindow();
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnClosed(e);
    }

    private async Task RunUiTaskAsync(Func<Task> action, string errorMessage)
    {
        try
        {
            // Keep recoverable command failures in the UI instead of surfacing dispatcher crashes.
            await action();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"{errorMessage}\n\n{exception.Message}",
                "CloudBerry", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void ResetParallax(TranslateTransform transform)
    {
        var easing = new QuarticEase { EasingMode = EasingMode.EaseOut };
        transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(transform.X, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = easing });
        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(transform.Y, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = easing });
    }

    private static ScaleTransform EnsureWritableScaleTransform(FrameworkElement element)
    {
        switch (element.RenderTransform)
        {
            case ScaleTransform scale when !scale.IsFrozen:
                return scale;
            case TransformGroup group:
            {
                if (group.IsFrozen)
                {
                    group = group.Clone();
                    element.RenderTransform = group;
                }

                var existingScale = group.Children.OfType<ScaleTransform>().FirstOrDefault();
                if (existingScale is { IsFrozen: false })
                {
                    return existingScale;
                }

                if (existingScale is { IsFrozen: true })
                {
                    var index = group.Children.IndexOf(existingScale);
                    var replacement = new ScaleTransform(existingScale.ScaleX, existingScale.ScaleY);
                    group.Children[index] = replacement;
                    return replacement;
                }

                var inserted = new ScaleTransform(1, 1);
                group.Children.Insert(0, inserted);
                return inserted;
            }
            case Transform transform when transform != Transform.Identity:
            {
                var group = new TransformGroup();
                group.Children.Add(new ScaleTransform(1, 1));
                group.Children.Add(transform.IsFrozen ? transform.Clone() : transform);
                element.RenderTransform = group;
                return (ScaleTransform)group.Children[0];
            }
            default:
            {
                var scale = new ScaleTransform(1, 1);
                element.RenderTransform = scale;
                return scale;
            }
        }
    }

    private (double BackgroundOpacity, int HeroCrossfadeMs, double ParallaxBackground, double ParallaxForeground) ResolveThemeMotionPack()
    {
        return ViewModel.AppearanceThemeKey switch
        {
            "NieR Automata" => (0.19, 300, 6, 3),
            "Wuthering Waves" => (0.27, 520, 14, 8),
            "Neon Matrix" => (0.22, 360, 9, 5),
            "Retro Glitch" => (0.2, 340, 8, 4),
            "Blueberry Midnight" => (0.24, 420, 10, 6),
            _ => (0.22, 420, 8, 5)
        };
    }

    // ── Game Detail Overlay ──────────────────────────────────────────────────

    private void OpenDetailOverlay(GameEntryViewModel game)
    {
        ViewModel.ShowCloudscapeDetail(game);

        // Populate background art — load off-thread so a slow/missing image
        // never blocks the UI or throws an unhandled dispatcher exception.
        DetailBgImage.Source = null;
        var bgPath = game.DisplayBackgroundImagePath;
        if (!string.IsNullOrWhiteSpace(bgPath))
        {
            LoadDetailBgAsync(bgPath);
        }

        // Slide in from the right.
        _isDetailOpen = true;
        CloudscapeDetailOverlay.Visibility = Visibility.Visible;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        CloudscapeDetailOverlay.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        DetailPanelLift.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(60, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease });

        _sound.PlayMenuOpen();
    }

    private async void LoadDetailBgAsync(string bgPath)
    {
        // Decode on a thread-pool thread so network/disk latency and format
        // failures (e.g. .webp with no native decoder) never crash the dispatcher.
        BitmapImage? bmp = null;
        try
        {
            bmp = await Task.Run(() =>
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = bgPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? new Uri(bgPath)
                    : new Uri(bgPath, UriKind.RelativeOrAbsolute);
                img.DecodePixelWidth = 960;
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze(); // must Freeze before crossing thread boundary
                return img;
            });
        }
        catch { /* bad path, unsupported format, or network error — leave source null */ }

        // Overlay may have been closed while we were loading; only apply if still open.
        if (_isDetailOpen)
            DetailBgImage.Source = bmp;
    }

    private void CloseDetailOverlay()
    {
        if (!_isDetailOpen) return;
        _isDetailOpen = false;

        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease };
        fadeOut.Completed += (_, _) =>
        {
            CloudscapeDetailOverlay.Visibility = Visibility.Collapsed;
        };
        CloudscapeDetailOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        DetailPanelLift.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(0, 40, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });

        ViewModel.CloseCloudscapeDetail();
        _sound.PlaySoftClick();
    }

    private void DetailBackClick(object sender, RoutedEventArgs e) => CloseDetailOverlay();

    private async void DetailLaunchClick(object sender, RoutedEventArgs e)
    {
        var game = ViewModel.CloudscapeDetailGame;
        if (game == null) return;
        CloseDetailOverlay();
        await RunUiTaskAsync(
            () => ViewModel.LaunchGameAsync(game),
            "CloudBerry couldn't launch the selected game.");
        _sound.PlayLaunchHandoff();
    }

    private async void DetailMetadataClick(object sender, RoutedEventArgs e)
    {
        var game = ViewModel.CloudscapeDetailGame;
        if (game == null) return;
        CloseDetailOverlay();
        // Return to desktop shell and open metadata window.
        Close();
        await Task.Delay(200);
        await ((MainWindow)Application.Current.MainWindow!).OpenMetadataWindowForGameFromCloudscapeAsync(game);
    }

    private void DetailAchievementsClick(object sender, RoutedEventArgs e)
    {
        var game = ViewModel.CloudscapeDetailGame;
        if (game == null) return;
        CloseDetailOverlay();
        // Stay in Cloudscape — jump directly to the Achievements tab with this game selected.
        ViewModel.SelectedGame = game;
        ViewModel.SelectedPrimaryPage = 2; // Achievements tab in Cloudscape nav
        _sound.PlayMenuOpen();
    }

    private async void DetailFavoriteClick(object sender, RoutedEventArgs e)
    {
        var game = ViewModel.CloudscapeDetailGame;
        if (game == null) return;
        await ViewModel.ToggleFavoriteAsync(game);
        _sound.PlayConfirm();
    }

    // ── Home shelf smooth scroll ─────────────────────────────────────────

    private void CloudscapeHomeScrollWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        // Kill trailer preview when the user scrolls the page — cards are moving away.
        StopTrailerPreview();
        if (_homeScrollTimer is null)
        {
            _homeScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _homeScrollTimer.Tick += HomeScrollTick;
        }
        if (!_homeScrollTimer.IsEnabled)
            _homeScrollTarget = CloudscapeHomeScroll.VerticalOffset;
        // Stronger multiplier (1.2) and additive accumulation for wheel spins feel natural.
        _homeScrollTarget = Math.Clamp(
            _homeScrollTarget - e.Delta * 1.1,
            0, CloudscapeHomeScroll.ScrollableHeight);
        _homeScrollTimer.Start();
    }

    private void HomeScrollTick(object? sender, EventArgs e)
    {
        var diff = _homeScrollTarget - CloudscapeHomeScroll.VerticalOffset;
        if (Math.Abs(diff) < 0.5)
        {
            CloudscapeHomeScroll.ScrollToVerticalOffset(_homeScrollTarget);
            _homeScrollTimer?.Stop();
            return;
        }
        // Ease-out: move 18% of remaining distance each frame — snappier initial response
        CloudscapeHomeScroll.ScrollToVerticalOffset(
            CloudscapeHomeScroll.VerticalOffset + diff * 0.18);
    }

    // ── Now Playing Bar ─────────────────────────────────────────────────────

    private void UpdateNowPlayingBar()
    {
        bool isRunning = ViewModel.IsRunning;
        if (isRunning && NowPlayingBar.Visibility == Visibility.Collapsed)
        {
            // Slide in from the bottom.
            NowPlayingBar.Visibility = Visibility.Visible;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            NowPlayingBar.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
        }
        else if (!isRunning && NowPlayingBar.Visibility == Visibility.Visible)
        {
            // Fade out and hide.
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease };
            fade.Completed += (_, _) => { NowPlayingBar.Visibility = Visibility.Collapsed; };
            NowPlayingBar.BeginAnimation(UIElement.OpacityProperty, fade);
        }
    }

    private void NowPlayingRevealClick(object sender, RoutedEventArgs e)
    {
        // Close Cloudscape and reveal the running game in the library.
        var running = ViewModel.Games.FirstOrDefault(g => g.IsRunning);
        Close();
        if (running != null)
            ViewModel.RevealGameInLibrary(running);
    }

    // ── Achievement Pop re-init for Cloudscape window ─────────────────────

    private void InitCloudscapeAchievementPop()
    {
        // Direct achievement pops to appear on this (Cloudscape) window while it's open.
        // This ensures toasts appear on the correct display/screen.
        ViewModel.ReinitAchievementPopForWindow(this,
            duration: 5,
            corner: "BottomRight");
    }

    // ── Gamepad Navigation ───────────────────────────────────────────────
    // Polls XInput via Win32 P/Invoke at ~60fps. No NuGet dependency required.
    // Only active while Cloudscape is open and focused.

    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Explicit)]
    private struct XInputState
    {
        [System.Runtime.InteropServices.FieldOffset(0)]  public uint dwPacketNumber;
        [System.Runtime.InteropServices.FieldOffset(4)]  public ushort wButtons;
        [System.Runtime.InteropServices.FieldOffset(6)]  public byte bLeftTrigger;
        [System.Runtime.InteropServices.FieldOffset(7)]  public byte bRightTrigger;
        [System.Runtime.InteropServices.FieldOffset(8)]  public short sThumbLX;
        [System.Runtime.InteropServices.FieldOffset(10)] public short sThumbLY;
        [System.Runtime.InteropServices.FieldOffset(12)] public short sThumbRX;
        [System.Runtime.InteropServices.FieldOffset(14)] public short sThumbRY;
    }

    [System.Runtime.InteropServices.DllImport("xinput1_4.dll",
        EntryPoint = "XInputGetState",
        CallingConvention = System.Runtime.InteropServices.CallingConvention.Winapi)]
    private static extern uint XInputGetState(uint dwUserIndex, out XInputState pState);

    // XInput button masks
    private const ushort XINPUT_GAMEPAD_DPAD_UP    = 0x0001;
    private const ushort XINPUT_GAMEPAD_DPAD_DOWN  = 0x0002;
    private const ushort XINPUT_GAMEPAD_DPAD_LEFT  = 0x0004;
    private const ushort XINPUT_GAMEPAD_DPAD_RIGHT = 0x0008;
    private const ushort XINPUT_GAMEPAD_START       = 0x0010;
    private const ushort XINPUT_GAMEPAD_BACK        = 0x0020;
    private const ushort XINPUT_GAMEPAD_A           = 0x1000;
    private const ushort XINPUT_GAMEPAD_B           = 0x2000;
    private const ushort XINPUT_GAMEPAD_X           = 0x4000;
    private const ushort XINPUT_GAMEPAD_Y           = 0x8000;
    private const ushort XINPUT_GAMEPAD_LEFT_SHOULDER  = 0x0100;
    private const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 0x0200;

    // Track previous button state so we only fire on rising edges (press, not hold).
    private ushort _prevGamepadButtons = 0;
    private int    _gamepadStickCooldown = 0; // frames until next stick repeat fires

    private void StartGamepadTimer()
    {
        _gamepadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _gamepadTimer.Tick += GamepadPollTick;
        _gamepadTimer.Start();
    }

    private async void GamepadPollTick(object? sender, EventArgs e)
    {
        // Skip polling when the window is not the active foreground window.
        if (!IsActive) return;

        XInputState state;
        uint result;
        try { result = XInputGetState(0, out state); }
        catch { return; } // xinput1_4.dll not found — silent no-op

        if (result != 0) return; // no controller connected

        ushort buttons  = state.wButtons;
        ushort newPress = (ushort)(buttons & ~_prevGamepadButtons); // rising edge only
        _prevGamepadButtons = buttons;

        // Stick threshold (32k range, use 50% deflection)
        const short StickThreshold = 16000;
        bool stickLeft  = state.sThumbLX < -StickThreshold;
        bool stickRight = state.sThumbLX >  StickThreshold;

        // Stick repeat cooldown
        if (_gamepadStickCooldown > 0) { _gamepadStickCooldown--; }

        bool fireStick = _gamepadStickCooldown == 0 && (stickLeft || stickRight);
        if (fireStick) _gamepadStickCooldown = 12; // ~200ms repeat at 60fps

        // ── Detail overlay: A = confirm/launch, B = back
        if (_isDetailOpen)
        {
            if ((newPress & XINPUT_GAMEPAD_A) != 0)
            {
                var game = ViewModel.CloudscapeDetailGame;
                if (game != null)
                {
                    CloseDetailOverlay();
                    await RunUiTaskAsync(() => ViewModel.LaunchGameAsync(game),
                        "CloudBerry couldn't launch the selected game.");
                    _sound.PlayLaunchHandoff();
                }
            }
            if ((newPress & XINPUT_GAMEPAD_B) != 0) CloseDetailOverlay();
            return; // while detail is open, suppress all other nav
        }

        // ── Global navigation
        if ((newPress & XINPUT_GAMEPAD_B) != 0)
        {
            // B closes Cloudscape (like Escape)
            _sound.PlaySoftClick();
            Close();
            return;
        }

        if ((newPress & XINPUT_GAMEPAD_START) != 0)
        {
            // Start = return to desktop
            _sound.PlayMenuOpen();
            Close();
            return;
        }

        // ── Tab navigation via bumpers
        if ((newPress & XINPUT_GAMEPAD_LEFT_SHOULDER) != 0)
        {
            int prev = Math.Max(0, ViewModel.SelectedPrimaryPage - 1);
            if (prev != ViewModel.SelectedPrimaryPage)
            {
                ViewModel.SelectedPrimaryPage = prev;
                _sound.PlayFocusMove();
            }
        }
        if ((newPress & XINPUT_GAMEPAD_RIGHT_SHOULDER) != 0)
        {
            int maxTab = ViewModel.IsCloudTheatreUnlocked ? 4 : 3;
            int next = Math.Min(maxTab, ViewModel.SelectedPrimaryPage + 1);
            if (next != ViewModel.SelectedPrimaryPage)
            {
                ViewModel.SelectedPrimaryPage = next;
                _sound.PlayFocusMove();
            }
        }

        // ── Hero selection via d-pad or left stick (Home tab only)
        if (ViewModel.SelectedPrimaryPage == 0)
        {
            bool moveLeft  = (newPress & XINPUT_GAMEPAD_DPAD_LEFT)  != 0 || (fireStick && stickLeft);
            bool moveRight = (newPress & XINPUT_GAMEPAD_DPAD_RIGHT) != 0 || (fireStick && stickRight);

            var games = ViewModel.Games;
            var current = ViewModel.BigPictureHeroGame;
            if (games.Count > 0 && current != null && (moveLeft || moveRight))
            {
                int idx  = games.IndexOf(current);
                int next = moveRight
                    ? (idx + 1) % games.Count
                    : (idx - 1 + games.Count) % games.Count;
                ViewModel.SelectedGame = games[next];
                _sound.PlayFocusMove();
                ResetHeroAdvanceTimer();
            }

            // A = open detail for hero game
            if ((newPress & XINPUT_GAMEPAD_A) != 0 && ViewModel.BigPictureHeroGame != null)
            {
                OpenDetailOverlay(ViewModel.BigPictureHeroGame);
            }
        }
    }
}
