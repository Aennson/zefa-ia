using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;
using ZefaIA.Overlay;
using ZefaIA.Persistence;

namespace ZefaIA.Overlay.Tests;

/// <summary>
/// The same layout guarantees the new-meeting dialog got, applied to the other themed
/// windows: content that is never squeezed into less room than it needs, and no control
/// covering another. That combination is what made fields unreadable at 125% scaling.
/// </summary>
public class WindowLayoutTests
{
    [WpfFact]
    public void SettingsWindow_ContentIsNotSqueezed()
    {
        WithWindow(new SettingsWindow(), root =>
            Assert.True(
                root.DesiredSize.Height <= root.ActualHeight + 0.5,
                $"the form needs {root.DesiredSize.Height:F0}px but only has {root.ActualHeight:F0}px"));
    }

    [WpfFact]
    public void SettingsWindow_NoControlsOverlap()
    {
        WithWindow(new SettingsWindow(), AssertNoOverlap);
    }

    [WpfFact]
    public void SettingsWindow_ScrollsWhenTooShort()
    {
        WithWindow(new SettingsWindow(), root =>
        {
            var scroller = Descendants(root).OfType<ScrollViewer>().FirstOrDefault();
            Assert.NotNull(scroller);
            Assert.Equal(ScrollBarVisibility.Auto, scroller!.VerticalScrollBarVisibility);
        });
    }

    [WpfFact]
    public void HistoryWindow_ContentIsNotSqueezed()
    {
        WithWindow(new MeetingHistoryWindow(new StubRepository()), root =>
            Assert.True(
                root.DesiredSize.Height <= root.ActualHeight + 0.5,
                $"the layout needs {root.DesiredSize.Height:F0}px but only has {root.ActualHeight:F0}px"));
    }

    [WpfFact]
    public void HistoryWindow_NoControlsOverlap()
    {
        WithWindow(new MeetingHistoryWindow(new StubRepository()), AssertNoOverlap);
    }

    // --- helpers -----------------------------------------------------------------

    private static void AssertNoOverlap(FrameworkElement root)
    {
        var controls = Descendants(root)
            .Where(e => e is TextBox or Button or ComboBox or CheckBox or Slider)
            .Select(e => (Element: e, Bounds: BoundsWithin(e, root)))
            .Where(x => x.Bounds is { Width: > 0, Height: > 0 })
            .ToList();

        for (int i = 0; i < controls.Count; i++)
        {
            for (int j = i + 1; j < controls.Count; j++)
            {
                // Skip nested controls: a ComboBox legitimately contains a TextBox.
                if (IsAncestorOf(controls[i].Element, controls[j].Element) ||
                    IsAncestorOf(controls[j].Element, controls[i].Element))
                    continue;

                // Only compare controls inside the same scrolling container. Layout
                // coordinates ignore clipping, so a field scrolled below the fold reports
                // a position past the viewport and would look like it overlaps the footer.
                if (!ReferenceEquals(NearestScroller(controls[i].Element),
                                     NearestScroller(controls[j].Element)))
                    continue;

                var overlap = Rect.Intersect(controls[i].Bounds, controls[j].Bounds);
                Assert.True(
                    overlap.IsEmpty || overlap.Height < 1 || overlap.Width < 1,
                    $"'{Describe(controls[i].Element)}' and '{Describe(controls[j].Element)}' " +
                    $"overlap by {overlap.Width:F0}x{overlap.Height:F0}px");
            }
        }
    }

    private static void WithWindow(Window window, Action<FrameworkElement> assert)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -10000;
        window.Top = -10000;
        window.ShowInTaskbar = false;

        window.Show();
        NewMeetingWindowLayoutTests.LayoutSettled(window);

        try { assert((FrameworkElement)window.Content); }
        finally { window.Close(); }
    }

    private static ScrollViewer? NearestScroller(DependencyObject element)
    {
        for (var p = VisualTreeHelper.GetParent(element); p != null; p = VisualTreeHelper.GetParent(p))
            if (p is ScrollViewer sv) return sv;
        return null;
    }

    private static bool IsAncestorOf(DependencyObject ancestor, DependencyObject child)
    {
        for (var p = VisualTreeHelper.GetParent(child); p != null; p = VisualTreeHelper.GetParent(p))
            if (ReferenceEquals(p, ancestor)) return true;
        return false;
    }

    private static Rect BoundsWithin(FrameworkElement element, FrameworkElement ancestor)
    {
        var origin = element.TransformToAncestor(ancestor).Transform(new Point(0, 0));
        return new Rect(origin, new Size(element.ActualWidth, element.ActualHeight));
    }

    private static string Describe(FrameworkElement e) =>
        !string.IsNullOrEmpty(e.Name) ? e.Name
        : e is ContentControl { Content: string s } ? s
        : e.GetType().Name;

    private static IEnumerable<FrameworkElement> Descendants(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement fe) yield return fe;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    /// <summary>Minimal repository: the history window only queries it on demand.</summary>
    private sealed class StubRepository : IMeetingRepository
    {
        public Task InitializeAsync() => Task.CompletedTask;
        public Task<MeetingSession> CreateSessionAsync(MeetingSession s) => Task.FromResult(s);
        public Task<MeetingSession?> GetSessionAsync(long id) => Task.FromResult<MeetingSession?>(null);
        public Task<List<MeetingSession>> GetAllSessionsAsync() => Task.FromResult(new List<MeetingSession>());
        public Task UpdateSessionAsync(MeetingSession s) => Task.CompletedTask;
        public Task DeleteSessionAsync(long id) => Task.CompletedTask;
        public Task AddTranscriptionAsync(TranscriptionEntry e) => Task.CompletedTask;
        public Task AddTranscriptionBatchAsync(IEnumerable<TranscriptionEntry> e) => Task.CompletedTask;
        public Task<List<TranscriptionEntry>> GetTranscriptionsAsync(long id) => Task.FromResult(new List<TranscriptionEntry>());
        public Task AddSuggestionAsync(SuggestionEntry e) => Task.CompletedTask;
        public Task<List<SuggestionEntry>> GetSuggestionsAsync(long id) => Task.FromResult(new List<SuggestionEntry>());
        public Task<List<MeetingSession>> SearchSessionsAsync(string q) => Task.FromResult(new List<MeetingSession>());
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
