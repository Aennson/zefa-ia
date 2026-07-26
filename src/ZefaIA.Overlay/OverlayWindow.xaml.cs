using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ZefaIA.Overlay;

public partial class OverlayWindow : Window
{
    private IntPtr _hwnd;
    private bool _isPinned;
    private bool _isExcludedFromCapture = true;
    private bool _clickThroughEnabled = true;
    private readonly DispatcherTimer? _autoHideTimer;
    private readonly ObservableCollection<TranscriptionDisplayItem> _transcriptionItems = new();
    private readonly ObservableCollection<SuggestionDisplayItem> _suggestionItems = new();

    public event EventHandler? CopyRequested;
    public event EventHandler? DismissRequested;
    public event EventHandler<bool>? PinToggled;

    private const int MaxVisibleSegments = 100;

    public OverlayWindow()
    {
        InitializeComponent();
        TranscriptionList.ItemsSource = _transcriptionItems;
        SuggestionHistory.ItemsSource = _suggestionItems;

        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _autoHideTimer.Tick += (_, _) =>
        {
            if (!_isPinned)
                HideContent();
        };

        Loaded += OnLoaded;
    }

    public OverlaySettings Settings { get; set; } = new();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        if (_clickThroughEnabled)
            NativeMethods.MakeClickThrough(_hwnd);

        if (_isExcludedFromCapture)
            NativeMethods.ExcludeFromCapture(_hwnd);

