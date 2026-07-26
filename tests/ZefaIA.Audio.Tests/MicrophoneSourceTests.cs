using Xunit;
using ZefaIA.Core.Models;
using ZefaIA.Audio;

namespace ZefaIA.Audio.Tests;

public class MicrophoneSourceTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        try
        {
            var source = new MicrophoneSource(0, "Test Mic");

            Assert.Equal("mic-0", source.SourceId);
            Assert.Equal("Test Mic", source.DisplayName);
            Assert.Equal(AudioSourceType.Microphone, source.Type);
        }
        catch (InvalidOperationException)
        {
            // No mic available in test environment
        }
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        try
        {
            var source = new MicrophoneSource(0, "Test Mic");
            source.Dispose();
            source.Dispose();
        }
        catch (InvalidOperationException)
        {
            // No mic available in test environment
        }
    }

    [Fact]
    public void Type_IsMicrophone()
    {
        try
        {
            var source = new MicrophoneSource(0, "Test Mic");
            Assert.Equal(AudioSourceType.Microphone, source.Type);
        }
        catch (InvalidOperationException)
        {
            // No mic available in test environment
        }
    }

    [RequiresAudioDeviceFact(AudioEndpoint.Capture)]
    public async Task StartAsync_RaisesStateChangedToCapturing()
    {
        var source = new MicrophoneSource();
        var states = new List<AudioSourceState>();
        source.StateChanged += (_, e) => states.Add(e.State);

        await source.StartAsync();
        await Task.Delay(100);
        await source.StopAsync();

        Assert.Contains(AudioSourceState.Starting, states);
        Assert.Contains(AudioSourceState.Capturing, states);
        source.Dispose();
    }

    [RequiresAudioDeviceFact(AudioEndpoint.Capture)]
    public async Task StartAsync_EmitsAudioChunks()
    {
        var source = new MicrophoneSource();
        var chunks = new List<AudioChunkEventArgs>();
        source.AudioChunkReceived += (_, e) => chunks.Add(e);

        await source.StartAsync();
        await Task.Delay(500);
        await source.StopAsync();

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.Equal(16000, c.SampleRate));
        Assert.All(chunks, c => Assert.Equal(AudioSourceType.Microphone, c.Source));
        source.Dispose();
    }
}
