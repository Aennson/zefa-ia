using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZefaIA.App.Pipeline;
using ZefaIA.Audio;
using ZefaIA.Core;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;
using ZefaIA.Core.Triggers;
using ZefaIA.LLM;
using ZefaIA.Overlay;
using ZefaIA.Persistence;
using ZefaIA.STT;

namespace ZefaIA.App;

public enum MeetingState
{
    Idle,
    Starting,
    Running,
    Stopping,
    Error
}

/// <summary>
/// Owns the lifecycle of a single meeting: builds the audio → STT → LLM → overlay
/// → persistence graph, starts it in dependency order, and tears it down in
/// reverse so nothing is lost on shutdown.
/// </summary>
public sealed class MeetingOrchestrator : IAsyncDisposable
{
    private readonly AppServices _services;
    private readonly ILogger<MeetingOrchestrator> _logger;

    private StageRunner? _runner;
    private AudioCaptureEngine? _captureEngine;
    private AudioPipeline? _audioPipeline;
    private TranscriptionEngine? _transcriptionEngine;
    private ISTTProvider? _micProvider;
    private ISTTProvider? _loopbackProvider;
    private ILLMSession? _llmSession;
    private SuggestionStreamPipeline? _suggestionPipeline;
    private SuggestionOrchestrator? _suggestionOrchestrator;
    private SilenceTrigger? _silenceTrigger;
    private HotkeyTrigger? _hotkeyTrigger;
    private MeetingRecorder? _recorder;

    private readonly TranscriptionTimeline _timeline = new();
    private readonly LanguageDetector _languageDetector = new();
    private readonly List<IDisposable> _subscriptions = new();
    private string _currentSuggestion = "";
    private MeetingState _state = MeetingState.Idle;
    private bool _disposed;

    public MeetingState State => _state;
    public MeetingSession? CurrentSession { get; private set; }
    public LanguageDetector LanguageDetector => _languageDetector;

    public event Action<MeetingState>? OnStateChanged;
    public event Action<string>? OnError;

    public MeetingOrchestrator(AppServices services, ILogger<MeetingOrchestrator>? logger = null)
    {
        _services = services;
        _logger = logger ?? NullLogger<MeetingOrchestrator>.Instance;
    }

    public async Task StartMeetingAsync(MeetingSession session, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_state is MeetingState.Running or MeetingState.Starting)
            throw new InvalidOperationException("A meeting is already running.");

        SetState(MeetingState.Starting);

        try
        {
            await BuildGraphAsync(session, ct);
            await _runner!.StartAsync(ct);

            CurrentSession = session;
            SetState(MeetingState.Running);
            _logger.LogInformation("Meeting started: {Title}", session.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start meeting");
            SetState(MeetingState.Error);
            OnError?.Invoke(ex.Message);
            await SafeTeardownAsync();
            throw;
        }
    }

    public async Task<MeetingSession?> StopMeetingAsync()
    {
        if (_state is not (MeetingState.Running or MeetingState.Error))
            return null;

        SetState(MeetingState.Stopping);

        try
        {
            if (_runner != null)
                await _runner.StopAsync();
        }
        catch (AggregateException ex)
        {
            // Stages report failures after all of them have been attempted, so the
            // meeting is already fully stopped by this point — log and carry on.
            _logger.LogError(ex, "Errors during meeting shutdown");
            OnError?.Invoke("Alguns componentes falharam ao parar; os dados foram salvos.");
        }

        var finished = CurrentSession;
        await SafeTeardownAsync();
        SetState(MeetingState.Idle);

        _logger.LogInformation("Meeting stopped: {Title}", finished?.Title);
        return finished;
    }

