using System.Reactive.Subjects;
using Moq;
using Xunit;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.STT.Tests;

public class TranscriptionEngineTests
{
    private static (TranscriptionEngine engine, Mock<ISTTProvider> micMock, Mock<ISTTProvider> loopMock) CreateEngine()
    {
        var micMock = new Mock<ISTTProvider>();
        micMock.Setup(p => p.ProviderId).Returns("mic-provider");
        micMock.Setup(p => p.Type).Returns(STTProviderType.WhisperLocal);

        var loopMock = new Mock<ISTTProvider>();
        loopMock.Setup(p => p.ProviderId).Returns("loop-provider");
        loopMock.Setup(p => p.Type).Returns(STTProviderType.WhisperLocal);

        var engine = new TranscriptionEngine(micMock.Object, loopMock.Object);
        return (engine, micMock, loopMock);
    }

    [Fact]
    public void Start_SubscribesToBothStreams()
    {
        var (engine, micMock, loopMock) = CreateEngine();
        var micStream = new Subject<AudioChunkEventArgs>();
        var loopStream = new Subject<AudioChunkEventArgs>();

        engine.Start(micStream, loopStream);

        Assert.True(micStream.HasObservers);
        Assert.True(loopStream.HasObservers);

        engine.Dispose();
    }

    [Fact]
    public void Start_AlreadyStarted_Throws()
    {
        var (engine, _, _) = CreateEngine();
        var micStream = new Subject<AudioChunkEventArgs>();
        var loopStream = new Subject<AudioChunkEventArgs>();

        engine.Start(micStream, loopStream);

        Assert.Throws<InvalidOperationException>(() =>
            engine.Start(micStream, loopStream));

        engine.Dispose();
    }

    [Fact]
    public async Task MicChunks_RouteToMicProvider()
    {
        var (engine, micMock, loopMock) = CreateEngine();
        var micStream = new Subject<AudioChunkEventArgs>();
        var loopStream = new Subject<AudioChunkEventArgs>();

        engine.Start(micStream, loopStream);

        var chunk = new AudioChunkEventArgs(
            new byte[320], 16000, TimeSpan.FromSeconds(1), AudioSourceType.Microphone);

        micStream.OnNext(chunk);
        await Task.Delay(50);

        micMock.Verify(p => p.ProcessAudioAsync(chunk, It.IsAny<CancellationToken>()), Times.Once);
        loopMock.Verify(p => p.ProcessAudioAsync(It.IsAny<AudioChunkEventArgs>(), It.IsAny<CancellationToken>()), Times.Never);

        engine.Dispose();
    }

    [Fact]
    public async Task LoopbackChunks_RouteToLoopbackProvider()
    {
        var (engine, micMock, loopMock) = CreateEngine();
        var micStream = new Subject<AudioChunkEventArgs>();
        var loopStream = new Subject<AudioChunkEventArgs>();

        engine.Start(micStream, loopStream);

        var chunk = new AudioChunkEventArgs(
            new byte[320], 16000, TimeSpan.FromSeconds(1), AudioSourceType.Loopback);

        loopStream.OnNext(chunk);
        await Task.Delay(50);

        loopMock.Verify(p => p.ProcessAudioAsync(chunk, It.IsAny<CancellationToken>()), Times.Once);
        micMock.Verify(p => p.ProcessAudioAsync(It.IsAny<AudioChunkEventArgs>(), It.IsAny<CancellationToken>()), Times.Never);

        engine.Dispose();
    }

    [Fact]
    public void SegmentReceived_FromMicProvider_EmitsOnTranscriptionStream()
    {
        var (engine, micMock, _) = CreateEngine();
        var micStream = new Subject<AudioChunkEventArgs>();
        var loopStream = new Subject<AudioChunkEventArgs>();

        var received = new List<TranscriptionSegmentEventArgs>();
        engine.TranscriptionStream.Subscribe(e => received.Add(e));
        engine.Start(micStream, loopStream);

        var segment = new TranscriptionSegment(
            "Hello", "en", 0.9f, TimeSpan.Zero, TimeSpan.FromSeconds(1),
            AudioSourceType.Microphone, true);

        micMock.Raise(p => p.SegmentReceived += null,
            micMock.Object,
            new TranscriptionSegmentEventArgs(segment, DateTime.UtcNow));

        Assert.Single(received);
        Assert.Equal(AudioSourceType.Microphone, received[0].Segment.Source);
        Assert.Equal("Hello", received[0].Segment.Text);

        engine.Dispose();
    }

