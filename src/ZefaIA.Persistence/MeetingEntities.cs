namespace ZefaIA.Persistence;

public class MeetingSession
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Agenda { get; set; } = "";
    public string Objective { get; set; } = "";
    public string Participants { get; set; } = "";
    public string DetectedLanguage { get; set; } = "auto";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public TimeSpan Duration => EndedAt.HasValue ? EndedAt.Value - StartedAt : TimeSpan.Zero;

    public List<TranscriptionEntry> Transcriptions { get; set; } = new();
    public List<SuggestionEntry> Suggestions { get; set; } = new();
}

public class TranscriptionEntry
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public string SpeakerName { get; set; } = "";
    public string Text { get; set; } = "";
    public bool IsFinal { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double StartTimeSeconds { get; set; }
}

public class SuggestionEntry
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public string Text { get; set; } = "";
    public string TriggerReason { get; set; } = "";
    public string TranscriptContext { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}
