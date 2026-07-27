using Xunit;
using ZefaIA.Core.Models;

namespace ZefaIA.STT.Tests;

/// <summary>
/// Which key the ElevenLabs provider ends up using. Getting this wrong is invisible until
/// a meeting starts and the socket is refused.
/// </summary>
public class ElevenLabsApiKeyResolutionTests : IDisposable
{
    private const string EnvVar = "ZEFA_TEST_ELEVENLABS_KEY";

    public void Dispose() => Environment.SetEnvironmentVariable(EnvVar, null);

    private static STTProviderConfig Config(string? apiKey = null, string? envVar = EnvVar)
    {
        var options = new Dictionary<string, string>();
        if (apiKey != null) options["ApiKey"] = apiKey;
        if (envVar != null) options["ApiKeyEnvVar"] = envVar;

        return new STTProviderConfig
        {
            ProviderType = STTProviderType.ElevenLabs,
            Language = "auto",
            Options = options
        };
    }

    [Fact]
    public void AKeyFromSettingsIsUsed()
    {
        Assert.Equal("sk_eleven_settings",
            ElevenLabsSTTProvider.ResolveApiKey(Config(apiKey: "sk_eleven_settings")));
    }

    [Fact]
    public void AKeyFromSettingsBeatsTheEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable(EnvVar, "sk_eleven_environment");

        Assert.Equal("sk_eleven_settings",
            ElevenLabsSTTProvider.ResolveApiKey(Config(apiKey: "sk_eleven_settings")));
    }

    [Fact]
    public void AnEmptySettingsKeyFallsBackToTheEnvironmentVariable()
    {
        // This is the normal case: the manager always sets the option, empty when unset.
        Environment.SetEnvironmentVariable(EnvVar, "sk_eleven_environment");

        Assert.Equal("sk_eleven_environment", ElevenLabsSTTProvider.ResolveApiKey(Config(apiKey: "")));
    }

    [Fact]
    public void WithNoKeyAtAllTheErrorNamesBothPlacesToPutOne()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ElevenLabsSTTProvider.ResolveApiKey(Config(apiKey: "")));

        Assert.Contains("Chaves de API", ex.Message, StringComparison.Ordinal);
        Assert.Contains(EnvVar, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("  sk_eleven_padded  ")]
    [InlineData("sk_eleven_padded\r\n")]
    public void PastedWhitespaceIsTrimmedOffTheKey(string pasted)
    {
        Assert.Equal("sk_eleven_padded", ElevenLabsSTTProvider.ResolveApiKey(Config(apiKey: pasted)));
    }

    [Fact]
    public void AWhitespaceOnlyKeyIsTreatedAsAbsent()
    {
        Environment.SetEnvironmentVariable(EnvVar, "sk_eleven_environment");

        Assert.Equal("sk_eleven_environment", ElevenLabsSTTProvider.ResolveApiKey(Config(apiKey: "   ")));
    }
}

/// <summary>The manager is what puts the configured key into the provider's options.</summary>
public class ElevenLabsManagerKeyPlumbingTests
{
    [Fact]
    public void AConfiguredKeyIsHandedToTheProvider()
    {
        var settings = new STTSettings
        {
            ActiveProvider = "ElevenLabs",
            ElevenLabs = new ElevenLabsSettings { ApiKey = "sk_eleven_from_settings" }
        };

        var config = STTServiceManager.BuildConfig(settings, STTProviderType.ElevenLabs);

        Assert.Equal("sk_eleven_from_settings", config.Options["ApiKey"]);
    }

    [Fact]
    public void ValidationAcceptsAKeyWithNoEnvironmentVariableNamed()
    {
        var settings = new STTSettings
        {
            ActiveProvider = "ElevenLabs",
            ElevenLabs = new ElevenLabsSettings { ApiKey = "sk_eleven_from_settings", ApiKeyEnvVar = "" }
        };

        var config = STTServiceManager.BuildConfig(settings, STTProviderType.ElevenLabs);

        // Used to demand an env-var name, which would reject a perfectly configured app.
        STTServiceManager.ValidateConfig(STTProviderType.ElevenLabs, config);
    }

    [Fact]
    public void ValidationStillRejectsHavingNeither()
    {
        var settings = new STTSettings
        {
            ActiveProvider = "ElevenLabs",
            ElevenLabs = new ElevenLabsSettings { ApiKey = "", ApiKeyEnvVar = "" }
        };

        var config = STTServiceManager.BuildConfig(settings, STTProviderType.ElevenLabs);

        Assert.Throws<InvalidOperationException>(
            () => STTServiceManager.ValidateConfig(STTProviderType.ElevenLabs, config));
    }
}
