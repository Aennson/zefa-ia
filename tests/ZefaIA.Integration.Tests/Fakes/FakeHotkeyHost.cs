using ZefaIA.Core.Interfaces;

namespace ZefaIA.Integration.Tests.Fakes;

/// <summary>
/// Stands in for the message-only window that receives WM_HOTKEY. Lets a test press the
/// shortcut without a desktop, a message loop, or actually registering a system-wide
/// hotkey (which would fail on a build agent and could collide with a real one).
/// </summary>
public sealed class FakeHotkeyHost : IHotkeyHost
{
    public IntPtr Handle => IntPtr.Zero;
    public bool Disposed { get; private set; }

    public event Action<IntPtr>? HotkeyMessage;

    /// <summary>Simulates the user pressing the registered shortcut.</summary>
    public void Press(int hotkeyId = 1) => HotkeyMessage?.Invoke(new IntPtr(hotkeyId));

    public void Dispose() => Disposed = true;
}
