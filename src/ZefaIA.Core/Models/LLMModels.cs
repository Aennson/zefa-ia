namespace ZefaIA.Core.Models;

/// <summary>
/// Per-meeting LLM settings.
/// </summary>
/// <param name="ModelId">
/// Undated alias on purpose: the previous default was the dated snapshot
/// <c>claude-sonnet-4-20250514</c>, which reached its retirement date and would make
/// every suggestion request fail. Aliases track the current model instead of expiring.
/// </param>
/// <param name="MaxTokens">
/// Caps the whole response. Sized with headroom because Claude Sonnet 5 uses a newer
/// tokenizer that produces roughly 30% more tokens for the same text than Sonnet 4 did,
/// so a budget tuned for the old model would now truncate an equivalent suggestion.
/// </param>
public record LLMSessionConfig(
    string SystemPrompt,
    string MeetingContext,
    string ModelId = "claude-sonnet-5",
    int MaxTokens = 1024
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
