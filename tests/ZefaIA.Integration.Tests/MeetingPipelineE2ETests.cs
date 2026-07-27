using Xunit;
using ZefaIA.App;
using ZefaIA.Core.Models;
using ZefaIA.Core.Triggers;
using ZefaIA.Integration.Tests.Fakes;
using ZefaIA.LLM;
using ZefaIA.Persistence;

namespace ZefaIA.Integration.Tests;

/// <summary>
/// End-to-end tests over the real meeting graph: audio in one end, transcript on the
/// overlay and rows in SQLite out the other. Only the process boundaries are faked
/// (devices, speech model, Anthropic API, WPF window) — see
/// <see cref="MeetingPipelineHarness"/>.
/// </summary>
public class MeetingPipelineE2ETests
{
    // --- Lifecycle ---------------------------------------------------------------

    [Fact]
    public async Task StartMeeting_BringsEveryStageUpAndCreatesTheSessionRow()
    {
        await using var h = await MeetingPipelineHarness.CreateAsync();

        await h.StartAsync();

        Assert.Equal(MeetingState.Running, h.Orchestrator.State);
        Assert.NotNull(h.Orchestrator.CurrentSession);

        // The persistence stage runs first, so the row exists as soon as we are Running.
        var sessions = await h.Repository.GetAllSessionsAsync();
        var stored = Assert.Single(sessions);
        Assert.Equal("Reuniao de teste", stored.Title);
        Assert.Equal("Validar o pipeline ponta a ponta", stored.Agenda);

        // Both audio sources were started by the capture engine.
        Assert.Equal(2, h.Services.AudioSourceFactory().Count());
    }

