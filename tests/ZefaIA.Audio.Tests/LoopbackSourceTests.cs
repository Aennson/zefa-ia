using Xunit;
using ZefaIA.Core.Models;
using ZefaIA.Audio;

namespace ZefaIA.Audio.Tests;

public class LoopbackSourceTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var source = new LoopbackSource("test-device", "Test Loopback");

        Assert.Equal("loopback-test-device", source.SourceId);
        Assert.Equal("Test Loopback", source.DisplayName);
        Assert.Equal(AudioSourceType.Loopback, source.Type);
    }

    [Fact]
    public void Constructor_DefaultDevice_SetsDefaultId()
    {
        var source = new LoopbackSource();
        Assert.Equal("loopback-default", source.SourceId);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var source = new LoopbackSource("test", "Test");
        source.Dispose();
        source.Dispose();
    }

    [Fact]
    public void StopAsync_BeforeStart_DoesNotThrow()
    {
        var source = new LoopbackSource("test", "Test");
        var task = source.StopAsync();
        Assert.True(task.IsCompleted);
        source.Dispose();
    }

    [Fact(Skip = "Requires Windows audio device")]
    public async Task StartAsync_RaisesStateChangedToCapturing()
    {
        var source = new LoopbackSource();
        var states = new List<AudioSourceState>();
        source.StateChanged += (_, e) => states.Add(e.State);

        await source.StartAsync();
        await Task.Delay(200);
        await source.StopAsync();

        Assert.Contains(AudioSourceState.Starting, states);
        Assert.Contains(AudioSourceState.Capturing, states);
        source.Dispose();
    }

    [Fact(Skip = "Requires Windows audio device with active output")]
    public async Task StartAsync_EmitsAudioChunks_WhenAudioPlaying()
    {
        var source = new LoopbackSource();
        var chunks = new List<AudioChunkEventArgs>();
        source.AudioChunkReceived += (_, e) => chunks.Add(e);

        await source.StartAsync();
        await Task.Delay(1000);
        await source.StopAsync();

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.Equal(Resampler.TargetSampleRate, c.SampleRate));
        Assert.All(chunks, c => Assert.Equal(AudioSourceType.Loopback, c.Source));
        source.Dispose();
    }

    [Fact]
    public void Resampler_Handles48kStereoFloat32()
    {
        var sampleCount = 960;
        var channels = 2;
        var input = new byte[sampleCount * channels * 4];
        for (int i = 0; i < sampleCount * channels; i++)
        {
            var value = (float)Math.Sin(2 * Math.PI * 440 * (i / channels) / 48000) * 0.5f;
            BitConverter.TryWriteBytes(input.AsSpan(i * 4), value);
        }

        var result = Resampler.ResampleToTarget(input, 48000, channels, 32);

        var expectedMonoSamples = (int)(sampleCount * (16000.0 / 48000));
        var actualSamples = result.Length / 2;
        Assert.InRange(actualSamples, expectedMonoSamples - 2, expectedMonoSamples + 2);
    }
}
