using Microsoft.Extensions.Logging;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.STT;

public sealed class STTServiceManager : IAsyncDisposable
{
    private readonly STTProviderFactory _factory;
    private readonly STTSettings _settings;
    private readonly ILogger<STTServiceManager>? _logger;
    private ISTTProvider? _activeProvider;
    private readonly object _lock = new();

    public ISTTProvider? ActiveProvider => _activeProvider;

    public STTServiceManager(
        STTProviderFactory factory,
        STTSettings settings,
        ILogger<STTServiceManager>? logger = null)
    {
        _factory = factory;
        _settings = settings;
        _logger = logger;
    }

    public static STTProviderFactory CreateDefaultFactory(ILoggerFactory? loggerFactory = null)
    {
        var factory = new STTProviderFactory();

        factory.Register(STTProviderType.WhisperLocal,
            () => new WhisperSTTProvider());

        factory.Register(STTProviderType.ElevenLabs,
            () => new ElevenLabsSTTProvider(loggerFactory?.CreateLogger<ElevenLabsSTTProvider>()));

        return factory;
    }

    public async Task<ISTTProvider> InitializeActiveProviderAsync(CancellationToken ct = default)
    {
        var provider = await CreateProviderAsync(ct);

        lock (_lock)
        {
            _activeProvider = provider;
        }

        _logger?.LogInformation("STT provider initialized: {Provider}", provider.ProviderId);
        return provider;
    }

    /// <summary>
    /// Builds a provider from the current settings without making it the active one.
    /// The mic and loopback channels each need their own instance because a provider
    /// carries per-stream audio buffer state; the caller owns disposal.
    /// </summary>
    public async Task<ISTTProvider> CreateProviderAsync(CancellationToken ct = default)
    {
        var providerType = ParseProviderType(_settings.ActiveProvider);
        var config = BuildConfig(_settings, providerType);

        ValidateConfig(providerType, config);

        var provider = _factory.Create(config);
        await provider.InitializeAsync(config, ct);

        return provider;
    }

    public async Task<ISTTProvider> SwapProviderAsync(STTProviderType newType, CancellationToken ct = default)
    {
        ISTTProvider? oldProvider;

        lock (_lock)
        {
            oldProvider = _activeProvider;
            _activeProvider = null;
        }

        if (oldProvider != null)
        {
            _logger?.LogInformation("Stopping previous provider: {Provider}", oldProvider.ProviderId);
            await oldProvider.DisposeAsync();
        }

        _settings.ActiveProvider = newType.ToString();
        return await InitializeActiveProviderAsync(ct);
    }

    internal static STTProviderConfig BuildConfig(STTSettings settings, STTProviderType type)
    {
        return type switch
        {
            STTProviderType.WhisperLocal => new STTProviderConfig
            {
                ProviderType = STTProviderType.WhisperLocal,
                Language = settings.WhisperLocal.Language,
                Options = new Dictionary<string, string>
                {
                    ["ModelSize"] = settings.WhisperLocal.ModelSize,
                    ["ModelPath"] = settings.WhisperLocal.ModelPath,
                    ["UseGPU"] = settings.WhisperLocal.UseGPU.ToString(),
                    ["BufferMs"] = settings.WhisperLocal.BufferMs.ToString()
                }
            },
            STTProviderType.ElevenLabs => new STTProviderConfig
            {
                ProviderType = STTProviderType.ElevenLabs,
                Language = settings.ElevenLabs.Language,
                Options = new Dictionary<string, string>
                {
                    // Only set when the user configured a key in Settings; otherwise the
                    // provider falls back to the environment variable.
                    ["ApiKey"] = settings.ElevenLabs.ApiKey,
                    ["ApiKeyEnvVar"] = settings.ElevenLabs.ApiKeyEnvVar,
                    ["VadEnabled"] = settings.ElevenLabs.VadEnabled.ToString()
                }
            },
            _ => throw new NotSupportedException($"Unknown provider type: {type}")
        };
    }

    private static STTProviderType ParseProviderType(string name)
    {
        if (Enum.TryParse<STTProviderType>(name, ignoreCase: true, out var type))
            return type;

        throw new InvalidOperationException(
            $"Invalid STT provider '{name}'. Valid options: {string.Join(", ", Enum.GetNames<STTProviderType>())}");
    }

    internal static void ValidateConfig(STTProviderType type, STTProviderConfig config)
    {
        switch (type)
        {
            case STTProviderType.WhisperLocal:
                var modelSize = config.Options.GetValueOrDefault("ModelSize", "");
                var validSizes = new[] { "tiny", "base", "small", "medium", "large" };
                if (!validSizes.Contains(modelSize))
                    throw new InvalidOperationException(
                        $"Invalid Whisper model size '{modelSize}'. Valid: {string.Join(", ", validSizes)}");
                break;

            case STTProviderType.ElevenLabs:
                // Either route to a key is fine; having neither is the misconfiguration.
                var directKey = config.Options.GetValueOrDefault("ApiKey", "");
                var envVar = config.Options.GetValueOrDefault("ApiKeyEnvVar", "");
                if (string.IsNullOrWhiteSpace(directKey) && string.IsNullOrWhiteSpace(envVar))
                    throw new InvalidOperationException(
                        "ElevenLabs needs an API key: set one in Settings or name an environment variable in ApiKeyEnvVar.");
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_activeProvider != null)
        {
            await _activeProvider.DisposeAsync();
            _activeProvider = null;
        }
    }
}
