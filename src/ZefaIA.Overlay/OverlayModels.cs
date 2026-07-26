using System.Windows;
using System.Windows.Media;

namespace ZefaIA.Overlay;

public class TranscriptionDisplayItem
{
    public string Text { get; init; } = "";
    public string SpeakerName { get; init; } = "";
    public Brush SpeakerColor { get; init; } = Brushes.White;
    public Brush TextColor { get; init; } = Brushes.White;
    public FontStyle FontStyle { get; init; } = FontStyles.Normal;
    public string Timestamp { get; init; } = "";
    public bool IsFinal { get; init; }
}

public class SuggestionDisplayItem
{
    public string Text { get; init; } = "";
    public string Timestamp { get; init; } = "";
}

public class OverlaySettings
{
    public double Opacity { get; set; } = 0.85;
    public double FontSize { get; set; } = 14;
    public OverlayPosition Position { get; set; } = OverlayPosition.BottomRight;
    public int AutoHideSeconds { get; set; } = 30;
    public bool ExcludeFromCapture { get; set; } = true;
}

public enum OverlayPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Center
}
