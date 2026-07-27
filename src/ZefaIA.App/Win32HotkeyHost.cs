using System.Runtime.Versioning;
using System.Windows.Interop;
using ZefaIA.Core.Interfaces;

namespace ZefaIA.App;

/// <summary>
/// A message-only window that exists solely to receive WM_HOTKEY.
///
/// Global hotkeys are delivered to a window, and the tray app has none: the overlay is
/// hidden and gets recreated per meeting, so it cannot own a registration that must
/// survive the whole session. This creates its own invisible HWND at startup and keeps
/// it for the lifetime of the app.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class Win32HotkeyHost : IHotkeyHost
{
    private const int WM_HOTKEY = 0x0312;

    private readonly HwndSource _source;
    private bool _disposed;

    public IntPtr Handle => _source.Handle;

    public event Action<IntPtr>? HotkeyMessage;

    public Win32HotkeyHost()
    {
        // HWND_MESSAGE (-3) as the parent makes this a message-only window: never
        // rendered, never in the taskbar, never in Alt+Tab.
        _source = new HwndSource(new HwndSourceParameters("ZefaIA.HotkeyHost")
        {
            Width = 0,
            Height = 0,
            ParentWindow = new IntPtr(-3)
        });

        _source.AddHook(OnMessage);
    }

    private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_HOTKEY) return IntPtr.Zero;

        HotkeyMessage?.Invoke(wParam);
        handled = true;
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _source.RemoveHook(OnMessage);
        _source.Dispose();
    }
}
