using System.Windows;
using ZefaIA.Core.Models;

namespace ZefaIA.Overlay;

public class OverlayController : IOverlayController
{
    private readonly OverlayWindow _window;
    private IDisposable? _transcriptionSubscription;
    private bool _disposed;

    private string _micSpeakerName = "Eu";
    private string _loopbackSpeakerName = "Interlocutor";

    public OverlayWindow Window => _window;

    public OverlayController(OverlaySettings? settings = null)
    {
        _window = new OverlayWindow
        {
            Settings = settings ?? new OverlaySettings()
        };
    }

    public void Show()
    {
        _window.Show();
    }

    public void Hide()
    {
        _window.Hide();
    }

    public void Toggle()
    {
        if (_window.IsVisible)
            _window.Hide();
        else
            _window.Show();
    }

    public void SetSpeakerNames(string micName, string loopbackName)
    {
        _micSpeakerName = micName;
        _loopbackSpeakerName = loopbackName;
    }

    public void SubscribeToTranscription(IObservable<TranscriptionSegmentEventArgs> stream)
    {
        _transcriptionSubscription?.Dispose();

        _transcriptionSubscription = stream.Subscribe(
            args => OnTranscriptionSegment(args),
            ex => System.Diagnostics.Debug.WriteLine($"[Overlay] Transcription error: {ex.Message}"));
    }

    private void OnTranscriptionSegment(TranscriptionSegmentEventArgs args)
    {
        var segment = args.Segment;
        var isMic = segment.Source == AudioSourceType.Microphone;
        var speakerName = isMic ? _micSpeakerName : _loopbackSpeakerName;

        _window.AddTranscriptionSegment(
            segment.Text,
            speakerName,
            isMic,
            segment.IsFinal,
            segment.StartTime);
    }

    public void ShowSuggestionThinking()
    {
        _window.ShowThinking();
    }

    public void AppendSuggestionText(string text)
    {
        _window.AppendSuggestionText(text);
    }

    public void FinalizeSuggestion()
    {
        _window.FinalizeSuggestion();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _transcriptionSubscription?.Dispose();
        _window.Close();

        GC.SuppressFinalize(this);
    }
}
