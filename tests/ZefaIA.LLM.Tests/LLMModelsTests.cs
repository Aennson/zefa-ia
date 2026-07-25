using Xunit;
using ZefaIA.Core.Models;

namespace ZefaIA.LLM.Tests;

public class LLMModelsTests
{
    [Fact]
    public void LLMSessionConfig_HasCorrectDefaults()
    {
        var config = new LLMSessionConfig("system prompt", "meeting context");

        Assert.Equal("claude-sonnet-4-20250514", config.ModelId);
        Assert.Equal(512, config.MaxTokens);
        Assert.Equal(0.7f, config.Temperature);
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
