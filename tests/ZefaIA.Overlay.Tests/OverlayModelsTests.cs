using System.Windows;
using System.Windows.Media;
using Xunit;

namespace ZefaIA.Overlay.Tests;

public class OverlayModelsTests
{
    [Fact]
    public void OverlaySettings_HasCorrectDefaults()
    {
        var settings = new OverlaySettings();

        Assert.Equal(0.85, settings.Opacity);
        Assert.Equal(14, settings.FontSize);
        Assert.Equal(OverlayPosition.BottomRight, settings.Position);
        Assert.Equal(30, settings.AutoHideSeconds);
        Assert.True(settings.ExcludeFromCapture);
    }

    [Fact]
    public void OverlayPosition_HasAllValues()
    {
        var values = Enum.GetValues<OverlayPosition>();

        Assert.Contains(OverlayPosition.TopLeft, values);
        Assert.Contains(OverlayPosition.TopRight, values);
        Assert.Contains(OverlayPosition.BottomLeft, values);
        Assert.Contains(OverlayPosition.BottomRight, values);
        Assert.Contains(OverlayPosition.Center, values);
        Assert.Equal(5, values.Length);
    }

    [Fact]
    public void TranscriptionDisplayItem_DefaultsAreCorrect()
    {
        var item = new TranscriptionDisplayItem();

        Assert.Equal("", item.Text);
        Assert.Equal("", item.SpeakerName);
        Assert.Equal("", item.Timestamp);
        Assert.False(item.IsFinal);
    }

    [Fact]
    public void TranscriptionDisplayItem_SetsProperties()
    {
        var item = new TranscriptionDisplayItem
        {
            Text = "Hello world",
            SpeakerName = "[Eu]",
            SpeakerColor = new SolidColorBrush(Colors.Blue),
            TextColor = new SolidColorBrush(Colors.White),
            FontStyle = FontStyles.Italic,
            Timestamp = "01:23",
            IsFinal = true
        };

        Assert.Equal("Hello world", item.Text);
        Assert.Equal("[Eu]", item.SpeakerName);
        Assert.Equal("01:23", item.Timestamp);
        Assert.True(item.IsFinal);
        Assert.Equal(FontStyles.Italic, item.FontStyle);
    }

    [Fact]
    public void SuggestionDisplayItem_DefaultsAreCorrect()
    {
        var item = new SuggestionDisplayItem();

        Assert.Equal("", item.Text);
        Assert.Equal("", item.Timestamp);
    }

    [Fact]
    public void SuggestionDisplayItem_SetsProperties()
    {
        var item = new SuggestionDisplayItem
        {
            Text = "Consider mentioning the budget",
            Timestamp = "14:30:00"
        };

        Assert.Equal("Consider mentioning the budget", item.Text);
        Assert.Equal("14:30:00", item.Timestamp);
    }

    [Fact]
    public void OverlaySettings_MutableProperties()
    {
        var settings = new OverlaySettings
        {
            Opacity = 0.5,
            FontSize = 18,
            Position = OverlayPosition.TopLeft,
            AutoHideSeconds = 60,
            ExcludeFromCapture = false
        };

        Assert.Equal(0.5, settings.Opacity);
        Assert.Equal(18, settings.FontSize);
        Assert.Equal(OverlayPosition.TopLeft, settings.Position);
        Assert.Equal(60, settings.AutoHideSeconds);
        Assert.False(settings.ExcludeFromCapture);
    }
}
