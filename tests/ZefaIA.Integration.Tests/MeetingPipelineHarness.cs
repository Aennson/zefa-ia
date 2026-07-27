using Microsoft.Extensions.Logging.Abstractions;
using ZefaIA.App;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;
using ZefaIA.Core.Triggers;
using ZefaIA.Integration.Tests.Fakes;
using ZefaIA.LLM;
using ZefaIA.Persistence;
using ZefaIA.STT;

namespace ZefaIA.Integration.Tests;

/// <summary>
/// Builds the production meeting graph with only its external boundaries replaced.
///
/// Real, under test: <see cref="MeetingOrchestrator"/>, StageRunner, AudioCaptureEngine,
/// AudioPipeline, EchoCanceller, TranscriptionEngine, TranscriptionTimeline,
/// LanguageDetector, SilenceTrigger, SuggestionOrchestrator, SuggestionStreamPipeline,
/// PromptBuilder, MeetingRecorder, SqliteMeetingRepository (a real database file) and
/// SessionExporter.
///
/// Faked, because they are outside the process: the audio devices, the speech model,
/// the Anthropic API, and the WPF window.
/// </summary>
public sealed class MeetingPipelineHarness : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly Queue<ScriptedSTTProvider> _pendingProviders = new();

    public AppServices Services { get; }
    public MeetingOrchestrator Orchestrator { get; }
    public SqliteMeetingRepository Repository { get; }

    public FakeAudioSource Mic { get; } = new(AudioSourceType.Microphone);
    public FakeAudioSource Loopback { get; } = new(AudioSourceType.Loopback);
    public RecordingOverlayController Overlay { get; } = new();
    public FakeHotkeyHost Hotkey { get; } = new();
    public ScriptedLLMClient? Llm { get; }

    /// <summary>STT providers handed to the orchestrator, in creation order (mic, then loopback).</summary>
    public List<ScriptedSTTProvider> SttProviders { get; } = new();

    private MeetingPipelineHarness(
        IEnumerable<string> micScript,
        IEnumerable<string> loopbackScript,
        ScriptedLLMClient? llm,
        SilenceTriggerConfig silenceConfig,
        OrchestratorConfig orchestratorConfig,
        int audioBufferMs,
        string language)
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"zefa_e2e_{Guid.NewGuid():N}.db");
        Repository = new SqliteMeetingRepository(_dbPath);
        Llm = llm;

        // The orchestrator creates one provider per channel, mic first. Queueing them
        // keeps the two scripts addressable from the test.
        _pendingProviders.Enqueue(new ScriptedSTTProvider(micScript, language: language));
        _pendingProviders.Enqueue(new ScriptedSTTProvider(loopbackScript, language: language));

        var factory = new STTProviderFactory();
        factory.Register(STTProviderType.WhisperLocal, () =>
        {
            var provider = _pendingProviders.Count > 0
                ? _pendingProviders.Dequeue()
                : new ScriptedSTTProvider(Array.Empty<string>());
            SttProviders.Add(provider);
            return provider;
        });

        var sttSettings = new STTSettings { ActiveProvider = "WhisperLocal" };

        Services = new AppServices
        {
            LoggerFactory = NullLoggerFactory.Instance,
            Settings = new ZefaIA.Overlay.AppSettings(),
            SttSettings = sttSettings,
            SttManager = new STTServiceManager(factory, sttSettings),
            LlmClient = llm,
            Repository = Repository,
            Overlay = Overlay,
            AudioSourceFactory = () => new IAudioSource[] { Mic, Loopback },
            HotkeyHost = Hotkey,
            AudioBufferSizeMs = audioBufferMs,
            OrchestratorConfig = orchestratorConfig,
            SilenceConfig = silenceConfig
        };

        Orchestrator = new MeetingOrchestrator(Services);
    }

    public static async Task<MeetingPipelineHarness> CreateAsync(
        IEnumerable<string>? micScript = null,
        IEnumerable<string>? loopbackScript = null,
        ScriptedLLMClient? llm = null,
        SilenceTriggerConfig? silenceConfig = null,
        OrchestratorConfig? orchestratorConfig = null,
        int audioBufferMs = 20,
        string language = "pt")
    {
        var harness = new MeetingPipelineHarness(
            micScript ?? Array.Empty<string>(),
            loopbackScript ?? Array.Empty<string>(),
            llm,
            // Short windows so a test does not wait on the production 1.5s/10s defaults.
            silenceConfig ?? new SilenceTriggerConfig
            {
                SilenceDuration = TimeSpan.FromMilliseconds(150),
                Cooldown = TimeSpan.FromMilliseconds(300),
                TranscriptRecencyWindow = TimeSpan.FromSeconds(30),
                TranscriptWindow = TimeSpan.FromSeconds(60)
            },
            orchestratorConfig ?? new OrchestratorConfig(),
            audioBufferMs,
            language);

        await harness.Repository.InitializeAsync();
        return harness;
    }

    public Task StartAsync(MeetingSession? session = null) =>
        Orchestrator.StartMeetingAsync(session ?? NewSession());

    public static MeetingSession NewSession() => new()
    {
        Title = "Reuniao de teste",
        Agenda = "Validar o pipeline ponta a ponta",
        Objective = "Garantir que audio vira transcricao, sugestao e historico",
        Participants = "Aennson, Interlocutor"
    };

    // --- Audio helpers -----------------------------------------------------------

    /// <summary>PCM16 loud enough to read as speech (well above the silence threshold).</summary>
    public static byte[] Speech(int samples = 320, short amplitude = 12000)
    {
        var pcm = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            // Alternating polarity keeps RMS high without being a DC offset.
            short value = (i % 2 == 0) ? amplitude : (short)-amplitude;
            pcm[i * 2] = (byte)(value & 0xFF);
            pcm[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }
        return pcm;
    }

    /// <summary>PCM16 of pure silence — what the silence trigger watches for.</summary>
    public static byte[] Silence(int samples = 320) => new byte[samples * 2];

    /// <summary>
    /// Feeds audio into both channels and waits for it to traverse the Rx pipeline.
    /// AudioPipeline buffers on a real timer, so the wait is not optional.
    /// </summary>
    public async Task PumpAsync(
        byte[] micPcm,
        byte[] loopbackPcm,
        int chunks = 1,
        int settleMs = 120)
    {
        for (int i = 0; i < chunks; i++)
        {
            var ts = TimeSpan.FromMilliseconds(_pumpedMs);
            Mic.Emit(micPcm, ts);
            Loopback.Emit(loopbackPcm, ts);
            _pumpedMs += 100;
            await Task.Delay(10);
        }

        await Task.Delay(settleMs);
    }

    /// <summary>Feeds silence on both channels for long enough to arm the silence trigger.</summary>
    public async Task PumpSilenceAsync(TimeSpan duration, int settleMs = 150)
    {
        var deadline = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < deadline)
        {
            var ts = TimeSpan.FromMilliseconds(_pumpedMs);
            Mic.Emit(Silence(), ts);
            Loopback.Emit(Silence(), ts);
            _pumpedMs += 100;
            await Task.Delay(30);
        }

        await Task.Delay(settleMs);
    }

    private int _pumpedMs;

    /// <summary>
    /// Polls until <paramref name="condition"/> holds. The pipeline is asynchronous end
    /// to end (Rx buffering, fire-and-forget STT, batched SQLite writes), so a fixed
    /// sleep would either be flaky or needlessly slow.
    /// </summary>
    public static async Task<bool> WaitForAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            await Task.Delay(25);
        }
        return condition();
    }

    public async ValueTask DisposeAsync()
    {
        await Orchestrator.DisposeAsync();
        await Services.DisposeAsync();

        try
        {
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }
        catch (IOException)
        {
            // A leaked handle here is itself a defect, but failing the test on cleanup
            // would hide whichever assertion actually matters.
        }
    }
}
