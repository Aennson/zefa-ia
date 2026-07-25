using Moq;
using Xunit;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;
using ZefaIA.Audio;

namespace ZefaIA.Audio.Tests;

public class AudioCaptureEngineTests
{
    private static Mock<IAudioSource> CreateMockSource(AudioSourceType type)
    {
        var mock = new Mock<IAudioSource>();
        mock.Setup(s => s.SourceId).Returns($"mock-{type}");
        mock.Setup(s => s.DisplayName).Returns($"Mock {type}");
        mock.Setup(s => s.Type).Returns(type);
        mock.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mock.Setup(s => s.StopAsync()).Returns(Task.CompletedTask);
        return mock;
    }

    [Fact]
    public void AddSource_AddsToActiveSources()
    {
        using var engine = new AudioCaptureEngine();
        var mockSource = CreateMockSource(AudioSourceType.Microphone);

        engine.AddSource(mockSource.Object);

        Assert.Single(engine.ActiveSources);
        Assert.Equal(AudioSourceType.Microphone, engine.ActiveSources[0].Type);
    }

    [Fact]
    public async Task StartAsync_StartsAllSources()
    {
        using var engine = new AudioCaptureEngine();
        var mic = CreateMockSource(AudioSourceType.Microphone);
        var loopback = CreateMockSource(AudioSourceType.Loopback);

        engine.AddSource(mic.Object);
        engine.AddSource(loopback.Object);

        await engine.StartAsync();

        mic.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        loopback.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(engine.IsRunning);
    }

    [Fact]
    public async Task StopAsync_StopsAllSources()
    {
        using var engine = new AudioCaptureEngine();
        var mic = CreateMockSource(AudioSourceType.Microphone);
        var loopback = CreateMockSource(AudioSourceType.Loopback);

        engine.AddSource(mic.Object);
        engine.AddSource(loopback.Object);
        await engine.StartAsync();
        await engine.StopAsync();

        mic.Verify(s => s.StopAsync(), Times.Once);
        loopback.Verify(s => s.StopAsync(), Times.Once);
        Assert.False(engine.IsRunning);
    }

    [Fact]
    public async Task StartAsync_NoSources_Throws()
    {
        using var engine = new AudioCaptureEngine();
        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.StartAsync());
    }

    [Fact]
    public void AddSource_WhileRunning_Throws()
    {
        using var engine = new AudioCaptureEngine();
        var mic = CreateMockSource(AudioSourceType.Microphone);
        engine.AddSource(mic.Object);
        engine.StartAsync().Wait();

        var loopback = CreateMockSource(AudioSourceType.Loopback);
        Assert.Throws<InvalidOperationException>(() => engine.AddSource(loopback.Object));
    }

    [Fact]
    public async Task AudioStream_EmitsChunksFromBothSources()
    {
        using var engine = new AudioCaptureEngine();
        var mic = CreateMockSource(AudioSourceType.Microphone);
        var loopback = CreateMockSource(AudioSourceType.Loopback);

        engine.AddSource(mic.Object);
        engine.AddSource(loopback.Object);

        var receivedChunks = new List<AudioChunkEventArgs>();
        engine.AudioStream.Subscribe(chunk => receivedChunks.Add(chunk));

        await engine.StartAsync();

        mic.Raise(s => s.AudioChunkReceived += null,
            mic.Object,
            new AudioChunkEventArgs(new byte[] { 1, 2 }, 16000, TimeSpan.FromMilliseconds(100), AudioSourceType.Microphone));

        loopback.Raise(s => s.AudioChunkReceived += null,
            loopback.Object,
            new AudioChunkEventArgs(new byte[] { 3, 4 }, 16000, TimeSpan.FromMilliseconds(100), AudioSourceType.Loopback));

        Assert.Equal(2, receivedChunks.Count);
        Assert.Equal(AudioSourceType.Microphone, receivedChunks[0].Source);
        Assert.Equal(AudioSourceType.Loopback, receivedChunks[1].Source);
    }

    [Fact]
    public async Task FailedSource_DoesNotPreventOtherFromStarting()
    {
        using var engine = new AudioCaptureEngine();
        var failing = CreateMockSource(AudioSourceType.Microphone);
        failing.Setup(s => s.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No device"));

        var working = CreateMockSource(AudioSourceType.Loopback);

        engine.AddSource(failing.Object);
        engine.AddSource(working.Object);

        await engine.StartAsync();

        working.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(engine.IsRunning);
    }

    [Fact]
    public void Dispose_DisposesAllSources()
    {
        var engine = new AudioCaptureEngine();
        var mic = CreateMockSource(AudioSourceType.Microphone);
        var loopback = CreateMockSource(AudioSourceType.Loopback);

        engine.AddSource(mic.Object);
        engine.AddSource(loopback.Object);
        engine.Dispose();

        mic.Verify(s => s.Dispose(), Times.Once);
        loopback.Verify(s => s.Dispose(), Times.Once);
    }
}
