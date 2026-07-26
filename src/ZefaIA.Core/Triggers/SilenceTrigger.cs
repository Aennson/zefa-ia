using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.Core.Triggers;

public sealed class SilenceTrigger : ITriggerStrategy
{
    private readonly SilenceTriggerConfig _config;
    private IDisposable? _subscription;
    private DateTime _silenceStart = DateTime.MaxValue;
    private DateTime _lastTriggerTime = DateTime.MinValue;
    private DateTime _lastTranscriptionTime = DateTime.MinValue;
    private bool _isSilent;
    private bool _disposed;

    public string TriggerName => "SilenceTrigger";
    public event EventHandler<TriggerEventArgs>? Triggered;

    public SilenceTrigger(SilenceTriggerConfig? config = null)
    {
        _config = config ?? new SilenceTriggerConfig();
    }

    public Task StartMonitoringAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public void SubscribeToAudio(IObservable<AudioChunkEventArgs> loopbackStream)
    {
        _subscription?.Dispose();
        _subscription = loopbackStream.Subscribe(OnAudioChunk);
    }

    public void NotifyTranscriptionReceived()
    {
        _lastTranscriptionTime = DateTime.UtcNow;
    }

    public Task StopMonitoringAsync()
    {
        _subscription?.Dispose();
        _subscription = null;
        return Task.CompletedTask;
    }

    internal void OnAudioChunk(AudioChunkEventArgs args)
    {
        var rms = CalculateRMS(args.PcmData);
        var now = DateTime.UtcNow;

        if (rms < _config.SilenceThresholdRMS)
        {
            if (!_isSilent)
            {
                _isSilent = true;
                _silenceStart = now;
            }
            else
            {
                var silenceDuration = now - _silenceStart;
                if (silenceDuration >= _config.SilenceDuration && ShouldTrigger(now))
                {
                    Fire(now);
                }
            }
        }
        else
        {
            _isSilent = false;
            _silenceStart = DateTime.MaxValue;
        }
    }

    private bool ShouldTrigger(DateTime now)
    {
        var cooldownElapsed = (now - _lastTriggerTime) >= _config.Cooldown;
        var hasRecentTranscription = (now - _lastTranscriptionTime) <= _config.TranscriptRecencyWindow;
        return cooldownElapsed && hasRecentTranscription;
    }

    private void Fire(DateTime now)
    {
        _lastTriggerTime = now;
        _isSilent = false;
        _silenceStart = DateTime.MaxValue;

        Triggered?.Invoke(this, new TriggerEventArgs(
            TriggerName,
            TriggerReason.Silence,
            _config.TranscriptWindow,
            now));
    }

    internal static double CalculateRMS(byte[] pcm16Data)
    {
        if (pcm16Data.Length < 2) return 0;

        double sumSquares = 0;
        int sampleCount = pcm16Data.Length / 2;

        for (int i = 0; i < pcm16Data.Length - 1; i += 2)
        {
            short sample = (short)(pcm16Data[i] | (pcm16Data[i + 1] << 8));
            double normalized = sample / 32768.0;
            sumSquares += normalized * normalized;
        }

        return Math.Sqrt(sumSquares / sampleCount);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _subscription?.Dispose();
    }
}

public record SilenceTriggerConfig
{
    public double SilenceThresholdRMS { get; init; } = 0.01;
    public TimeSpan SilenceDuration { get; init; } = TimeSpan.FromMilliseconds(1500);
    public TimeSpan Cooldown { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan TranscriptRecencyWindow { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan TranscriptWindow { get; init; } = TimeSpan.FromSeconds(60);
}
