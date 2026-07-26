using Xunit;
using ZefaIA.Core.Models;
using ZefaIA.Core.Triggers;

namespace ZefaIA.LLM.Tests;

public class HotkeyTriggerTests
{
    [Fact]
    public void TriggerName_IsHotkeyTrigger()
    {
        var trigger = new HotkeyTrigger();
        Assert.Equal("HotkeyTrigger", trigger.TriggerName);
    }

    [Fact]
    public void RegisterHotkey_ReturnsIncrementingIds()
    {
        var trigger = new HotkeyTrigger();
        var binding = new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x20);

        var id1 = trigger.RegisterHotkey(binding);
        var id2 = trigger.RegisterHotkey(binding);

        Assert.Equal(1, id1);
        Assert.Equal(2, id2);
    }

    [Fact]
    public void ProcessMessage_RegisteredHotkey_FiresTriggered()
    {
        var trigger = new HotkeyTrigger();
        var binding = new HotkeyBinding(HotkeyModifiers.Control, 0x20);
        var id = trigger.RegisterHotkey(binding);

        TriggerEventArgs? args = null;
        trigger.Triggered += (_, e) => args = e;

        var handled = trigger.ProcessMessage(new IntPtr(id));

        Assert.True(handled);
        Assert.NotNull(args);
        Assert.Equal(TriggerReason.Hotkey, args!.Reason);
        Assert.Equal("HotkeyTrigger", args.TriggerName);
    }

    [Fact]
    public void ProcessMessage_UnregisteredId_ReturnsFalse()
    {
        var trigger = new HotkeyTrigger();
        bool triggered = false;
        trigger.Triggered += (_, _) => triggered = true;

        var handled = trigger.ProcessMessage(new IntPtr(999));

        Assert.False(handled);
        Assert.False(triggered);
    }

    [Fact]
    public void ProcessMessage_AfterUnregister_ReturnsFalse()
    {
        var trigger = new HotkeyTrigger();
        var binding = new HotkeyBinding(HotkeyModifiers.Control, 0x20);
        var id = trigger.RegisterHotkey(binding);
        trigger.UnregisterHotkey(id);

        var handled = trigger.ProcessMessage(new IntPtr(id));

        Assert.False(handled);
    }

    [Fact]
    public void ProcessMessage_SetsTranscriptWindowFromBinding()
    {
        var trigger = new HotkeyTrigger();
        var binding = new HotkeyBinding(HotkeyModifiers.Control, 0x20,
            TranscriptWindow: TimeSpan.FromSeconds(90));
        var id = trigger.RegisterHotkey(binding);

        TriggerEventArgs? args = null;
        trigger.Triggered += (_, e) => args = e;
        trigger.ProcessMessage(new IntPtr(id));

        Assert.Equal(TimeSpan.FromSeconds(90), args!.TranscriptWindow);
    }

    [Fact]
    public void ParseHotkeyString_CtrlShiftSpace()
    {
        var binding = HotkeyTrigger.ParseHotkeyString("Ctrl+Shift+Space");

        Assert.True(binding.Modifiers.HasFlag(HotkeyModifiers.Control));
        Assert.True(binding.Modifiers.HasFlag(HotkeyModifiers.Shift));
        Assert.Equal(0x20, binding.Key);
    }

    [Fact]
    public void ParseHotkeyString_CtrlShiftZ()
    {
        var binding = HotkeyTrigger.ParseHotkeyString("Ctrl+Shift+Z");

        Assert.True(binding.Modifiers.HasFlag(HotkeyModifiers.Control));
        Assert.True(binding.Modifiers.HasFlag(HotkeyModifiers.Shift));
        Assert.Equal('Z', binding.Key);
    }

    [Fact]
    public void ParseHotkeyString_AltC()
    {
        var binding = HotkeyTrigger.ParseHotkeyString("Alt+C");

        Assert.True(binding.Modifiers.HasFlag(HotkeyModifiers.Alt));
        Assert.False(binding.Modifiers.HasFlag(HotkeyModifiers.Control));
        Assert.Equal('C', binding.Key);
    }

    [Fact]
    public void ParseHotkeyString_CaseInsensitive()
    {
        var binding = HotkeyTrigger.ParseHotkeyString("ctrl+shift+space");

        Assert.True(binding.Modifiers.HasFlag(HotkeyModifiers.Control));
        Assert.True(binding.Modifiers.HasFlag(HotkeyModifiers.Shift));
        Assert.Equal(0x20, binding.Key);
    }

    [Fact]
    public void HotkeyBinding_DefaultTranscriptWindow_Is60s()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.Control, 0x20);
        Assert.Equal(TimeSpan.FromSeconds(60), binding.TranscriptWindow);
    }

    [Fact]
    public void HotkeyModifiers_FlagsWorkCorrectly()
    {
        var combined = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt;

        Assert.True(combined.HasFlag(HotkeyModifiers.Control));
        Assert.True(combined.HasFlag(HotkeyModifiers.Shift));
        Assert.True(combined.HasFlag(HotkeyModifiers.Alt));
        Assert.False(combined.HasFlag(HotkeyModifiers.Win));
    }

    [Fact]
    public void Dispose_MultipleCallsDoNotThrow()
    {
        var trigger = new HotkeyTrigger();
        trigger.Dispose();
        trigger.Dispose();
    }
}
