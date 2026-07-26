using Microsoft.Extensions.Configuration;
using ZefaIA.Overlay;

namespace ZefaIA.App.Tests;

public class AppBootstrapperTests
{
    #region Settings precedence

    [Fact]
    public void BindSttSettings_NoUserOverrides_UsesConfigurationValues()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["STT:ActiveProvider"] = "ElevenLabs",
            ["STT:WhisperLocal:ModelSize"] = "small",
            ["STT:WhisperLocal:BufferMs"] = "3000"
        });

        var stt = AppBootstrapper.BindSttSettings(config, new AppSettings
        {
            SttProvider = "",
            WhisperModelSize = "",
            Language = ""
        });

        Assert.Equal("ElevenLabs", stt.ActiveProvider);
        Assert.Equal("small", stt.WhisperLocal.ModelSize);
        Assert.Equal(3000, stt.WhisperLocal.BufferMs);
    }

    [Fact]
    public void BindSttSettings_UserSettingsWinOverConfiguration()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["STT:ActiveProvider"] = "WhisperLocal",
            ["STT:WhisperLocal:ModelSize"] = "base"
        });

        var stt = AppBootstrapper.BindSttSettings(config, new AppSettings
        {
            SttProvider = "ElevenLabs",
            WhisperModelSize = "medium"
        });

        Assert.Equal("ElevenLabs", stt.ActiveProvider);
        Assert.Equal("medium", stt.WhisperLocal.ModelSize);
    }

    [Fact]
    public void BindSttSettings_EmptyConfiguration_UsesDefaults()
    {
        var config = BuildConfig(new Dictionary<string, string?>());

        var stt = AppBootstrapper.BindSttSettings(config, new AppSettings
        {
            SttProvider = "",
            WhisperModelSize = "",
            Language = ""
        });

        Assert.Equal("WhisperLocal", stt.ActiveProvider);
        Assert.Equal("base", stt.WhisperLocal.ModelSize);
        Assert.Equal("./models", stt.WhisperLocal.ModelPath);
        Assert.Equal("ELEVENLABS_API_KEY", stt.ElevenLabs.ApiKeyEnvVar);
    }

    [Fact]
    public void BindSttSettings_LanguageAppliesToBothProviders()
    {
        var config = BuildConfig(new Dictionary<string, string?>());

        var stt = AppBootstrapper.BindSttSettings(config, new AppSettings { Language = "en" });

        Assert.Equal("en", stt.WhisperLocal.Language);
        Assert.Equal("en", stt.ElevenLabs.Language);
    }

    [Fact]
    public void BindSttSettings_UseGpuAlwaysFromUserSettings()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["STT:WhisperLocal:UseGPU"] = "true"
        });

        var stt = AppBootstrapper.BindSttSettings(config, new AppSettings { UseGPU = false });

        // UseGPU is a checkbox in the Settings UI, so the user's value is
        // authoritative even when it matches the config default of false.
        Assert.False(stt.WhisperLocal.UseGPU);
    }

    [Fact]
    public void BindSttSettings_WhitespaceUserProvider_FallsBackToConfiguration()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["STT:ActiveProvider"] = "ElevenLabs"
        });

        var stt = AppBootstrapper.BindSttSettings(config, new AppSettings { SttProvider = "   " });

        Assert.Equal("ElevenLabs", stt.ActiveProvider);
    }

    #endregion

    #region Paths

    [Fact]
    public void SettingsPath_LivesUnderZefaIAAppData()
    {
        var path = AppBootstrapper.SettingsPath;

        Assert.Contains("ZefaIA", path);
        Assert.EndsWith("settings.json", path);
    }

    #endregion

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