    private async Task BuildGraphAsync(MeetingSession session, CancellationToken ct)
    {
        var settings = _services.Settings;
        _pendingSession = session;

        // --- Audio ---
        _captureEngine = new AudioCaptureEngine(_services.LoggerFactory.CreateLogger<AudioCaptureEngine>());
        foreach (var source in _services.AudioSourceFactory())
            _captureEngine.AddSource(source);

        var echoCanceller = new EchoCanceller(
            logger: _services.LoggerFactory.CreateLogger<EchoCanceller>());

        _audioPipeline = new AudioPipeline(
            _captureEngine,
            echoCanceller,
            _services.AudioBufferSizeMs,
            _services.LoggerFactory.CreateLogger<AudioPipeline>());

        // --- STT ---
        // Two instances: each provider buffers audio per stream, so sharing one
        // between mic and loopback would interleave the two speakers' audio.
        _micProvider = await _services.SttManager.CreateProviderAsync(ct);
        _loopbackProvider = await _services.SttManager.CreateProviderAsync(ct);

        _transcriptionEngine = new TranscriptionEngine(
            _micProvider,
            _loopbackProvider,
            _services.LoggerFactory.CreateLogger<TranscriptionEngine>());

        // --- LLM (skipped entirely when no API key is configured) ---
        _silenceTrigger = new SilenceTrigger(_services.SilenceConfig);

        if (_services.LlmClient != null)
        {
            var systemPrompt = new PromptBuilder()
                .WithProfile(
                    settings.UserName,
                    settings.UserRole,
                    settings.UserExpertise,
                    settings.PreferredTone,
                    settings.AdditionalContext)
                .WithMeetingContext(session.Agenda, session.Objective, session.Participants)
                .Build();

            _llmSession = await _services.LlmClient.CreateSessionAsync(
                new LLMSessionConfig(systemPrompt, session.Agenda), ct);

            _suggestionPipeline = new SuggestionStreamPipeline(
                _services.LoggerFactory.CreateLogger<SuggestionStreamPipeline>());

            _suggestionOrchestrator = new SuggestionOrchestrator(
                _llmSession,
                _suggestionPipeline,
                _services.OrchestratorConfig,
                _services.LoggerFactory.CreateLogger<SuggestionOrchestrator>());

            _suggestionOrchestrator.TranscriptProvider = BuildTranscriptWindow;
            _suggestionOrchestrator.RegisterTrigger(_silenceTrigger);

            RegisterSuggestionHotkey(settings.HotkeySuggestion);
        }
        else
        {
            _logger.LogInformation("Suggestions disabled — no LLM client configured");
        }

        // --- Persistence ---
        _recorder = new MeetingRecorder(
            _services.Repository,
            logger: _services.LoggerFactory.CreateLogger<MeetingRecorder>());

        WireStreams();
        BuildRunner();
    }

    private void WireStreams()
    {
        var overlay = _services.Overlay;

        // Transcription fans out to overlay, timeline, language detection,
        // the silence trigger's "has anything been said" check, and persistence.
        overlay.SubscribeToTranscription(_transcriptionEngine!.TranscriptionStream);
        _recorder!.SubscribeToTranscriptions(_transcriptionEngine.TranscriptionStream);

        _subscriptions.Add(_transcriptionEngine.TranscriptionStream
            .Where(e => e.Segment.IsFinal)
            .Subscribe(OnFinalSegment));

        _silenceTrigger!.SubscribeToAudio(_audioPipeline!.LoopbackStream);

        // Suggestion stream drives the overlay and, on completion, persistence.
        // Absent when the LLM is disabled; transcription and recording still run.
        if (_suggestionPipeline != null)
        {
            _suggestionPipeline.OnThinkingStarted += overlay.ShowSuggestionThinking;
            _suggestionPipeline.OnTokenReceived += OnSuggestionToken;
            _suggestionPipeline.OnComplete += OnSuggestionComplete;
            _suggestionPipeline.OnError += OnSuggestionError;
        }

        _languageDetector.OnLanguageDetected += OnLanguageDetected;
    }

