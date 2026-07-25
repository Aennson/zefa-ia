namespace ZefaIA.Core.Models;

public enum AudioSourceType
{
    Microphone,
    Loopback
}

public record AudioChunkEventArgs(
    byte[] PcmData,
    int SampleRate,
    TimeSpan Timestamp,
    AudioSourceType Source
);

public record AudioSourceStateEventArgs(
    AudioSourceType Source,
    AudioSourceState State,
    string? ErrorMessage = null
);

public enum AudioSourceState
{
    Idle,
    Starting,
    Capturing,
    Stopping,
    Stopped,
    Error
}
