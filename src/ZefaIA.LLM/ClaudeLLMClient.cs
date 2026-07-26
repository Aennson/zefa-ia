using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.LLM;

public sealed class ClaudeLLMClient : ILLMClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<ClaudeLLMClient> _logger;
    private readonly TimeSpan _timeout;
    private bool _disposed;

    internal const string ApiBaseUrl = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";
    private const string PromptCachingBeta = "prompt-caching-2024-07-31";

    public ClaudeLLMClient(
        string? apiKey = null,
        HttpClient? httpClient = null,
        ILogger<ClaudeLLMClient>? logger = null,
        TimeSpan? timeout = null)
    {
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            ?? throw new InvalidOperationException("ANTHROPIC_API_KEY not set");
        _httpClient = httpClient ?? new HttpClient();
        _logger = logger ?? NullLogger<ClaudeLLMClient>.Instance;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);

        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", ApiVersion);
        _httpClient.DefaultRequestHeaders.Add("anthropic-beta", PromptCachingBeta);
    }

    public Task<ILLMSession> CreateSessionAsync(LLMSessionConfig config, CancellationToken ct = default)
    {
        var session = new ClaudeLLMSession(config, _httpClient, _logger, _timeout);
        return Task.FromResult<ILLMSession>(session);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _httpClient.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal sealed class ClaudeLLMSession : ILLMSession
{
    private readonly LLMSessionConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly TimeSpan _timeout;
    private bool _disposed;

    private const int MaxRetries = 3;
    private static readonly int[] RetryDelaysMs = [1000, 2000, 4000];

    public LLMSessionMetrics Metrics { get; } = new();

    internal ClaudeLLMSession(
        LLMSessionConfig config,
        HttpClient httpClient,
        ILogger logger,
        TimeSpan timeout)
    {
        _config = config;
        _httpClient = httpClient;
        _logger = logger;
        _timeout = timeout;
    }

    public async IAsyncEnumerable<string> GetSuggestionStreamAsync(
        string recentTranscript,
        SuggestionContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var requestBody = BuildRequestBody(recentTranscript);
        var json = JsonSerializer.Serialize(requestBody, LLMJsonContext.Default.ClaudeRequest);

        var startTime = DateTime.UtcNow;
        Metrics.TotalRequests++;

        using var response = await SendWithRetryAsync(json, ct);
        response.EnsureSuccessStatusCode();

        await foreach (var token in ParseSSEStreamAsync(response, ct))
        {
            yield return token;
        }

        var latencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
        UpdateAverageLatency(latencyMs);
    }

    internal ClaudeRequest BuildRequestBody(string recentTranscript)
    {
        return new ClaudeRequest
        {
            Model = _config.ModelId,
            MaxTokens = _config.MaxTokens,
            Stream = true,
            System = new[]
            {
                new SystemBlock
                {
                    Type = "text",
                    Text = _config.SystemPrompt,
                    CacheControl = new CacheControl { Type = "ephemeral" }
                }
            },
            Messages = new[]
            {
                new Message
                {
                    Role = "user",
                    Content = recentTranscript
                }
            }
        };
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(string json, CancellationToken ct)
    {
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, ClaudeLLMClient.ApiBaseUrl)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(_timeout);

                var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cts.Token);

                var statusCode = (int)response.StatusCode;
                if (statusCode == 429 || statusCode >= 500)
                {
                    response.Dispose();
                    if (attempt < MaxRetries)
                    {
                        _logger.LogWarning("Claude API returned {Status}, retry {Attempt}/{Max}",
                            statusCode, attempt + 1, MaxRetries);
                        await Task.Delay(RetryDelaysMs[attempt], ct);
                        continue;
                    }
                }

                return response;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < MaxRetries)
            {
                _logger.LogWarning("Claude API timeout, retry {Attempt}/{Max}", attempt + 1, MaxRetries);
                await Task.Delay(RetryDelaysMs[attempt], ct);
            }
        }

        throw new HttpRequestException("Claude API failed after all retries");
    }

    internal static async IAsyncEnumerable<string> ParseSSEStreamAsync(
        HttpResponseMessage response,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;

            if (!line.StartsWith("data: ")) continue;

            var data = line[6..];
            if (data == "[DONE]") break;

            var sseEvent = ParseSSEData(data);
            if (sseEvent == null) continue;

            switch (sseEvent.Type)
            {
                case "content_block_delta":
                    if (sseEvent.Delta?.Text is { } text)
                        yield return text;
                    break;

                case "message_start":
                    break;

                case "message_delta":
                    break;
            }
        }
    }

    internal static SSEEvent? ParseSSEData(string data)
    {
        try
        {
            return JsonSerializer.Deserialize(data, LLMJsonContext.Default.SSEEvent);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal void UpdateCacheMetrics(SSEEvent sseEvent)
    {
        if (sseEvent.Message?.Usage is { } usage)
        {
            Metrics.TotalInputTokens += usage.InputTokens;
            Metrics.TotalOutputTokens += usage.OutputTokens;

            if (usage.CacheReadInputTokens > 0)
                Metrics.CacheHits++;
        }

        if (sseEvent.Usage is { } deltaUsage)
        {
            Metrics.TotalOutputTokens += deltaUsage.OutputTokens;
        }
    }

    private void UpdateAverageLatency(double latencyMs)
    {
        if (Metrics.TotalRequests <= 1)
        {
            Metrics.AverageLatencyMs = latencyMs;
        }
        else
        {
            var total = Metrics.AverageLatencyMs * (Metrics.TotalRequests - 1) + latencyMs;
            Metrics.AverageLatencyMs = total / Metrics.TotalRequests;
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}

#region JSON Models

internal sealed class ClaudeRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("system")]
    public SystemBlock[] System { get; set; } = [];

    [JsonPropertyName("messages")]
    public Message[] Messages { get; set; } = [];
}

internal sealed class SystemBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("cache_control")]
    public CacheControl? CacheControl { get; set; }
}

internal sealed class CacheControl
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "ephemeral";
}

internal sealed class Message
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

internal sealed class SSEEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("delta")]
    public DeltaContent? Delta { get; set; }

    [JsonPropertyName("message")]
    public MessageContent? Message { get; set; }

    [JsonPropertyName("usage")]
    public UsageInfo? Usage { get; set; }
}

internal sealed class DeltaContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

internal sealed class MessageContent
{
    [JsonPropertyName("usage")]
    public UsageInfo? Usage { get; set; }
}

internal sealed class UsageInfo
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("cache_creation_input_tokens")]
    public int CacheCreationInputTokens { get; set; }

    [JsonPropertyName("cache_read_input_tokens")]
    public int CacheReadInputTokens { get; set; }
}

[JsonSerializable(typeof(ClaudeRequest))]
[JsonSerializable(typeof(SSEEvent))]
internal partial class LLMJsonContext : JsonSerializerContext
{
}

#endregion
