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
        var providerType = ParseProviderType(_settings.ActiveProvider);
        var config = BuildConfig(providerType);

        ValidateConfig(providerType, config);

        var provider = _factory.Create(config);
        await provider.InitializeAsync(config, ct);

        lock (_lock)
        {
            _activeProvider = provider;
        }

        _logger?.LogInformation("STT provider initialized: {Provider}", provider.ProviderId);
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

    private STTProviderConfig BuildConfig(STTProviderType type)
    {
        return type switch
        {
            STTProviderType.WhisperLocal => new STTProviderConfig
            {
                ProviderType = STTProviderType.WhisperLocal,
                Language = _settings.WhisperLocal.Language,
                Options = new Dictionary<string, string>
                {
                    ["ModelSize"] = _settings.WhisperLocal.ModelSize,
                    ["ModelPath"] = _settings.WhisperLocal.ModelPath,
                    ["UseGPU"] = _settings.WhisperLocal.UseGPU.ToString(),
                    ["BufferMs"] = _settings.WhisperLocal.BufferMs.ToString()
                }
            },
            STTProviderType.ElevenLabs => new STTProviderConfig
            {
                ProviderType = STTProviderType.ElevenLabs,
                Language = _settings.ElevenLabs.Language,
                Options = new Dictionary<string, string>
                {
                    ["ApiKeyEnvVar"] = _settings.ElevenLabs.ApiKeyEnvVar,
                    ["VadEnabled"] = _settings.ElevenLabs.VadEnabled.ToString()
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

    private static void ValidateConfig(STTProviderType type, STTProviderConfig config)
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
                var envVar = config.Options.GetValueOrDefault("ApiKeyEnvVar", "");
                if (string.IsNullOrWhiteSpace(envVar))
                    throw new InvalidOperationException("ElevenLabs ApiKeyEnvVar must be specified.");
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
