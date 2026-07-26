using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.Integration.Tests.Fakes;

/// <summary>
/// An <see cref="ISTTProvider"/> that turns audio into a scripted transcript instead of
/// running a model. It stands in for Whisper/ElevenLabs so the pipeline's behaviour is
/// asserted against known text: real recognition would make every assertion downstream
/// depend on model accuracy, and the model is already covered by its own tests.
///
/// It still honours the provider contract the engine relies on — it only emits after
/// enough audio has arrived, marks segments final, and refuses work before
/// initialisation or after disposal.
/// </summary>
public sealed class ScriptedSTTProvider : ISTTProvider
{
    private readonly Queue<string> _script;
    private readonly int _bytesPerSegment;
    private readonly string _language;

    private int _bufferedBytes;
    private TimeSpan _lastTimestamp;
    private bool _initialized;
    private bool _disposed;

    public string ProviderId => "scripted-test";
    public STTProviderType Type => STTProviderType.WhisperLocal;
    public IReadOnlyList<string> SupportedLanguages => new[] { "auto", "pt", "en" };

    /// <summary>Chunks handed to this provider, to assert routing (mic vs loopback).</summary>
    public List<AudioChunkEventArgs> ReceivedChunks { get; } = new();

    public event EventHandler<TranscriptionSegmentEventArgs>? SegmentReceived;
    public event EventHandler<TranscriptionSegmentEventArgs>? PartialReceived;

    /// <param name="script">Phrases emitted in order, one per completed buffer.</param>
    /// <param name="bytesPerSegment">
    /// Audio to accumulate before emitting one phrase. Mirrors how a real provider
    /// batches rather than transcribing every 100 ms chunk.
    /// </param>
    public ScriptedSTTProvider(IEnumerable<string> script, int bytesPerSegment = 3200, string language = "pt")
    {
        _script = new Queue<string>(script);
        _bytesPerSegment = bytesPerSegment;
        _language = language;
    }

    public Task InitializeAsync(STTProviderConfig config, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized)
            throw new InvalidOperationException("Provider already initialized.");

        _initialized = true;
        return Task.CompletedTask;
    }

    public Task ProcessAudioAsync(AudioChunkEventArgs chunk, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
            throw new InvalidOperationException("Provider not initialized.");

        ReceivedChunks.Add(chunk);
        _bufferedBytes += chunk.PcmData.Length;
        _lastTimestamp = chunk.Timestamp;

        while (_bufferedBytes >= _bytesPerSegment && _script.Count > 0)
        {
            _bufferedBytes -= _bytesPerSegment;
            EmitSegment(_script.Dequeue());
        }

        return Task.CompletedTask;
    }

    /// <summary>Drains whatever is still scripted, as a real provider does on stop.</summary>
    public Task FlushAsync()
    {
        while (_script.Count > 0)
            EmitSegment(_script.Dequeue());

        _bufferedBytes = 0;
        return Task.CompletedTask;
    }

    private void EmitSegment(string text)
    {
        var segment = new TranscriptionSegment(
            Text: text,
            Language: _language,
            Confidence: 0.95f,
            StartTime: _lastTimestamp,
            EndTime: _lastTimestamp + TimeSpan.FromMilliseconds(500),
            Source: ReceivedChunks.Count > 0 ? ReceivedChunks[^1].Source : AudioSourceType.Microphone,
            IsFinal: true);

        SegmentReceived?.Invoke(this, new TranscriptionSegmentEventArgs(segment, DateTime.UtcNow));
    }

    /// <summary>Emits a non-final segment, to assert that partials are not persisted.</summary>
    public void EmitPartial(string text, AudioSourceType source)
    {
        var segment = new TranscriptionSegment(
            text, _language, 0.5f, _lastTimestamp,
            _lastTimestamp + TimeSpan.FromMilliseconds(200), source, IsFinal: false);

        PartialReceived?.Invoke(this, new TranscriptionSegmentEventArgs(segment, DateTime.UtcNow));
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
