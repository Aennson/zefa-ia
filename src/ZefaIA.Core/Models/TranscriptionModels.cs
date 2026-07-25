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
