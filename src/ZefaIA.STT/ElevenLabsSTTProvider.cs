using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.STT;

public sealed class ElevenLabsSTTProvider : ISTTProvider
{
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private STTProviderConfig _config = null!;
    private string _apiKey = string.Empty;
    private bool _initialized;
    private bool _disposed;
    private int _reconnectAttempts;
    private TimeSpan _sessionOffset;
    private readonly ILogger<ElevenLabsSTTProvider>? _logger;

    private const string WsEndpoint = "wss://api.elevenlabs.io/v1/speech-to-text/realtime";
    private const string RealtimeModelId = "scribe_v2_realtime";
    private const int TargetSampleRate = 16000;
    private const int MaxReconnectAttempts = 5;
    private const int ReconnectBaseDelayMs = 1000;

    public string ProviderId => "elevenlabs-scribe";
    public STTProviderType Type => STTProviderType.ElevenLabs;
    public IReadOnlyList<string> SupportedLanguages => new[] { "auto", "pt", "en", "es", "fr", "de", "it", "ja", "zh" };

    public event EventHandler<TranscriptionSegmentEventArgs>? SegmentReceived;
    public event EventHandler<TranscriptionSegmentEventArgs>? PartialReceived;

    public ElevenLabsSTTProvider(ILogger<ElevenLabsSTTProvider>? logger = null)
    {
        _logger = logger;
    }

    public async Task InitializeAsync(STTProviderConfig config, CancellationToken ct = default)
    {
        if (_initialized)
            throw new InvalidOperationException("Provider already initialized.");

        _config = config;

        _apiKey = ResolveApiKey(config);

        await ConnectAsync(ct);
        _initialized = true;

        _receiveCts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoop(_receiveCts.Token), _receiveCts.Token);
    }

    /// <summary>
    /// A key configured in the Settings window wins; the environment variable is the
    /// fallback for setups that predate that field. Whitespace counts as absent, because
    /// pasted keys pick up trailing newlines and the API answers those with a 401 that
    /// looks like a wrong key.
    /// </summary>
    internal static string ResolveApiKey(STTProviderConfig config)
    {
        var configured = config.Options.GetValueOrDefault("ApiKey", "");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();

        var envVar = config.Options.GetValueOrDefault("ApiKeyEnvVar", "ELEVENLABS_API_KEY");
        var fromEnvironment = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment.Trim();

        throw new InvalidOperationException(
            "ElevenLabs API key not found. Set it in Configuracoes -> Chaves de API, " +
            $"or in the environment variable '{envVar}'.");
    }

    public async Task ProcessAudioAsync(AudioChunkEventArgs chunk, CancellationToken ct = default)
    {
        ThrowIfNotInitialized();

        if (_sessionOffset == TimeSpan.Zero && chunk.Timestamp > TimeSpan.Zero)
            _sessionOffset = chunk.Timestamp;

        if (_webSocket?.State != WebSocketState.Open)
        {
            _logger?.LogWarning("WebSocket not open, attempting reconnect");
            await ReconnectAsync(ct);
        }

        await SendChunkAsync(chunk.PcmData, chunk.SampleRate, commit: false, ct);
    }

    public async Task FlushAsync()
    {
        ThrowIfNotInitialized();

        if (_webSocket?.State != WebSocketState.Open)
            return;

        // There is no separate flush message in this protocol: a commit rides on an audio
        // chunk, so an empty one closes whatever the VAD has buffered.
        await SendChunkAsync([], TargetSampleRate, commit: true, CancellationToken.None);
    }

    private async Task SendChunkAsync(byte[] pcm, int sampleRate, bool commit, CancellationToken ct)
    {
        var message = new ElevenLabsAudioMessage
        {
            AudioBase64 = Convert.ToBase64String(pcm),
            Commit = commit,
            SampleRate = sampleRate
        };

        var json = JsonSerializer.Serialize(message, JsonContext.Default.ElevenLabsAudioMessage);
        await _webSocket!.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        _webSocket?.Dispose();
        _webSocket = new ClientWebSocket();
        _webSocket.Options.SetRequestHeader("xi-api-key", _apiKey);

        // The session is configured entirely through the query string. Without
        // audio_format the server assumes a different encoding than the 16 kHz mono PCM
        // the pipeline produces, and commit_strategy=vad is what segments on silence
        // instead of waiting for an explicit commit per utterance.
        var query = new List<string>
        {
            $"model_id={RealtimeModelId}",
            "audio_format=pcm_16000",
            "commit_strategy=vad"
        };

        var language = _config.Language ?? "auto";
        if (!string.Equals(language, "auto", StringComparison.OrdinalIgnoreCase))
            query.Add($"language_code={Uri.EscapeDataString(language)}");

        var uri = new Uri($"{WsEndpoint}?{string.Join("&", query)}");

        await _webSocket.ConnectAsync(uri, ct);
        _reconnectAttempts = 0;
        _logger?.LogInformation("Connected to ElevenLabs Scribe WebSocket");
    }

    private async Task ReconnectAsync(CancellationToken ct)
    {
        if (_reconnectAttempts >= MaxReconnectAttempts)
        {
            _logger?.LogError("Max reconnect attempts ({Max}) reached", MaxReconnectAttempts);
            throw new InvalidOperationException("Failed to reconnect to ElevenLabs after max attempts.");
        }

        var delay = ReconnectBaseDelayMs * (int)Math.Pow(2, _reconnectAttempts);
        _reconnectAttempts++;

        _logger?.LogWarning("Reconnecting to ElevenLabs (attempt {Attempt}/{Max}, delay {Delay}ms)",
            _reconnectAttempts, MaxReconnectAttempts, delay);

        await Task.Delay(delay, ct);
        await ConnectAsync(ct);
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        var buffer = new byte[8192];

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_webSocket?.State != WebSocketState.Open)
                {
                    await Task.Delay(500, ct);
                    continue;
                }

                var result = await _webSocket.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger?.LogWarning("WebSocket closed by server");
                    await ReconnectAsync(ct);
                    continue;
                }

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                ProcessResponse(json);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException ex)
            {
                _logger?.LogError(ex, "WebSocket error in receive loop");
                try { await ReconnectAsync(ct); }
                catch { break; }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error in receive loop");
                await Task.Delay(1000, ct);
            }
        }
    }

    internal void ProcessResponse(string json)
    {
        try
        {
            var response = JsonSerializer.Deserialize(json, JsonContext.Default.ElevenLabsResponse);
            if (response == null) return;

            switch (response.MessageType)
            {
                // Interim text that keeps being revised as more audio arrives.
                case "partial_transcript":
                    Emit(response, isFinal: false);
                    break;

                // "final" settles the wording; "committed" closes the segment. Both are
                // final as far as the rest of the pipeline is concerned.
                case "final_transcript":
                case "committed_transcript":
                case "committed_transcript_with_timestamps":
                    Emit(response, isFinal: true);
                    break;

                case "rate_limited":
                    _logger?.LogWarning("ElevenLabs rate limited: {Error}", response.Error);
                    break;

                case "input_error":
                    // Worth shouting about: it means we are speaking the wrong protocol,
                    // which previously went unnoticed because nothing ever transcribed.
                    _logger?.LogError("ElevenLabs rejected our message: {Error}", response.Error);
                    break;

                case "session_started":
                    _logger?.LogInformation("ElevenLabs session started");
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "Failed to parse ElevenLabs response");
        }
    }

    private void Emit(ElevenLabsResponse response, bool isFinal)
    {
        if (string.IsNullOrWhiteSpace(response.Text))
            return;

        // Word timestamps only come with the *_with_timestamps variant; otherwise the
        // segment is stamped with the session clock, which is what the timeline uses.
        var start = response.Words?.FirstOrDefault()?.Start ?? 0;
        var end = response.Words?.LastOrDefault()?.End ?? start;

        var segment = new TranscriptionSegment(
            Text: response.Text.Trim(),
            // Null-safe on purpose: partial_transcript carries no language_code, and the
            // config is only populated after InitializeAsync.
            Language: response.LanguageCode ?? _config?.Language ?? "unknown",
            Confidence: isFinal ? 1f : 0.5f,
            StartTime: _sessionOffset + TimeSpan.FromSeconds(start),
            EndTime: _sessionOffset + TimeSpan.FromSeconds(end),
            // The TranscriptionEngine rewrites this per channel; the provider itself
            // cannot know whether it was handed mic or loopback audio.
            Source: AudioSourceType.Microphone,
            IsFinal: isFinal
        );

        var args = new TranscriptionSegmentEventArgs(segment, DateTime.UtcNow);

        if (isFinal)
            SegmentReceived?.Invoke(this, args);
        else
            PartialReceived?.Invoke(this, args);
    }

    private void ThrowIfNotInitialized()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
            throw new InvalidOperationException("Provider not initialized. Call InitializeAsync first.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _receiveCts?.Cancel();
        if (_receiveTask != null)
        {
            try { await _receiveTask; }
            catch (OperationCanceledException) { }
        }

        if (_webSocket?.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "Disposing", CancellationToken.None);
            }
            catch { }
        }

        _receiveCts?.Dispose();
        _webSocket?.Dispose();
    }
}

