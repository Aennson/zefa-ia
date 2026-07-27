using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;
using ZefaIA.Overlay;

namespace ZefaIA.Overlay.Tests;

/// <summary>
/// Guards the reported defect: fields and buttons on the new-meeting dialog were cut off
/// because the window had a fixed height smaller than its own content, with no scrolling
/// and no way to resize. These tests assert the layout from the user's point of view —
/// every control is fully inside the window — instead of asserting pixel constants,
/// which would break on a different DPI or font size.
/// </summary>
public class NewMeetingWindowLayoutTests
{
    [WpfFact]
    public void ContentIsNotSqueezedIntoLessSpaceThanItNeeds()
    {
        RunOnHiddenWindow(window =>
        {
            var root = (FrameworkElement)window.Content;

            // The original failure: the window was a fixed 440px tall while its content
            // wanted 402px of a 362px client area at 125% display scaling. WPF does not
            // clip in that situation — it compresses the star-sized row, and the footer
            // ends up drawn on top of the last field.
            Assert.True(
                root.DesiredSize.Height <= root.ActualHeight + 0.5,
                $"the form needs {root.DesiredSize.Height:F0}px but only has " +
                $"{root.ActualHeight:F0}px, so part of it is being covered.");
        });
    }

    [WpfFact]
    public void NoTwoControlsOverlapEachOther()
    {
        RunOnHiddenWindow(window =>
        {
            var root = (FrameworkElement)window.Content;
            var controls = InteractiveElements(root)
                .Select(e => (Element: e, Bounds: BoundsWithin(e, root)))
                .Where(x => x.Bounds is { Width: > 0, Height: > 0 })
                .ToList();

            for (int i = 0; i < controls.Count; i++)
            {
                for (int j = i + 1; j < controls.Count; j++)
                {
                    var a = controls[i];
                    var b = controls[j];

                    // Layout coordinates ignore clipping, so a field scrolled below the
                    // fold would look like it overlaps the footer. Only compare controls
                    // that share a scrolling container.
                    if (!ReferenceEquals(NearestScroller(a.Element), NearestScroller(b.Element)))
                        continue;

                    var overlap = Rect.Intersect(a.Bounds, b.Bounds);

                    Assert.True(
                        overlap.IsEmpty || overlap.Height < 1 || overlap.Width < 1,
                        $"'{Describe(a.Element)}' and '{Describe(b.Element)}' overlap by " +
                        $"{overlap.Width:F0}x{overlap.Height:F0}px — one is covering the other.");
                }
            }
        });
    }

    [WpfFact]
    public void EveryFieldAndButtonIsInsideTheWindow()
    {
        RunOnHiddenWindow(window =>
        {
            var root = (FrameworkElement)window.Content;

            foreach (var element in InteractiveElements(root))
            {
                var bounds = BoundsWithin(element, root);

                Assert.True(
                    bounds.Bottom <= root.ActualHeight + 0.5,
                    $"'{Describe(element)}' is cut off at the bottom: ends at {bounds.Bottom:F0}px " +
                    $"but the window content is only {root.ActualHeight:F0}px tall.");

                Assert.True(
                    bounds.Right <= root.ActualWidth + 0.5,
                    $"'{Describe(element)}' is cut off on the right: ends at {bounds.Right:F0}px " +
                    $"but the window content is only {root.ActualWidth:F0}px wide.");
            }
        });
    }

    [WpfFact]
    public void ContentScrollsWhenTheWindowIsTooShortForIt()
    {
        // Belt and braces: even at a hostile size the form must stay reachable rather
        // than silently clipping, which is what made the original bug invisible to us.
        RunOnHiddenWindow(window =>
        {
            var scroller = Descendants(window).OfType<ScrollViewer>().FirstOrDefault();

            Assert.True(scroller != null,
                "the form is not inside a ScrollViewer, so it will clip on smaller screens or larger fonts");
            Assert.Equal(ScrollBarVisibility.Auto, scroller!.VerticalScrollBarVisibility);
        });
    }

    [WpfFact]
    public void TemplateButtonsStayInsideTheWindowWidth()
    {
        RunOnHiddenWindow(window =>
        {
            var root = (FrameworkElement)window.Content;
            var templateButtons = Descendants(root)
                .OfType<Button>()
                .Where(b => b.Tag is not null)
                .ToList();

            Assert.NotEmpty(templateButtons);
            foreach (var button in templateButtons)
                Assert.True(BoundsWithin(button, root).Right <= root.ActualWidth + 0.5,
                    $"template button '{Describe(button)}' overflows the window width");
        });
    }

    // --- helpers -----------------------------------------------------------------

    private static void RunOnHiddenWindow(Action<Window> assert)
    {
        var window = new NewMeetingWindow
        {
            // Off-screen so the suite does not flash dialogs at whoever is running it.
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -10000,
            Top = -10000,
            ShowInTaskbar = false
        };

        window.Show();
        LayoutSettled(window);

        try
        {
            assert(window);
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<FrameworkElement> InteractiveElements(DependencyObject root) =>
        Descendants(root).Where(e => e is TextBox or Button);

    /// <summary>
    /// Waits for Loaded handlers and the layout pass to finish. UpdateLayout alone left
    /// measurements occasionally unsettled when several windows ran back to back on the
    /// same STA thread, which showed up as a test that passed alone and failed in a suite.
    /// </summary>
    internal static void LayoutSettled(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
        window.UpdateLayout();
    }

    private static ScrollViewer? NearestScroller(DependencyObject element)
    {
        for (var p = VisualTreeHelper.GetParent(element); p != null; p = VisualTreeHelper.GetParent(p))
            if (p is ScrollViewer sv) return sv;
        return null;
    }

    private static Rect BoundsWithin(FrameworkElement element, FrameworkElement ancestor)
    {
        var origin = element.TransformToAncestor(ancestor).Transform(new Point(0, 0));
        return new Rect(origin, new Size(element.ActualWidth, element.ActualHeight));
    }

    private static string Describe(FrameworkElement element) => element switch
    {
        Button { Content: string text } => text,
        Button b => b.Name,
        TextBox t => string.IsNullOrEmpty(t.Name) ? "TextBox" : t.Name,
        _ => element.GetType().Name
    };

    private static IEnumerable<FrameworkElement> Descendants(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement fe)
                yield return fe;

            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }
}