    /// <summary>
    /// Wires the on-demand suggestion hotkey. This is the only trigger that works when
    /// nothing is playing through the speakers: the silence trigger watches the loopback
    /// stream, and WASAPI delivers no loopback audio at all while the render endpoint is
    /// idle, so a user talking alone into the microphone would never get a suggestion.
    /// </summary>
    private void RegisterSuggestionHotkey(string? hotkey)
    {
        if (_services.HotkeyHost is null)
        {
            _logger.LogInformation("Suggestion hotkey disabled — no hotkey host available");
            return;
        }

        if (string.IsNullOrWhiteSpace(hotkey))
            return;

        var binding = HotkeyTrigger.ParseHotkeyString(hotkey);
        if (binding.Key == 0)
        {
            _logger.LogWarning("Could not parse suggestion hotkey '{Hotkey}'", hotkey);
            return;
        }

        _hotkeyTrigger = new HotkeyTrigger();
        _hotkeyTrigger.RegisterHotkey(binding with
        {
            TranscriptWindow = _services.SilenceConfig.TranscriptWindow
        });
        _hotkeyTrigger.AttachToWindow(_services.HotkeyHost.Handle);
        _services.HotkeyHost.HotkeyMessage += OnHotkeyMessage;

        _suggestionOrchestrator!.RegisterTrigger(_hotkeyTrigger);
        _logger.LogInformation("Suggestion hotkey registered: {Hotkey}", hotkey);
    }

    private void OnHotkeyMessage(IntPtr wParam) => _hotkeyTrigger?.ProcessMessage(wParam);

    private void OnFinalSegment(TranscriptionSegmentEventArgs args)
    {
        _timeline.Add(args.Segment);
        _languageDetector.ProcessSegment(args.Segment);
        _silenceTrigger!.NotifyTranscriptionReceived();
    }

    private void OnLanguageDetected(string language)
    {
        var labels = _languageDetector.GetSpeakerLabels();
        _timeline.MicSpeaker = SpeakerLabel.Me(labels.Self);
        _timeline.LoopbackSpeaker = SpeakerLabel.Other(labels.Other);
        _services.Overlay.SetSpeakerNames(labels.Self, labels.Other);

        _logger.LogInformation("Language detected: {Language} (labels: {Self}/{Other})",
            language, labels.Self, labels.Other);
    }

    private void OnSuggestionToken(string token)
    {
        _currentSuggestion += token;
        _services.Overlay.AppendSuggestionText(token);
    }

    private void OnSuggestionComplete()
    {
        _services.Overlay.FinalizeSuggestion();

        if (!string.IsNullOrWhiteSpace(_currentSuggestion))
        {
            _recorder!.OnSuggestionReceived(
                _currentSuggestion,
                TriggerReason.Silence.ToString(),
                BuildTranscriptWindow(_services.SilenceConfig.TranscriptWindow),
                inputTokens: 0,
                outputTokens: 0);
        }

        _currentSuggestion = "";
    }

    private void OnSuggestionError(string message)
    {
        _currentSuggestion = "";
        _logger.LogWarning("Suggestion failed: {Message}", message);
        OnError?.Invoke(message);
    }

    private string BuildTranscriptWindow(TimeSpan window)
    {
        var timeline = _timeline.GetTimeline();
        if (timeline.Count == 0) return "";

        var latest = timeline[^1].Segment.EndTime;
        var from = latest - window;
        if (from < TimeSpan.Zero) from = TimeSpan.Zero;

        return _timeline.FormatTimelineWindow(from, latest);
    }

