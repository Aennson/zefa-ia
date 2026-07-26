using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZefaIA.Core.Models;

namespace ZefaIA.Persistence;

public sealed class MeetingRecorder : IAsyncDisposable
{
    private readonly IMeetingRepository _repository;
    private readonly ILogger<MeetingRecorder> _logger;
    private readonly int _batchSize;
    private readonly TimeSpan _batchInterval;

    private MeetingSession? _session;
    private readonly List<TranscriptionEntry> _buffer = new();
    private readonly object _bufferLock = new();
    private Timer? _flushTimer;
    private IDisposable? _transcriptionSubscription;
    private bool _recording;
    private bool _disposed;

    public bool IsRecording => _recording;
    public long? CurrentSessionId => _session?.Id;
    public MeetingRecorderMetrics Metrics { get; } = new();

    public event Action<bool>? OnRecordingStateChanged;

    public MeetingRecorder(
        IMeetingRepository repository,
        int batchSize = 5,
        TimeSpan? batchInterval = null,
        ILogger<MeetingRecorder>? logger = null)
    {
        _repository = repository;
        _batchSize = batchSize;
        _batchInterval = batchInterval ?? TimeSpan.FromSeconds(5);
        _logger = logger ?? NullLogger<MeetingRecorder>.Instance;
    }

    public async Task<MeetingSession> StartAsync(MeetingSession session)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_recording)
            throw new InvalidOperationException("Already recording.");

        session.StartedAt = DateTime.UtcNow;
        _session = await _repository.CreateSessionAsync(session);

        _flushTimer = new Timer(_ => _ = FlushBufferAsync(), null, _batchInterval, _batchInterval);
        _recording = true;
        OnRecordingStateChanged?.Invoke(true);

        _logger.LogInformation("Recording started for session {Id}: {Title}", _session.Id, _session.Title);
        return _session;
    }

    public void SubscribeToTranscriptions(IObservable<TranscriptionSegmentEventArgs> stream)
    {
        _transcriptionSubscription?.Dispose();
        _transcriptionSubscription = stream
            .Where(e => e.Segment.IsFinal)
            .Subscribe(OnTranscriptionReceived);
    }

    private void OnTranscriptionReceived(TranscriptionSegmentEventArgs args)
    {
        if (!_recording || _session == null) return;

        var speaker = args.Segment.Source == AudioSourceType.Microphone ? "Eu" : "Interlocutor";
        var entry = new TranscriptionEntry
        {
            SessionId = _session.Id,
            SpeakerName = speaker,
            Text = args.Segment.Text,
            IsFinal = true,
            Timestamp = args.ReceivedAt,
            StartTimeSeconds = args.Segment.StartTime.TotalSeconds
        };

        bool shouldFlush;
        lock (_bufferLock)
        {
            _buffer.Add(entry);
            Metrics.TotalTranscriptions++;
            shouldFlush = _buffer.Count >= _batchSize;
        }

        if (shouldFlush)
            _ = FlushBufferAsync();
    }

    public void OnSuggestionReceived(string text, string triggerReason, string transcriptContext, int inputTokens, int outputTokens)
    {
        if (!_recording || _session == null) return;

        var entry = new SuggestionEntry
        {
            SessionId = _session.Id,
            Text = text,
            TriggerReason = triggerReason,
            TranscriptContext = transcriptContext,
            Timestamp = DateTime.UtcNow,
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        };

        _ = SaveSuggestionAsync(entry);
    }

    private async Task SaveSuggestionAsync(SuggestionEntry entry)
    {
        try
        {
            await _repository.AddSuggestionAsync(entry);
            Metrics.TotalSuggestions++;
            _logger.LogDebug("Suggestion saved for session {Id}", _session?.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save suggestion");
        }
    }

    internal async Task FlushBufferAsync()
    {
        List<TranscriptionEntry> toFlush;
        lock (_bufferLock)
        {
            if (_buffer.Count == 0) return;
            toFlush = new List<TranscriptionEntry>(_buffer);
            _buffer.Clear();
        }

        try
        {
            await _repository.AddTranscriptionBatchAsync(toFlush);
            Metrics.BatchesWritten++;
            _logger.LogDebug("Flushed {Count} transcriptions for session {Id}", toFlush.Count, _session?.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush transcription batch");
            lock (_bufferLock)
            {
                _buffer.InsertRange(0, toFlush);
            }
        }
    }

    public async Task<MeetingSession?> StopAsync()
    {
        if (!_recording || _session == null) return null;

        _flushTimer?.Dispose();
        _flushTimer = null;
        _transcriptionSubscription?.Dispose();
        _transcriptionSubscription = null;

        await FlushBufferAsync();

        _session.EndedAt = DateTime.UtcNow;
        await _repository.UpdateSessionAsync(_session);

        _recording = false;
        OnRecordingStateChanged?.Invoke(false);

        _logger.LogInformation("Recording stopped for session {Id}, duration: {Duration}",
            _session.Id, _session.Duration);

        var result = _session;
        _session = null;
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_recording)
            await StopAsync();

        _flushTimer?.Dispose();
        _transcriptionSubscription?.Dispose();
    }
}

public class MeetingRecorderMetrics
{
    public int TotalTranscriptions { get; set; }
    public int TotalSuggestions { get; set; }
    public int BatchesWritten { get; set; }
}
