using System.Net;
using System.Text;
using System.Text.Json;
using Moq;
using Moq.Protected;
using Xunit;
using ZefaIA.Core.Models;

namespace ZefaIA.LLM.Tests;

public class ClaudeLLMClientTests
{
    [Fact]
    public void Constructor_ThrowsWhenNoApiKey()
    {
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        Assert.Throws<InvalidOperationException>(() =>
            new ClaudeLLMClient(apiKey: null, httpClient: new HttpClient()));
    }

    [Fact]
    public async Task CreateSession_ReturnsSession()
    {
        var client = new ClaudeLLMClient(apiKey: "test-key", httpClient: new HttpClient());
        var config = new LLMSessionConfig("system", "context");

        var session = await client.CreateSessionAsync(config);

        Assert.NotNull(session);
        Assert.NotNull(session.Metrics);
        Assert.Equal(0, session.Metrics.TotalRequests);
    }

    [Fact]
    public void BuildRequestBody_IncludesCacheControl()
    {
        var config = new LLMSessionConfig("You are a helpful assistant", "meeting about Q4");
        var session = CreateTestSession(config);

        var request = session.BuildRequestBody("User said hello");

        Assert.Equal(config.ModelId, request.Model);
        Assert.Equal(config.MaxTokens, request.MaxTokens);
        Assert.True(request.Stream);
        Assert.Single(request.System);
        Assert.Equal("You are a helpful assistant", request.System[0].Text);
        Assert.NotNull(request.System[0].CacheControl);
        Assert.Equal("ephemeral", request.System[0].CacheControl!.Type);
        Assert.Single(request.Messages);
        Assert.Equal("user", request.Messages[0].Role);
        Assert.Equal("User said hello", request.Messages[0].Content);
    }

    [Fact]
    public void BuildRequestBody_UsesConfigModelAndTokens()
    {
        var config = new LLMSessionConfig("sys", "ctx", ModelId: "claude-haiku-4-5-20251001", MaxTokens: 256);
        var session = CreateTestSession(config);

        var request = session.BuildRequestBody("transcript");

        Assert.Equal("claude-haiku-4-5-20251001", request.Model);
        Assert.Equal(256, request.MaxTokens);
    }

    [Fact]
    public void BuildRequestBody_SerializesToValidJson()
    {
        var config = new LLMSessionConfig("system prompt", "context");
        var session = CreateTestSession(config);
        var request = session.BuildRequestBody("hello world");

        var json = JsonSerializer.Serialize(request, LLMJsonContext.Default.ClaudeRequest);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("model", out _));
        Assert.True(root.TryGetProperty("max_tokens", out _));
        Assert.True(root.TryGetProperty("stream", out _));
        Assert.True(root.TryGetProperty("system", out var system));
        Assert.Equal(JsonValueKind.Array, system.ValueKind);

