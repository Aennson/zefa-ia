using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Triggers;
using ZefaIA.LLM;
using ZefaIA.Overlay;
using ZefaIA.Persistence;
using ZefaIA.STT;

namespace ZefaIA.App;

/// <summary>
/// Runs the startup sequence: settings, database, profile, then the app-lifetime
/// services. Each step logs its outcome so a failed launch says which step broke.
/// </summary>
public static class AppBootstrapper
{
    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ZefaIA", "settings.json");

    public static async Task<AppServices> BuildAsync(
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        CancellationToken ct = default)
    {
        var logger = loggerFactory.CreateLogger("Bootstrap");

        // 1-3. Settings and user profile
        var settings = await AppSettings.LoadAsync(SettingsPath);
        logger.LogInformation("Settings loaded from {Path}", SettingsPath);

        var sttSettings = BindSttSettings(configuration, settings);
        logger.LogInformation("STT provider: {Provider} ({Model})",
            sttSettings.ActiveProvider, sttSettings.WhisperLocal.ModelSize);

        // 4. Database
        var repository = new SqliteMeetingRepository(SqliteMeetingRepository.DefaultDbPath);
        await repository.InitializeAsync();
        logger.LogInformation("Database ready at {Path}", SqliteMeetingRepository.DefaultDbPath);

        // 5. STT manager (providers are created per meeting)
        var factory = STTServiceManager.CreateDefaultFactory(loggerFactory);
        var sttManager = new STTServiceManager(
            factory, sttSettings, loggerFactory.CreateLogger<STTServiceManager>());

        // 6. LLM client. A missing API key is not fatal: the app degrades to
        // transcribe-and-record mode rather than refusing to launch.
        ILLMClient? llmClient = null;
        try
        {
            llmClient = new ClaudeLLMClient(
                logger: loggerFactory.CreateLogger<ClaudeLLMClient>());
            logger.LogInformation("LLM client ready");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("LLM disabled: {Reason}. Transcription and history still work.", ex.Message);
        }

        // 7. Overlay, created hidden
        var overlay = new OverlayController(new OverlaySettings
        {
            Opacity = settings.OverlayOpacity,
            FontSize = settings.OverlayFontSize,
            Position = settings.OverlayPosition,
            AutoHideSeconds = settings.AutoHideSeconds,
            ExcludeFromCapture = settings.ExcludeFromCapture
        });
        logger.LogInformation("Overlay initialized (hidden)");

        // 8. Message-only window for the global suggestion hotkey. Created once for the
        // app's lifetime so the registration survives across meetings.
        var hotkeyHost = new Win32HotkeyHost();
        logger.LogInformation("Hotkey host ready (suggestion: {Hotkey})", settings.HotkeySuggestion);

        return new AppServices
        {
            HotkeyHost = hotkeyHost,
            LoggerFactory = loggerFactory,
            Settings = settings,
            SttSettings = sttSettings,
            SttManager = sttManager,
            LlmClient = llmClient,
            Repository = repository,
            Overlay = overlay,
            AudioBufferSizeMs = configuration.GetValue("Audio:BufferSizeMs", 100),
            OrchestratorConfig = new OrchestratorConfig
            {
                MaxRequestsPerMinute = configuration.GetValue("Triggers:MaxRequestsPerMinute", 4)
            },
            SilenceConfig = new SilenceTriggerConfig
            {
                SilenceDuration = TimeSpan.FromMilliseconds(
                    configuration.GetValue("Triggers:SilenceThresholdMs", 1500)),
                Cooldown = TimeSpan.FromMilliseconds(
                    configuration.GetValue("Triggers:CooldownMs", 10000))
            }
        };
    }

    /// <summary>
    /// appsettings.json holds the deployment defaults; the user's settings.json
    /// wins for anything editable in the Settings UI.
    /// </summary>
    internal static STTSettings BindSttSettings(IConfiguration configuration, AppSettings userSettings)
    {
        var stt = new STTSettings
        {
            ActiveProvider = configuration.GetValue("STT:ActiveProvider", "WhisperLocal")!,
            WhisperLocal = new WhisperLocalSettings
            {
                ModelSize = configuration.GetValue("STT:WhisperLocal:ModelSize", "base")!,
                Language = configuration.GetValue("STT:WhisperLocal:Language", "auto")!,
                UseGPU = configuration.GetValue("STT:WhisperLocal:UseGPU", false),
                ModelPath = configuration.GetValue("STT:WhisperLocal:ModelPath", "./models")!,
                BufferMs = configuration.GetValue("STT:WhisperLocal:BufferMs", 2500)
            },
            ElevenLabs = new ElevenLabsSettings
            {
                ApiKeyEnvVar = configuration.GetValue("STT:ElevenLabs:ApiKeyEnvVar", "ELEVENLABS_API_KEY")!,
                Language = configuration.GetValue("STT:ElevenLabs:Language", "auto")!,
                VadEnabled = configuration.GetValue("STT:ElevenLabs:VadEnabled", true)
            }
        };

        if (!string.IsNullOrWhiteSpace(userSettings.SttProvider))
            stt.ActiveProvider = userSettings.SttProvider;
        if (!string.IsNullOrWhiteSpace(userSettings.WhisperModelSize))
            stt.WhisperLocal.ModelSize = userSettings.WhisperModelSize;
        if (!string.IsNullOrWhiteSpace(userSettings.Language))
        {
            stt.WhisperLocal.Language = userSettings.Language;
            stt.ElevenLabs.Language = userSettings.Language;
        }
        stt.WhisperLocal.UseGPU = userSettings.UseGPU;

        return stt;
    }
}