    private void BuildRunner()
    {
        _runner = new StageRunner(_services.LoggerFactory.CreateLogger<StageRunner>());

        // Start order is dependency order; StageRunner stops in reverse, which is
        // exactly the shutdown sequence the spec calls for (triggers first,
        // persistence flush last).
        _runner
            .Add(new DelegateStage("Persistence",
                async _ => await _recorder!.StartAsync(CurrentSessionOrThrow()),
                async () => await _recorder!.StopAsync()))
            .Add(DelegateStage.Sync("AudioPipeline",
                () => _audioPipeline!.Start(),
                () => _audioPipeline!.Stop()))
            .Add(new DelegateStage("AudioCapture",
                ct => _captureEngine!.StartAsync(ct),
                () => _captureEngine!.StopAsync()))
            .Add(DelegateStage.Sync("Transcription",
                () => _transcriptionEngine!.Start(_audioPipeline!.MicStream, _audioPipeline.LoopbackStream),
                () => _transcriptionEngine!.Stop()))
            .Add(new DelegateStage("Triggers",
                ct => _silenceTrigger!.StartMonitoringAsync(ct),
                () => _silenceTrigger!.StopMonitoringAsync()));
    }

    /// <summary>
    /// Set by BuildGraphAsync before the runner executes so the persistence stage can
    /// create the row; CurrentSession is only published once every stage is up.
    /// </summary>
    private MeetingSession? _pendingSession;

    private MeetingSession CurrentSessionOrThrow() =>
        _pendingSession ?? throw new InvalidOperationException("No session being started.");

    private async Task SafeTeardownAsync()
    {
        foreach (var sub in _subscriptions)
        {
            try { sub.Dispose(); } catch (Exception ex) { _logger.LogWarning(ex, "Subscription dispose failed"); }
        }
        _subscriptions.Clear();

        if (_suggestionPipeline != null)
        {
            _suggestionPipeline.OnThinkingStarted -= _services.Overlay.ShowSuggestionThinking;
            _suggestionPipeline.OnTokenReceived -= OnSuggestionToken;
            _suggestionPipeline.OnComplete -= OnSuggestionComplete;
            _suggestionPipeline.OnError -= OnSuggestionError;
        }
        _languageDetector.OnLanguageDetected -= OnLanguageDetected;

        if (_services.HotkeyHost != null)
            _services.HotkeyHost.HotkeyMessage -= OnHotkeyMessage;

        await DisposeSafe(_recorder);
        DisposeSafe(_hotkeyTrigger);
        DisposeSafe(_suggestionOrchestrator);
        DisposeSafe(_suggestionPipeline);
        DisposeSafe(_silenceTrigger);
        await DisposeSafe(_llmSession);
        DisposeSafe(_transcriptionEngine);
        // The orchestrator creates these two directly, so it is responsible for
        // disposing them — STTServiceManager only tracks its own active provider.
        await DisposeSafe(_micProvider);
        await DisposeSafe(_loopbackProvider);
        DisposeSafe(_audioPipeline);
        DisposeSafe(_captureEngine);

        _micProvider = null;
        _loopbackProvider = null;
        _recorder = null;
        _suggestionOrchestrator = null;
        _suggestionPipeline = null;
        _silenceTrigger = null;
        _hotkeyTrigger = null;
        _llmSession = null;
        _transcriptionEngine = null;
        _audioPipeline = null;
        _captureEngine = null;
        _runner = null;
        _pendingSession = null;
        CurrentSession = null;
        _currentSuggestion = "";
        _timeline.Clear();
        _languageDetector.Reset();
    }

    private void DisposeSafe(IDisposable? disposable)
    {
        try { disposable?.Dispose(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Dispose failed for {Type}", disposable?.GetType().Name); }
    }

    private async Task DisposeSafe(IAsyncDisposable? disposable)
    {
        try { if (disposable != null) await disposable.DisposeAsync(); }
        catch (Exception ex) { _logger.LogWarning(ex, "DisposeAsync failed for {Type}", disposable?.GetType().Name); }
    }

    private void SetState(MeetingState newState)
    {
        if (_state == newState) return;
        _state = newState;
        OnStateChanged?.Invoke(newState);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_state is MeetingState.Running or MeetingState.Error)
            await StopMeetingAsync();
        else
            await SafeTeardownAsync();
    }
}
