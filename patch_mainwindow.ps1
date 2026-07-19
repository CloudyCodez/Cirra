# Patch MainWindow.xaml.cs with all missing features
$path = 'C:\Users\conno\Documents\CloudBerry\src\CloudBerry.App\MainWindow.xaml.cs'
$content = [System.IO.File]::ReadAllText($path)

# 1. Add hotkey P/Invokes after GetMonitorInfo
$old1 = "    private const uint MONITOR_DEFAULTTONEAREST = 2;"
$new1 = @"
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int  HOTKEY_ID_OPEN           = 0x4CB1;
    private const uint MOD_CONTROL              = 0x0002;
    private const uint MOD_SHIFT                = 0x0004;
    private const uint VK_G                     = 0x47;
    private const int  WM_HOTKEY               = 0x0312;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
"@
$content = $content.Replace($old1, $new1)

# 2. Add WM_HOTKEY to WndProc (after the WM_GETMINMAXINFO block close)
$old2 = "        if (msg == WM_GETMINMAXINFO)"
$new2 = @"
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID_OPEN)
        {
            // Ctrl+Shift+G — bring CloudBerry to front or toggle BigPicture
            if (WindowState == System.Windows.WindowState.Minimized)
                WindowState = System.Windows.WindowState.Normal;
            Activate();
            handled = true;
        }

        if (msg == WM_GETMINMAXINFO)
"@
$content = $content.Replace($old2, $new2)

# 3. Register hotkey in SourceInitialized
$old3 = "            hwndSource?.AddHook(WndProc);"
$new3 = @"            hwndSource?.AddHook(WndProc);
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            RegisterHotKey(handle, HOTKEY_ID_OPEN, MOD_CONTROL | MOD_SHIFT, VK_G);"
$content = $content.Replace($old3, $new3)

# 4. Unregister hotkey in OnClosed
$old4 = "        base.OnClosed(e);"
$new4 = @"        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        UnregisterHotKey(handle, HOTKEY_ID_OPEN);
        base.OnClosed(e);"
$content = $content.Replace($old4, $new4)

# 5. Add OpenYearWrappedClick handler near other click handlers
$old5 = "    private void OpenMetadataWindow()"
$new5 = @"    private void OpenYearWrappedClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var win = new WrappedWindow(ViewModel);
        win.Owner = this;
        win.ShowDialog();
    }

    private void OpenMetadataWindow()
"@
$content = $content.Replace($old5, $new5)

[System.IO.File]::WriteAllText($path, $content)
Write-Host "Done. Checking changes..."

# Verify
$verify = [System.IO.File]::ReadAllText($path)
@('RegisterHotKey', 'WM_HOTKEY', 'OpenYearWrappedClick', 'UnregisterHotKey', 'WrappedWindow') | ForEach-Object {
    $found = $verify.Contains($_)
    Write-Host ("  {0,-30} {1}" -f $_, $(if ($found) { "OK" } else { "MISSING" }))
}