    [Fact]
    public async Task StartMeeting_Twice_IsRejected()
    {
        await using var h = await MeetingPipelineHarness.CreateAsync();
        await h.StartAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.StartAsync());
        Assert.Equal(MeetingState.Running, h.Orchestrator.State);
    }

    [Fact]
    public async Task StopMeeting_ReturnsToIdleAndClosesTheSession()
    {
        await using var h = await MeetingPipelineHarness.CreateAsync();
        await h.StartAsync();

        var finished = await h.Orchestrator.StopMeetingAsync();

        Assert.NotNull(finished);
        Assert.Equal(MeetingState.Idle, h.Orchestrator.State);
        Assert.Null(h.Orchestrator.CurrentSession);

        var stored = (await h.Repository.GetAllSessionsAsync()).Single();
        Assert.NotNull(stored.EndedAt);
        Assert.True(stored.EndedAt >= stored.StartedAt);
    }

    [Fact]
    public async Task StopMeeting_WhenIdle_IsANoOp()
    {
        await using var h = await MeetingPipelineHarness.CreateAsync();

        Assert.Null(await h.Orchestrator.StopMeetingAsync());
        Assert.Equal(MeetingState.Idle, h.Orchestrator.State);
    }

    [Fact]
    public async Task StartStopStart_ReusesTheGraphAndKeepsBothSessions()
    {
        await using var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "primeira reuniao" });

        await h.StartAsync();
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Silence(), chunks: 12);
        await h.Orchestrator.StopMeetingAsync();

        var second = MeetingPipelineHarness.NewSession();
        second.Title = "Segunda reuniao";
        await h.StartAsync(second);
        await h.Orchestrator.StopMeetingAsync();

        var sessions = await h.Repository.GetAllSessionsAsync();
        Assert.Equal(2, sessions.Count);
        Assert.Contains(sessions, s => s.Title == "Reuniao de teste");
        Assert.Contains(sessions, s => s.Title == "Segunda reuniao");
    }

    // --- Audio → STT → overlay + persistence ------------------------------------

    [Fact]
    public async Task AudioFlowsThroughToTranscriptOverlayAndDatabase()
    {
        await using var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "bom dia a todos", "vamos comecar" });

        await h.StartAsync();
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Silence(), chunks: 20);

        var rendered = await MeetingPipelineHarness.WaitForAsync(
            () => h.Overlay.RenderedSegments.Count >= 2);
        Assert.True(rendered, "overlay never received the transcribed segments");

        Assert.Contains(h.Overlay.RenderedSegments, s => s.Text == "bom dia a todos");
        Assert.Contains(h.Overlay.RenderedSegments, s => s.Text == "vamos comecar");

        await h.Orchestrator.StopMeetingAsync();

        // StopAsync flushes the recorder's batch, so everything must be durable by now.
        var sessionId = (await h.Repository.GetAllSessionsAsync()).Single().Id;
        var entries = await h.Repository.GetTranscriptionsAsync(sessionId);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Text == "bom dia a todos");
        Assert.All(entries, e => Assert.True(e.IsFinal));
    }

    [Fact]
    public async Task MicAndLoopbackAreTranscribedIndependentlyAndLabelledPerSpeaker()
    {
        await using var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "aqui e o anfitriao" },
            loopbackScript: new[] { "aqui e o convidado" });

        await h.StartAsync();
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Speech(), chunks: 20);

        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.RenderedSegments.Count >= 2);
        await h.Orchestrator.StopMeetingAsync();

        var sessionId = (await h.Repository.GetAllSessionsAsync()).Single().Id;
        var entries = await h.Repository.GetTranscriptionsAsync(sessionId);

        var mine = Assert.Single(entries, e => e.Text == "aqui e o anfitriao");
        var theirs = Assert.Single(entries, e => e.Text == "aqui e o convidado");

        // The speaker label is what distinguishes the two channels in the history.
        Assert.Equal("Eu", mine.SpeakerName);
        Assert.Equal("Interlocutor", theirs.SpeakerName);

        // Two provider instances: sharing one would interleave the speakers' audio.
        Assert.Equal(2, h.SttProviders.Count);
        Assert.NotSame(h.SttProviders[0], h.SttProviders[1]);
        Assert.All(h.SttProviders[0].ReceivedChunks, c => Assert.Equal(AudioSourceType.Microphone, c.Source));
        Assert.All(h.SttProviders[1].ReceivedChunks, c => Assert.Equal(AudioSourceType.Loopback, c.Source));
    }

    [Fact]
    public async Task PartialSegmentsReachTheOverlayButAreNeverPersisted()
    {
        await using var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "texto definitivo" });

        await h.StartAsync();
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Silence(), chunks: 12);
        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.RenderedSegments.Count >= 1);

        h.SttProviders[0].EmitPartial("texto parci", AudioSourceType.Microphone);

        await MeetingPipelineHarness.WaitForAsync(
            () => h.Overlay.RenderedSegments.Any(s => !s.IsFinal));
        await h.Orchestrator.StopMeetingAsync();

        // The overlay shows partials so typing feels live...
        Assert.Contains(h.Overlay.RenderedSegments, s => s.Text == "texto parci" && !s.IsFinal);

        // ...but the transcript of record only keeps finals.
        var sessionId = (await h.Repository.GetAllSessionsAsync()).Single().Id;
        var entries = await h.Repository.GetTranscriptionsAsync(sessionId);
        Assert.Equal(new[] { "texto definitivo" }, entries.Select(e => e.Text));
    }

    [Fact]
    public async Task EchoCancellationRunsOnTheMicPathWhileLoopbackPassesThrough()
    {
        await using var h = await MeetingPipelineHarness.CreateAsync();

        await h.StartAsync();
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Speech(), chunks: 10);

        var flowed = await MeetingPipelineHarness.WaitForAsync(
            () => h.SttProviders.Count == 2 &&
                  h.SttProviders[0].ReceivedChunks.Count > 0 &&
                  h.SttProviders[1].ReceivedChunks.Count > 0);
        Assert.True(flowed, "audio never reached both STT providers");

        // The loopback path is the echo reference and must arrive untouched.
        var loopbackChunk = h.SttProviders[1].ReceivedChunks[0];
        Assert.Equal(MeetingPipelineHarness.Speech(), loopbackChunk.PcmData);

        // The mic path goes through the canceller, so its buffer is a distinct array.
        var micChunk = h.SttProviders[0].ReceivedChunks[0];
        Assert.Equal(loopbackChunk.PcmData.Length, micChunk.PcmData.Length);
        Assert.NotSame(loopbackChunk.PcmData, micChunk.PcmData);
    }

    // --- Silence trigger → LLM → overlay + persistence ---------------------------

    [Fact]
    public async Task SilenceAfterSpeechTriggersASuggestionThatReachesOverlayAndHistory()
    {
        var llm = new ScriptedLLMClient(_ => new[] { "Pergunte ", "sobre o prazo." });
        await using var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "precisamos definir o escopo" },
            llm: llm);

        await h.StartAsync();

        // Speech first: the trigger only fires when something was actually said.
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Speech(), chunks: 12);
        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.RenderedSegments.Count >= 1);

        // Then silence long enough to arm the trigger.
        await h.PumpSilenceAsync(TimeSpan.FromMilliseconds(500));

        var suggested = await MeetingPipelineHarness.WaitForAsync(
            () => h.Overlay.SuggestionsFinalized >= 1);
        Assert.True(suggested, "silence never produced a suggestion");

        Assert.True(h.Overlay.ThinkingShownCount >= 1, "overlay never showed the thinking state");
        Assert.Equal("Pergunte sobre o prazo.", h.Overlay.RenderedSuggestion);

        // The transcript the model saw must be the real one, not an empty window.
        var prompt = Assert.Single(llm.ReceivedTranscripts);
        Assert.Contains("precisamos definir o escopo", prompt);

        await h.Orchestrator.StopMeetingAsync();

        var sessionId = (await h.Repository.GetAllSessionsAsync()).Single().Id;
        var suggestions = await h.Repository.GetSuggestionsAsync(sessionId);
        var stored = Assert.Single(suggestions);
        Assert.Equal("Pergunte sobre o prazo.", stored.Text);
        Assert.Equal(TriggerReason.Silence.ToString(), stored.TriggerReason);
        Assert.Contains("precisamos definir o escopo", stored.TranscriptContext);
    }

    [Fact]
    public async Task SilenceWithNothingSaidYetProducesNoSuggestion()
    {
        var llm = new ScriptedLLMClient();
        await using var h = await MeetingPipelineHarness.CreateAsync(llm: llm);

        await h.StartAsync();
        await h.PumpSilenceAsync(TimeSpan.FromMilliseconds(600));

        // No transcription happened, so there is nothing worth asking the model about.
        Assert.Empty(llm.ReceivedTranscripts);
        Assert.Equal(0, h.Overlay.SuggestionsFinalized);
    }

    [Fact]
    public async Task NoSuggestionMarkerIsSwallowedInsteadOfRenderedOrStored()
    {
        // The marker arrives split across tokens, exactly as the API streams it.
        var llm = new ScriptedLLMClient(_ => new[] { "[SEM", " SUGESTAO]" });
        await using var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "conversa trivial" },
            llm: llm);

        await h.StartAsync();
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Speech(), chunks: 12);
        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.RenderedSegments.Count >= 1);
        await h.PumpSilenceAsync(TimeSpan.FromMilliseconds(500));

        await MeetingPipelineHarness.WaitForAsync(() => llm.ReceivedTranscripts.Count >= 1);
        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.SuggestionsFinalized >= 1);
        await h.Orchestrator.StopMeetingAsync();

        // The model was consulted, but the user must see nothing — not even "[SEM".
        Assert.Single(llm.ReceivedTranscripts);
        Assert.Empty(h.Overlay.SuggestionTokens);

        var sessionId = (await h.Repository.GetAllSessionsAsync()).Single().Id;
        Assert.Empty(await h.Repository.GetSuggestionsAsync(sessionId));
    }

    [Fact]
    public async Task WhenTheLlmFailsTheMeetingKeepsTranscribing()
    {
        var llm = new ScriptedLLMClient(_ => throw new HttpRequestException("upstream down"));
        await using var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "primeira frase", "segunda frase" },
            llm: llm);

        string? reportedError = null;
        h.Orchestrator.OnError += msg => reportedError = msg;

        await h.StartAsync();
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Speech(), chunks: 12);
        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.RenderedSegments.Count >= 1);
        await h.PumpSilenceAsync(TimeSpan.FromMilliseconds(500));

        await MeetingPipelineHarness.WaitForAsync(() => reportedError != null);

        // The failure is surfaced...
        Assert.NotNull(reportedError);

        // ...but the meeting is still up and still transcribing.
        Assert.Equal(MeetingState.Running, h.Orchestrator.State);
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Silence(), chunks: 12);
        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.RenderedSegments.Count >= 2);

        await h.Orchestrator.StopMeetingAsync();

        var sessionId = (await h.Repository.GetAllSessionsAsync()).Single().Id;
        var entries = await h.Repository.GetTranscriptionsAsync(sessionId);
        Assert.Equal(2, entries.Count);
        Assert.Empty(await h.Repository.GetSuggestionsAsync(sessionId));
    }

    [Fact]
    public async Task WithoutAnLlmClientTheMeetingStillTranscribesAndRecords()
    {
        // AppBootstrapper leaves LlmClient null when ANTHROPIC_API_KEY is absent; the
        // app is documented to degrade to transcribe-and-record rather than refuse.
        await using var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "sem chave de api" },
            llm: null);

        Assert.False(h.Services.IsLlmEnabled);

        await h.StartAsync();
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Speech(), chunks: 12);
        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.RenderedSegments.Count >= 1);
        await h.PumpSilenceAsync(TimeSpan.FromMilliseconds(500));
        await h.Orchestrator.StopMeetingAsync();

        var sessionId = (await h.Repository.GetAllSessionsAsync()).Single().Id;
        Assert.Single(await h.Repository.GetTranscriptionsAsync(sessionId));
        Assert.Empty(await h.Repository.GetSuggestionsAsync(sessionId));
        Assert.Equal(0, h.Overlay.SuggestionsFinalized);
    }

    [Fact]
    public async Task RateLimitingStopsASecondSuggestionInsideTheCooldown()
    {
        var llm = new ScriptedLLMClient(_ => new[] { "ideia" });
        await using var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "assunto um", "assunto dois" },
            llm: llm,
            orchestratorConfig: new OrchestratorConfig { MaxRequestsPerMinute = 1 });

        await h.StartAsync();
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Speech(), chunks: 12);
        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.RenderedSegments.Count >= 1);

        await h.PumpSilenceAsync(TimeSpan.FromMilliseconds(500));
        await MeetingPipelineHarness.WaitForAsync(() => llm.ReceivedTranscripts.Count >= 1);

        // More speech, more silence — the trigger fires again but the budget is spent.
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Speech(), chunks: 12);
        await h.PumpSilenceAsync(TimeSpan.FromMilliseconds(600));

        Assert.Single(llm.ReceivedTranscripts);
    }

    // --- Hotkey (on-demand suggestion) -------------------------------------------

    /// <summary>
    /// Silence config that the silence trigger can never satisfy, so these tests observe
    /// the hotkey and nothing else. The harness feeds silent loopback chunks, which is
    /// generous compared to reality — WASAPI delivers no loopback audio at all when
    /// nothing is playing, which is the very reason the hotkey exists.
    /// </summary>
    private static SilenceTriggerConfig HotkeyOnly() => new()
    {
        SilenceDuration = TimeSpan.FromHours(1),
        Cooldown = TimeSpan.FromMilliseconds(1),
        TranscriptRecencyWindow = TimeSpan.FromSeconds(30),
        TranscriptWindow = TimeSpan.FromSeconds(60)
    };

    [Fact]
    public async Task PressingTheHotkeyProducesASuggestionWithNoAudioPlaying()
    {
        var llm = new ScriptedLLMClient(_ => new[] { "Pergunte ", "sobre o orcamento." });
        await using var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "falando sozinho no microfone" },
            llm: llm,
            silenceConfig: HotkeyOnly());

        await h.StartAsync();

        // Only the microphone produces audio — the loopback stays silent, which is what
        // happens when nothing is playing through the speakers. The silence trigger
        // cannot fire here; the hotkey is the only way to ask for a suggestion.
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Silence(), chunks: 12);
        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.RenderedSegments.Count >= 1);

        h.Hotkey.Press();

        var suggested = await MeetingPipelineHarness.WaitForAsync(
            () => h.Overlay.SuggestionsFinalized >= 1);
        Assert.True(suggested, "the hotkey did not produce a suggestion");

        Assert.Equal("Pergunte sobre o orcamento.", h.Overlay.RenderedSuggestion);
        Assert.Contains("falando sozinho no microfone", Assert.Single(llm.ReceivedTranscripts));

        await h.Orchestrator.StopMeetingAsync();

        var sessionId = (await h.Repository.GetAllSessionsAsync()).Single().Id;
        var stored = Assert.Single(await h.Repository.GetSuggestionsAsync(sessionId));
        Assert.Equal("Pergunte sobre o orcamento.", stored.Text);
    }

    [Fact]
    public async Task PressingTheHotkeyTwiceAsksAgainEvenWithTheSameTranscript()
    {
        var llm = new ScriptedLLMClient(_ => new[] { "ideia" });
        await using var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "mesmo assunto" },
            llm: llm,
            silenceConfig: HotkeyOnly());

        await h.StartAsync();
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Silence(), chunks: 12);
        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.RenderedSegments.Count >= 1);

        h.Hotkey.Press();
        await MeetingPipelineHarness.WaitForAsync(() => llm.ReceivedTranscripts.Count >= 1);

        h.Hotkey.Press();
        var askedAgain = await MeetingPipelineHarness.WaitForAsync(
            () => llm.ReceivedTranscripts.Count >= 2);

        // Deduplication is meant to stop automatic triggers repeating themselves. An
        // explicit press must always go through, or the shortcut looks broken.
        Assert.True(askedAgain,
            "the second press was swallowed by deduplication");
    }

    [Fact]
    public async Task HotkeyWithoutAnLlmDoesNothingRatherThanFailing()
    {
        await using var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "sem llm configurado" },
            llm: null);

        await h.StartAsync();
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Silence(), chunks: 12);
        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.RenderedSegments.Count >= 1);

        h.Hotkey.Press();
        await Task.Delay(200);

        Assert.Equal(0, h.Overlay.SuggestionsFinalized);
        Assert.Equal(MeetingState.Running, h.Orchestrator.State);
    }

    [Fact]
    public async Task StoppingTheMeetingReleasesTheHotkey()
    {
        var llm = new ScriptedLLMClient(_ => new[] { "sugestao" });
        await using var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "antes de parar" },
            llm: llm,
            silenceConfig: HotkeyOnly());

        await h.StartAsync();
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Silence(), chunks: 12);
        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.RenderedSegments.Count >= 1);
        await h.Orchestrator.StopMeetingAsync();

        h.Hotkey.Press();
        await Task.Delay(200);

        // A press after teardown must not reach a disposed graph.
        Assert.Empty(llm.ReceivedTranscripts);
    }

    // --- Language detection ------------------------------------------------------

    [Fact]
    public async Task DetectedLanguageRenamesTheSpeakersOnTheOverlay()
    {
        // English, because the detector needs 5 samples to lock and pt-BR maps to the
        // same "Eu"/"Interlocutor" defaults — which would prove nothing.
        await using var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "good morning", "all right", "let us start", "sure", "agreed" },
            language: "en");

        await h.StartAsync();
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Silence(), chunks: 40);

        var renamed = await MeetingPipelineHarness.WaitForAsync(
            () => h.Overlay.MicSpeakerName == "Me");

        Assert.True(renamed, "language detection never relabelled the speakers");
        Assert.Equal("Me", h.Overlay.MicSpeakerName);
        Assert.Equal("Other", h.Overlay.LoopbackSpeakerName);
        Assert.Equal("en", h.Orchestrator.LanguageDetector.DetectedLanguage);
    }

    [Fact]
    public async Task PortugueseKeepsTheDefaultSpeakerLabels()
    {
        await using var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "bom dia", "tudo bem", "vamos la", "certo", "combinado" },
            language: "pt");

        await h.StartAsync();
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Silence(), chunks: 40);

        var detected = await MeetingPipelineHarness.WaitForAsync(
            () => h.Orchestrator.LanguageDetector.IsDetected);

        Assert.True(detected, "language was never detected");
        Assert.Equal("pt", h.Orchestrator.LanguageDetector.DetectedLanguage);
        Assert.Equal("Eu", h.Overlay.MicSpeakerName);
        Assert.Equal("Interlocutor", h.Overlay.LoopbackSpeakerName);
    }

    // --- Export ------------------------------------------------------------------

    [Fact]
    public async Task AFinishedMeetingExportsToTextAndJsonWithItsRealContent()
    {
        var llm = new ScriptedLLMClient(_ => new[] { "sugestao exportada" });
        await using var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "conteudo para exportar" },
            llm: llm);

        await h.StartAsync();
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Speech(), chunks: 12);
        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.RenderedSegments.Count >= 1);
        await h.PumpSilenceAsync(TimeSpan.FromMilliseconds(500));
        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.SuggestionsFinalized >= 1);
        await h.Orchestrator.StopMeetingAsync();

        var sessionId = (await h.Repository.GetAllSessionsAsync()).Single().Id;
        var exporter = new SessionExporter(h.Repository);

        var text = await exporter.ExportToTextAsync(sessionId);
        Assert.Contains("Reuniao de teste", text);
        Assert.Contains("conteudo para exportar", text);
        Assert.Contains("sugestao exportada", text);

        var json = await exporter.ExportToJsonAsync(sessionId);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        // The export keeps the CLR property names (PascalCase) — see SessionExporter.
        Assert.Equal("Reuniao de teste", root.GetProperty("Title").GetString());
        Assert.Equal(1, root.GetProperty("Transcriptions").GetArrayLength());
        Assert.Equal("conteudo para exportar",
            root.GetProperty("Transcriptions")[0].GetProperty("Text").GetString());
        Assert.Equal(1, root.GetProperty("Suggestions").GetArrayLength());
        Assert.Equal("sugestao exportada",
            root.GetProperty("Suggestions")[0].GetProperty("Text").GetString());
    }

    [Fact]
    public async Task DeletingAMeetingRemovesItsTranscriptAndSuggestions()
    {
        var llm = new ScriptedLLMClient(_ => new[] { "sera apagada" });
        await using var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "linha que sera apagada" },
            llm: llm);

        await h.StartAsync();
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Speech(), chunks: 12);
        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.RenderedSegments.Count >= 1);
        await h.PumpSilenceAsync(TimeSpan.FromMilliseconds(500));
        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.SuggestionsFinalized >= 1);
        await h.Orchestrator.StopMeetingAsync();

        var sessionId = (await h.Repository.GetAllSessionsAsync()).Single().Id;
        Assert.NotEmpty(await h.Repository.GetTranscriptionsAsync(sessionId));

        await h.Repository.DeleteSessionAsync(sessionId);

        // The cascade is what keeps orphaned rows out of the history view.
        Assert.Empty(await h.Repository.GetAllSessionsAsync());
        Assert.Empty(await h.Repository.GetTranscriptionsAsync(sessionId));
        Assert.Empty(await h.Repository.GetSuggestionsAsync(sessionId));
    }

    // --- Teardown ----------------------------------------------------------------

    [Fact]
    public async Task DisposingMidMeetingStopsItAndFlushesWhatWasTranscribed()
    {
        var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "nao pode ser perdido" });

        await h.StartAsync();
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Silence(), chunks: 12);
        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.RenderedSegments.Count >= 1);

        // A crash or app exit mid-meeting must not lose the transcript.
        await h.Orchestrator.DisposeAsync();

        var sessionId = (await h.Repository.GetAllSessionsAsync()).Single().Id;
        var entries = await h.Repository.GetTranscriptionsAsync(sessionId);
        Assert.Contains(entries, e => e.Text == "nao pode ser perdido");

        await h.DisposeAsync();
    }

    [Fact]
    public async Task StoppingReleasesTheAudioSourcesAndTheOverlaySubscription()
    {
        await using var h = await MeetingPipelineHarness.CreateAsync(
            micScript: new[] { "antes de parar" });

        await h.StartAsync();
        await h.PumpAsync(MeetingPipelineHarness.Speech(), MeetingPipelineHarness.Silence(), chunks: 12);
        await MeetingPipelineHarness.WaitForAsync(() => h.Overlay.RenderedSegments.Count >= 1);

        var renderedBeforeStop = h.Overlay.RenderedSegments.Count;
        await h.Orchestrator.StopMeetingAsync();

        // Segments emitted after teardown must not reach a disposed overlay.
        h.SttProviders[0].EmitPartial("depois de parar", AudioSourceType.Microphone);
        await Task.Delay(100);

        Assert.Equal(renderedBeforeStop, h.Overlay.RenderedSegments.Count);
    }
}
