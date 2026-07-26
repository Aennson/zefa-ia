using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZefaIA.Persistence;

public sealed class SessionExporter
{
    private readonly IMeetingRepository _repository;

    public SessionExporter(IMeetingRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> ExportToTextAsync(long sessionId)
    {
        var (session, transcriptions, suggestions) = await LoadAsync(sessionId);
        return FormatText(session, transcriptions, suggestions);
    }

    public async Task<string> ExportToJsonAsync(long sessionId)
    {
        var (session, transcriptions, suggestions) = await LoadAsync(sessionId);
        return FormatJson(session, transcriptions, suggestions);
    }

    public async Task ExportToFileAsync(long sessionId, string filePath, ExportFormat format)
    {
        var content = format == ExportFormat.Json
            ? await ExportToJsonAsync(sessionId)
            : await ExportToTextAsync(sessionId);

        await File.WriteAllTextAsync(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static string SuggestFileName(MeetingSession session, ExportFormat format)
    {
        var title = string.IsNullOrWhiteSpace(session.Title) ? $"reuniao-{session.Id}" : session.Title;
        var safe = new string(title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c).ToArray());
        var extension = format == ExportFormat.Json ? "json" : "txt";
        return $"{safe}_{session.StartedAt:yyyy-MM-dd}.{extension}";
    }

    private async Task<(MeetingSession, List<TranscriptionEntry>, List<SuggestionEntry>)> LoadAsync(long sessionId)
    {
        var session = await _repository.GetSessionAsync(sessionId)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        var transcriptions = await _repository.GetTranscriptionsAsync(sessionId);
        var suggestions = await _repository.GetSuggestionsAsync(sessionId);

        return (session, transcriptions, suggestions);
    }

    internal static string FormatText(
        MeetingSession session,
        List<TranscriptionEntry> transcriptions,
        List<SuggestionEntry> suggestions)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Reuniao: {(string.IsNullOrWhiteSpace(session.Title) ? $"#{session.Id}" : session.Title)}");
        sb.AppendLine($"Data: {session.StartedAt:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"Duracao: {FormatDuration(session.Duration)}");

        if (!string.IsNullOrWhiteSpace(session.Participants))
            sb.AppendLine($"Participantes: {session.Participants}");
        if (!string.IsNullOrWhiteSpace(session.Agenda))
            sb.AppendLine($"Agenda: {session.Agenda}");
        if (!string.IsNullOrWhiteSpace(session.Objective))
            sb.AppendLine($"Objetivo: {session.Objective}");

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var item in Interleave(transcriptions, suggestions))
        {
            if (item.Transcription is { } t)
            {
                sb.AppendLine($"[{t.Timestamp:HH:mm:ss}] [{t.SpeakerName}] {t.Text}");
            }
            else if (item.Suggestion is { } s)
            {
                sb.AppendLine();
                sb.AppendLine($"  >> Sugestao ({s.Timestamp:HH:mm:ss}): {s.Text}");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    internal static string FormatJson(
        MeetingSession session,
        List<TranscriptionEntry> transcriptions,
        List<SuggestionEntry> suggestions)
    {
        var export = new SessionExport
        {
            Id = session.Id,
            Title = session.Title,
            Agenda = session.Agenda,
            Objective = session.Objective,
            Participants = session.Participants,
            DetectedLanguage = session.DetectedLanguage,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            DurationSeconds = session.Duration.TotalSeconds,
            Transcriptions = transcriptions.Select(t => new TranscriptionExport
            {
                SpeakerName = t.SpeakerName,
                Text = t.Text,
                Timestamp = t.Timestamp,
                StartTimeSeconds = t.StartTimeSeconds
            }).ToList(),
            Suggestions = suggestions.Select(s => new SuggestionExport
            {
                Text = s.Text,
                TriggerReason = s.TriggerReason,
                TranscriptContext = s.TranscriptContext,
                Timestamp = s.Timestamp,
                InputTokens = s.InputTokens,
                OutputTokens = s.OutputTokens
            }).ToList()
        };

        return JsonSerializer.Serialize(export, ExportJsonContext.Default.SessionExport);
    }

    internal static IEnumerable<TimelineItem> Interleave(
        List<TranscriptionEntry> transcriptions,
        List<SuggestionEntry> suggestions)
    {
        var ordered = transcriptions.OrderBy(t => t.Timestamp).ToList();
        var pending = suggestions.OrderBy(s => s.Timestamp).ToList();

        var sugIdx = 0;
        foreach (var t in ordered)
        {
            while (sugIdx < pending.Count && pending[sugIdx].Timestamp <= t.Timestamp)
            {
                yield return new TimelineItem(null, pending[sugIdx]);
                sugIdx++;
            }

            yield return new TimelineItem(t, null);
        }

        while (sugIdx < pending.Count)
        {
            yield return new TimelineItem(null, pending[sugIdx]);
            sugIdx++;
        }
    }

    internal static string FormatDuration(TimeSpan duration)
    {
        if (duration == TimeSpan.Zero) return "em andamento";
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}min";
        return $"{(int)duration.TotalMinutes} min";
    }
}

public enum ExportFormat
{
    Text,
    Json
}

public readonly record struct TimelineItem(TranscriptionEntry? Transcription, SuggestionEntry? Suggestion);

public class SessionExport
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Agenda { get; set; } = "";
    public string Objective { get; set; } = "";
    public string Participants { get; set; } = "";
    public string DetectedLanguage { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public double DurationSeconds { get; set; }
    public List<TranscriptionExport> Transcriptions { get; set; } = new();
    public List<SuggestionExport> Suggestions { get; set; } = new();
}

public class TranscriptionExport
{
    public string SpeakerName { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public double StartTimeSeconds { get; set; }
}

public class SuggestionExport
{
    public string Text { get; set; } = "";
    public string TriggerReason { get; set; } = "";
    public string TranscriptContext { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SessionExport))]
internal partial class ExportJsonContext : JsonSerializerContext
{
}
