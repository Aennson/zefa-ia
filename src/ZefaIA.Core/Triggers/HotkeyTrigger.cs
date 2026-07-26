using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.Core.Triggers;

[SupportedOSPlatform("windows")]
public sealed class HotkeyTrigger : ITriggerStrategy
{
    private readonly Dictionary<int, HotkeyBinding> _bindings = new();
    private IntPtr _hwnd;
    private int _nextId = 1;
    private bool _disposed;

    public string TriggerName => "HotkeyTrigger";
    public event EventHandler<TriggerEventArgs>? Triggered;

    private const int WM_HOTKEY = 0x0312;

    public int RegisterHotkey(HotkeyBinding binding)
    {
        var id = _nextId++;
        _bindings[id] = binding;

        if (_hwnd != IntPtr.Zero)
            RegisterWithSystem(id, binding);

        return id;
    }

    public void UnregisterHotkey(int id)
    {
        if (_bindings.Remove(id) && _hwnd != IntPtr.Zero)
            NativeHotkey.UnregisterHotKey(_hwnd, id);
    }

    public Task StartMonitoringAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public void AttachToWindow(IntPtr hwnd)
    {
        _hwnd = hwnd;
        foreach (var (id, binding) in _bindings)
            RegisterWithSystem(id, binding);
    }

    public bool ProcessMessage(IntPtr wParam)
    {
        var id = wParam.ToInt32();
        if (!_bindings.TryGetValue(id, out var binding))
            return false;

        Triggered?.Invoke(this, new TriggerEventArgs(
            TriggerName,
            TriggerReason.Hotkey,
            binding.TranscriptWindow,
            DateTime.UtcNow));

        return true;
    }

    public Task StopMonitoringAsync()
    {
        UnregisterAll();
        return Task.CompletedTask;
    }

    private void RegisterWithSystem(int id, HotkeyBinding binding)
    {
        NativeHotkey.RegisterHotKey(_hwnd, id, (uint)binding.Modifiers, (uint)binding.Key);
    }

    private void UnregisterAll()
    {
        if (_hwnd == IntPtr.Zero) return;
        foreach (var id in _bindings.Keys)
            NativeHotkey.UnregisterHotKey(_hwnd, id);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnregisterAll();
    }

    public static HotkeyBinding ParseHotkeyString(string hotkey)
    {
        var parts = hotkey.Split('+', StringSplitOptions.TrimEntries);
        var modifiers = HotkeyModifiers.None;
        var key = 0;

        foreach (var part in parts)
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= HotkeyModifiers.Control;
                    break;
                case "ALT":
                    modifiers |= HotkeyModifiers.Alt;
                    break;
                case "SHIFT":
                    modifiers |= HotkeyModifiers.Shift;
                    break;
                case "WIN":
                    modifiers |= HotkeyModifiers.Win;
                    break;
                case "SPACE":
                    key = 0x20;
                    break;
                default:
                    if (part.Length == 1 && char.IsLetterOrDigit(part[0]))
                        key = char.ToUpperInvariant(part[0]);
                    break;
            }
        }

        return new HotkeyBinding(modifiers, key);
    }
}

public record HotkeyBinding(
    HotkeyModifiers Modifiers,
    int Key,
    TimeSpan TranscriptWindow = default)
{
    public TimeSpan TranscriptWindow { get; init; } =
        TranscriptWindow == default ? TimeSpan.FromSeconds(60) : TranscriptWindow;
}

[Flags]
public enum HotkeyModifiers
{
    None = 0x0000,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
    NoRepeat = 0x4000
}

internal static partial class NativeHotkey
{
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}
