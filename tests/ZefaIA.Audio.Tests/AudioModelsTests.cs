using Xunit;
using ZefaIA.Core.Models;

namespace ZefaIA.Audio.Tests;

public class AudioModelsTests
{
    [Fact]
    public void AudioChunkEventArgs_CreatesCorrectly()
    {
        var pcm = new byte[] { 0x00, 0x01, 0x02 };
        var chunk = new AudioChunkEventArgs(pcm, 16000, TimeSpan.FromSeconds(1), AudioSourceType.Microphone);

        Assert.Equal(16000, chunk.SampleRate);
        Assert.Equal(AudioSourceType.Microphone, chunk.Source);
        Assert.Equal(3, chunk.PcmData.Length);
        Assert.Equal(TimeSpan.FromSeconds(1), chunk.Timestamp);
    }

    [Fact]
    public void SpeakerLabel_Me_ReturnsCorrectDefaults()
    {
        var label = SpeakerLabel.Me();
        Assert.Equal("Eu", label.DisplayName);
        Assert.Equal(AudioSourceType.Microphone, label.Source);
    }

    [Fact]
    public void SpeakerLabel_Other_ReturnsCorrectDefaults()
    {
        var label = SpeakerLabel.Other();
        Assert.Equal("Interlocutor", label.DisplayName);
        Assert.Equal(AudioSourceType.Loopback, label.Source);
    }

    [Fact]
    public void SpeakerLabel_CustomName_Works()
    {
        var label = SpeakerLabel.Me("João");
        Assert.Equal("João", label.DisplayName);
    }

    [Fact]
    public void AudioSourceState_HasAllExpectedValues()
    {
        var values = Enum.GetValues<AudioSourceState>();
        Assert.Contains(AudioSourceState.Idle, values);
        Assert.Contains(AudioSourceState.Capturing, values);
        Assert.Contains(AudioSourceState.Error, values);
    }
}
