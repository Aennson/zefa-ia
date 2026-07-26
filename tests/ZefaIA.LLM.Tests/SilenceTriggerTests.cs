using Xunit;
using ZefaIA.Core.Models;
using ZefaIA.Core.Triggers;

namespace ZefaIA.LLM.Tests;

public class SilenceTriggerTests
{
    [Fact]
    public void CalculateRMS_SilentAudio_ReturnsZero()
    {
        var silence = new byte[320];
        var rms = SilenceTrigger.CalculateRMS(silence);
        Assert.Equal(0, rms);
    }

    [Fact]
    public void CalculateRMS_LoudAudio_ReturnsHighValue()
    {
        var loud = new byte[320];
        for (int i = 0; i < loud.Length; i += 2)
        {
            loud[i] = 0x00;
            loud[i + 1] = 0x40; // ~16384 = 0.5 normalized
        }

        var rms = SilenceTrigger.CalculateRMS(loud);
        Assert.True(rms > 0.3, $"RMS should be high for loud audio, got {rms}");
    }

    [Fact]
    public void CalculateRMS_EmptyData_ReturnsZero()
    {
        var rms = SilenceTrigger.CalculateRMS([]);
        Assert.Equal(0, rms);
    }

    [Fact]
    public void CalculateRMS_SingleByte_ReturnsZero()
    {
        var rms = SilenceTrigger.CalculateRMS([0x42]);
        Assert.Equal(0, rms);
    }

    [Fact]
    public void TriggerName_IsSilenceTrigger()
    {
        var trigger = new SilenceTrigger();
        Assert.Equal("SilenceTrigger", trigger.TriggerName);
    }

    [Fact]
    public void OnAudioChunk_LoudAudio_DoesNotTrigger()
    {
        var trigger = new SilenceTrigger(new SilenceTriggerConfig
        {
            SilenceDuration = TimeSpan.FromMilliseconds(100)
        });
        bool triggered = false;
        trigger.Triggered += (_, _) => triggered = true;

        var loud = CreateLoudChunk();
        trigger.OnAudioChunk(loud);

        Assert.False(triggered);
    }

    [Fact]
    public void OnAudioChunk_SilenceWithoutTranscription_DoesNotTrigger()
    {
        var config = new SilenceTriggerConfig
        {
            SilenceDuration = TimeSpan.Zero,
            Cooldown = TimeSpan.Zero
        };
        var trigger = new SilenceTrigger(config);
        bool triggered = false;
        trigger.Triggered += (_, _) => triggered = true;

        var silence = CreateSilentChunk();
        trigger.OnAudioChunk(silence);
        trigger.OnAudioChunk(silence);

        Assert.False(triggered, "Should not trigger without recent transcription");
    }

    [Fact]
    public void OnAudioChunk_SilenceWithTranscription_Triggers()
    {
        var config = new SilenceTriggerConfig
        {
            SilenceDuration = TimeSpan.Zero,
            Cooldown = TimeSpan.Zero
        };
        var trigger = new SilenceTrigger(config);
        TriggerEventArgs? args = null;
        trigger.Triggered += (_, e) => args = e;

        trigger.NotifyTranscriptionReceived();

        var silence = CreateSilentChunk();
        trigger.OnAudioChunk(silence);
        trigger.OnAudioChunk(silence);

        Assert.NotNull(args);
        Assert.Equal(TriggerReason.Silence, args!.Reason);
        Assert.Equal("SilenceTrigger", args.TriggerName);
    }

    [Fact]
    public void OnAudioChunk_CooldownPreventsRefire()
    {
        var config = new SilenceTriggerConfig
        {
            SilenceDuration = TimeSpan.Zero,
            Cooldown = TimeSpan.FromHours(1)
        };
        var trigger = new SilenceTrigger(config);
        int triggerCount = 0;
        trigger.Triggered += (_, _) => triggerCount++;

        trigger.NotifyTranscriptionReceived();

        var silence = CreateSilentChunk();
        for (int i = 0; i < 10; i++)
            trigger.OnAudioChunk(silence);

        Assert.Equal(1, triggerCount);
    }

    [Fact]
    public void OnAudioChunk_LoudAfterSilence_ResetsSilenceTimer()
    {
        var config = new SilenceTriggerConfig
        {
            SilenceDuration = TimeSpan.FromHours(1),
            Cooldown = TimeSpan.Zero
        };
        var trigger = new SilenceTrigger(config);
        bool triggered = false;
        trigger.Triggered += (_, _) => triggered = true;

        trigger.NotifyTranscriptionReceived();

        trigger.OnAudioChunk(CreateSilentChunk());
        trigger.OnAudioChunk(CreateLoudChunk());
        trigger.OnAudioChunk(CreateSilentChunk());

        Assert.False(triggered, "Should not trigger because silence timer was reset");
    }

    [Fact]
    public void Config_HasCorrectDefaults()
    {
        var config = new SilenceTriggerConfig();

        Assert.Equal(0.01, config.SilenceThresholdRMS);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), config.SilenceDuration);
        Assert.Equal(TimeSpan.FromSeconds(10), config.Cooldown);
        Assert.Equal(TimeSpan.FromSeconds(30), config.TranscriptRecencyWindow);
        Assert.Equal(TimeSpan.FromSeconds(60), config.TranscriptWindow);
    }

    [Fact]
    public void Dispose_MultipleCallsDoNotThrow()
    {
        var trigger = new SilenceTrigger();
        trigger.Dispose();
        trigger.Dispose();
    }

    private static AudioChunkEventArgs CreateSilentChunk() =>
        new(new byte[320], 16000, TimeSpan.Zero, AudioSourceType.Loopback);

    private static AudioChunkEventArgs CreateLoudChunk()
    {
        var data = new byte[320];
        for (int i = 0; i < data.Length; i += 2)
        {
            data[i] = 0x00;
            data[i + 1] = 0x40;
        }
        return new AudioChunkEventArgs(data, 16000, TimeSpan.Zero, AudioSourceType.Loopback);
    }
}
