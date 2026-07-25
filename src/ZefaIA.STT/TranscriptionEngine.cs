using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.STT;

public sealed class TranscriptionEngine : IDisposable
{
    private readonly ISTTProvider _micProvider;
    private readonly ISTTProvider _loopbackProvider;
    private readonly ILogger<TranscriptionEngine>? _logger;

    private readonly Subject<TranscriptionSegmentEventArgs> _transcriptionSubject = new();
    private IDisposable? _micSubscription;
    private IDisposable? _loopbackSubscription;
    private bool _started;
    private bool _disposed;

    public IObservable<TranscriptionSegmentEventArgs> TranscriptionStream =>
        _transcriptionSubject.AsObservable();

    public TranscriptionEngineMetrics Metrics { get; } = new();

    public TranscriptionEngine(
        ISTTProvider micProvider,
        ISTTProvider loopbackProvider,
        ILogger<TranscriptionEngine>? logger = null)
    {
        _micProvider = micProvider;
        _loopbackProvider = loopbackProvider;
        _logger = logger;

        _micProvider.SegmentReceived += OnMicSegment;
        _micProvider.PartialReceived += OnMicPartial;
        _loopbackProvider.SegmentReceived += OnLoopbackSegment;
        _loopbackProvider.PartialReceived += OnLoopbackPartial;
    }

    public void Start(IObservable<AudioChunkEventArgs> micStream, IObservable<AudioChunkEventArgs> loopbackStream)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
            throw new InvalidOperationException("TranscriptionEngine already started.");

        _micSubscription = micStream.Subscribe(
            async chunk =>
            {
                try
                {
                    await _micProvider.ProcessAudioAsync(chunk);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Mic STT processing error");
                    Metrics.IncrementErrors();
                }
            },
            ex => _logger?.LogError(ex, "Mic stream error"));

        _loopbackSubscription = loopbackStream.Subscribe(
            async chunk =>
            {
                try
                {
                    await _loopbackProvider.ProcessAudioAsync(chunk);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Loopback STT processing error");
                    Metrics.IncrementErrors();
                }
            },
            ex => _logger?.LogError(ex, "Loopback stream error"));

        _started = true;
        _logger?.LogInformation("TranscriptionEngine started with mic={MicProvider}, loopback={LoopbackProvider}",
            _micProvider.ProviderId, _loopbackProvider.ProviderId);
    }

    public void Stop()
    {
        _micSubscription?.Dispose();
        _loopbackSubscription?.Dispose();
        _micSubscription = null;
        _loopbackSubscription = null;
        _started = false;
        _logger?.LogInformation("TranscriptionEngine stopped");
    }

    public async Task FlushAsync()
    {
        await _micProvider.FlushAsync();
        await _loopbackProvider.FlushAsync();
    }

    private void OnMicSegment(object? sender, TranscriptionSegmentEventArgs e)
    {
        var segment = e.Segment with { Source = AudioSourceType.Microphone };
        EmitSegment(segment, e.ReceivedAt, isFinal: true);
    }

    private void OnLoopbackSegment(object? sender, TranscriptionSegmentEventArgs e)
    {
        var segment = e.Segment with { Source = AudioSourceType.Loopback };
        EmitSegment(segment, e.ReceivedAt, isFinal: true);
    }

    private void OnMicPartial(object? sender, TranscriptionSegmentEventArgs e)
    {
        var segment = e.Segment with { Source = AudioSourceType.Microphone, IsFinal = false };
        EmitSegment(segment, e.ReceivedAt, isFinal: false);
    }

    private void OnLoopbackPartial(object? sender, TranscriptionSegmentEventArgs e)
    {
        var segment = e.Segment with { Source = AudioSourceType.Loopback, IsFinal = false };
        EmitSegment(segment, e.ReceivedAt, isFinal: false);
    }

    private void EmitSegment(TranscriptionSegment segment, DateTime receivedAt, bool isFinal)
    {
        var args = new TranscriptionSegmentEventArgs(segment, receivedAt);
        _transcriptionSubject.OnNext(args);

        if (isFinal)
            Metrics.IncrementFinalSegments();
        else
            Metrics.IncrementPartialSegments();

        var latency = (DateTime.UtcNow - receivedAt).TotalMilliseconds;
        Metrics.UpdateLatency(latency);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();

        _micProvider.SegmentReceived -= OnMicSegment;
        _micProvider.PartialReceived -= OnMicPartial;
        _loopbackProvider.SegmentReceived -= OnLoopbackSegment;
        _loopbackProvider.PartialReceived -= OnLoopbackPartial;

        _transcriptionSubject.OnCompleted();
        _transcriptionSubject.Dispose();

        GC.SuppressFinalize(this);
    }
}

public class TranscriptionEngineMetrics
{
    private long _finalSegments;
    private long _partialSegments;
    private long _errors;
    private double _totalLatencyMs;
    private long _latencySamples;

    public long FinalSegments => Interlocked.Read(ref _finalSegments);
    public long PartialSegments => Interlocked.Read(ref _partialSegments);
    public long TotalSegments => FinalSegments + PartialSegments;
    public long Errors => Interlocked.Read(ref _errors);

    public double AverageLatencyMs =>
        _latencySamples > 0 ? _totalLatencyMs / _latencySamples : 0;

    internal void IncrementFinalSegments() => Interlocked.Increment(ref _finalSegments);
    internal void IncrementPartialSegments() => Interlocked.Increment(ref _partialSegments);
    internal void IncrementErrors() => Interlocked.Increment(ref _errors);

    internal void UpdateLatency(double ms)
    {
        Interlocked.Exchange(ref _totalLatencyMs, _totalLatencyMs + ms);
        Interlocked.Increment(ref _latencySamples);
    }
}
