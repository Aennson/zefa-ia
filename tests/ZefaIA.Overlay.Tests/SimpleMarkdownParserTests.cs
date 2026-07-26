using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;

namespace ZefaIA.Overlay.Tests;

public class SimpleMarkdownParserTests
{
    [WpfFact]
    public void Parse_PlainText_CreatesTextBlock()
    {
        var result = SimpleMarkdownParser.Parse("Hello world");

        Assert.IsType<TextBlock>(result);
        Assert.Equal(TextWrapping.Wrap, result.TextWrapping);
    }

    [WpfFact]
    public void Parse_BoldText_CreatesBoldRun()
    {
        var result = SimpleMarkdownParser.Parse("This is **bold** text");

        var runs = result.Inlines.OfType<Run>().ToList();
        Assert.True(runs.Count >= 3);

        var boldRun = runs.First(r => r.Text == "bold");
        Assert.Equal(FontWeights.Bold, boldRun.FontWeight);
    }

    [WpfFact]
    public void Parse_ItalicText_CreatesItalicRun()
    {
        var result = SimpleMarkdownParser.Parse("This is *italic* text");

        var runs = result.Inlines.OfType<Run>().ToList();
        var italicRun = runs.First(r => r.Text == "italic");
        Assert.Equal(FontStyles.Italic, italicRun.FontStyle);
    }

    [WpfFact]
    public void Parse_BulletList_AddsBulletPrefix()
    {
        var result = SimpleMarkdownParser.Parse("- Item one\n- Item two");

        var text = string.Join("", result.Inlines.OfType<Run>().Select(r => r.Text));
        Assert.Contains("•", text);
        Assert.Contains("Item one", text);
        Assert.Contains("Item two", text);
    }

    [WpfFact]
    public void Parse_Multiline_AddsLineBreaks()
    {
        var result = SimpleMarkdownParser.Parse("Line one\nLine two");

        var lineBreaks = result.Inlines.OfType<LineBreak>().ToList();
        Assert.Single(lineBreaks);
    }

    [WpfFact]
    public void Parse_EmptyString_ReturnsEmptyTextBlock()
    {
        var result = SimpleMarkdownParser.Parse("");

        Assert.NotNull(result);
    }

    [WpfFact]
    public void Parse_CustomFontSize_Applied()
    {
        var result = SimpleMarkdownParser.Parse("Text", fontSize: 20);

        Assert.Equal(20, result.FontSize);
    }
}
