using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CloudBerry.App.Controls;

/// <summary>
/// Hosts a foreign top-level window (a RetroArch process's own window) as a child element inside
/// CloudBerry's visual tree, so "Play" can keep the emulator's picture inside the app instead of
/// popping out a separate window. This is Win32 window reparenting (SetParent + style stripping),
/// not emulation — CloudBerry never touches how the game actually runs, only where its window sits
/// on screen.
///
/// Deliberately conservative failure handling throughout: if the target window has already closed,
/// or any native call fails, methods here return quietly rather than throwing. A failed embed
/// attempt must never crash CloudBerry or the game — see <see cref="EmbeddedPlayWindow"/> for the
/// fallback path when this control simply doesn't manage to attach.
/// </summary>
public sealed class EmbeddedProcessWindowHost : HwndHost
{
    private const int GWL_STYLE = -16;

    private const long WS_CHILD        = 0x40000000L;
    private const long WS_POPUP        = 0x80000000L;
    private const long WS_CAPTION      = 0x00C00000L;
    private const long WS_THICKFRAME   = 0x00040000L;
    private const long WS_SYSMENU      = 0x00080000L;
    private const long WS_MINIMIZEBOX  = 0x00020000L;
    private const long WS_MAXIMIZEBOX  = 0x00010000L;
    private const long WS_BORDER       = 0x00800000L;
    private const long WS_DLGFRAME     = 0x00400000L;

    private const uint SWP_NOZORDER    = 0x0004;
    private const uint SWP_NOACTIVATE  = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_NOSIZE      = 0x0001;
    private const uint SWP_NOMOVE      = 0x0002;

    private const int SW_SHOW = 5;

    private readonly IntPtr _childHandle;
    private bool _attached;

    public EmbeddedProcessWindowHost(IntPtr childHandle)
    {
        _childHandle = childHandle;
    }

    /// <summary>True once BuildWindowCore has actually reparented the child window. If this is false after construction, embedding didn't take — the caller should treat the game as running externally.</summary>
    public bool IsAttached => _attached;

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        if (_childHandle == IntPtr.Zero || !IsWindow(_childHandle))
        {
            // Nothing to attach to — return a null handle so this control just renders as empty
            // space instead of throwing. EmbeddedPlayWindow checks IsAttached and falls back.
            return new HandleRef(this, IntPtr.Zero);
        }

        try
        {
            // Strip title bar / border / system menu so the child fills the host cleanly, then mark
            // it as a child window (required for SetParent to behave like embedding rather than
            // just changing z-order of a still-top-level window).
            var style = GetWindowLongPtr(_childHandle, GWL_STYLE);
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_POPUP | WS_BORDER | WS_DLGFRAME);
            style |= WS_CHILD;
            SetWindowLongPtr(_childHandle, GWL_STYLE, style);

            SetParent(_childHandle, hwndParent.Handle);

            // Refresh the window frame after the style change above — deliberately NOT touching
            // geometry here (SWP_NOSIZE | SWP_NOMOVE). The very first version of this method omitted
            // those flags, which meant this call actually resized the freshly-reparented RetroArch
            // window down to 0×0 for a moment before anything else ran — giving its renderer an
            // invalid size to recover from right at the worst possible time, and was very likely the
            // real cause of the reported oversized/off-frame rendering. Actual sizing to fit the host
            // panel is handled explicitly and reliably by ResizeToFit, called from
            // BigPictureWindow's own SizeChanged handler — not by relying on this method or on
            // OnWindowPositionChanged below, which turned out not to fire consistently for this
            // control's specific nested-layout position.
            SetWindowPos(_childHandle, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_NOSIZE | SWP_NOMOVE);
            ShowWindow(_childHandle, SW_SHOW);
            SetFocus(_childHandle);

            _attached = true;
            return new HandleRef(this, _childHandle);
        }
        catch (Exception)
        {
            // Any failure here (window closed mid-reparent, access denied, etc.) — leave unattached
            // rather than let a native interop exception escape into WPF's layout/render pipeline.
            _attached = false;
            return new HandleRef(this, IntPtr.Zero);
        }
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        // Deliberately does not call DestroyWindow on the child. By the time this control is torn
        // down, EmbeddedPlayWindow has already terminated the emulator process on Closing (see its
        // code-behind) — the window is gone or on its way out with the process. Forcibly destroying
        // a window we don't fully own the lifetime semantics of is the riskier move here; doing
        // nothing and letting the OS clean up with the process is the safer default.
    }

    /// <summary>
    /// Explicitly resizes the reparented window to fill the given dimensions. Called from
    /// BigPictureWindow's SizeChanged handler on the container that hosts this control — a plain,
    /// always-fires WPF event — rather than depending on HwndHost's own OnWindowPositionChanged
    /// override, which in practice did not fire reliably for this control's specific nested-layout
    /// position: resizing only actually took effect after a window minimize/restore cycle forced a
    /// full re-layout, exactly the "buggy until you maximize and minimize" symptom that was reported.
    /// Driving this off an explicit, guaranteed event removes that dependency entirely.
    /// </summary>
    public void ResizeToFit(double width, double height)
    {
        if (!_attached || _childHandle == IntPtr.Zero || !IsWindow(_childHandle))
        {
            return;
        }

        var w = (int)Math.Max(0, width);
        var h = (int)Math.Max(0, height);
        SetWindowPos(_childHandle, IntPtr.Zero, 0, 0, w, h, SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOMOVE);
    }

    /// <summary>
    /// Forwards real Win32 focus to the embedded window whenever this host gets focus (e.g. the
    /// player clicks back into the embedded panel after alt-tabbing). RetroArch specifically treats
    /// "not the focused window" as "not active," and by default pauses the running core when it isn't
    /// active (its <c>pause_nonactive</c> setting) — that's the exact cause of the reported black
    /// screen with a pause icon. The temp appendconfig override in GameLauncher now also forces
    /// <c>pause_nonactive = "false"</c> for RetroArch specifically, but keeping real Win32 focus on
    /// the child window is still correct on its own merits: it's also what lets keyboard/gamepad
    /// input actually reach the emulator once embedded.
    /// </summary>
    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);
        if (_attached && _childHandle != IntPtr.Zero && IsWindow(_childHandle))
        {
            SetFocus(_childHandle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern long GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern long SetWindowLongPtr(IntPtr hWnd, int nIndex, long dwNewLong);
}
