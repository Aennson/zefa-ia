using System.Collections.Concurrent;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.STT;

public sealed class WhisperSTTProvider : ISTTProvider
{
    private WhisperNet.WhisperProcessor? _processor;
    private WhisperNet.WhisperFactory? _factory;
    private readonly ConcurrentQueue<byte[]> _audioBuffer = new();
    private int _bufferedBytes;
    private int _bufferThresholdBytes;
    private CancellationTokenSource? _processingCts;
    private Task? _processingTask;
    private bool _initialized;
    private bool _disposed;
    private STTProviderConfig _config = null!;
    private TimeSpan _sessionOffset;

    private const int TargetSampleRate = 16000;
    private const int BytesPerSample = 2; // int16
    private const int DefaultBufferMs = 2500;
    private const float NoSpeechThreshold = 0.6f;

    public string ProviderId => "whisper-local";
    public STTProviderType Type => STTProviderType.WhisperLocal;
    public IReadOnlyList<string> SupportedLanguages => new[] { "auto", "pt", "en", "es", "fr", "de", "it", "ja", "zh" };

    public event EventHandler<TranscriptionSegmentEventArgs>? SegmentReceived;
    public event EventHandler<TranscriptionSegmentEventArgs>? PartialReceived;

    public async Task InitializeAsync(STTProviderConfig config, CancellationToken ct = default)
    {
        if (_initialized)
            throw new InvalidOperationException("Provider already initialized.");

        _config = config;

        var modelSize = config.Options.GetValueOrDefault("ModelSize", "base");
        var modelPath = config.Options.GetValueOrDefault("ModelPath", "./models");
        var useGpu = bool.TryParse(config.Options.GetValueOrDefault("UseGPU", "false"), out var gpu) && gpu;

        var bufferMs = int.TryParse(config.Options.GetValueOrDefault("BufferMs", DefaultBufferMs.ToString()), out var bms) ? bms : DefaultBufferMs;
        _bufferThresholdBytes = TargetSampleRate * BytesPerSample * bufferMs / 1000;

        var modelFile = await EnsureModelAsync(modelPath, modelSize, ct);

        _factory = WhisperNet.WhisperFactory.FromPath(modelFile);

        var builder = _factory.CreateBuilder()
            .WithLanguage(config.Language == "auto" ? "auto" : config.Language ?? "auto")
            .WithNoSpeechThreshold(NoSpeechThreshold)
            .WithThreads(Math.Max(1, Environment.ProcessorCount / 2));

        if (!useGpu)
            builder.WithNoGPU();

        _processor = builder.Build();
        _initialized = true;

        _processingCts = new CancellationTokenSource();
        _processingTask = Task.Run(() => ProcessingLoop(_processingCts.Token), _processingCts.Token);
    }

    public Task ProcessAudioAsync(AudioChunkEventArgs chunk, CancellationToken ct = default)
    {
        ThrowIfNotInitialized();

        if (_sessionOffset == TimeSpan.Zero && chunk.Timestamp > TimeSpan.Zero)
            _sessionOffset = chunk.Timestamp;

        _audioBuffer.Enqueue(chunk.PcmData);
        Interlocked.Add(ref _bufferedBytes, chunk.PcmData.Length);

        return Task.CompletedTask;
    }

    public async Task FlushAsync()
    {
        ThrowIfNotInitialized();

        if (_bufferedBytes > 0)
            await ProcessBufferedAudio();
    }

    private async Task ProcessingLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_bufferedBytes >= _bufferThresholdBytes)
                {
                    await ProcessBufferedAudio();
                }
                else
                {
                    await Task.Delay(100, ct);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WhisperSTT] Processing error: {ex.Message}");
                await Task.Delay(500, ct);
            }
        }
    }

    private async Task ProcessBufferedAudio()
    {
        var chunks = new List<byte[]>();
        var totalBytes = 0;

        while (_audioBuffer.TryDequeue(out var chunk))
        {
            chunks.Add(chunk);
            totalBytes += chunk.Length;
        }

        Interlocked.Exchange(ref _bufferedBytes, 0);

        if (totalBytes == 0)
            return;

        var pcmData = CombineChunks(chunks, totalBytes);
        var samples = ConvertToFloat(pcmData);

        if (IsSilence(samples))
            return;

        var processStart = DateTime.UtcNow;

        await foreach (var result in _processor!.ProcessAsync(samples))
        {
            var segment = new TranscriptionSegment(
                Text: result.Text.Trim(),
                Language: result.Language ?? _config.Language ?? "unknown",
                Confidence: result.Probability,
                StartTime: _sessionOffset + result.Start,
                EndTime: _sessionOffset + result.End,
                Source: AudioSourceType.Microphone,
                IsFinal: true
            );

            if (string.IsNullOrWhiteSpace(segment.Text))
                continue;

            var args = new TranscriptionSegmentEventArgs(segment, DateTime.UtcNow);
            SegmentReceived?.Invoke(this, args);
        }
    }

    private static byte[] CombineChunks(List<byte[]> chunks, int totalBytes)
    {
        var combined = new byte[totalBytes];
        var offset = 0;
        foreach (var chunk in chunks)
        {
            Buffer.BlockCopy(chunk, 0, combined, offset, chunk.Length);
            offset += chunk.Length;
        }
        return combined;
    }

    private static float[] ConvertToFloat(byte[] pcmData)
    {
        var sampleCount = pcmData.Length / BytesPerSample;
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BitConverter.ToInt16(pcmData, i * BytesPerSample);
            samples[i] = sample / 32768f;
        }
        return samples;
    }

    internal static bool IsSilence(float[] samples, float threshold = 0.01f)
    {
        if (samples.Length == 0)
            return true;

        double sumSquares = 0;
        foreach (var s in samples)
            sumSquares += s * s;

        var rms = Math.Sqrt(sumSquares / samples.Length);
        return rms < threshold;
    }

    private static async Task<string> EnsureModelAsync(string modelPath, string modelSize, CancellationToken ct)
    {
        Directory.CreateDirectory(modelPath);

        var modelFile = Path.Combine(modelPath, $"ggml-{modelSize}.bin");

        if (File.Exists(modelFile))
            return modelFile;

        using var downloader = new WhisperNet.Ggml.WhisperGgmlDownloader();
        var modelType = modelSize switch
        {
            "tiny" => WhisperNet.Ggml.GgmlType.Tiny,
            "base" => WhisperNet.Ggml.GgmlType.Base,
            "small" => WhisperNet.Ggml.GgmlType.Small,
            "medium" => WhisperNet.Ggml.GgmlType.Medium,
            "large" => WhisperNet.Ggml.GgmlType.LargeV3,
            _ => WhisperNet.Ggml.GgmlType.Base
        };

        using var modelStream = await downloader.GetGgmlModelAsync(modelType, ct);
        using var fileStream = File.Create(modelFile);
        await modelStream.CopyToAsync(fileStream, ct);

        return modelFile;
    }

    private void ThrowIfNotInitialized()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
            throw new InvalidOperationException("Provider not initialized. Call InitializeAsync first.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _processingCts?.Cancel();
        if (_processingTask != null)
        {
            try { await _processingTask; }
            catch (OperationCanceledException) { }
        }

        _processingCts?.Dispose();
        _processor?.Dispose();
        _factory?.Dispose();
    }
}
