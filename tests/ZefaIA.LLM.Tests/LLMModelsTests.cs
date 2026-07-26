using Xunit;
using ZefaIA.Core.Models;

namespace ZefaIA.LLM.Tests;

public class LLMModelsTests
{
    [Fact]
    public void LLMSessionConfig_HasCorrectDefaults()
    {
        var config = new LLMSessionConfig("system prompt", "meeting context");

        Assert.Equal("claude-sonnet-5", config.ModelId);
        Assert.Equal(1024, config.MaxTokens);
    }

    [Fact]
    public void LLMSessionConfig_ModelIdIsAnUndatedAlias()
    {
        var config = new LLMSessionConfig("system prompt", "meeting context");

        // A dated snapshot is what broke this before: claude-sonnet-4-20250514 hit its
        // retirement date and every request would have started failing. Aliases do not
        // expire, so keep the default free of a date suffix.
        Assert.DoesNotMatch(@"-\d{8}$", config.ModelId);
    }

    [Fact]
    public void TriggerEventArgs_CreatesCorrectly()
    {
        var trigger = new TriggerEventArgs(
            "SilenceTrigger",
            TriggerReason.Silence,
            TimeSpan.FromSeconds(60),
            DateTime.UtcNow
        );

        Assert.Equal(TriggerReason.Silence, trigger.Reason);
        Assert.Equal("SilenceTrigger", trigger.TriggerName);
    }

    [Fact]
    public void LLMSessionMetrics_InitializesToZero()
    {
        var metrics = new LLMSessionMetrics();

        Assert.Equal(0, metrics.TotalRequests);
        Assert.Equal(0, metrics.CacheHits);
        Assert.Equal(0, metrics.TotalInputTokens);
        Assert.Equal(0, metrics.TotalOutputTokens);
        Assert.Equal(0, metrics.AverageLatencyMs);
    }
}
