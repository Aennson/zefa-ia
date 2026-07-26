using Xunit;
using System.Text.Json;
using ZefaIA.Persistence;

namespace ZefaIA.Persistence.Tests;

public sealed class SessionExporterTests : IAsyncLifetime, IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteMeetingRepository _repo;
    private SessionExporter _exporter = null!;

    public SessionExporterTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"zefa_exp_{Guid.NewGuid():N}.db");
        _repo = new SqliteMeetingRepository(_dbPath);
    }

    public async Task InitializeAsync()
    {
        await _repo.InitializeAsync();
        _exporter = new SessionExporter(_repo);
    }

    public async Task DisposeAsync()
    {
        await _repo.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    async ValueTask IAsyncDisposable.DisposeAsync() => await ((IAsyncLifetime)this).DisposeAsync();

    #region TXT Formatting

    [Fact]
    public void FormatText_IncludesHeaderFields()
    {
        var session = new MeetingSession
        {
            Id = 1,
            Title = "Sync Semanal",
            Participants = "Alice, Bob",
            Agenda = "Orcamento Q3",
            Objective = "Aprovar budget",
            StartedAt = new DateTime(2026, 7, 25, 14, 0, 0),
            EndedAt = new DateTime(2026, 7, 25, 14, 45, 0)
        };

        var txt = SessionExporter.FormatText(session, new(), new());

        Assert.Contains("Reuniao: Sync Semanal", txt);
        Assert.Contains("Data: 2026-07-25 14:00", txt);
        Assert.Contains("Duracao: 45 min", txt);
        Assert.Contains("Participantes: Alice, Bob", txt);
        Assert.Contains("Agenda: Orcamento Q3", txt);
        Assert.Contains("Objetivo: Aprovar budget", txt);
    }

    [Fact]
    public void FormatText_EmptyOptionalFields_OmitsLines()
    {
        var session = new MeetingSession { Id = 1, Title = "Test" };

        var txt = SessionExporter.FormatText(session, new(), new());

        Assert.DoesNotContain("Participantes:", txt);
        Assert.DoesNotContain("Agenda:", txt);
        Assert.DoesNotContain("Objetivo:", txt);
    }

    [Fact]
    public void FormatText_EmptyTitle_UsesSessionId()
    {
        var session = new MeetingSession { Id = 42, Title = "" };

        var txt = SessionExporter.FormatText(session, new(), new());

        Assert.Contains("Reuniao: #42", txt);
    }

    [Fact]
    public void FormatText_FormatsTranscriptionLines()
    {
        var session = new MeetingSession { Id = 1, Title = "Test" };
        var transcriptions = new List<TranscriptionEntry>
        {
            MakeTranscription("Interlocutor", "Ola, vamos comecar?", new DateTime(2026, 7, 25, 14, 0, 5)),
            MakeTranscription("Eu", "Vamos sim.", new DateTime(2026, 7, 25, 14, 0, 8))
        };

        var txt = SessionExporter.FormatText(session, transcriptions, new());

        Assert.Contains("[14:00:05] [Interlocutor] Ola, vamos comecar?", txt);
        Assert.Contains("[14:00:08] [Eu] Vamos sim.", txt);
    }

    [Fact]
    public void FormatText_InlinesSuggestionsBetweenTranscriptions()
    {
        var session = new MeetingSession { Id = 1, Title = "Test" };
        var transcriptions = new List<TranscriptionEntry>
        {
            MakeTranscription("Eu", "Primeiro", new DateTime(2026, 7, 25, 14, 0, 0)),
            MakeTranscription("Eu", "Segundo", new DateTime(2026, 7, 25, 14, 0, 30))
        };
        var suggestions = new List<SuggestionEntry>
        {
            MakeSuggestion("Mencione a meta", new DateTime(2026, 7, 25, 14, 0, 15))
        };

        var txt = SessionExporter.FormatText(session, transcriptions, suggestions);

        var firstIdx = txt.IndexOf("Primeiro", StringComparison.Ordinal);
        var sugIdx = txt.IndexOf("Mencione a meta", StringComparison.Ordinal);
        var secondIdx = txt.IndexOf("Segundo", StringComparison.Ordinal);

        Assert.True(firstIdx < sugIdx, "suggestion should come after first transcription");
        Assert.True(sugIdx < secondIdx, "suggestion should come before second transcription");
        Assert.Contains(">> Sugestao (14:00:15): Mencione a meta", txt);
    }

    [Fact]
    public void FormatText_TrailingSuggestions_AppearAtEnd()
    {
        var session = new MeetingSession { Id = 1, Title = "Test" };
        var transcriptions = new List<TranscriptionEntry>
        {
            MakeTranscription("Eu", "Unica linha", new DateTime(2026, 7, 25, 14, 0, 0))
        };
        var suggestions = new List<SuggestionEntry>
        {
            MakeSuggestion("Depois do fim", new DateTime(2026, 7, 25, 14, 5, 0))
        };

        var txt = SessionExporter.FormatText(session, transcriptions, suggestions);

        Assert.True(txt.IndexOf("Unica linha", StringComparison.Ordinal)
                  < txt.IndexOf("Depois do fim", StringComparison.Ordinal));
    }

    #endregion

    #region Duration

    [Fact]
    public void FormatDuration_Zero_ReturnsEmAndamento()
    {
        Assert.Equal("em andamento", SessionExporter.FormatDuration(TimeSpan.Zero));
    }

    [Fact]
    public void FormatDuration_UnderOneHour_ReturnsMinutes()
    {
        Assert.Equal("45 min", SessionExporter.FormatDuration(TimeSpan.FromMinutes(45)));
    }

    [Fact]
    public void FormatDuration_OverOneHour_ReturnsHoursAndMinutes()
    {
        Assert.Equal("2h 15min", SessionExporter.FormatDuration(TimeSpan.FromMinutes(135)));
    }

    #endregion

    #region JSON

    [Fact]
    public void FormatJson_ProducesParseableJson()
    {
        var session = new MeetingSession
        {
            Id = 7,
            Title = "JSON Test",
            Agenda = "agenda",
            Objective = "objective",
            Participants = "Alice",
            DetectedLanguage = "pt",
            StartedAt = new DateTime(2026, 7, 25, 10, 0, 0, DateTimeKind.Utc),
            EndedAt = new DateTime(2026, 7, 25, 11, 0, 0, DateTimeKind.Utc)
        };

        var json = SessionExporter.FormatJson(session, new(), new());

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(7, root.GetProperty("Id").GetInt64());
        Assert.Equal("JSON Test", root.GetProperty("Title").GetString());
        Assert.Equal("pt", root.GetProperty("DetectedLanguage").GetString());
        Assert.Equal(3600, root.GetProperty("DurationSeconds").GetDouble());
    }

    [Fact]
    public void FormatJson_IncludesTranscriptionsAndSuggestions()
    {
        var session = new MeetingSession { Id = 1, Title = "Test" };
        var transcriptions = new List<TranscriptionEntry>
        {
            MakeTranscription("Eu", "Linha um", new DateTime(2026, 7, 25, 14, 0, 0)),
            MakeTranscription("Interlocutor", "Linha dois", new DateTime(2026, 7, 25, 14, 0, 10))
        };
        var suggestions = new List<SuggestionEntry>
        {
            MakeSuggestion("Sugestao aqui", new DateTime(2026, 7, 25, 14, 0, 5))
        };

        var json = SessionExporter.FormatJson(session, transcriptions, suggestions);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var txArray = root.GetProperty("Transcriptions");
        Assert.Equal(2, txArray.GetArrayLength());
        Assert.Equal("Eu", txArray[0].GetProperty("SpeakerName").GetString());
        Assert.Equal("Linha um", txArray[0].GetProperty("Text").GetString());

        var sugArray = root.GetProperty("Suggestions");
        Assert.Equal(1, sugArray.GetArrayLength());
        Assert.Equal("Sugestao aqui", sugArray[0].GetProperty("Text").GetString());
        Assert.Equal(100, sugArray[0].GetProperty("InputTokens").GetInt32());
    }

    [Fact]
    public void FormatJson_NullEndedAt_SerializesAsNull()
    {
        var session = new MeetingSession { Id = 1, Title = "Test", EndedAt = null };

        var json = SessionExporter.FormatJson(session, new(), new());

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("EndedAt").ValueKind);
    }

    #endregion

    #region Interleave

    [Fact]
    public void Interleave_OrdersByTimestamp()
    {
        var transcriptions = new List<TranscriptionEntry>
        {
            MakeTranscription("Eu", "T2", new DateTime(2026, 7, 25, 14, 0, 20)),
            MakeTranscription("Eu", "T1", new DateTime(2026, 7, 25, 14, 0, 0))
        };
        var suggestions = new List<SuggestionEntry>
        {
            MakeSuggestion("S1", new DateTime(2026, 7, 25, 14, 0, 10))
        };

        var items = SessionExporter.Interleave(transcriptions, suggestions).ToList();

        Assert.Equal(3, items.Count);
        Assert.Equal("T1", items[0].Transcription!.Text);
        Assert.Equal("S1", items[1].Suggestion!.Text);
        Assert.Equal("T2", items[2].Transcription!.Text);
    }

    [Fact]
    public void Interleave_NoSuggestions_ReturnsOnlyTranscriptions()
    {
        var transcriptions = new List<TranscriptionEntry>
        {
            MakeTranscription("Eu", "Only", new DateTime(2026, 7, 25, 14, 0, 0))
        };

        var items = SessionExporter.Interleave(transcriptions, new()).ToList();

        Assert.Single(items);
        Assert.NotNull(items[0].Transcription);
        Assert.Null(items[0].Suggestion);
    }

    [Fact]
    public void Interleave_NoTranscriptions_ReturnsOnlySuggestions()
    {
        var suggestions = new List<SuggestionEntry>
        {
            MakeSuggestion("Alone", new DateTime(2026, 7, 25, 14, 0, 0))
        };

        var items = SessionExporter.Interleave(new(), suggestions).ToList();

        Assert.Single(items);
        Assert.Null(items[0].Transcription);
        Assert.NotNull(items[0].Suggestion);
    }

    [Fact]
    public void Interleave_BothEmpty_ReturnsEmpty()
    {
        Assert.Empty(SessionExporter.Interleave(new(), new()));
    }

    #endregion

    #region SuggestFileName

    [Fact]
    public void SuggestFileName_Text_UsesTxtExtension()
    {
        var session = new MeetingSession
        {
            Id = 1,
            Title = "Sync Semanal",
            StartedAt = new DateTime(2026, 7, 25)
        };

        var name = SessionExporter.SuggestFileName(session, ExportFormat.Text);

        Assert.Equal("Sync Semanal_2026-07-25.txt", name);
    }

    [Fact]
    public void SuggestFileName_Json_UsesJsonExtension()
    {
        var session = new MeetingSession
        {
            Id = 1,
            Title = "Review",
            StartedAt = new DateTime(2026, 7, 25)
        };

        var name = SessionExporter.SuggestFileName(session, ExportFormat.Json);

        Assert.Equal("Review_2026-07-25.json", name);
    }

    [Fact]
    public void SuggestFileName_EmptyTitle_UsesSessionId()
    {
        var session = new MeetingSession
        {
            Id = 99,
            Title = "",
            StartedAt = new DateTime(2026, 7, 25)
        };

        var name = SessionExporter.SuggestFileName(session, ExportFormat.Text);

        Assert.Equal("reuniao-99_2026-07-25.txt", name);
    }

    [Fact]
    public void SuggestFileName_InvalidChars_AreReplaced()
    {
        var session = new MeetingSession
        {
            Id = 1,
            Title = "Q3: budget/review",
            StartedAt = new DateTime(2026, 7, 25)
        };

        var name = SessionExporter.SuggestFileName(session, ExportFormat.Text);

        Assert.DoesNotContain('/', name);
        Assert.Equal(-1, name.IndexOfAny(Path.GetInvalidFileNameChars()));
    }

    #endregion

    #region End-to-end

    [Fact]
    public async Task ExportToTextAsync_LoadsFromRepository()
    {
        var session = await _repo.CreateSessionAsync(new MeetingSession
        {
            Title = "Persisted Meeting",
            StartedAt = new DateTime(2026, 7, 25, 9, 0, 0, DateTimeKind.Utc)
        });
        await _repo.AddTranscriptionAsync(new TranscriptionEntry
        {
            SessionId = session.Id,
            SpeakerName = "Eu",
            Text = "Conteudo salvo",
            IsFinal = true,
            Timestamp = new DateTime(2026, 7, 25, 9, 0, 5, DateTimeKind.Utc)
        });

        var txt = await _exporter.ExportToTextAsync(session.Id);

        Assert.Contains("Reuniao: Persisted Meeting", txt);
        Assert.Contains("Conteudo salvo", txt);
    }

    [Fact]
    public async Task ExportToJsonAsync_LoadsFromRepository()
    {
        var session = await _repo.CreateSessionAsync(new MeetingSession { Title = "JSON Meeting" });
        await _repo.AddSuggestionAsync(new SuggestionEntry
        {
            SessionId = session.Id,
            Text = "Sugestao persistida",
            TriggerReason = "silence",
            Timestamp = DateTime.UtcNow
        });

        var json = await _exporter.ExportToJsonAsync(session.Id);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("JSON Meeting", doc.RootElement.GetProperty("Title").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("Suggestions").GetArrayLength());
    }

    [Fact]
    public async Task ExportToFileAsync_WritesFile()
    {
        var session = await _repo.CreateSessionAsync(new MeetingSession { Title = "File Export" });
        var path = Path.Combine(Path.GetTempPath(), $"zefa_export_{Guid.NewGuid():N}.txt");

        try
        {
            await _exporter.ExportToFileAsync(session.Id, path, ExportFormat.Text);

            Assert.True(File.Exists(path));
            var content = await File.ReadAllTextAsync(path);
            Assert.Contains("Reuniao: File Export", content);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportToTextAsync_MissingSession_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _exporter.ExportToTextAsync(9999));
    }

    #endregion

    #region Helpers

    private static TranscriptionEntry MakeTranscription(string speaker, string text, DateTime timestamp) => new()
    {
        SpeakerName = speaker,
        Text = text,
        IsFinal = true,
        Timestamp = timestamp
    };

    private static SuggestionEntry MakeSuggestion(string text, DateTime timestamp) => new()
    {
        Text = text,
        TriggerReason = "silence",
        TranscriptContext = "context",
        Timestamp = timestamp,
        InputTokens = 100,
        OutputTokens = 40
    };

    #endregion
}