        var sysBlock = system[0];
        Assert.True(sysBlock.TryGetProperty("cache_control", out var cache));
        Assert.Equal("ephemeral", cache.GetProperty("type").GetString());
    }

    [Fact]
    public void ParseSSEData_ParsesContentDelta()
    {
        var data = """{"type":"content_block_delta","delta":{"type":"text_delta","text":"Hello"}}""";

        var result = ClaudeLLMSession.ParseSSEData(data);

        Assert.NotNull(result);
        Assert.Equal("content_block_delta", result!.Type);
        Assert.Equal("Hello", result.Delta?.Text);
    }

    [Fact]
    public void ParseSSEData_ParsesMessageStart()
    {
        var data = """{"type":"message_start","message":{"usage":{"input_tokens":100,"output_tokens":0,"cache_read_input_tokens":80,"cache_creation_input_tokens":0}}}""";

        var result = ClaudeLLMSession.ParseSSEData(data);

        Assert.NotNull(result);
        Assert.Equal("message_start", result!.Type);
        Assert.Equal(100, result.Message?.Usage?.InputTokens);
        Assert.Equal(80, result.Message?.Usage?.CacheReadInputTokens);
    }

    [Fact]
    public void ParseSSEData_InvalidJson_ReturnsNull()
    {
        var result = ClaudeLLMSession.ParseSSEData("{invalid json}");
        Assert.Null(result);
    }

    [Fact]
    public void ParseSSEData_MessageDeltaWithUsage()
    {
        var data = """{"type":"message_delta","usage":{"output_tokens":42}}""";

        var result = ClaudeLLMSession.ParseSSEData(data);

        Assert.NotNull(result);
        Assert.Equal("message_delta", result!.Type);
        Assert.Equal(42, result.Usage?.OutputTokens);
    }

    [Fact]
    public async Task ParseSSEStream_ExtractsTextTokens()
    {
        var sseContent = """
            data: {"type":"message_start","message":{"usage":{"input_tokens":10,"output_tokens":0,"cache_read_input_tokens":0,"cache_creation_input_tokens":0}}}
            data: {"type":"content_block_start","content_block":{"type":"text","text":""}}
            data: {"type":"content_block_delta","delta":{"type":"text_delta","text":"Hello"}}
            data: {"type":"content_block_delta","delta":{"type":"text_delta","text":" world"}}
            data: {"type":"content_block_stop"}
            data: {"type":"message_delta","usage":{"output_tokens":5}}
            data: [DONE]
            """;

        var response = CreateFakeSSEResponse(sseContent);
        var tokens = new List<string>();

        await foreach (var token in ClaudeLLMSession.ParseSSEStreamAsync(response))
        {
            tokens.Add(token);
        }

        Assert.Equal(2, tokens.Count);
        Assert.Equal("Hello", tokens[0]);
        Assert.Equal(" world", tokens[1]);
    }

    [Fact]
    public async Task ParseSSEStream_HandlesEmptyStream()
    {
        var response = CreateFakeSSEResponse("data: [DONE]\n");
        var tokens = new List<string>();

        await foreach (var token in ClaudeLLMSession.ParseSSEStreamAsync(response))
        {
            tokens.Add(token);
        }

        Assert.Empty(tokens);
    }

    [Fact]
    public async Task ParseSSEStream_IgnoresNonDataLines()
    {
        var sseContent = """
            event: message_start
            : comment
            data: {"type":"content_block_delta","delta":{"type":"text_delta","text":"token"}}
            data: [DONE]
            """;

        var response = CreateFakeSSEResponse(sseContent);
        var tokens = new List<string>();

        await foreach (var token in ClaudeLLMSession.ParseSSEStreamAsync(response))
        {
            tokens.Add(token);
        }

        Assert.Single(tokens);
        Assert.Equal("token", tokens[0]);
    }

    [Fact]
    public void UpdateCacheMetrics_TracksInputTokensAndCacheHits()
    {
        var config = new LLMSessionConfig("sys", "ctx");
        var session = CreateTestSession(config);

        var sseEvent = new SSEEvent
        {
            Type = "message_start",
            Message = new MessageContent
            {
                Usage = new UsageInfo
                {
                    InputTokens = 150,
                    OutputTokens = 0,
                    CacheReadInputTokens = 100,
                    CacheCreationInputTokens = 50
                }
            }
        };

        session.UpdateCacheMetrics(sseEvent);

        Assert.Equal(150, session.Metrics.TotalInputTokens);
        Assert.Equal(1, session.Metrics.CacheHits);
    }

    [Fact]
    public void UpdateCacheMetrics_NoCacheRead_NoCacheHit()
    {
        var config = new LLMSessionConfig("sys", "ctx");
        var session = CreateTestSession(config);

        var sseEvent = new SSEEvent
        {
            Type = "message_start",
            Message = new MessageContent
            {
                Usage = new UsageInfo
                {
                    InputTokens = 200,
                    OutputTokens = 0,
                    CacheReadInputTokens = 0,
                    CacheCreationInputTokens = 200
                }
            }
        };

        session.UpdateCacheMetrics(sseEvent);

        Assert.Equal(200, session.Metrics.TotalInputTokens);
        Assert.Equal(0, session.Metrics.CacheHits);
    }

    [Fact]
    public void UpdateCacheMetrics_DeltaUsageTracksOutputTokens()
    {
        var config = new LLMSessionConfig("sys", "ctx");
        var session = CreateTestSession(config);

        var sseEvent = new SSEEvent
        {
            Type = "message_delta",
            Usage = new UsageInfo { OutputTokens = 42 }
        };

        session.UpdateCacheMetrics(sseEvent);

        Assert.Equal(42, session.Metrics.TotalOutputTokens);
    }

    [Fact]
    public void Metrics_InitializedToZero()
    {
        var config = new LLMSessionConfig("sys", "ctx");
        var session = CreateTestSession(config);

        Assert.Equal(0, session.Metrics.TotalRequests);
        Assert.Equal(0, session.Metrics.CacheHits);
        Assert.Equal(0, session.Metrics.TotalInputTokens);
        Assert.Equal(0, session.Metrics.TotalOutputTokens);
        Assert.Equal(0, session.Metrics.AverageLatencyMs);
    }

    [Fact(Skip = "Requires ANTHROPIC_API_KEY")]
    public async Task Integration_StreamsRealResponse()
    {
        var client = new ClaudeLLMClient();
        var config = new LLMSessionConfig(
            "You are a test assistant. Reply with exactly: OK",
            "test");
        var session = await client.CreateSessionAsync(config);
        var context = new SuggestionContext("User: hello", TriggerReason.Manual, TimeSpan.FromSeconds(30));

        var tokens = new List<string>();
        await foreach (var token in session.GetSuggestionStreamAsync("Say OK", context))
        {
            tokens.Add(token);
        }

        Assert.NotEmpty(tokens);
        var fullText = string.Join("", tokens);
        Assert.Contains("OK", fullText);
    }

    private static ClaudeLLMSession CreateTestSession(LLMSessionConfig config)
    {
        return new ClaudeLLMSession(config, new HttpClient(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            TimeSpan.FromSeconds(30));
    }

    private static HttpResponseMessage CreateFakeSSEResponse(string content)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "text/event-stream")
        };
        return response;
    }
}
