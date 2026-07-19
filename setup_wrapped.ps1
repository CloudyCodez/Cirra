# Full setup script - run once to wire everything in

$proj = 'C:\Users\conno\Documents\CloudBerry\src\CloudBerry.App'
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'

# ── 1. Patch MainWindow.xaml.cs ──────────────────────────────────────────────
$path = "$proj\MainWindow.xaml.cs"
$c = [System.IO.File]::ReadAllText($path)

if (-not $c.Contains('RegisterHotKey')) {
    $c = $c.Replace(
        "    private const uint MONITOR_DEFAULTTONEAREST = 2;",
        @"    [DllImport(""user32.dll"", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport(""user32.dll"", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int  HOTKEY_ID_OPEN           = 0x4CB1;
    private const uint MOD_CONTROL              = 0x0002;
    private const uint MOD_SHIFT                = 0x0004;
    private const uint VK_G                     = 0x47;
    private const int  WM_HOTKEY               = 0x0312;
    private const uint MONITOR_DEFAULTTONEAREST = 2;"
    )
    Write-Host "  Added RegisterHotKey P/Invokes"
} else { Write-Host "  RegisterHotKey already present" }

if (-not $c.Contains('WM_HOTKEY')) {
    $c = $c.Replace(
        "        if (msg == WM_GETMINMAXINFO)",
        @"        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID_OPEN)
        {
            if (WindowState == System.Windows.WindowState.Minimized)
                WindowState = System.Windows.WindowState.Normal;
            Activate();
            handled = true;
        }

        if (msg == WM_GETMINMAXINFO)"
    )
    Write-Host "  Added WM_HOTKEY handler"
} else { Write-Host "  WM_HOTKEY already present" }

if (-not $c.Contains('RegisterHotKey(handle')) {
    $c = $c.Replace(
        "            hwndSource?.AddHook(WndProc);",
        @"            hwndSource?.AddHook(WndProc);
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            RegisterHotKey(handle, HOTKEY_ID_OPEN, MOD_CONTROL | MOD_SHIFT, VK_G);"
    )
    Write-Host "  Registered hotkey in SourceInitialized"
} else { Write-Host "  Hotkey registration already present" }

if (-not $c.Contains('UnregisterHotKey')) {
    $c = $c.Replace(
        "        base.OnClosed(e);",
        @"        var hwnd2 = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        UnregisterHotKey(hwnd2, HOTKEY_ID_OPEN);
        base.OnClosed(e);"
    )
    Write-Host "  Added UnregisterHotKey in OnClosed"
} else { Write-Host "  UnregisterHotKey already present" }

if (-not $c.Contains('OpenYearWrappedClick')) {
    $c = $c.Replace(
        "    private void OpenMetadataWindow()",
        @"    private void OpenYearWrappedClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var win = new WrappedWindow(ViewModel);
        win.Owner = this;
        win.ShowDialog();
    }

    private void OpenMetadataWindow()"
    )
    Write-Host "  Added OpenYearWrappedClick"
} else { Write-Host "  OpenYearWrappedClick already present" }

[System.IO.File]::WriteAllText($path, $c)
Write-Host "MainWindow.xaml.cs patched."

# ── 2. Copy WrappedWindow files ──────────────────────────────────────────────
# These need to be dropped from outputs - copy them here manually after downloading
# from Claude's artifact output, OR paste the content below:

$xamlPath   = "$proj\WrappedWindow.xaml"
$csharpPath = "$proj\WrappedWindow.xaml.cs"

if (-not (Test-Path $xamlPath)) {
    Write-Host "  NEED TO COPY: WrappedWindow.xaml -> $xamlPath"
    Write-Host "  Download from Claude's output and place there."
} else { Write-Host "  WrappedWindow.xaml: OK" }

if (-not (Test-Path $csharpPath)) {
    Write-Host "  NEED TO COPY: WrappedWindow.xaml.cs -> $csharpPath"
} else { Write-Host "  WrappedWindow.xaml.cs: OK" }

# ── 3. Add Year Wrapped button to MainWindow XAML ───────────────────────────
$xamlMain = "$proj\MainWindow.xaml"
$xamlContent = [System.IO.File]::ReadAllText($xamlMain)

if (-not $xamlContent.Contains('OpenYearWrappedClick')) {
    # Add button near the Launch Cloudscape button in the title bar area
    $xamlContent = $xamlContent.Replace(
        'Content="Launch Cloudscape"',
        'Content="Launch Cloudscape"'  # we find a nearby anchor below
    )
    # Find the settings/action area to insert Wrapped button  
    $anchor = 'Click="LaunchCloudscapeClick"'
    if ($xamlContent.Contains($anchor)) {
        $xamlContent = $xamlContent.Replace($anchor,
            $anchor + "`n                    <Button Content=""&#x2728; Wrapped"" Margin=""6,0,0,0"" Padding=""10,5"" Style=""{StaticResource GhostButtonStyle}"" Click=""OpenYearWrappedClick"" ToolTip=""View your Year Wrapped stats""/>")
        [System.IO.File]::WriteAllText($xamlMain, $xamlContent)
        Write-Host "  Added Year Wrapped button to MainWindow.xaml"
    } else {
        Write-Host "  Could not find LaunchCloudscapeClick anchor - add button manually"
    }
} else { Write-Host "  Year Wrapped button already present in XAML" }

# ── 4. Verify ────────────────────────────────────────────────────────────────
Write-Host "`nVerification:"
$final = [System.IO.File]::ReadAllText("$proj\MainWindow.xaml.cs")
@('RegisterHotKey','WM_HOTKEY','OpenYearWrappedClick','UnregisterHotKey') | ForEach-Object {
    Write-Host ("  {0,-30} {1}" -f $_, $(if ($final.Contains($_)) {"OK"} else {"MISSING"}))
}
