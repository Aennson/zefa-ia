namespace ZefaIA.Persistence;

public interface IMeetingRepository : IAsyncDisposable
{
    Task InitializeAsync();
    Task<MeetingSession> CreateSessionAsync(MeetingSession session);
    Task<MeetingSession?> GetSessionAsync(long id);
    Task<List<MeetingSession>> GetAllSessionsAsync();
    Task UpdateSessionAsync(MeetingSession session);
    Task DeleteSessionAsync(long id);

    Task AddTranscriptionAsync(TranscriptionEntry entry);
    Task AddTranscriptionBatchAsync(IEnumerable<TranscriptionEntry> entries);
    Task<List<TranscriptionEntry>> GetTranscriptionsAsync(long sessionId);

    Task AddSuggestionAsync(SuggestionEntry entry);
    Task<List<SuggestionEntry>> GetSuggestionsAsync(long sessionId);

    Task<List<MeetingSession>> SearchSessionsAsync(string query);
}
