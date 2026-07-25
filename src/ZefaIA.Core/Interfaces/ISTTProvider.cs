using ZefaIA.Core.Models;

namespace ZefaIA.Core.Interfaces;

public interface ISTTProvider : IAsyncDisposable
{
    string ProviderId { get; }
    STTProviderType Type { get; }
    IReadOnlyList<string> SupportedLanguages { get; }

    event EventHandler<TranscriptionSegmentEventArgs> SegmentReceived;
    event EventHandler<TranscriptionSegmentEventArgs> PartialReceived;

    Task InitializeAsync(STTProviderConfig config, CancellationToken ct = default);
    Task ProcessAudioAsync(AudioChunkEventArgs chunk, CancellationToken ct = default);
    Task FlushAsync();
}
