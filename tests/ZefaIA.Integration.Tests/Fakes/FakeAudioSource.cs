using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.Integration.Tests.Fakes;

/// <summary>
/// An <see cref="IAudioSource"/> the test drives by hand. Stands in for WASAPI so the
/// pipeline can be fed exact PCM at exact timestamps: real capture would make the
/// assertions depend on ambient noise and on the machine having a microphone.
/// Everything downstream of this class is production code.
/// </summary>
public sealed class FakeAudioSource : IAudioSource
{
    private bool _running;
    private bool _disposed;

    public string SourceId { get; }
    public string DisplayName { get; }
    public AudioSourceType Type { get; }

    /// <summary>Chunks emitted so far, for asserting what the pipeline consumed.</summary>
    public int EmittedChunks { get; private set; }

    public event EventHandler<AudioChunkEventArgs>? AudioChunkReceived;
    public event EventHandler<AudioSourceStateEventArgs>? StateChanged;

    public FakeAudioSource(AudioSourceType type)
    {
        Type = type;
        SourceId = $"fake-{type}".ToLowerInvariant();
        DisplayName = $"Fake {type}";
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StateChanged?.Invoke(this, new AudioSourceStateEventArgs(Type, AudioSourceState.Starting));
        _running = true;
        StateChanged?.Invoke(this, new AudioSourceStateEventArgs(Type, AudioSourceState.Capturing));
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_running)
        {
            _running = false;
            StateChanged?.Invoke(this, new AudioSourceStateEventArgs(Type, AudioSourceState.Stopped));
        }
        return Task.CompletedTask;
    }

    /// <summary>Pushes one chunk through as if the device had captured it.</summary>
    public void Emit(byte[] pcm16, TimeSpan timestamp, int sampleRate = 16000)
    {
        if (!_running)
            throw new InvalidOperationException("Emit called on a source that is not capturing.");

        EmittedChunks++;
        AudioChunkReceived?.Invoke(this, new AudioChunkEventArgs(pcm16, sampleRate, timestamp, Type));
    }

    public void Dispose()
    {
        _disposed = true;
        _running = false;
    }
}
