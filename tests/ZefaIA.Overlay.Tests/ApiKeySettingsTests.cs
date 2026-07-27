using Xunit;
using ZefaIA.Overlay;

namespace ZefaIA.Overlay.Tests;

/// <summary>
/// How a configured key travels from the Settings window to disk and back, and which
/// source wins when both a stored key and an environment variable exist.
/// </summary>
public class ApiKeySettingsTests : IDisposable
{
    private const string AnthropicVar = "ANTHROPIC_API_KEY";
    private const string ElevenLabsVar = "ELEVENLABS_API_KEY";

    private readonly string? _originalAnthropic = Environment.GetEnvironmentVariable(AnthropicVar);
    private readonly string? _originalElevenLabs = Environment.GetEnvironmentVariable(ElevenLabsVar);

    public ApiKeySettingsTests()
    {
        // The developer machine may well have these set; the tests must not depend on that.
        Environment.SetEnvironmentVariable(AnthropicVar, null);
        Environment.SetEnvironmentVariable(ElevenLabsVar, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(AnthropicVar, _originalAnthropic);
        Environment.SetEnvironmentVariable(ElevenLabsVar, _originalElevenLabs);
    }

    [Fact]
    public void AKeySurvivesBeingSavedAndLoadedAgain()
    {
        var saved = new AppSettings { AnthropicApiKey = "sk-ant-round-trip", ElevenLabsApiKey = "sk_eleven" };

        var loaded = AppSettings.FromJson(saved.ToJson());

        Assert.Equal("sk-ant-round-trip", loaded.AnthropicApiKey);
        Assert.Equal("sk_eleven", loaded.ElevenLabsApiKey);
    }

    [Fact]
    public void TheSettingsFileNeverContainsTheKeyInPlaintext()
    {
        var settings = new AppSettings
        {
            AnthropicApiKey = "sk-ant-api03-SECRET-VALUE",
            ElevenLabsApiKey = "sk_eleven_SECRET_VALUE"
        };

        var json = settings.ToJson();

        Assert.DoesNotContain("sk-ant-api03-SECRET-VALUE", json, StringComparison.Ordinal);
        Assert.DoesNotContain("sk_eleven_SECRET_VALUE", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AKeySavedToDiskIsReadBackByTheNextLaunch()
    {
        var path = Path.Combine(Path.GetTempPath(), $"zefa_keys_{Guid.NewGuid():N}.json");

        try
        {
            await new AppSettings { AnthropicApiKey = "sk-ant-persisted" }.SaveAsync(path);

            Assert.DoesNotContain("sk-ant-persisted", await File.ReadAllTextAsync(path), StringComparison.Ordinal);
            Assert.Equal("sk-ant-persisted", (await AppSettings.LoadAsync(path)).AnthropicApiKey);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ClearingTheFieldClearsTheStoredKey()
    {
        var settings = new AppSettings { AnthropicApiKey = "sk-ant-old" };

        settings.AnthropicApiKey = "";

        Assert.Equal(string.Empty, settings.AnthropicApiKeyProtected);
        Assert.Equal(ApiKeySource.None, settings.AnthropicApiKeySource);
    }

    // --- resolution order ---------------------------------------------------------

    [Fact]
    public void AKeySavedInSettingsWinsOverTheEnvironmentVariable()
    {
        // Deliberate: someone who just pasted a key into the UI expects it to take effect,
        // and would never find a stale variable set months ago.
        Environment.SetEnvironmentVariable(AnthropicVar, "sk-ant-from-environment");
        var settings = new AppSettings { AnthropicApiKey = "sk-ant-from-settings" };

        Assert.Equal("sk-ant-from-settings", settings.ResolveAnthropicApiKey());
        Assert.Equal(ApiKeySource.Settings, settings.AnthropicApiKeySource);
    }

    [Fact]
    public void WithoutAStoredKeyTheEnvironmentVariableIsUsed()
    {
        Environment.SetEnvironmentVariable(AnthropicVar, "sk-ant-from-environment");
        Environment.SetEnvironmentVariable(ElevenLabsVar, "sk_eleven_from_environment");

        var settings = new AppSettings();

        Assert.Equal("sk-ant-from-environment", settings.ResolveAnthropicApiKey());
        Assert.Equal("sk_eleven_from_environment", settings.ResolveElevenLabsApiKey());
        Assert.Equal(ApiKeySource.Environment, settings.AnthropicApiKeySource);
        Assert.Equal(ApiKeySource.Environment, settings.ElevenLabsApiKeySource);
    }

    [Fact]
    public void WithNeitherSourceThereIsNoKey()
    {
        var settings = new AppSettings();

        Assert.Null(settings.ResolveAnthropicApiKey());
        Assert.Null(settings.ResolveElevenLabsApiKey());
        Assert.Equal(ApiKeySource.None, settings.AnthropicApiKeySource);
    }

    [Fact]
    public void AnEnvironmentVariableSetToBlankCountsAsUnset()
    {
        // "$env:ANTHROPIC_API_KEY = ''" is a common way to try to clear it.
        Environment.SetEnvironmentVariable(AnthropicVar, "   ");

        var settings = new AppSettings();

        Assert.Null(settings.ResolveAnthropicApiKey());
        Assert.Equal(ApiKeySource.None, settings.AnthropicApiKeySource);
    }

    [Fact]
    public void TheTwoKeysAreIndependent()
    {
        var settings = new AppSettings { AnthropicApiKey = "sk-ant-only" };

        Assert.Equal("sk-ant-only", settings.ResolveAnthropicApiKey());
        Assert.Null(settings.ResolveElevenLabsApiKey());
    }
}
