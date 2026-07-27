namespace ZefaIA.Core.Interfaces;

/// <summary>
/// Supplies the window handle that Windows delivers global hotkey messages to.
///
/// <c>RegisterHotKey</c> needs an HWND with a running message loop, and this app has no
/// main window — it lives in the tray. The interface keeps that Win32 detail out of the
/// meeting graph so the pipeline can be driven from tests without a desktop.
/// </summary>
public interface IHotkeyHost : IDisposable
{
    IntPtr Handle { get; }

    /// <summary>
    /// Raised for each WM_HOTKEY message, carrying its wParam (the hotkey id that
    /// <see cref="Triggers.HotkeyTrigger.RegisterHotkey"/> returned).
    /// </summary>
    event Action<IntPtr>? HotkeyMessage;
}