    [Fact]
    public void SegmentReceived_FromLoopbackProvider_EmitsWithLoopbackSource()
    {
        var (engine, _, loopMock) = CreateEngine();
        var micStream = new Subject<AudioChunkEventArgs>();
        var loopStream = new Subject<AudioChunkEventArgs>();

        var received = new List<TranscriptionSegmentEventArgs>();
        engine.TranscriptionStream.Subscribe(e => received.Add(e));
        engine.Start(micStream, loopStream);

        var segment = new TranscriptionSegment(
            "Tudo bem?", "pt", 0.85f, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4),
            AudioSourceType.Loopback, true);

        loopMock.Raise(p => p.SegmentReceived += null,
            loopMock.Object,
            new TranscriptionSegmentEventArgs(segment, DateTime.UtcNow));

        Assert.Single(received);
        Assert.Equal(AudioSourceType.Loopback, received[0].Segment.Source);

        engine.Dispose();
    }

    [Fact]
    public void PartialReceived_EmitsAsNonFinal()
    {
        var (engine, micMock, _) = CreateEngine();
        var micStream = new Subject<AudioChunkEventArgs>();
        var loopStream = new Subject<AudioChunkEventArgs>();

        var received = new List<TranscriptionSegmentEventArgs>();
        engine.TranscriptionStream.Subscribe(e => received.Add(e));
        engine.Start(micStream, loopStream);

        var segment = new TranscriptionSegment(
            "Hel", "en", 0.5f, TimeSpan.Zero, TimeSpan.FromMilliseconds(500),
            AudioSourceType.Microphone, true);

        micMock.Raise(p => p.PartialReceived += null,
            micMock.Object,
            new TranscriptionSegmentEventArgs(segment, DateTime.UtcNow));

        Assert.Single(received);
        Assert.False(received[0].Segment.IsFinal);

        engine.Dispose();
    }

    [Fact]
    public void Metrics_TracksSegments()
    {
        var (engine, micMock, loopMock) = CreateEngine();
        var micStream = new Subject<AudioChunkEventArgs>();
        var loopStream = new Subject<AudioChunkEventArgs>();
        engine.Start(micStream, loopStream);

        var segment = new TranscriptionSegment(
            "Test", "en", 0.9f, TimeSpan.Zero, TimeSpan.FromSeconds(1),
            AudioSourceType.Microphone, true);

        micMock.Raise(p => p.SegmentReceived += null,
            micMock.Object,
            new TranscriptionSegmentEventArgs(segment, DateTime.UtcNow));

        micMock.Raise(p => p.PartialReceived += null,
            micMock.Object,
            new TranscriptionSegmentEventArgs(segment, DateTime.UtcNow));

        Assert.Equal(1, engine.Metrics.FinalSegments);
        Assert.Equal(1, engine.Metrics.PartialSegments);
        Assert.Equal(2, engine.Metrics.TotalSegments);

        engine.Dispose();
    }

    [Fact]
    public void Metrics_InitializeToZero()
    {
        var metrics = new TranscriptionEngineMetrics();

        Assert.Equal(0, metrics.FinalSegments);
        Assert.Equal(0, metrics.PartialSegments);
        Assert.Equal(0, metrics.Errors);
        Assert.Equal(0, metrics.AverageLatencyMs);
    }

    [Fact]
    public void Stop_UnsubscribesStreams()
    {
        var (engine, _, _) = CreateEngine();
        var micStream = new Subject<AudioChunkEventArgs>();
        var loopStream = new Subject<AudioChunkEventArgs>();

        engine.Start(micStream, loopStream);
        engine.Stop();

        Assert.False(micStream.HasObservers);
        Assert.False(loopStream.HasObservers);

        engine.Dispose();
    }

    [Fact]
    public void Dispose_MultipleCallsDoNotThrow()
    {
        var (engine, _, _) = CreateEngine();
        engine.Dispose();
        engine.Dispose();
    }

    [Fact]
    public async Task FlushAsync_FlushBothProviders()
    {
        var (engine, micMock, loopMock) = CreateEngine();

        await engine.FlushAsync();

        micMock.Verify(p => p.FlushAsync(), Times.Once);
        loopMock.Verify(p => p.FlushAsync(), Times.Once);

        engine.Dispose();
    }
}
