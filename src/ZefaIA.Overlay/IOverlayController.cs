using ZefaIA.Core.Models;

namespace ZefaIA.Overlay;

/// <summary>
/// The overlay surface the meeting pipeline drives. Extracted from
/// <see cref="OverlayController"/> so the orchestrator depends on the behaviour
/// rather than on a WPF window: the real implementation needs an STA thread and a
/// desktop, which an end-to-end test of the pipeline should not require.
/// </summary>
public interface IOverlayController : IDisposable
{
    void Show();
    void Hide();
    void Toggle();

    /// <summary>Labels applied to the two audio sources once the language is known.</summary>
    void SetSpeakerNames(string micName, string loopbackName);

    void SubscribeToTranscription(IObservable<TranscriptionSegmentEventArgs> stream);

    void ShowSuggestionThinking();
    void AppendSuggestionText(string text);
    void FinalizeSuggestion();
}
