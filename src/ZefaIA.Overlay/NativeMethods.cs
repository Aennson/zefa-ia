using System.Runtime.InteropServices;

namespace ZefaIA.Overlay;

internal static partial class NativeMethods
{
    internal const int GWL_EXSTYLE = -20;

    internal const int WS_EX_TRANSPARENT = 0x00000020;
    internal const int WS_EX_LAYERED = 0x00080000;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;
    internal const int WS_EX_NOACTIVATE = 0x08000000;

    internal const uint WDA_NONE = 0x00000000;
    internal const uint WDA_MONITOR = 0x00000001;
    internal const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    internal const int WM_NCHITTEST = 0x0084;
    internal const int HTTRANSPARENT = -1;
    internal const int HTCLIENT = 1;
    internal const int HTCAPTION = 2;

    // Resize regions returned from WM_NCHITTEST. A window with custom chrome gets no
    // resize border for free, so it has to name these itself.
    internal const int HTLEFT = 10;
    internal const int HTRIGHT = 11;
    internal const int HTTOP = 12;
    internal const int HTTOPLEFT = 13;
    internal const int HTTOPRIGHT = 14;
    internal const int HTBOTTOM = 15;
    internal const int HTBOTTOMLEFT = 16;
    internal const int HTBOTTOMRIGHT = 17;

    /// <summary>
    /// Maps a point inside the window to the resize region it belongs to, or
    /// <see cref="HTCLIENT"/> when it is not close enough to an edge.
    /// Kept free of WPF types so the edge maths can be unit-tested directly.
    /// </summary>
    internal static int HitTestResizeBorder(
        double x, double y, double width, double height, double border)
    {
        var left = x <= border;
        var right = x >= width - border;
        var top = y <= border;
        var bottom = y >= height - border;

        if (top && left) return HTTOPLEFT;
        if (top && right) return HTTOPRIGHT;
        if (bottom && left) return HTBOTTOMLEFT;
        if (bottom && right) return HTBOTTOMRIGHT;
        if (left) return HTLEFT;
        if (right) return HTRIGHT;
        if (top) return HTTOP;
        if (bottom) return HTBOTTOM;

        return HTCLIENT;
    }

    [LibraryImport("user32.dll")]
    internal static partial int GetWindowLongW(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll")]
    internal static partial int SetWindowLongW(IntPtr hWnd, int nIndex, int dwNewLong);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    /// <summary>
    /// Styles that make this behave like an overlay rather than an app window: layered so
    /// it can be translucent, tool-window so it stays out of Alt+Tab and the taskbar, and
    /// no-activate so clicking it never steals focus from the meeting app.
    ///
    /// Deliberately excludes WS_EX_TRANSPARENT. These four used to be applied together,
    /// and that one style makes the window ignore every mouse message — the tabs, the
    /// buttons and even dragging were dead, with nothing in the UI able to turn it off.
    /// </summary>
    internal static void ApplyOverlayStyles(IntPtr hwnd)
    {
        var style = GetWindowLongW(hwnd, GWL_EXSTYLE);
        SetWindowLongW(hwnd, GWL_EXSTYLE,
            style | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    /// <summary>
    /// Toggles click-through ("ghost mode"): the overlay stays visible but every click
    /// lands on whatever is behind it. All-or-nothing per window — Windows cannot make
    /// only part of a window transparent to the mouse.
    /// </summary>
    internal static void SetClickThrough(IntPtr hwnd, bool enabled)
    {
        var style = GetWindowLongW(hwnd, GWL_EXSTYLE);

        SetWindowLongW(hwnd, GWL_EXSTYLE, enabled
            ? style | WS_EX_TRANSPARENT
            : style & ~WS_EX_TRANSPARENT);
    }

    internal static bool IsClickThrough(IntPtr hwnd) =>
        (GetWindowLongW(hwnd, GWL_EXSTYLE) & WS_EX_TRANSPARENT) != 0;

    internal static bool ExcludeFromCapture(IntPtr hwnd)
    {
        if (SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE))
            return true;

        return SetWindowDisplayAffinity(hwnd, WDA_MONITOR);
    }

    internal static void IncludeInCapture(IntPtr hwnd)
    {
        SetWindowDisplayAffinity(hwnd, WDA_NONE);
    }
}
