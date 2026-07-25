using System.Diagnostics;
using NAudio.Wave;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.Audio;

public class MicrophoneSource : IAudioSource
{
    private readonly int _deviceIndex;
    private WaveInEvent? _waveIn;
    private readonly Stopwatch _sessionClock = new();
    private bool _disposed;

    public string SourceId { get; }
    public string DisplayName { get; }
    public AudioSourceType Type => AudioSourceType.Microphone;

    public event EventHandler<AudioChunkEventArgs>? AudioChunkReceived;
    public event EventHandler<AudioSourceStateEventArgs>? StateChanged;

    public MicrophoneSource(int deviceIndex = -1, string? displayName = null)
    {
        _deviceIndex = deviceIndex < 0 ? GetDefaultDeviceIndex() : deviceIndex;
        DisplayName = displayName ?? GetDeviceName(_deviceIndex);
        SourceId = $"mic-{_deviceIndex}";
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        RaiseStateChanged(AudioSourceState.Starting);

        _waveIn = new WaveInEvent
        {
            DeviceNumber = _deviceIndex,
            WaveFormat = new WaveFormat(Resampler.TargetSampleRate, Resampler.TargetBitsPerSample, Resampler.TargetChannels),
            BufferMilliseconds = 100
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;

        _sessionClock.Restart();
        _waveIn.StartRecording();

        RaiseStateChanged(AudioSourceState.Capturing);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_waveIn is null) return Task.CompletedTask;

        RaiseStateChanged(AudioSourceState.Stopping);
        _waveIn.StopRecording();
        _sessionClock.Stop();
        return Task.CompletedTask;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;

        var pcmData = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, pcmData, 0, e.BytesRecorded);

        AudioChunkReceived?.Invoke(this, new AudioChunkEventArgs(
            pcmData,
            Resampler.TargetSampleRate,
            _sessionClock.Elapsed,
            AudioSourceType.Microphone
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
        StateChanged?.Invoke(this, new AudioSourceStateEventArgs(AudioSourceType.Microphone, state, error));
    }

    private static int GetDefaultDeviceIndex()
    {
        return WaveInEvent.DeviceCount > 0 ? 0 : throw new InvalidOperationException("No microphone found");
    }

    private static string GetDeviceName(int index)
    {
        if (index < 0 || index >= WaveInEvent.DeviceCount)
            return "Unknown Microphone";
        return WaveInEvent.GetCapabilities(index).ProductName;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _waveIn?.StopRecording();
        if (_waveIn is not null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;
        }

        GC.SuppressFinalize(this);
    }
}
