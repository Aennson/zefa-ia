using Xunit;
using System.Reactive.Subjects;
using ZefaIA.Core.Models;
using ZefaIA.Persistence;

namespace ZefaIA.Persistence.Tests;

public sealed class MeetingRecorderTests : IAsyncLifetime, IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteMeetingRepository _repo;
    private MeetingRecorder _recorder = null!;

    public MeetingRecorderTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"zefa_rec_{Guid.NewGuid():N}.db");
        _repo = new SqliteMeetingRepository(_dbPath);
    }

    public async Task InitializeAsync()
    {
        await _repo.InitializeAsync();
        _recorder = new MeetingRecorder(_repo, batchSize: 3, batchInterval: TimeSpan.FromHours(1));
    }

    public async Task DisposeAsync()
    {
        await ((IAsyncDisposable)_recorder).DisposeAsync();
        await _repo.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await ((IAsyncLifetime)this).DisposeAsync();
    }

    #region Start/Stop

    [Fact]
    public async Task StartAsync_CreatesSession()
    {
        var session = await _recorder.StartAsync(new MeetingSession { Title = "Test" });

        Assert.True(session.Id > 0);
        Assert.True(_recorder.IsRecording);
        Assert.Equal(session.Id, _recorder.CurrentSessionId);
    }

    [Fact]
    public async Task StartAsync_AlreadyRecording_Throws()
    {
        await _recorder.StartAsync(new MeetingSession { Title = "Test" });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _recorder.StartAsync(new MeetingSession { Title = "Second" }));
    }

    [Fact]
    public async Task StopAsync_SetsEndedAt()
    {
        await _recorder.StartAsync(new MeetingSession { Title = "Test" });
        var stopped = await _recorder.StopAsync();

        Assert.NotNull(stopped);
        Assert.NotNull(stopped!.EndedAt);
        Assert.False(_recorder.IsRecording);
        Assert.Null(_recorder.CurrentSessionId);
    }

    [Fact]
    public async Task StopAsync_WhenNotRecording_ReturnsNull()
    {
        var result = await _recorder.StopAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task StartAsync_FiresRecordingStateChanged()
    {
        var states = new List<bool>();
        _recorder.OnRecordingStateChanged += s => states.Add(s);

        await _recorder.StartAsync(new MeetingSession { Title = "Test" });
        await _recorder.StopAsync();

        Assert.Equal(2, states.Count);
        Assert.True(states[0]);
        Assert.False(states[1]);
    }

    #endregion

    #region Transcription Recording

    [Fact]
    public async Task SubscribeToTranscriptions_RecordsFinalSegments()
    {
        var subject = new Subject<TranscriptionSegmentEventArgs>();
        var session = await _recorder.StartAsync(new MeetingSession { Title = "Test" });
        _recorder.SubscribeToTranscriptions(subject);

        EmitSegment(subject, "Hello world", isFinal: true);
        EmitSegment(subject, "partial...", isFinal: false);
        EmitSegment(subject, "Goodbye", isFinal: true);

        await Task.Delay(50);
        await _recorder.StopAsync();

        var entries = await _repo.GetTranscriptionsAsync(session.Id);
        Assert.Equal(2, entries.Count);
        Assert.Equal("Hello world", entries[0].Text);
        Assert.Equal("Goodbye", entries[1].Text);
    }

    [Fact]
    public async Task SubscribeToTranscriptions_SetsSpeakerBySource()
    {
        var subject = new Subject<TranscriptionSegmentEventArgs>();
        var session = await _recorder.StartAsync(new MeetingSession { Title = "Test" });
        _recorder.SubscribeToTranscriptions(subject);

        EmitSegment(subject, "My words", isFinal: true, source: AudioSourceType.Microphone);
        EmitSegment(subject, "Their words", isFinal: true, source: AudioSourceType.Loopback);

        await Task.Delay(50);
        await _recorder.StopAsync();

        var entries = await _repo.GetTranscriptionsAsync(session.Id);
        Assert.Equal("Eu", entries[0].SpeakerName);
        Assert.Equal("Interlocutor", entries[1].SpeakerName);
    }

    [Fact]
    public async Task BatchFlush_TriggersAtBatchSize()
    {
        var subject = new Subject<TranscriptionSegmentEventArgs>();
        var session = await _recorder.StartAsync(new MeetingSession { Title = "Test" });
        _recorder.SubscribeToTranscriptions(subject);

        EmitSegment(subject, "One", isFinal: true);
        EmitSegment(subject, "Two", isFinal: true);
        EmitSegment(subject, "Three", isFinal: true);

        await Task.Delay(100);

        var entries = await _repo.GetTranscriptionsAsync(session.Id);
        Assert.Equal(3, entries.Count);
        Assert.Equal(1, _recorder.Metrics.BatchesWritten);

        await _recorder.StopAsync();
    }

    [Fact]
    public async Task StopAsync_FlushesRemainingBuffer()
    {
        var subject = new Subject<TranscriptionSegmentEventArgs>();
        var session = await _recorder.StartAsync(new MeetingSession { Title = "Test" });
        _recorder.SubscribeToTranscriptions(subject);

        EmitSegment(subject, "Partial batch", isFinal: true);

        await _recorder.StopAsync();

        var entries = await _repo.GetTranscriptionsAsync(session.Id);
        Assert.Single(entries);
        Assert.Equal("Partial batch", entries[0].Text);
    }

    [Fact]
    public async Task Metrics_TracksTranscriptionCount()
    {
        var subject = new Subject<TranscriptionSegmentEventArgs>();
        await _recorder.StartAsync(new MeetingSession { Title = "Test" });
        _recorder.SubscribeToTranscriptions(subject);

        EmitSegment(subject, "One", isFinal: true);
        EmitSegment(subject, "Two", isFinal: true);
        await Task.Delay(50);

        Assert.Equal(2, _recorder.Metrics.TotalTranscriptions);

        await _recorder.StopAsync();
    }

    #endregion

    #region Suggestion Recording

    [Fact]
    public async Task OnSuggestionReceived_SavesSuggestion()
    {
        var session = await _recorder.StartAsync(new MeetingSession { Title = "Test" });

        _recorder.OnSuggestionReceived("Try this", "silence", "recent context", 100, 50);
        await Task.Delay(100);

        var suggestions = await _repo.GetSuggestionsAsync(session.Id);
        Assert.Single(suggestions);
        Assert.Equal("Try this", suggestions[0].Text);
        Assert.Equal("silence", suggestions[0].TriggerReason);
        Assert.Equal("recent context", suggestions[0].TranscriptContext);
        Assert.Equal(100, suggestions[0].InputTokens);
        Assert.Equal(50, suggestions[0].OutputTokens);

        await _recorder.StopAsync();
    }

    [Fact]
    public async Task OnSuggestionReceived_WhenNotRecording_Ignored()
    {
        _recorder.OnSuggestionReceived("Should not save", "test", "ctx", 0, 0);
        await Task.Delay(50);

        Assert.Equal(0, _recorder.Metrics.TotalSuggestions);
    }

    [Fact]
    public async Task Metrics_TracksSuggestionCount()
    {
        await _recorder.StartAsync(new MeetingSession { Title = "Test" });

        _recorder.OnSuggestionReceived("S1", "silence", "ctx", 100, 50);
        _recorder.OnSuggestionReceived("S2", "hotkey", "ctx", 200, 80);
        await Task.Delay(100);

        Assert.Equal(2, _recorder.Metrics.TotalSuggestions);

        await _recorder.StopAsync();
    }

    #endregion

    #region Dispose

    [Fact]
    public async Task DisposeAsync_StopsRecordingIfActive()
    {
        var session = await _recorder.StartAsync(new MeetingSession { Title = "Test" });

        await _recorder.DisposeAsync();

        Assert.False(_recorder.IsRecording);
        var loaded = await _repo.GetSessionAsync(session.Id);
        Assert.NotNull(loaded!.EndedAt);
    }

    #endregion

    #region Helpers

    private static void EmitSegment(
        Subject<TranscriptionSegmentEventArgs> subject,
        string text,
        bool isFinal,
        AudioSourceType source = AudioSourceType.Microphone)
    {
        var segment = new TranscriptionSegment(
            Text: text,
            Language: "pt",
            Confidence: 0.95f,
            StartTime: TimeSpan.Zero,
            EndTime: TimeSpan.FromSeconds(1),
            Source: source,
            IsFinal: isFinal
        );
        subject.OnNext(new TranscriptionSegmentEventArgs(segment, DateTime.UtcNow));
    }

    #endregion
}
