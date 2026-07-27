using Microsoft.Extensions.Logging;
using ZefaIA.Audio;
using ZefaIA.Core.Triggers;
using ZefaIA.Core.Interfaces;
using ZefaIA.LLM;
using ZefaIA.Overlay;
using ZefaIA.Persistence;
using ZefaIA.STT;

namespace ZefaIA.App;

/// <summary>
/// The app-lifetime services shared across meetings. Everything here is created
/// once at startup and disposed at exit; per-meeting objects live in
/// <see cref="MeetingOrchestrator"/> instead.
/// </summary>
public sealed class AppServices : IAsyncDisposable
{
    public required ILoggerFactory LoggerFactory { get; init; }
    public required AppSettings Settings { get; init; }
    public required STTSettings SttSettings { get; init; }
    public required STTServiceManager SttManager { get; init; }
    /// <summary>Null when no ANTHROPIC_API_KEY is configured — the app then runs
    /// in transcribe-and-record mode with suggestions disabled.</summary>
    public ILLMClient? LlmClient { get; init; }

    public bool IsLlmEnabled => LlmClient != null;
    public required IMeetingRepository Repository { get; init; }
    public required IOverlayController Overlay { get; init; }

    /// <summary>
    /// Builds the capture sources for a meeting. Defaults to the real WASAPI
    /// microphone + loopback pair; substituting it lets the pipeline be driven from
    /// synthetic audio, which is how the end-to-end tests run without hardware.
    /// </summary>
    public Func<IEnumerable<IAudioSource>> AudioSourceFactory { get; init; } =
        static () => new IAudioSource[] { new MicrophoneSource(), new LoopbackSource() };

    /// <summary>
    /// Receives the global hotkey messages. Null disables the manual trigger — the
    /// pipeline still transcribes and records, exactly as it does without an LLM.
    /// </summary>
    public IHotkeyHost? HotkeyHost { get; init; }

    public int AudioBufferSizeMs { get; init; } = 100;
    public OrchestratorConfig OrchestratorConfig { get; init; } = new();
    public SilenceTriggerConfig SilenceConfig { get; init; } = new();

    public async ValueTask DisposeAsync()
    {
        HotkeyHost?.Dispose();
        Overlay.Dispose();
        await SttManager.DisposeAsync();
        if (LlmClient != null)
            await LlmClient.DisposeAsync();
        await Repository.DisposeAsync();
        LoggerFactory.Dispose();
    }
}
