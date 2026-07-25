using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.Audio;

public class AudioCaptureEngine : IDisposable
{
    private readonly ILogger<AudioCaptureEngine>? _logger;
    private readonly Subject<AudioChunkEventArgs> _audioSubject = new();
    private readonly List<IAudioSource> _sources = [];
    private bool _running;
    private bool _disposed;

    public IObservable<AudioChunkEventArgs> AudioStream => _audioSubject.AsObservable();
    public IReadOnlyList<IAudioSource> ActiveSources => _sources.AsReadOnly();
    public bool IsRunning => _running;

    public AudioCaptureEngine(ILogger<AudioCaptureEngine>? logger = null)
    {
        _logger = logger;
    }

    public void AddSource(IAudioSource source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_running) throw new InvalidOperationException("Cannot add sources while running");

        source.AudioChunkReceived += OnAudioChunkReceived;
        source.StateChanged += OnSourceStateChanged;
        _sources.Add(source);

        _logger?.LogInformation("Audio source added: {SourceId} ({Type})", source.SourceId, source.Type);
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_running) return;
        if (_sources.Count == 0) throw new InvalidOperationException("No audio sources configured");

        _logger?.LogInformation("Starting audio capture with {Count} sources", _sources.Count);

        var startTasks = _sources.Select(s => StartSourceSafe(s, ct));
        await Task.WhenAll(startTasks);

        _running = true;
        _logger?.LogInformation("Audio capture started");
    }

    public async Task StopAsync()
    {
        if (!_running) return;

        _logger?.LogInformation("Stopping audio capture");

        var stopTasks = _sources.Select(StopSourceSafe);
        await Task.WhenAll(stopTasks);

        _running = false;
        _logger?.LogInformation("Audio capture stopped");
    }

    private async Task StartSourceSafe(IAudioSource source, CancellationToken ct)
    {
        try
        {
            await source.StartAsync(ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start source {SourceId}", source.SourceId);
        }
    }

    private async Task StopSourceSafe(IAudioSource source)
    {
        try
        {
            await source.StopAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to stop source {SourceId}", source.SourceId);
        }
    }

    private void OnAudioChunkReceived(object? sender, AudioChunkEventArgs e)
    {
        if (!_disposed)
            _audioSubject.OnNext(e);
    }

    private void OnSourceStateChanged(object? sender, AudioSourceStateEventArgs e)
    {
        _logger?.LogInformation("Source {Source} state: {State}{Error}",
            e.Source, e.State, e.ErrorMessage is not null ? $" - {e.ErrorMessage}" : "");

        if (e.State == AudioSourceState.Error)
            _logger?.LogWarning("Audio source {Source} entered error state: {Error}", e.Source, e.ErrorMessage);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var source in _sources)
        {
            source.AudioChunkReceived -= OnAudioChunkReceived;
            source.StateChanged -= OnSourceStateChanged;
            source.Dispose();
        }
        _sources.Clear();

        _audioSubject.OnCompleted();
        _audioSubject.Dispose();

        GC.SuppressFinalize(this);
    }
}