/// <summary>
/// The only client-to-server message the realtime endpoint accepts. Every field here is
/// required by the protocol: an earlier version sent {"audio":..., "sample_rate":...},
/// which the server rejected with
/// {"message_type":"input_error","error":"Message must be a valid protocol message"}
/// for every single chunk — so this provider never transcribed anything.
/// </summary>
internal record ElevenLabsAudioMessage
{
    [JsonPropertyName("message_type")]
    public string MessageType { get; init; } = "input_audio_chunk";

    [JsonPropertyName("audio_base_64")]
    public string AudioBase64 { get; init; } = string.Empty;

    /// <summary>
    /// Forces the server to close the current segment. Left false while
    /// commit_strategy=vad segments on silence; set on <see cref="ElevenLabsSTTProvider.FlushAsync"/>.
    /// </summary>
    [JsonPropertyName("commit")]
    public bool Commit { get; init; }

    [JsonPropertyName("sample_rate")]
    public int SampleRate { get; init; }
}

/// <summary>
/// Server-to-client message. The discriminator is <c>message_type</c>, not <c>type</c>:
/// reading the wrong field meant every response was silently discarded.
/// </summary>
internal record ElevenLabsResponse
{
    [JsonPropertyName("message_type")]
    public string? MessageType { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("language_code")]
    public string? LanguageCode { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("words")]
    public ElevenLabsWord[]? Words { get; init; }
}

internal record ElevenLabsWord
{
    [JsonPropertyName("start")]
    public double? Start { get; init; }

    [JsonPropertyName("end")]
    public double? End { get; init; }
}

[JsonSerializable(typeof(ElevenLabsAudioMessage))]
[JsonSerializable(typeof(ElevenLabsResponse))]
internal partial class JsonContext : JsonSerializerContext
{
}
