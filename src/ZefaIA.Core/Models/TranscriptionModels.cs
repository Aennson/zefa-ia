namespace ZefaIA.Core.Models;

public record TranscriptionSegment(
    string Text,
    string Language,
    float Confidence,
    TimeSpan StartTime,
    TimeSpan EndTime,
    AudioSourceType Source,
    bool IsFinal
);

public record TranscriptionSegmentEventArgs(
    TranscriptionSegment Segment,
    DateTime ReceivedAt
);

public enum STTProviderType
{
    WhisperLocal,
    ElevenLabs
}

public record STTProviderConfig
{
    public STTProviderType ProviderType { get; init; }
    public string? Language { get; init; }
    public Dictionary<string, string> Options { get; init; } = new();
}

public record TranscriptionResult
{
    public IReadOnlyList<TranscriptionSegment> Segments { get; init; } = Array.Empty<TranscriptionSegment>();
    public string FullText => string.Join(" ", Segments.Select(s => s.Text));
    public TimeSpan Duration => Segments.Count > 0
        ? Segments[^1].EndTime - Segments[0].StartTime
        : TimeSpan.Zero;
    public string DetectedLanguage => Segments.Count > 0 ? Segments[0].Language : "unknown";
}
