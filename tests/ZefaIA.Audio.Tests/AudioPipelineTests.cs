using System.Reactive.Linq;
using Moq;
using Xunit;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;
using ZefaIA.Audio;

namespace ZefaIA.Audio.Tests;

public class AudioPipelineTests
{
    private static (AudioCaptureEngine engine, Mock<IAudioSource> mic, Mock<IAudioSource> loopback) CreateEngine()
    {
        var engine = new AudioCaptureEngine();

        var mic = new Mock<IAudioSource>();
        mic.Setup(s => s.SourceId).Returns("mock-mic");
        mic.Setup(s => s.Type).Returns(AudioSourceType.Microphone);
        mic.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mic.Setup(s => s.StopAsync()).Returns(Task.CompletedTask);

        var loopback = new Mock<IAudioSource>();
        loopback.Setup(s => s.SourceId).Returns("mock-loopback");
        loopback.Setup(s => s.Type).Returns(AudioSourceType.Loopback);
        loopback.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        loopback.Setup(s => s.StopAsync()).Returns(Task.CompletedTask);

        engine.AddSource(mic.Object);
        engine.AddSource(loopback.Object);

        return (engine, mic, loopback);
    }

    private static byte[] CreatePcmChunk(int samples = 160)
    {
        var data = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            var value = (short)(Math.Sin(2 * Math.PI * 440 * i / 16000) * 1000);
            BitConverter.TryWriteBytes(data.AsSpan(i * 2), value);
        }
        return data;
    }

    [Fact]
    public async Task Pipeline_RoutesChunksCorrectly()
    {
        var (engine, mic, loopback) = CreateEngine();
        var aec = new EchoCanceller(sampleRate: 16000, filterLengthMs: 50);
        var pipeline = new AudioPipeline(engine, aec, bufferSizeMs: 50);

        var micChunks = new List<AudioChunkEventArgs>();
        var loopbackChunks = new List<AudioChunkEventArgs>();

        pipeline.MicStream.Subscribe(c => micChunks.Add(c));
        pipeline.LoopbackStream.Subscribe(c => loopbackChunks.Add(c));

        await engine.StartAsync();
        pipeline.Start();

        var pcm = CreatePcmChunk();

        loopback.Raise(s => s.AudioChunkReceived += null, loopback.Object,
            new AudioChunkEventArgs(pcm, 16000, TimeSpan.FromMilliseconds(100), AudioSourceType.Loopback));

        mic.Raise(s => s.AudioChunkReceived += null, mic.Object,
            new AudioChunkEventArgs(pcm, 16000, TimeSpan.FromMilliseconds(100), AudioSourceType.Microphone));

        await Task.Delay(200);

        Assert.NotEmpty(loopbackChunks);
        Assert.NotEmpty(micChunks);
        Assert.All(micChunks, c => Assert.Equal(AudioSourceType.Microphone, c.Source));
        Assert.All(loopbackChunks, c => Assert.Equal(AudioSourceType.Loopback, c.Source));

        pipeline.Dispose();
        aec.Dispose();
        engine.Dispose();
    }

    [Fact]
    public async Task CombinedStream_ReceivesBothSources()
    {
        var (engine, mic, loopback) = CreateEngine();
        var aec = new EchoCanceller(sampleRate: 16000, filterLengthMs: 50);
        var pipeline = new AudioPipeline(engine, aec, bufferSizeMs: 50);

        var allChunks = new List<AudioChunkEventArgs>();
        pipeline.CombinedStream.Subscribe(c => allChunks.Add(c));

        await engine.StartAsync();
        pipeline.Start();

        var pcm = CreatePcmChunk();

        mic.Raise(s => s.AudioChunkReceived += null, mic.Object,
            new AudioChunkEventArgs(pcm, 16000, TimeSpan.Zero, AudioSourceType.Microphone));
        loopback.Raise(s => s.AudioChunkReceived += null, loopback.Object,
            new AudioChunkEventArgs(pcm, 16000, TimeSpan.Zero, AudioSourceType.Loopback));

        await Task.Delay(200);

        Assert.True(allChunks.Count >= 2);
        Assert.Contains(allChunks, c => c.Source == AudioSourceType.Microphone);
        Assert.Contains(allChunks, c => c.Source == AudioSourceType.Loopback);

        pipeline.Dispose();
        aec.Dispose();
        engine.Dispose();
    }

    [Fact]
    public void Metrics_InitializeToZero()
    {
        var metrics = new AudioPipelineMetrics();

        Assert.Equal(0, metrics.MicChunksProcessed);
        Assert.Equal(0, metrics.LoopbackChunksProcessed);
        Assert.Equal(0, metrics.DroppedChunks);
        Assert.Equal(0, metrics.AverageLatencyMs);
    }

    [Fact]
    public void Dispose_StopsPipeline()
    {
        var (engine, _, _) = CreateEngine();
        var aec = new EchoCanceller();
        var pipeline = new AudioPipeline(engine, aec);

        pipeline.Start();
        pipeline.Dispose();
        pipeline.Dispose();

        aec.Dispose();
        engine.Dispose();
    }
}
