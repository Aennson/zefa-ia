using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZefaIA.App;
using ZefaIA.Overlay;
using ZefaIA.STT;

namespace ZefaIA.App.Tests;

/// <summary>
/// A key configured in the Settings window is worthless if it never reaches the client
/// that makes the call. These tests cover that hand-off, including the part that has to
/// work without restarting the app.
/// </summary>
public class ApiKeyWiringTests : IDisposable
{
    private const string AnthropicVar = "ANTHROPIC_API_KEY";
    private const string ElevenLabsVar = "ELEVENLABS_API_KEY";

    private readonly string? _originalAnthropic = Environment.GetEnvironmentVariable(AnthropicVar);
    private readonly string? _originalElevenLabs = Environment.GetEnvironmentVariable(ElevenLabsVar);

    public ApiKeyWiringTests()
    {
        Environment.SetEnvironmentVariable(AnthropicVar, null);
        Environment.SetEnvironmentVariable(ElevenLabsVar, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(AnthropicVar, _originalAnthropic);
        Environment.SetEnvironmentVariable(ElevenLabsVar, _originalElevenLabs);
    }

    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection().Build();

    [Fact]
    public void TheElevenLabsKeyFromSettingsReachesTheSttConfiguration()
    {
        var stt = AppBootstrapper.BindSttSettings(
            EmptyConfig(), new AppSettings { ElevenLabsApiKey = "sk_eleven_from_settings" });

        Assert.Equal("sk_eleven_from_settings", stt.ElevenLabs.ApiKey);
    }

    [Fact]
    public void WithoutAStoredKeyTheSttConfigurationFallsBackToTheEnvironment()
    {
        Environment.SetEnvironmentVariable(ElevenLabsVar, "sk_eleven_from_environment");

        var stt = AppBootstrapper.BindSttSettings(EmptyConfig(), new AppSettings());

        Assert.Equal("sk_eleven_from_environment", stt.ElevenLabs.ApiKey);
    }

    [Fact]
    public void WithNoKeyAnywhereTheSttConfigurationCarriesNone()
    {
        var stt = AppBootstrapper.BindSttSettings(EmptyConfig(), new AppSettings());

        Assert.Equal("", stt.ElevenLabs.ApiKey);
        // The env-var name survives, so a key exported later still works.
        Assert.Equal(ElevenLabsVar, stt.ElevenLabs.ApiKeyEnvVar);
    }

    [Fact]
    public void ApplyingSavedSettingsMutatesTheSameInstanceTheSttManagerHolds()
    {
        // STTServiceManager captures STTSettings at construction. Returning a new object
        // instead of mutating would leave it serving the old key until the app restarted.
        var stt = AppBootstrapper.BindSttSettings(EmptyConfig(), new AppSettings());

        AppBootstrapper.ApplyUserSettings(stt, new AppSettings
        {
            ElevenLabsApiKey = "sk_eleven_pasted_just_now",
            SttProvider = "ElevenLabs"
        });

        Assert.Equal("sk_eleven_pasted_just_now", stt.ElevenLabs.ApiKey);
        Assert.Equal("ElevenLabs", stt.ActiveProvider);
    }

    [Fact]
    public void ClearingTheKeyInSettingsClearsItInTheSttConfiguration()
    {
        var stt = AppBootstrapper.BindSttSettings(
            EmptyConfig(), new AppSettings { ElevenLabsApiKey = "sk_eleven_old" });

        AppBootstrapper.ApplyUserSettings(stt, new AppSettings());

        Assert.Equal("", stt.ElevenLabs.ApiKey);
    }

    // --- LLM client ---------------------------------------------------------------

    [Fact]
    public void AKeyInSettingsIsEnoughToBuildTheLlmClient()
    {
        var client = AppBootstrapper.TryCreateLlmClient(
            new AppSettings { AnthropicApiKey = "sk-ant-from-settings" },
            NullLoggerFactory.Instance,
            NullLogger.Instance);

        Assert.NotNull(client);
    }

    [Fact]
    public void NoKeyAnywhereDisablesSuggestionsInsteadOfThrowing()
    {
        // The app must still launch and transcribe. Throwing here would make a missing key
        // a failed startup.
        var client = AppBootstrapper.TryCreateLlmClient(
            new AppSettings(), NullLoggerFactory.Instance, NullLogger.Instance);

        Assert.Null(client);
    }

    [Fact]
    public void TheEnvironmentVariableStillBuildsTheClientOnItsOwn()
    {
        Environment.SetEnvironmentVariable(AnthropicVar, "sk-ant-from-environment");

        var client = AppBootstrapper.TryCreateLlmClient(
            new AppSettings(), NullLoggerFactory.Instance, NullLogger.Instance);

        Assert.NotNull(client);
    }

    [Fact]
    public void AWhitespaceOnlyKeyCountsAsNoKey()
    {
        var client = AppBootstrapper.TryCreateLlmClient(
            new AppSettings { AnthropicApiKey = "   " },
            NullLoggerFactory.Instance,
            NullLogger.Instance);

        Assert.Null(client);
    }
}
