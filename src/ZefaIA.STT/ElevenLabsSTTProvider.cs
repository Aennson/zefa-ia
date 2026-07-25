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

        var apiKeyEnvVar = config.Options.GetValueOrDefault("ApiKeyEnvVar", "ELEVENLABS_API_KEY");
        _apiKey = Environment.GetEnvironmentVariable(apiKeyEnvVar)
            ?? throw new InvalidOperationException(
                $"ElevenLabs API key not found. Set environment variable '{apiKeyEnvVar}'.");

        await ConnectAsync(ct);
        _initialized = true;

        _receiveCts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoop(_receiveCts.Token), _receiveCts.Token);
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

        var message = new ElevenLabsAudioMessage
        {
            Audio = Convert.ToBase64String(chunk.PcmData),
            SampleRate = chunk.SampleRate
        };

        var json = JsonSerializer.Serialize(message, JsonContext.Default.ElevenLabsAudioMessage);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _webSocket!.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    public async Task FlushAsync()
    {
        ThrowIfNotInitialized();

        if (_webSocket?.State != WebSocketState.Open)
            return;

        var flushMessage = "{\"flush\": true}";
        var bytes = Encoding.UTF8.GetBytes(flushMessage);
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        _webSocket?.Dispose();
        _webSocket = new ClientWebSocket();
        _webSocket.Options.SetRequestHeader("xi-api-key", _apiKey);

        var uri = new Uri(WsEndpoint);

        var language = _config.Language ?? "auto";
        if (language != "auto")
            uri = new Uri($"{WsEndpoint}?language={language}");

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
            if (response == null || response.Type != "transcript")
                return;

            if (string.IsNullOrWhiteSpace(response.Text))
                return;

            var segment = new TranscriptionSegment(
                Text: response.Text.Trim(),
                Language: response.Language ?? _config.Language ?? "unknown",
                Confidence: response.Confidence ?? 0f,
                StartTime: _sessionOffset + TimeSpan.FromSeconds(response.StartTime ?? 0),
                EndTime: _sessionOffset + TimeSpan.FromSeconds(response.EndTime ?? 0),
                Source: AudioSourceType.Microphone,
                IsFinal: response.IsFinal ?? false
            );

            var args = new TranscriptionSegmentEventArgs(segment, DateTime.UtcNow);

            if (segment.IsFinal)
                SegmentReceived?.Invoke(this, args);
            else
                PartialReceived?.Invoke(this, args);
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "Failed to parse ElevenLabs response");
        }
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

internal record ElevenLabsAudioMessage
{
    [JsonPropertyName("audio")]
    public string Audio { get; init; } = string.Empty;

    [JsonPropertyName("sample_rate")]
    public int SampleRate { get; init; }
}

internal record ElevenLabsResponse
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("is_final")]
    public bool? IsFinal { get; init; }

    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("confidence")]
    public float? Confidence { get; init; }

    [JsonPropertyName("start_time")]
    public double? StartTime { get; init; }

    [JsonPropertyName("end_time")]
    public double? EndTime { get; init; }
}

[JsonSerializable(typeof(ElevenLabsAudioMessage))]
[JsonSerializable(typeof(ElevenLabsResponse))]
internal partial class JsonContext : JsonSerializerContext
{
}
