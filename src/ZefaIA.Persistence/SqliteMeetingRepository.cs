using System.Globalization;
using Microsoft.Data.Sqlite;

namespace ZefaIA.Persistence;

public sealed class SqliteMeetingRepository : IMeetingRepository
{
    private readonly string _connectionString;
    private bool _disposed;

    public SqliteMeetingRepository(string dbPath)
    {
        // Foreign Keys=True is required for the ON DELETE CASCADE in the schema to
        // fire; SQLite ignores cascade rules when the pragma is off.
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            ForeignKeys = true
        }.ToString();
    }

    public static string DefaultDbPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ZefaIA", "meetings.db");

    public async Task InitializeAsync()
    {
        var dir = Path.GetDirectoryName(GetDbPath());
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = Schema;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<MeetingSession> CreateSessionAsync(MeetingSession session)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO MeetingSessions (Title, Agenda, Objective, Participants, DetectedLanguage, StartedAt, EndedAt)
            VALUES (@title, @agenda, @objective, @participants, @lang, @started, @ended);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@title", session.Title);
        cmd.Parameters.AddWithValue("@agenda", session.Agenda);
        cmd.Parameters.AddWithValue("@objective", session.Objective);
        cmd.Parameters.AddWithValue("@participants", session.Participants);
        cmd.Parameters.AddWithValue("@lang", session.DetectedLanguage);
        cmd.Parameters.AddWithValue("@started", session.StartedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@ended", session.EndedAt?.ToString("o") ?? (object)DBNull.Value);

        session.Id = (long)(await cmd.ExecuteScalarAsync())!;
        return session;
    }

    public async Task<MeetingSession?> GetSessionAsync(long id)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM MeetingSessions WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return ReadSession(reader);
    }

    public async Task<List<MeetingSession>> GetAllSessionsAsync()
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM MeetingSessions ORDER BY StartedAt DESC";

        var sessions = new List<MeetingSession>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            sessions.Add(ReadSession(reader));

        return sessions;
    }

    public async Task UpdateSessionAsync(MeetingSession session)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE MeetingSessions SET Title=@title, Agenda=@agenda, Objective=@objective,
            Participants=@participants, DetectedLanguage=@lang, EndedAt=@ended WHERE Id=@id
            """;
        cmd.Parameters.AddWithValue("@id", session.Id);
        cmd.Parameters.AddWithValue("@title", session.Title);
        cmd.Parameters.AddWithValue("@agenda", session.Agenda);
        cmd.Parameters.AddWithValue("@objective", session.Objective);
        cmd.Parameters.AddWithValue("@participants", session.Participants);
        cmd.Parameters.AddWithValue("@lang", session.DetectedLanguage);
        cmd.Parameters.AddWithValue("@ended", session.EndedAt?.ToString("o") ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteSessionAsync(long id)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM MeetingSessions WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task AddTranscriptionAsync(TranscriptionEntry entry)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await InsertTranscription(conn, entry);
    }

    public async Task AddTranscriptionBatchAsync(IEnumerable<TranscriptionEntry> entries)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var tx = await conn.BeginTransactionAsync();
        foreach (var entry in entries)
            await InsertTranscription(conn, entry);
        await tx.CommitAsync();
    }

    public async Task<List<TranscriptionEntry>> GetTranscriptionsAsync(long sessionId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM TranscriptionEntries WHERE SessionId=@sid ORDER BY Timestamp";
        cmd.Parameters.AddWithValue("@sid", sessionId);

        var entries = new List<TranscriptionEntry>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new TranscriptionEntry
            {
                Id = reader.GetInt64(0),
                SessionId = reader.GetInt64(1),
                SpeakerName = reader.GetString(2),
                Text = reader.GetString(3),
                IsFinal = reader.GetBoolean(4),
                Timestamp = ParseTimestamp(reader.GetString(5)),
                StartTimeSeconds = reader.GetDouble(6)
            });
        }

        return entries;
    }

    public async Task AddSuggestionAsync(SuggestionEntry entry)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO SuggestionEntries (SessionId, Text, TriggerReason, TranscriptContext, Timestamp, InputTokens, OutputTokens)
            VALUES (@sid, @text, @reason, @context, @ts, @input, @output);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@sid", entry.SessionId);
        cmd.Parameters.AddWithValue("@text", entry.Text);
        cmd.Parameters.AddWithValue("@reason", entry.TriggerReason);
        cmd.Parameters.AddWithValue("@context", entry.TranscriptContext);
        cmd.Parameters.AddWithValue("@ts", entry.Timestamp.ToString("o"));
        cmd.Parameters.AddWithValue("@input", entry.InputTokens);
        cmd.Parameters.AddWithValue("@output", entry.OutputTokens);

        entry.Id = (long)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task<List<SuggestionEntry>> GetSuggestionsAsync(long sessionId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM SuggestionEntries WHERE SessionId=@sid ORDER BY Timestamp";
        cmd.Parameters.AddWithValue("@sid", sessionId);

        var entries = new List<SuggestionEntry>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new SuggestionEntry
            {
                Id = reader.GetInt64(0),
                SessionId = reader.GetInt64(1),
                Text = reader.GetString(2),
                TriggerReason = reader.GetString(3),
                TranscriptContext = reader.GetString(4),
                Timestamp = ParseTimestamp(reader.GetString(5)),
                InputTokens = reader.GetInt32(6),
                OutputTokens = reader.GetInt32(7)
            });
        }

        return entries;
    }

    public async Task<List<MeetingSession>> SearchSessionsAsync(string query)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT s.* FROM MeetingSessions s
            LEFT JOIN TranscriptionEntries t ON t.SessionId = s.Id
            WHERE s.Title LIKE @q OR s.Agenda LIKE @q OR t.Text LIKE @q
            ORDER BY s.StartedAt DESC
            """;
        cmd.Parameters.AddWithValue("@q", $"%{query}%");

        var sessions = new List<MeetingSession>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            sessions.Add(ReadSession(reader));

        return sessions;
    }

    private static async Task InsertTranscription(SqliteConnection conn, TranscriptionEntry entry)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO TranscriptionEntries (SessionId, SpeakerName, Text, IsFinal, Timestamp, StartTimeSeconds)
            VALUES (@sid, @speaker, @text, @final, @ts, @start);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@sid", entry.SessionId);
        cmd.Parameters.AddWithValue("@speaker", entry.SpeakerName);
        cmd.Parameters.AddWithValue("@text", entry.Text);
        cmd.Parameters.AddWithValue("@final", entry.IsFinal);
        cmd.Parameters.AddWithValue("@ts", entry.Timestamp.ToString("o"));
        cmd.Parameters.AddWithValue("@start", entry.StartTimeSeconds);

        entry.Id = (long)(await cmd.ExecuteScalarAsync())!;
    }

    private static MeetingSession ReadSession(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Title = reader.GetString(1),
        Agenda = reader.GetString(2),
        Objective = reader.GetString(3),
        Participants = reader.GetString(4),
        DetectedLanguage = reader.GetString(5),
        StartedAt = ParseTimestamp(reader.GetString(6)),
        EndedAt = reader.IsDBNull(7) ? null : ParseTimestamp(reader.GetString(7))
    };

    /// <summary>
    /// Timestamps are written with "o", which carries the offset. A plain DateTime.Parse
    /// would shift a UTC value into local time and return Kind=Local, so a session stored
    /// at 10:00Z reads back as 07:00-03:00 — same instant, but every downstream consumer
    /// that formats or re-serializes it produces the wrong string.
    /// </summary>
    private static DateTime ParseTimestamp(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private SqliteConnection CreateConnection() => new(_connectionString);

    private string GetDbPath()
    {
        var builder = new SqliteConnectionStringBuilder(_connectionString);
        return builder.DataSource;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        // Microsoft.Data.Sqlite pools connections, so disposing each SqliteConnection
        // returns it to the pool rather than closing the underlying file handle. The
        // handle keeps the .db locked, which blocks deleting, moving, or backing it up
        // after the repository is gone. Draining the pool releases it for good.
        using (var conn = CreateConnection())
        {
            SqliteConnection.ClearPool(conn);
        }

        return ValueTask.CompletedTask;
    }

    private const string Schema = """
        CREATE TABLE IF NOT EXISTS MeetingSessions (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Title TEXT NOT NULL DEFAULT '',
            Agenda TEXT NOT NULL DEFAULT '',
            Objective TEXT NOT NULL DEFAULT '',
            Participants TEXT NOT NULL DEFAULT '',
            DetectedLanguage TEXT NOT NULL DEFAULT 'auto',
            StartedAt TEXT NOT NULL,
            EndedAt TEXT
        );

        CREATE TABLE IF NOT EXISTS TranscriptionEntries (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SessionId INTEGER NOT NULL,
            SpeakerName TEXT NOT NULL DEFAULT '',
            Text TEXT NOT NULL DEFAULT '',
            IsFinal INTEGER NOT NULL DEFAULT 0,
            Timestamp TEXT NOT NULL,
            StartTimeSeconds REAL NOT NULL DEFAULT 0,
            FOREIGN KEY (SessionId) REFERENCES MeetingSessions(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS SuggestionEntries (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SessionId INTEGER NOT NULL,
            Text TEXT NOT NULL DEFAULT '',
            TriggerReason TEXT NOT NULL DEFAULT '',
            TranscriptContext TEXT NOT NULL DEFAULT '',
            Timestamp TEXT NOT NULL,
            InputTokens INTEGER NOT NULL DEFAULT 0,
            OutputTokens INTEGER NOT NULL DEFAULT 0,
            FOREIGN KEY (SessionId) REFERENCES MeetingSessions(Id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS idx_transcriptions_session ON TranscriptionEntries(SessionId);
        CREATE INDEX IF NOT EXISTS idx_suggestions_session ON SuggestionEntries(SessionId);
        """;
}
