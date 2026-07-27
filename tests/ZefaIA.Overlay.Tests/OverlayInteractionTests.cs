using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Xunit;
using ZefaIA.Overlay;

namespace ZefaIA.Overlay.Tests;

/// <summary>
/// Two properties that pull in opposite directions and must both hold: the overlay has to
/// be clickable, and it must not show up in a screen share.
///
/// It shipped unclickable — WS_EX_TRANSPARENT was applied at load with nothing in the UI
/// able to remove it, so tabs, buttons and dragging were all dead.
/// </summary>
public class OverlayInteractionTests
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowDisplayAffinity(IntPtr hWnd, out uint pdwAffinity);

    [WpfFact]
    public void OverlayIsClickableByDefault()
    {
        WithOverlay(w =>
        {
            var style = GetWindowLong(Handle(w), GWL_EXSTYLE);

            Assert.True((style & WS_EX_TRANSPARENT) == 0,
                "WS_EX_TRANSPARENT is set, so the window ignores every mouse message — " +
                "no tab, button or drag will respond.");
            Assert.False(w.IsClickThrough);
        });
    }

    [WpfFact]
    public void OverlayKeepsItsWindowChromeStyles()
    {
        WithOverlay(w =>
        {
            var style = GetWindowLong(Handle(w), GWL_EXSTYLE);

            // Making it clickable must not cost the overlay behaviours: out of Alt+Tab,
            // and never stealing focus from the meeting app.
            Assert.True((style & WS_EX_TOOLWINDOW) != 0, "overlay should stay out of Alt+Tab");
            Assert.True((style & WS_EX_NOACTIVATE) != 0, "overlay should not take focus");
        });
    }

    [WpfFact]
    public void ClickThroughCanBeTurnedOnAndOffAgain()
    {
        WithOverlay(w =>
        {
            w.SetClickThrough(true);
            Assert.True((GetWindowLong(Handle(w), GWL_EXSTYLE) & WS_EX_TRANSPARENT) != 0);

            w.SetClickThrough(false);
            Assert.True((GetWindowLong(Handle(w), GWL_EXSTYLE) & WS_EX_TRANSPARENT) == 0,
                "click-through must be reversible, otherwise turning it on locks the user out");
        });
    }

    [WpfFact]
    public void OverlayIsHiddenFromScreenCaptureByDefault()
    {
        WithOverlay(w =>
        {
            Assert.True(GetWindowDisplayAffinity(Handle(w), out var affinity) != 0,
                "could not read the display affinity");

            // WDA_EXCLUDEFROMCAPTURE (0x11) keeps it off Teams/Meet screen shares while
            // staying visible and interactive locally. WDA_MONITOR (0x01) is the fallback
            // on older Windows: it renders black in the capture.
            Assert.True(affinity is 0x11 or 0x01,
                $"display affinity is {affinity:X}, so the overlay would appear in a screen share");
        });
    }

    [WpfFact]
    public void CaptureExclusionFollowsTheUserSetting()
    {
        // The setting used to be ignored: the window kept its own default and the
        // checkbox in Settings changed nothing.
        WithOverlay(w =>
        {
            w.Settings = new OverlaySettings { ExcludeFromCapture = false };
            w.ApplySettings();

            GetWindowDisplayAffinity(Handle(w), out var off);
            Assert.Equal(0u, off); // WDA_NONE

            w.Settings = new OverlaySettings { ExcludeFromCapture = true };
            w.ApplySettings();

            GetWindowDisplayAffinity(Handle(w), out var on);
            Assert.True(on is 0x11 or 0x01);
        });
    }

    [WpfFact]
    public void ClickingTheTranscriptionTabShowsTheTranscript()
    {
        WithOverlay(w =>
        {
            var transcription = (RadioButton)w.FindName("TabTranscription")!;
            var suggestions = (RadioButton)w.FindName("TabSuggestions")!;
            var scroller = (ScrollViewer)w.FindName("TranscriptionScroller")!;
            var panel = (Grid)w.FindName("SuggestionsPanel")!;

            suggestions.IsChecked = true;
            Assert.Equal(Visibility.Collapsed, scroller.Visibility);
            Assert.Equal(Visibility.Visible, panel.Visibility);

            // Going back is what the user could not do while the window ate every click.
            transcription.IsChecked = true;
            Assert.Equal(Visibility.Visible, scroller.Visibility);
            Assert.Equal(Visibility.Collapsed, panel.Visibility);
        });
    }

    [WpfFact]
    public void TabsAndButtonsAreHitTestable()
    {
        WithOverlay(w =>
        {
            foreach (var name in new[] { "TabTranscription", "TabSuggestions", "BtnPin", "BtnCopy", "BtnDismiss" })
            {
                var element = (FrameworkElement)w.FindName(name)!;

                Assert.True(element.IsHitTestVisible, $"'{name}' is not hit-testable");
                Assert.True(element.ActualWidth > 0 && element.ActualHeight > 0,
                    $"'{name}' has no clickable area");
            }
        });
    }

    // --- resizing ----------------------------------------------------------------

    [WpfFact]
    public void OverlayCanBeResized()
    {
        WithOverlay(w =>
        {
            Assert.Equal(ResizeMode.CanResize, w.ResizeMode);

            w.Width = 700;
            w.Height = 560;
            NewMeetingWindowLayoutTests.LayoutSettled(w);

            Assert.Equal(700, w.ActualWidth);
            Assert.Equal(560, w.ActualHeight);
        });
    }

    [WpfFact]
    public void ResizingIsBounded()
    {
        WithOverlay(w =>
        {
            // A window that can be dragged down to nothing, or past the screen, is worse
            // than one that cannot be resized at all.
            Assert.True(w.MinWidth >= 200 && w.MinHeight >= 150);
            Assert.True(w.MaxWidth > w.MinWidth && w.MaxHeight > w.MinHeight);
        });
    }

    [Theory]
    // corners
    [InlineData(2, 2, 13)]        // HTTOPLEFT
    [InlineData(398, 2, 14)]      // HTTOPRIGHT
    [InlineData(2, 298, 16)]      // HTBOTTOMLEFT
    [InlineData(398, 298, 17)]    // HTBOTTOMRIGHT
    // edges
    [InlineData(2, 150, 10)]      // HTLEFT
    [InlineData(398, 150, 11)]    // HTRIGHT
    [InlineData(200, 2, 12)]      // HTTOP
    [InlineData(200, 298, 15)]    // HTBOTTOM
    // interior stays interactive
    [InlineData(200, 150, 1)]     // HTCLIENT
    [InlineData(60, 40, 1)]       // over the title bar buttons
    public void EdgesReportResizeRegionsAndTheInteriorDoesNot(
        double x, double y, int expected)
    {
        var region = ZefaIA.Overlay.NativeMethods.HitTestResizeBorder(
            x, y, width: 400, height: 300, border: 8);

        Assert.Equal(expected, region);
    }

    private static IntPtr Handle(Window w) => new WindowInteropHelper(w).Handle;

    private static void WithOverlay(Action<OverlayWindow> assert)
    {
        var w = new OverlayWindow
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -10000,
            Top = -10000
        };

        w.Show();
        NewMeetingWindowLayoutTests.LayoutSettled(w);

        try { assert(w); }
        finally { w.Close(); }
    }
}
