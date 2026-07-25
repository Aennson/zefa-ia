namespace ZefaIA.Core.Models;

public record LLMSessionConfig(
    string SystemPrompt,
    string MeetingContext,
    string ModelId = "claude-sonnet-4-20250514",
    int MaxTokens = 512,
    float Temperature = 0.7f
);

public record SuggestionContext(
    string RecentTranscript,
    TriggerReason Reason,
    TimeSpan TranscriptWindow
);

public enum TriggerReason
{
    Silence,
    Hotkey,
    Manual
}

public record TriggerEventArgs(
    string TriggerName,
    TriggerReason Reason,
    TimeSpan TranscriptWindow,
    DateTime Timestamp
);

public record LLMSessionMetrics
{
    public int TotalRequests { get; set; }
    public int CacheHits { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public double AverageLatencyMs { get; set; }
}
