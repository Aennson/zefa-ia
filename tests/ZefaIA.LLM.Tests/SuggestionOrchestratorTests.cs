using Moq;
using Xunit;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.LLM.Tests;

public class SuggestionOrchestratorTests
{
    [Fact]
    public async Task HandleTrigger_WithTranscript_MakesRequest()
    {
        var (orchestrator, _) = CreateOrchestrator();
        orchestrator.TranscriptProvider = _ => "User: hello\nInterlocutor: hi there";

        var args = CreateTriggerArgs(TriggerReason.Silence);
        await orchestrator.HandleTriggerAsync(args);

        Assert.Equal(1, orchestrator.Metrics.TotalRequests);
    }

    [Fact]
    public async Task HandleTrigger_EmptyTranscript_SkipsRequest()
    {
        var (orchestrator, _) = CreateOrchestrator();
        orchestrator.TranscriptProvider = _ => "";

        await orchestrator.HandleTriggerAsync(CreateTriggerArgs());

        Assert.Equal(0, orchestrator.Metrics.TotalRequests);
    }

    [Fact]
    public async Task HandleTrigger_NoTranscriptProvider_SkipsRequest()
    {
        var (orchestrator, _) = CreateOrchestrator();

        await orchestrator.HandleTriggerAsync(CreateTriggerArgs());

        Assert.Equal(0, orchestrator.Metrics.TotalRequests);
    }

    [Fact]
    public async Task HandleTrigger_DuplicateTranscript_SkipsSecond()
    {
        var (orchestrator, _) = CreateOrchestrator();
        orchestrator.TranscriptProvider = _ => "same transcript";

        await orchestrator.HandleTriggerAsync(CreateTriggerArgs());
        await orchestrator.HandleTriggerAsync(CreateTriggerArgs());

        Assert.Equal(1, orchestrator.Metrics.TotalRequests);
        Assert.Equal(1, orchestrator.Metrics.DeduplicatedCount);
    }

    [Fact]
    public async Task HandleTrigger_DifferentTranscripts_ProcessesBoth()
    {
        var (orchestrator, _) = CreateOrchestrator();
        int callCount = 0;
        orchestrator.TranscriptProvider = _ =>
        {
            callCount++;
            return $"transcript {callCount}";
        };

        await orchestrator.HandleTriggerAsync(CreateTriggerArgs());
        await orchestrator.HandleTriggerAsync(CreateTriggerArgs());

        Assert.Equal(2, orchestrator.Metrics.TotalRequests);
    }

    [Fact]
    public async Task RateLimiter_BlocksExcessRequests()
    {
        var config = new OrchestratorConfig { MaxRequestsPerMinute = 2 };
        var (orchestrator, _) = CreateOrchestrator(config);
        int callCount = 0;
        orchestrator.TranscriptProvider = _ => $"transcript {++callCount}";

        await orchestrator.HandleTriggerAsync(CreateTriggerArgs());
        await orchestrator.HandleTriggerAsync(CreateTriggerArgs());
        await orchestrator.HandleTriggerAsync(CreateTriggerArgs());

        Assert.Equal(2, orchestrator.Metrics.TotalRequests);
        Assert.Equal(1, orchestrator.Metrics.RateLimitedCount);
    }

    [Fact]
    public void IsRateLimitAllowed_UnderLimit_ReturnsTrue()
    {
        var config = new OrchestratorConfig { MaxRequestsPerMinute = 4 };
        var (orchestrator, _) = CreateOrchestrator(config);

        Assert.True(orchestrator.IsRateLimitAllowed());
    }

    [Fact]
    public void IsDuplicate_SameText_ReturnsTrue()
    {
        var (orchestrator, _) = CreateOrchestrator();

        Assert.False(orchestrator.IsDuplicate("first time"));
    }

    [Fact]
    public void ComputeHash_SameInput_SameOutput()
    {
        var hash1 = SuggestionOrchestrator.ComputeHash("hello world");
        var hash2 = SuggestionOrchestrator.ComputeHash("hello world");

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_DifferentInput_DifferentOutput()
    {
        var hash1 = SuggestionOrchestrator.ComputeHash("hello world");
        var hash2 = SuggestionOrchestrator.ComputeHash("goodbye world");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Config_HasCorrectDefaults()
    {
        var config = new OrchestratorConfig();

        Assert.Equal(4, config.MaxRequestsPerMinute);
        Assert.Equal(TimeSpan.FromSeconds(60), config.DefaultTranscriptWindow);
    }

    [Fact]
    public void Metrics_InitializedToZero()
    {
        var metrics = new OrchestratorMetrics();

        Assert.Equal(0, metrics.TotalRequests);
        Assert.Equal(0, metrics.RateLimitedCount);
        Assert.Equal(0, metrics.DeduplicatedCount);
        Assert.Equal(0, metrics.EstimatedCostUsd);
    }

    [Fact]
    public void RegisterTrigger_SubscribesToEvents()
    {
        var (orchestrator, _) = CreateOrchestrator();
        var mockTrigger = new Mock<ITriggerStrategy>();
        mockTrigger.SetupAdd(t => t.Triggered += It.IsAny<EventHandler<TriggerEventArgs>>());

        orchestrator.RegisterTrigger(mockTrigger.Object);

        mockTrigger.VerifyAdd(t => t.Triggered += It.IsAny<EventHandler<TriggerEventArgs>>(), Times.Once);
    }

    [Fact]
    public void Dispose_UnsubscribesFromTriggers()
    {
        var (orchestrator, _) = CreateOrchestrator();
        var mockTrigger = new Mock<ITriggerStrategy>();
        orchestrator.RegisterTrigger(mockTrigger.Object);

        orchestrator.Dispose();

        mockTrigger.VerifyRemove(t => t.Triggered -= It.IsAny<EventHandler<TriggerEventArgs>>(), Times.Once);
    }

    [Fact]
    public void Dispose_MultipleCallsDoNotThrow()
    {
        var (orchestrator, _) = CreateOrchestrator();
        orchestrator.Dispose();
        orchestrator.Dispose();
    }

    private static (SuggestionOrchestrator orchestrator, Mock<ILLMSession> session) CreateOrchestrator(
        OrchestratorConfig? config = null)
    {
        var session = new Mock<ILLMSession>();
        session.Setup(s => s.GetSuggestionStreamAsync(
                It.IsAny<string>(), It.IsAny<SuggestionContext>(), It.IsAny<CancellationToken>()))
            .Returns(EmptyAsyncEnumerable());
        session.Setup(s => s.Metrics).Returns(new LLMSessionMetrics());

        var pipeline = new SuggestionStreamPipeline();
        var orchestrator = new SuggestionOrchestrator(session.Object, pipeline, config);

        return (orchestrator, session);
    }

    private static TriggerEventArgs CreateTriggerArgs(TriggerReason reason = TriggerReason.Silence) =>
        new("TestTrigger", reason, TimeSpan.FromSeconds(60), DateTime.UtcNow);

    private static async IAsyncEnumerable<string> EmptyAsyncEnumerable()
    {
        await Task.Yield();
        yield break;
    }
}
