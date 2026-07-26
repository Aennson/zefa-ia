using ZefaIA.Persistence;

namespace ZefaIA.Persistence.Tests;

public sealed class SqliteMeetingRepositoryTests : IAsyncLifetime, IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteMeetingRepository _repo;

    public SqliteMeetingRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"zefa_test_{Guid.NewGuid():N}.db");
        _repo = new SqliteMeetingRepository(_dbPath);
    }

    public async Task InitializeAsync() => await _repo.InitializeAsync();

    public async Task DisposeAsync()
    {
        await ((IAsyncDisposable)_repo).DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await ((IAsyncLifetime)this).DisposeAsync();
    }

    #region Initialize

    [Fact]
    public async Task InitializeAsync_CreatesDatabase()
    {
        Assert.True(File.Exists(_dbPath));
    }

    [Fact]
    public async Task InitializeAsync_Idempotent_DoesNotThrow()
    {
        await _repo.InitializeAsync();
        await _repo.InitializeAsync();
    }

    #endregion

    #region CreateSession

    [Fact]
    public async Task CreateSessionAsync_AssignsId()
    {
        var session = MakeSession("Test Meeting");
        var created = await _repo.CreateSessionAsync(session);

        Assert.True(created.Id > 0);
    }

    [Fact]
    public async Task CreateSessionAsync_PersistsAllFields()
    {
        var session = new MeetingSession
        {
            Title = "Sprint Review",
            Agenda = "Review deliverables",
            Objective = "Align on progress",
            Participants = "Alice, Bob",
            DetectedLanguage = "pt",
            StartedAt = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc)
        };

        await _repo.CreateSessionAsync(session);
        var loaded = await _repo.GetSessionAsync(session.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Sprint Review", loaded.Title);
        Assert.Equal("Review deliverables", loaded.Agenda);
        Assert.Equal("Align on progress", loaded.Objective);
        Assert.Equal("Alice, Bob", loaded.Participants);
        Assert.Equal("pt", loaded.DetectedLanguage);
        Assert.Equal(session.StartedAt, loaded.StartedAt);
        Assert.Null(loaded.EndedAt);
    }

    [Fact]
    public async Task CreateSessionAsync_WithEndedAt_Persists()
    {
        var ended = new DateTime(2025, 1, 15, 11, 30, 0, DateTimeKind.Utc);
        var session = MakeSession("Ended Meeting");
        session.EndedAt = ended;

        await _repo.CreateSessionAsync(session);
        var loaded = await _repo.GetSessionAsync(session.Id);

        Assert.NotNull(loaded);
        Assert.Equal(ended, loaded!.EndedAt);
    }

    [Fact]
    public async Task CreateSessionAsync_MultipleSessions_IncrementingIds()
    {
        var s1 = await _repo.CreateSessionAsync(MakeSession("Meeting 1"));
        var s2 = await _repo.CreateSessionAsync(MakeSession("Meeting 2"));

        Assert.True(s2.Id > s1.Id);
    }

    #endregion

    #region GetSession

    [Fact]
    public async Task GetSessionAsync_NonExistent_ReturnsNull()
    {
        var result = await _repo.GetSessionAsync(999);
        Assert.Null(result);
    }

    #endregion

    #region GetAllSessions

    [Fact]
    public async Task GetAllSessionsAsync_Empty_ReturnsEmptyList()
    {
        var sessions = await _repo.GetAllSessionsAsync();
        Assert.Empty(sessions);
    }

    [Fact]
    public async Task GetAllSessionsAsync_ReturnsSortedByStartedAtDesc()
    {
        var older = MakeSession("Old");
        older.StartedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = MakeSession("New");
        newer.StartedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        await _repo.CreateSessionAsync(older);
        await _repo.CreateSessionAsync(newer);

        var all = await _repo.GetAllSessionsAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal("New", all[0].Title);
        Assert.Equal("Old", all[1].Title);
    }

    #endregion

    #region UpdateSession

    [Fact]
    public async Task UpdateSessionAsync_UpdatesAllFields()
    {
        var session = await _repo.CreateSessionAsync(MakeSession("Original"));
        session.Title = "Updated";
        session.Agenda = "New agenda";
        session.Objective = "New objective";
        session.Participants = "Charlie";
        session.DetectedLanguage = "en";
        session.EndedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        await _repo.UpdateSessionAsync(session);
        var loaded = await _repo.GetSessionAsync(session.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Updated", loaded!.Title);
        Assert.Equal("New agenda", loaded.Agenda);
        Assert.Equal("New objective", loaded.Objective);
        Assert.Equal("Charlie", loaded.Participants);
        Assert.Equal("en", loaded.DetectedLanguage);
        Assert.NotNull(loaded.EndedAt);
    }

    #endregion

    #region DeleteSession

    [Fact]
    public async Task DeleteSessionAsync_RemovesSession()
    {
        var session = await _repo.CreateSessionAsync(MakeSession("ToDelete"));
        await _repo.DeleteSessionAsync(session.Id);

        var loaded = await _repo.GetSessionAsync(session.Id);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task DeleteSessionAsync_NonExistent_DoesNotThrow()
    {
        await _repo.DeleteSessionAsync(999);
    }

    #endregion

    #region Transcriptions

    [Fact]
    public async Task AddTranscriptionAsync_AssignsId()
    {
        var session = await _repo.CreateSessionAsync(MakeSession("T"));
        var entry = MakeTranscription(session.Id, "Hello");

        await _repo.AddTranscriptionAsync(entry);

        Assert.True(entry.Id > 0);
    }

    [Fact]
    public async Task GetTranscriptionsAsync_ReturnsOrderedByTimestamp()
    {
        var session = await _repo.CreateSessionAsync(MakeSession("T"));

        var e1 = MakeTranscription(session.Id, "First");
        e1.Timestamp = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var e2 = MakeTranscription(session.Id, "Second");
        e2.Timestamp = new DateTime(2025, 1, 1, 10, 1, 0, DateTimeKind.Utc);

        await _repo.AddTranscriptionAsync(e2);
        await _repo.AddTranscriptionAsync(e1);

        var entries = await _repo.GetTranscriptionsAsync(session.Id);
        Assert.Equal(2, entries.Count);
        Assert.Equal("First", entries[0].Text);
        Assert.Equal("Second", entries[1].Text);
    }

    [Fact]
    public async Task AddTranscriptionAsync_PersistsAllFields()
    {
        var session = await _repo.CreateSessionAsync(MakeSession("T"));
        var entry = new TranscriptionEntry
        {
            SessionId = session.Id,
            SpeakerName = "Alice",
            Text = "Important point",
            IsFinal = true,
            Timestamp = new DateTime(2025, 3, 1, 14, 30, 0, DateTimeKind.Utc),
            StartTimeSeconds = 42.5
        };

        await _repo.AddTranscriptionAsync(entry);
        var loaded = (await _repo.GetTranscriptionsAsync(session.Id))[0];

        Assert.Equal("Alice", loaded.SpeakerName);
        Assert.Equal("Important point", loaded.Text);
        Assert.True(loaded.IsFinal);
        Assert.Equal(42.5, loaded.StartTimeSeconds);
    }

    [Fact]
    public async Task AddTranscriptionBatchAsync_InsertsAll()
    {
        var session = await _repo.CreateSessionAsync(MakeSession("T"));
        var entries = new[]
        {
            MakeTranscription(session.Id, "Line 1"),
            MakeTranscription(session.Id, "Line 2"),
            MakeTranscription(session.Id, "Line 3")
        };

        await _repo.AddTranscriptionBatchAsync(entries);

        var loaded = await _repo.GetTranscriptionsAsync(session.Id);
        Assert.Equal(3, loaded.Count);
        Assert.All(entries, e => Assert.True(e.Id > 0));
    }

    [Fact]
    public async Task GetTranscriptionsAsync_EmptySession_ReturnsEmpty()
    {
        var session = await _repo.CreateSessionAsync(MakeSession("T"));
        var entries = await _repo.GetTranscriptionsAsync(session.Id);
        Assert.Empty(entries);
    }

    #endregion

    #region Suggestions

    [Fact]
    public async Task AddSuggestionAsync_AssignsId()
    {
        var session = await _repo.CreateSessionAsync(MakeSession("S"));
        var entry = MakeSuggestion(session.Id, "Try this approach");

        await _repo.AddSuggestionAsync(entry);

        Assert.True(entry.Id > 0);
    }

    [Fact]
    public async Task AddSuggestionAsync_PersistsAllFields()
    {
        var session = await _repo.CreateSessionAsync(MakeSession("S"));
        var entry = new SuggestionEntry
        {
            SessionId = session.Id,
            Text = "Consider rephrasing",
            TriggerReason = "silence",
            TranscriptContext = "Alice: We should...",
            Timestamp = new DateTime(2025, 3, 1, 14, 30, 0, DateTimeKind.Utc),
            InputTokens = 150,
            OutputTokens = 50
        };

        await _repo.AddSuggestionAsync(entry);
        var loaded = (await _repo.GetSuggestionsAsync(session.Id))[0];

        Assert.Equal("Consider rephrasing", loaded.Text);
        Assert.Equal("silence", loaded.TriggerReason);
        Assert.Equal("Alice: We should...", loaded.TranscriptContext);
        Assert.Equal(150, loaded.InputTokens);
        Assert.Equal(50, loaded.OutputTokens);
    }

    [Fact]
    public async Task GetSuggestionsAsync_ReturnsOrderedByTimestamp()
    {
        var session = await _repo.CreateSessionAsync(MakeSession("S"));

        var s1 = MakeSuggestion(session.Id, "First");
        s1.Timestamp = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var s2 = MakeSuggestion(session.Id, "Second");
        s2.Timestamp = new DateTime(2025, 1, 1, 10, 5, 0, DateTimeKind.Utc);

        await _repo.AddSuggestionAsync(s2);
        await _repo.AddSuggestionAsync(s1);

        var loaded = await _repo.GetSuggestionsAsync(session.Id);
        Assert.Equal(2, loaded.Count);
        Assert.Equal("First", loaded[0].Text);
        Assert.Equal("Second", loaded[1].Text);
    }

    [Fact]
    public async Task GetSuggestionsAsync_EmptySession_ReturnsEmpty()
    {
        var session = await _repo.CreateSessionAsync(MakeSession("S"));
        var suggestions = await _repo.GetSuggestionsAsync(session.Id);
        Assert.Empty(suggestions);
    }

    #endregion

    #region Search

    [Fact]
    public async Task SearchSessionsAsync_ByTitle_FindsMatch()
    {
        await _repo.CreateSessionAsync(MakeSession("Daily Standup"));
        await _repo.CreateSessionAsync(MakeSession("Sprint Review"));

        var results = await _repo.SearchSessionsAsync("Standup");
        Assert.Single(results);
        Assert.Equal("Daily Standup", results[0].Title);
    }

    [Fact]
    public async Task SearchSessionsAsync_ByAgenda_FindsMatch()
    {
        var session = MakeSession("Meeting");
        session.Agenda = "Discuss deployment pipeline";
        await _repo.CreateSessionAsync(session);

        var results = await _repo.SearchSessionsAsync("pipeline");
        Assert.Single(results);
    }

    [Fact]
    public async Task SearchSessionsAsync_ByTranscriptionText_FindsMatch()
    {
        var session = await _repo.CreateSessionAsync(MakeSession("Meeting"));
        await _repo.AddTranscriptionAsync(MakeTranscription(session.Id, "We need to refactor the auth module"));

        var results = await _repo.SearchSessionsAsync("refactor");
        Assert.Single(results);
        Assert.Equal(session.Id, results[0].Id);
    }

    [Fact]
    public async Task SearchSessionsAsync_NoMatch_ReturnsEmpty()
    {
        await _repo.CreateSessionAsync(MakeSession("Meeting"));
        var results = await _repo.SearchSessionsAsync("nonexistent");
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchSessionsAsync_MultipleTranscriptions_NoDuplicateSessions()
    {
        var session = await _repo.CreateSessionAsync(MakeSession("Meeting"));
        await _repo.AddTranscriptionAsync(MakeTranscription(session.Id, "keyword here"));
        await _repo.AddTranscriptionAsync(MakeTranscription(session.Id, "keyword again"));

        var results = await _repo.SearchSessionsAsync("keyword");
        Assert.Single(results);
    }

    #endregion

    #region DefaultDbPath

    [Fact]
    public void DefaultDbPath_ContainsZefaIA()
    {
        var path = SqliteMeetingRepository.DefaultDbPath;
        Assert.Contains("ZefaIA", path);
        Assert.EndsWith("meetings.db", path);
    }

    #endregion

    #region MeetingEntities

    [Fact]
    public void MeetingSession_Duration_WithEndedAt_CalculatesCorrectly()
    {
        var session = new MeetingSession
        {
            StartedAt = new DateTime(2025, 1, 1, 10, 0, 0),
            EndedAt = new DateTime(2025, 1, 1, 11, 30, 0)
        };

        Assert.Equal(TimeSpan.FromMinutes(90), session.Duration);
    }

    [Fact]
    public void MeetingSession_Duration_WithoutEndedAt_ReturnsZero()
    {
        var session = new MeetingSession();
        Assert.Equal(TimeSpan.Zero, session.Duration);
    }

    [Fact]
    public void MeetingSession_Defaults_AreCorrect()
    {
        var session = new MeetingSession();
        Assert.Equal("", session.Title);
        Assert.Equal("", session.Agenda);
        Assert.Equal("", session.Objective);
        Assert.Equal("", session.Participants);
        Assert.Equal("auto", session.DetectedLanguage);
        Assert.Null(session.EndedAt);
    }

    [Fact]
    public void TranscriptionEntry_Defaults_AreCorrect()
    {
        var entry = new TranscriptionEntry();
        Assert.Equal("", entry.SpeakerName);
        Assert.Equal("", entry.Text);
        Assert.False(entry.IsFinal);
        Assert.Equal(0, entry.StartTimeSeconds);
    }

    [Fact]
    public void SuggestionEntry_Defaults_AreCorrect()
    {
        var entry = new SuggestionEntry();
        Assert.Equal("", entry.Text);
        Assert.Equal("", entry.TriggerReason);
        Assert.Equal("", entry.TranscriptContext);
        Assert.Equal(0, entry.InputTokens);
        Assert.Equal(0, entry.OutputTokens);
    }

    #endregion

    #region Helpers

    private static MeetingSession MakeSession(string title) => new()
    {
        Title = title,
        StartedAt = DateTime.UtcNow
    };

    private static TranscriptionEntry MakeTranscription(long sessionId, string text) => new()
    {
        SessionId = sessionId,
        SpeakerName = "Speaker",
        Text = text,
        IsFinal = true,
        Timestamp = DateTime.UtcNow,
        StartTimeSeconds = 0
    };

    private static SuggestionEntry MakeSuggestion(long sessionId, string text) => new()
    {
        SessionId = sessionId,
        Text = text,
        TriggerReason = "test",
        TranscriptContext = "context",
        Timestamp = DateTime.UtcNow,
        InputTokens = 100,
        OutputTokens = 30
    };

    #endregion
}
