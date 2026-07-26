using System.Text.Json;
using Xunit;

namespace ZefaIA.Overlay.Tests;

public class AppSettingsTests
{
    [Fact]
    public void HasCorrectDefaults()
    {
        var settings = new AppSettings();

        Assert.Equal("WhisperLocal", settings.SttProvider);
        Assert.Equal("base", settings.WhisperModelSize);
        Assert.Equal("auto", settings.Language);
        Assert.False(settings.UseGPU);
        Assert.Equal("", settings.UserName);
        Assert.Equal("Formal", settings.PreferredTone);
        Assert.Equal("Ctrl+Shift+Space", settings.HotkeySuggestion);
        Assert.Equal(0.85, settings.OverlayOpacity);
        Assert.Equal(14, settings.OverlayFontSize);
        Assert.Equal(OverlayPosition.BottomRight, settings.OverlayPosition);
        Assert.Equal(30, settings.AutoHideSeconds);
        Assert.True(settings.ExcludeFromCapture);
    }

    [Fact]
    public void ToJson_ProducesValidJson()
    {
        var settings = new AppSettings
        {
            UserName = "Aennson",
            SttProvider = "ElevenLabs"
        };

        var json = settings.ToJson();

        Assert.Contains("\"UserName\": \"Aennson\"", json);
        Assert.Contains("\"SttProvider\": \"ElevenLabs\"", json);
    }

    [Fact]
    public void FromJson_ParsesCorrectly()
    {
        var original = new AppSettings
        {
            SttProvider = "ElevenLabs",
            WhisperModelSize = "small",
            Language = "pt",
            UseGPU = true,
            UserName = "Test User",
            OverlayOpacity = 0.5,
            OverlayPosition = OverlayPosition.TopLeft,
            AutoHideSeconds = 60
        };

        var json = original.ToJson();
        var loaded = AppSettings.FromJson(json);

        Assert.Equal("ElevenLabs", loaded.SttProvider);
        Assert.Equal("small", loaded.WhisperModelSize);
        Assert.Equal("pt", loaded.Language);
        Assert.True(loaded.UseGPU);
        Assert.Equal("Test User", loaded.UserName);
        Assert.Equal(0.5, loaded.OverlayOpacity);
        Assert.Equal(OverlayPosition.TopLeft, loaded.OverlayPosition);
        Assert.Equal(60, loaded.AutoHideSeconds);
    }

    [Fact]
    public void FromJson_InvalidJson_ReturnsDefaults()
    {
        var result = AppSettings.FromJson("{}");

        Assert.Equal("WhisperLocal", result.SttProvider);
        Assert.Equal("base", result.WhisperModelSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = new AppSettings
        {
            SttProvider = "ElevenLabs",
            WhisperModelSize = "medium",
            Language = "en",
            UseGPU = true,
            UserName = "Alice",
            UserRole = "Engineer",
            UserExpertise = "Backend",
            PreferredTone = "Casual",
            AdditionalContext = "Test context",
            HotkeySuggestion = "Ctrl+Space",
            HotkeyToggle = "Ctrl+Z",
            HotkeyCopy = "Ctrl+C",
            OverlayOpacity = 0.7,
            OverlayFontSize = 18,
            OverlayPosition = OverlayPosition.Center,
            AutoHideSeconds = 0,
            ExcludeFromCapture = false
        };

        var json = original.ToJson();
        var loaded = AppSettings.FromJson(json);

        Assert.Equal(original.SttProvider, loaded.SttProvider);
        Assert.Equal(original.WhisperModelSize, loaded.WhisperModelSize);
        Assert.Equal(original.Language, loaded.Language);
        Assert.Equal(original.UseGPU, loaded.UseGPU);
        Assert.Equal(original.UserName, loaded.UserName);
        Assert.Equal(original.UserRole, loaded.UserRole);
        Assert.Equal(original.UserExpertise, loaded.UserExpertise);
        Assert.Equal(original.PreferredTone, loaded.PreferredTone);
        Assert.Equal(original.AdditionalContext, loaded.AdditionalContext);
        Assert.Equal(original.HotkeySuggestion, loaded.HotkeySuggestion);
        Assert.Equal(original.HotkeyToggle, loaded.HotkeyToggle);
        Assert.Equal(original.HotkeyCopy, loaded.HotkeyCopy);
        Assert.Equal(original.OverlayOpacity, loaded.OverlayOpacity);
        Assert.Equal(original.OverlayFontSize, loaded.OverlayFontSize);
        Assert.Equal(original.OverlayPosition, loaded.OverlayPosition);
        Assert.Equal(original.AutoHideSeconds, loaded.AutoHideSeconds);
        Assert.Equal(original.ExcludeFromCapture, loaded.ExcludeFromCapture);
    }

    [Fact]
    public async Task SaveAndLoad_FilePersistence()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var original = new AppSettings
            {
                UserName = "PersistTest",
                OverlayPosition = OverlayPosition.TopRight
            };

            await original.SaveAsync(tempFile);
            var loaded = await AppSettings.LoadAsync(tempFile);

            Assert.Equal("PersistTest", loaded.UserName);
            Assert.Equal(OverlayPosition.TopRight, loaded.OverlayPosition);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadAsync_FileNotExists_ReturnsDefaults()
    {
        var result = await AppSettings.LoadAsync("/nonexistent/path/settings.json");

        Assert.Equal("WhisperLocal", result.SttProvider);
    }

    [Fact]
    public void ToJson_EnumSerializesAsString()
    {
        var settings = new AppSettings { OverlayPosition = OverlayPosition.Center };

        var json = settings.ToJson();

        Assert.Contains("\"Center\"", json);
        Assert.DoesNotContain("4", json.Split("OverlayPosition")[1].Split(",")[0]);
    }
}
