using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.Audio;

public class LoopbackSource : IAudioSource
{
    private WasapiLoopbackCapture? _capture;
    private readonly Stopwatch _sessionClock = new();
    private WaveFormat? _captureFormat;
    private bool _disposed;

    public string SourceId { get; }
    public string DisplayName { get; }
    public AudioSourceType Type => AudioSourceType.Loopback;

    public event EventHandler<AudioChunkEventArgs>? AudioChunkReceived;
    public event EventHandler<AudioSourceStateEventArgs>? StateChanged;

    public LoopbackSource(string? deviceId = null, string? displayName = null)
    {
        SourceId = $"loopback-{deviceId ?? "default"}";
        DisplayName = displayName ?? GetDefaultDeviceName();
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        RaiseStateChanged(AudioSourceState.Starting);

        _capture = new WasapiLoopbackCapture();
        _captureFormat = _capture.WaveFormat;

        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;

        _sessionClock.Restart();
        _capture.StartRecording();

        RaiseStateChanged(AudioSourceState.Capturing);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_capture is null) return Task.CompletedTask;

        RaiseStateChanged(AudioSourceState.Stopping);
        _capture.StopRecording();
        _sessionClock.Stop();
        return Task.CompletedTask;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0 || _captureFormat is null) return;

        var rawData = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, rawData, 0, e.BytesRecorded);

        var pcmData = Resampler.ResampleToTarget(
            rawData,
            _captureFormat.SampleRate,
            _captureFormat.Channels,
            _captureFormat.BitsPerSample
        );

        AudioChunkReceived?.Invoke(this, new AudioChunkEventArgs(
            pcmData,
            Resampler.TargetSampleRate,
            _sessionClock.Elapsed,
            AudioSourceType.Loopback
        ));
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            RaiseStateChanged(AudioSourceState.Error, e.Exception.Message);
            return;
        }
        RaiseStateChanged(AudioSourceState.Stopped);
    }

    private void RaiseStateChanged(AudioSourceState state, string? error = null)
    {
        StateChanged?.Invoke(this, new AudioSourceStateEventArgs(AudioSourceType.Loopback, state, error));
    }

    private static string GetDefaultDeviceName()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return $"Loopback: {device.FriendlyName}";
        }
        catch
        {
            return "System Audio (Loopback)";
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_capture is not null)
        {
            _capture.StopRecording();
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            _capture.Dispose();
            _capture = null;
        }

        GC.SuppressFinalize(this);
    }
}