        ApplySettings();
        PositionWindow();
    }

    public void ApplySettings()
    {
        RootBorder.Opacity = Settings.Opacity;
        UpdateFontSize(Settings.FontSize);

        if (Settings.AutoHideSeconds > 0)
        {
            _autoHideTimer!.Interval = TimeSpan.FromSeconds(Settings.AutoHideSeconds);
            _autoHideTimer.Start();
        }
    }

    private void PositionWindow()
    {
        var screen = SystemParameters.WorkArea;

        switch (Settings.Position)
        {
            case OverlayPosition.TopLeft:
                Left = 20; Top = 20;
                break;
            case OverlayPosition.TopRight:
                Left = screen.Width - Width - 20; Top = 20;
                break;
            case OverlayPosition.BottomLeft:
                Left = 20; Top = screen.Height - Height - 20;
                break;
            case OverlayPosition.BottomRight:
            default:
                Left = screen.Width - Width - 20;
                Top = screen.Height - Height - 20;
                break;
            case OverlayPosition.Center:
                Left = (screen.Width - Width) / 2;
                Top = (screen.Height - Height) / 2;
                break;
        }
    }

    #region Public API

    public void AddTranscriptionSegment(string text, string speakerName, bool isMic, bool isFinal, TimeSpan timestamp)
    {
        Dispatcher.Invoke(() =>
        {
            var item = new TranscriptionDisplayItem
            {
                Text = text,
                SpeakerName = $"[{speakerName}]",
                SpeakerColor = isMic
                    ? new SolidColorBrush(Color.FromRgb(96, 165, 250))   // blue-400
                    : new SolidColorBrush(Color.FromRgb(52, 211, 153)),  // emerald-400
                TextColor = isFinal
                    ? new SolidColorBrush(Color.FromRgb(238, 238, 255))
                    : new SolidColorBrush(Color.FromArgb(153, 238, 238, 255)),
                FontStyle = isFinal ? FontStyles.Normal : FontStyles.Italic,
                Timestamp = timestamp.ToString(@"mm\:ss"),
                IsFinal = isFinal
            };

            if (_transcriptionItems.Count >= MaxVisibleSegments)
                _transcriptionItems.RemoveAt(0);

            _transcriptionItems.Add(item);
            ScrollToBottom();
            ResetAutoHide();
        });
    }

    public void ShowThinking()
    {
        Dispatcher.Invoke(() =>
        {
            ThinkingIndicator.Visibility = Visibility.Visible;
            CurrentSuggestionBorder.Visibility = Visibility.Collapsed;
            CurrentSuggestionText.Text = "";

            if (TabSuggestions.IsChecked != true)
                TabSuggestions.IsChecked = true;
        });
    }

    public void AppendSuggestionText(string text)
    {
        Dispatcher.Invoke(() =>
        {
            ThinkingIndicator.Visibility = Visibility.Collapsed;
            CurrentSuggestionBorder.Visibility = Visibility.Visible;
            CurrentSuggestionText.Text += text;
            ResetAutoHide();
        });
    }

    public void FinalizeSuggestion()
    {
        Dispatcher.Invoke(() =>
        {
            ThinkingIndicator.Visibility = Visibility.Collapsed;
            var text = CurrentSuggestionText.Text;

            if (!string.IsNullOrWhiteSpace(text))
            {
                _suggestionItems.Insert(0, new SuggestionDisplayItem
                {
                    Text = text,
                    Timestamp = DateTime.Now.ToString("HH:mm:ss")
                });
            }

            CurrentSuggestionText.Text = "";
            CurrentSuggestionBorder.Visibility = Visibility.Collapsed;
        });
    }

    public void SetExcludeFromCapture(bool exclude)
    {
        _isExcludedFromCapture = exclude;

        if (_hwnd == IntPtr.Zero) return;

        if (exclude)
            NativeMethods.ExcludeFromCapture(_hwnd);
        else
            NativeMethods.IncludeInCapture(_hwnd);
    }

    public void SetClickThrough(bool enabled)
    {
        _clickThroughEnabled = enabled;

        if (_hwnd == IntPtr.Zero) return;

        if (enabled)
            NativeMethods.MakeClickThrough(_hwnd);
        else
            NativeMethods.RemoveClickThrough(_hwnd);
    }

    public string GetVisibleText()
    {
        if (TabSuggestions.IsChecked == true)
        {
            var suggestion = CurrentSuggestionText.Text;
            return !string.IsNullOrWhiteSpace(suggestion)
                ? suggestion
                : string.Join(Environment.NewLine, _suggestionItems.Select(s => s.Text));
        }

        return string.Join(Environment.NewLine,
            _transcriptionItems.Select(t => $"{t.Timestamp} {t.SpeakerName} {t.Text}"));
    }

    #endregion

    #region Event Handlers

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_hwnd != IntPtr.Zero)
            NativeMethods.RemoveClickThrough(_hwnd);

        DragMove();

        if (_clickThroughEnabled && _hwnd != IntPtr.Zero)
            NativeMethods.MakeClickThrough(_hwnd);
    }

    private void BtnPin_Click(object sender, RoutedEventArgs e)
    {
        _isPinned = !_isPinned;
        BtnPin.Opacity = _isPinned ? 1.0 : 0.6;
        BtnPin.ToolTip = _isPinned ? "Desafixar" : "Fixar";

        if (_isPinned)
            _autoHideTimer?.Stop();
        else
            ResetAutoHide();

        PinToggled?.Invoke(this, _isPinned);
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        var text = GetVisibleText();
        if (!string.IsNullOrWhiteSpace(text))
        {
            try { Clipboard.SetText(text); }
            catch { }
        }

        CopyRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BtnDismiss_Click(object sender, RoutedEventArgs e)
    {
        HideContent();
        DismissRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (TranscriptionScroller == null || SuggestionsPanel == null) return;

        if (TabTranscription.IsChecked == true)
        {
            TranscriptionScroller.Visibility = Visibility.Visible;
            SuggestionsPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            TranscriptionScroller.Visibility = Visibility.Collapsed;
            SuggestionsPanel.Visibility = Visibility.Visible;
        }
    }

    #endregion

    #region Helpers

    private void ScrollToBottom()
    {
        TranscriptionScroller.ScrollToEnd();
    }

    private void HideContent()
    {
        _transcriptionItems.Clear();
        CurrentSuggestionText.Text = "";
        CurrentSuggestionBorder.Visibility = Visibility.Collapsed;
        ThinkingIndicator.Visibility = Visibility.Collapsed;
    }

    private void ResetAutoHide()
    {
        if (_isPinned) return;

        _autoHideTimer?.Stop();
        _autoHideTimer?.Start();
    }

    private void UpdateFontSize(double size)
    {
        FontSize = size;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _autoHideTimer?.Stop();
        base.OnClosing(e);
    }

    #endregion
}
