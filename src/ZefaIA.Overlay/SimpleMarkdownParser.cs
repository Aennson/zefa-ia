using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ZefaIA.Overlay;

public static partial class SimpleMarkdownParser
{
    public static TextBlock Parse(string markdown, double fontSize = 13)
    {
        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = fontSize,
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 255))
        };

        var lines = markdown.Split('\n');

        foreach (var line in lines)
        {
            if (textBlock.Inlines.Count > 0)
                textBlock.Inlines.Add(new LineBreak());

            ParseLine(textBlock.Inlines, line);
        }

        return textBlock;
    }

    private static void ParseLine(InlineCollection inlines, string line)
    {
        var trimmed = line.TrimStart();

        if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
        {
            inlines.Add(new Run("  • "));
            ParseInline(inlines, trimmed[2..]);
            return;
        }

        if (BoldRegex().IsMatch(trimmed))
        {
            ParseInline(inlines, trimmed);
            return;
        }

        ParseInline(inlines, line);
    }

    internal static void ParseInline(InlineCollection inlines, string text)
    {
        var remaining = text;

        while (remaining.Length > 0)
        {
            var boldMatch = BoldRegex().Match(remaining);
            var italicMatch = ItalicRegex().Match(remaining);

            Match? earliest = null;
            var isBold = false;

            if (boldMatch.Success && (!italicMatch.Success || boldMatch.Index <= italicMatch.Index))
            {
                earliest = boldMatch;
                isBold = true;
            }
            else if (italicMatch.Success)
            {
                earliest = italicMatch;
            }

            if (earliest == null)
            {
                if (remaining.Length > 0)
                    inlines.Add(new Run(remaining));
                break;
            }

            if (earliest.Index > 0)
                inlines.Add(new Run(remaining[..earliest.Index]));

            var content = earliest.Groups[1].Value;
            if (isBold)
            {
                inlines.Add(new Run(content) { FontWeight = FontWeights.Bold });
            }
            else
            {
                inlines.Add(new Run(content) { FontStyle = FontStyles.Italic });
            }

            remaining = remaining[(earliest.Index + earliest.Length)..];
        }
    }

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"\*(.+?)\*")]
    private static partial Regex ItalicRegex();
}
