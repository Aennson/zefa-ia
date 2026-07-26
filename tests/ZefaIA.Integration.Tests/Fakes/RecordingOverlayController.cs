using ZefaIA.Core.Models;
using ZefaIA.Overlay;

namespace ZefaIA.Integration.Tests.Fakes;

/// <summary>
/// An <see cref="IOverlayController"/> that records what the pipeline told it to render.
/// The real controller owns a WPF window, which needs an STA thread and a desktop; this
/// keeps the assertions on *what would be shown* rather than on WPF itself (the real
/// controller has its own tests in ZefaIA.Overlay.Tests).
/// </summary>
public sealed class RecordingOverlayController : IOverlayController
{
    private IDisposable? _subscription;

    public bool IsVisible { get; private set; }
    public string MicSpeakerName { get; private set; } = "Eu";
    public string LoopbackSpeakerName { get; private set; } = "Interlocutor";

    /// <summary>Every segment the overlay was asked to render, finals and partials.</summary>
    public List<TranscriptionSegment> RenderedSegments { get; } = new();

    /// <summary>Suggestion tokens in arrival order — the overlay's streaming view.</summary>
    public List<string> SuggestionTokens { get; } = new();

    public int ThinkingShownCount { get; private set; }
    public int SuggestionsFinalized { get; private set; }
    public bool Disposed { get; private set; }

    /// <summary>The suggestion text as the user would see it after streaming.</summary>
    public string RenderedSuggestion => string.Concat(SuggestionTokens);

    public void Show() => IsVisible = true;
    public void Hide() => IsVisible = false;
    public void Toggle() => IsVisible = !IsVisible;

    public void SetSpeakerNames(string micName, string loopbackName)
    {
        MicSpeakerName = micName;
        LoopbackSpeakerName = loopbackName;
    }

    public void SubscribeToTranscription(IObservable<TranscriptionSegmentEventArgs> stream)
    {
        _subscription?.Dispose();
        _subscription = stream.Subscribe(args =>
        {
            lock (RenderedSegments)
                RenderedSegments.Add(args.Segment);
        });
    }

    public void ShowSuggestionThinking() => ThinkingShownCount++;

    public void AppendSuggestionText(string text)
    {
        lock (SuggestionTokens)
            SuggestionTokens.Add(text);
    }

    public void FinalizeSuggestion() => SuggestionsFinalized++;

    public void Dispose()
    {
        Disposed = true;
        _subscription?.Dispose();
    }
}
