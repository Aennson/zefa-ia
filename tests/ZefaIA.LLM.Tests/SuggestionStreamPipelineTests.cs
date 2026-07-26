using Moq;
using Xunit;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.LLM.Tests;

public class SuggestionStreamPipelineTests
{
    [Fact]
    public void InitialState_IsIdle()
    {
        var pipeline = new SuggestionStreamPipeline();
        Assert.Equal(SuggestionState.Idle, pipeline.State);
    }

    [Fact]
    public async Task RequestSuggestion_TransitionsThinkingToStreamingToComplete()
    {
        var states = new List<SuggestionState>();
        var session = CreateMockSession("Hello", " world");
        var pipeline = new SuggestionStreamPipeline();
        pipeline.OnStateChanged += s => states.Add(s);

        await pipeline.RequestSuggestionAsync(session, "transcript", CreateContext());

        Assert.Contains(SuggestionState.Thinking, states);
        Assert.Contains(SuggestionState.Streaming, states);
        Assert.Contains(SuggestionState.Complete, states);
    }

    [Fact]
    public async Task RequestSuggestion_EmitsTokens()
    {
        var tokens = new List<string>();
        var session = CreateMockSession("Hello", " world", "!");
        var pipeline = new SuggestionStreamPipeline();
        pipeline.OnTokenReceived += t => tokens.Add(t);

        await pipeline.RequestSuggestionAsync(session, "transcript", CreateContext());

        Assert.Equal(3, tokens.Count);
        Assert.Equal("Hello", tokens[0]);
        Assert.Equal(" world", tokens[1]);
        Assert.Equal("!", tokens[2]);
    }

    [Fact]
    public async Task RequestSuggestion_FiresThinkingStarted()
    {
        bool thinkingStarted = false;
        var session = CreateMockSession("token");
        var pipeline = new SuggestionStreamPipeline();
        pipeline.OnThinkingStarted += () => thinkingStarted = true;

        await pipeline.RequestSuggestionAsync(session, "transcript", CreateContext());

        Assert.True(thinkingStarted);
    }

    [Fact]
    public async Task RequestSuggestion_FiresOnComplete()
    {
        bool completed = false;
        var session = CreateMockSession("done");
        var pipeline = new SuggestionStreamPipeline();
        pipeline.OnComplete += () => completed = true;

        await pipeline.RequestSuggestionAsync(session, "transcript", CreateContext());

        Assert.True(completed);
    }

    [Fact]
    public async Task RequestSuggestion_NoSuggestion_FilteredOut()
    {
        var tokens = new List<string>();
        var session = CreateMockSession("[SEM", " SUGESTAO]");
        var pipeline = new SuggestionStreamPipeline();
        pipeline.OnTokenReceived += t => tokens.Add(t);

        await pipeline.RequestSuggestionAsync(session, "transcript", CreateContext());

        Assert.Empty(tokens);
    }

    [Fact]
    public async Task RequestSuggestion_ApiError_TransitionsToError()
    {
        string? errorMsg = null;
        var session = CreateErrorSession("API error");
        var pipeline = new SuggestionStreamPipeline();
        pipeline.OnError += msg => errorMsg = msg;

        await pipeline.RequestSuggestionAsync(session, "transcript", CreateContext());

        Assert.Equal(SuggestionState.Error, pipeline.State);
        Assert.NotNull(errorMsg);
        Assert.Contains("API error", errorMsg);
    }

    [Fact]
    public void IsNoSuggestion_ExactMatch_ReturnsTrue()
    {
        Assert.True(SuggestionStreamPipeline.IsNoSuggestion("[SEM SUGESTAO]"));
    }

    [Fact]
    public void IsNoSuggestion_WithWhitespace_ReturnsTrue()
    {
        Assert.True(SuggestionStreamPipeline.IsNoSuggestion("  [SEM SUGESTAO]  "));
    }

    [Fact]
    public void IsNoSuggestion_PartialText_ReturnsFalse()
    {
        Assert.False(SuggestionStreamPipeline.IsNoSuggestion("[SEM"));
    }

    [Fact]
    public void IsNoSuggestion_RegularText_ReturnsFalse()
    {
        Assert.False(SuggestionStreamPipeline.IsNoSuggestion("Considere o impacto financeiro"));
    }

    [Fact]
    public void Dispose_MultipleCallsDoNotThrow()
    {
        var pipeline = new SuggestionStreamPipeline();
        pipeline.Dispose();
        pipeline.Dispose();
    }

    private static SuggestionContext CreateContext() =>
        new("transcript", TriggerReason.Silence, TimeSpan.FromSeconds(60));

    private static ILLMSession CreateMockSession(params string[] tokens)
    {
        var mock = new Mock<ILLMSession>();
        mock.Setup(s => s.GetSuggestionStreamAsync(
                It.IsAny<string>(), It.IsAny<SuggestionContext>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(tokens));
        mock.Setup(s => s.Metrics).Returns(new LLMSessionMetrics());
        return mock.Object;
    }

    private static ILLMSession CreateErrorSession(string errorMessage)
    {
        var mock = new Mock<ILLMSession>();
        mock.Setup(s => s.GetSuggestionStreamAsync(
                It.IsAny<string>(), It.IsAny<SuggestionContext>(), It.IsAny<CancellationToken>()))
            .Returns(ErrorAsyncEnumerable(errorMessage));
        mock.Setup(s => s.Metrics).Returns(new LLMSessionMetrics());
        return mock.Object;
    }

    private static async IAsyncEnumerable<string> ToAsyncEnumerable(string[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    private static async IAsyncEnumerable<string> ErrorAsyncEnumerable(string message)
    {
        await Task.Yield();
        throw new HttpRequestException(message);
        yield break;
    }
}
