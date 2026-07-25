using ZefaIA.Core.Models;

namespace ZefaIA.Core.Interfaces;

public interface IAudioSource : IDisposable
{
    string SourceId { get; }
    string DisplayName { get; }
    AudioSourceType Type { get; }

    event EventHandler<AudioChunkEventArgs> AudioChunkReceived;
    event EventHandler<AudioSourceStateEventArgs> StateChanged;

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();
}
