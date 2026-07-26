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

    [LibraryImport("user32.dll")]
    internal static partial int GetWindowLongW(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll")]
    internal static partial int SetWindowLongW(IntPtr hWnd, int nIndex, int dwNewLong);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    internal static void MakeClickThrough(IntPtr hwnd)
    {
        var style = GetWindowLongW(hwnd, GWL_EXSTYLE);
        SetWindowLongW(hwnd, GWL_EXSTYLE,
            style | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    internal static void RemoveClickThrough(IntPtr hwnd)
    {
        var style = GetWindowLongW(hwnd, GWL_EXSTYLE);
        SetWindowLongW(hwnd, GWL_EXSTYLE,
            style & ~WS_EX_TRANSPARENT);
    }

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
