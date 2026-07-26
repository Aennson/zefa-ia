using Xunit;
using System.Drawing;

namespace ZefaIA.App.Tests;

/// <summary>
/// Covers the pure state-to-presentation mapping. The NotifyIcon itself needs a
/// message pump and is verified manually.
/// </summary>
public class TrayIconControllerTests
{
    [Theory]
    [InlineData(MeetingState.Idle, "ocioso")]
    [InlineData(MeetingState.Starting, "iniciando")]
    [InlineData(MeetingState.Running, "gravando")]
    [InlineData(MeetingState.Stopping, "encerrando")]
    [InlineData(MeetingState.Error, "erro")]
    public void BuildTooltip_DescribesEachState(MeetingState state, string expectedFragment)
    {
        var tooltip = TrayIconController.BuildTooltip(state);

        Assert.StartsWith("Zefa IA", tooltip);
        Assert.Contains(expectedFragment, tooltip);
    }

    [Fact]
    public void BuildTooltip_StaysWithinWindowsTooltipLimit()
    {
        // NotifyIcon.Text throws above 63 characters.
        foreach (MeetingState state in Enum.GetValues<MeetingState>())
            Assert.True(TrayIconController.BuildTooltip(state).Length <= 63);
    }

    [Fact]
    public void GetStateColor_RecordingIsRed()
    {
        var color = TrayIconController.GetStateColor(MeetingState.Running);

        Assert.True(color.R > color.G && color.R > color.B);
    }

    [Fact]
    public void GetStateColor_IdleDiffersFromRecording()
    {
        Assert.NotEqual(
            TrayIconController.GetStateColor(MeetingState.Idle),
            TrayIconController.GetStateColor(MeetingState.Running));
    }

    [Fact]
    public void GetStateColor_TransitionalStatesShareAmber()
    {
        Assert.Equal(
            TrayIconController.GetStateColor(MeetingState.Starting),
            TrayIconController.GetStateColor(MeetingState.Stopping));
    }

    [Fact]
    public void GetStateColor_EveryStateIsOpaque()
    {
        foreach (MeetingState state in Enum.GetValues<MeetingState>())
            Assert.Equal(255, TrayIconController.GetStateColor(state).A);
    }
}
