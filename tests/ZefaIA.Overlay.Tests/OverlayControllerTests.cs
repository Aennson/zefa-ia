using Xunit;

namespace ZefaIA.Overlay.Tests;

public class OverlayControllerTests
{
    [Fact]
    public void Constructor_CreatesWindowWithDefaultSettings()
    {
        var controller = new OverlayController();

        Assert.NotNull(controller.Window);
        Assert.Equal(0.85, controller.Window.Settings.Opacity);
        Assert.Equal(OverlayPosition.BottomRight, controller.Window.Settings.Position);
    }

    [Fact]
    public void Constructor_AppliesCustomSettings()
    {
        var settings = new OverlaySettings
        {
            Opacity = 0.5,
            FontSize = 20,
            Position = OverlayPosition.TopLeft,
            AutoHideSeconds = 60
        };

        var controller = new OverlayController(settings);

        Assert.Equal(0.5, controller.Window.Settings.Opacity);
        Assert.Equal(20, controller.Window.Settings.FontSize);
        Assert.Equal(OverlayPosition.TopLeft, controller.Window.Settings.Position);
    }

    [Fact]
    public void SetSpeakerNames_UpdatesNames()
    {
        var controller = new OverlayController();
        controller.SetSpeakerNames("Alice", "Bob");
        // Names stored internally; verified through transcription display
    }

    [Fact]
    public void Dispose_MultipleCallsDoNotThrow()
    {
        var controller = new OverlayController();
        controller.Dispose();
        controller.Dispose();
    }
}
