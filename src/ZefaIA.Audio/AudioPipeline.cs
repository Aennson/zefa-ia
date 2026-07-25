using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using ZefaIA.Core.Models;

namespace ZefaIA.Audio;

public class AudioPipeline : IDisposable
{
    private readonly AudioCaptureEngine _captureEngine;
    private readonly EchoCanceller _echoCanceller;
    private readonly ILogger<AudioPipeline>? _logger;
    private readonly int _bufferSizeMs;

    private readonly Subject<AudioChunkEventArgs> _processedMicSubject = new();
    private readonly Subject<AudioChunkEventArgs> _processedLoopbackSubject = new();
    private IDisposable? _micSubscription;
    private IDisposable? _loopbackSubscription;
    private bool _disposed;

    public IObservable<AudioChunkEventArgs> MicStream => _processedMicSubject.AsObservable();
    public IObservable<AudioChunkEventArgs> LoopbackStream => _processedLoopbackSubject.AsObservable();
    public IObservable<AudioChunkEventArgs> CombinedStream =>
        MicStream.Merge(LoopbackStream);

    public AudioPipelineMetrics Metrics { get; } = new();

    public AudioPipeline(
        AudioCaptureEngine captureEngine,
        EchoCanceller echoCanceller,
        int bufferSizeMs = 100,
        ILogger<AudioPipeline>? logger = null)
    {
        _captureEngine = captureEngine;
        _echoCanceller = echoCanceller;
        _bufferSizeMs = bufferSizeMs;
        _logger = logger;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var rawStream = _captureEngine.AudioStream;

        _loopbackSubscription = rawStream
            .Where(c => c.Source == AudioSourceType.Loopback)
            .Buffer(TimeSpan.FromMilliseconds(_bufferSizeMs))
            .Where(buffer => buffer.Count > 0)
            .Subscribe(
                chunks => ProcessLoopbackBatch(chunks),
                ex => _logger?.LogError(ex, "Loopback stream error"));

        _micSubscription = rawStream
            .Where(c => c.Source == AudioSourceType.Microphone)
            .Buffer(TimeSpan.FromMilliseconds(_bufferSizeMs))
            .Where(buffer => buffer.Count > 0)
            .Subscribe(
                chunks => ProcessMicBatch(chunks),
                ex => _logger?.LogError(ex, "Mic stream error"));

        _logger?.LogInformation("Audio pipeline started (buffer={BufferMs}ms)", _bufferSizeMs);
    }

    public void Stop()
    {
        _micSubscription?.Dispose();
        _loopbackSubscription?.Dispose();
        _micSubscription = null;
        _loopbackSubscription = null;
        _logger?.LogInformation("Audio pipeline stopped");
    }

    private void ProcessLoopbackBatch(IList<AudioChunkEventArgs> chunks)
    {
        var sw = Stopwatch.StartNew();

        foreach (var chunk in chunks)
        {
            _echoCanceller.FeedReference(chunk.PcmData);

            _processedLoopbackSubject.OnNext(chunk);
            Metrics.IncrementLoopbackChunks();
        }

        sw.Stop();
        Metrics.UpdateLatency(sw.Elapsed.TotalMilliseconds);
    }

    private void ProcessMicBatch(IList<AudioChunkEventArgs> chunks)
    {
        var sw = Stopwatch.StartNew();

        foreach (var chunk in chunks)
        {
            var processed = _echoCanceller.Process(chunk.PcmData);
            var processedChunk = chunk with { PcmData = processed };

            if (Metrics.PendingMicChunks > Metrics.MaxBufferSize)
            {
                Metrics.IncrementDropped();
                _logger?.LogWarning("Backpressure: dropping mic chunk (buffer={Pending})", Metrics.PendingMicChunks);
                continue;
            }

            _processedMicSubject.OnNext(processedChunk);
            Metrics.IncrementMicChunks();
        }

        sw.Stop();
        Metrics.UpdateLatency(sw.Elapsed.TotalMilliseconds);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();

        _processedMicSubject.OnCompleted();
        _processedMicSubject.Dispose();
        _processedLoopbackSubject.OnCompleted();
        _processedLoopbackSubject.Dispose();

        GC.SuppressFinalize(this);
    }
}

public class AudioPipelineMetrics
{
    private long _micChunks;
    private long _loopbackChunks;
    private long _droppedChunks;
    private double _totalLatencyMs;
    private long _latencySamples;

    public long MicChunksProcessed => Interlocked.Read(ref _micChunks);
    public long LoopbackChunksProcessed => Interlocked.Read(ref _loopbackChunks);
    public long DroppedChunks => Interlocked.Read(ref _droppedChunks);
    public int PendingMicChunks { get; private set; }
    public int MaxBufferSize { get; set; } = 500;

    public double AverageLatencyMs =>
        _latencySamples > 0 ? _totalLatencyMs / _latencySamples : 0;

    public double ChunksPerSecond
    {
        get
        {
            var total = MicChunksProcessed + LoopbackChunksProcessed;
            return total;
        }
    }

    internal void IncrementMicChunks()
    {
        Interlocked.Increment(ref _micChunks);
        PendingMicChunks = Math.Max(0, PendingMicChunks - 1);
    }

    internal void IncrementLoopbackChunks() => Interlocked.Increment(ref _loopbackChunks);
    internal void IncrementDropped() => Interlocked.Increment(ref _droppedChunks);

    internal void UpdateLatency(double ms)
    {
        Interlocked.Exchange(ref _totalLatencyMs, _totalLatencyMs + ms);
        Interlocked.Increment(ref _latencySamples);
    }
}
