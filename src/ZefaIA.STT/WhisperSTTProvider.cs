using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.STT;

public sealed class WhisperSTTProvider : ISTTProvider
{
    private Whisper.net.WhisperProcessor? _processor;
    private Whisper.net.WhisperFactory? _factory;
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

    // Whisper.net emits only finalized segments for a completed audio buffer, so this
    // provider never raises PartialReceived. It stays on the type to satisfy ISTTProvider.
#pragma warning disable CS0067
    public event EventHandler<TranscriptionSegmentEventArgs>? PartialReceived;
#pragma warning restore CS0067

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

        // GPU selection is a process-wide runtime setting in Whisper.net; there is no
        // per-builder toggle. It must be set before the factory loads the native library.
        Whisper.net.LibraryLoader.RuntimeOptions.Instance.SetUseGpu(useGpu);

        try
        {
            _factory = Whisper.net.WhisperFactory.FromPath(modelFile);
        }
        catch (Exception ex) when (ex.Message.Contains("native whisper library", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(DescribeNativeLoadFailure(), ex);
        }

        var builder = _factory.CreateBuilder()
            .WithLanguage(config.Language == "auto" ? "auto" : config.Language ?? "auto")
            .WithNoSpeechThreshold(NoSpeechThreshold)
            .WithThreads(Math.Max(1, Environment.ProcessorCount / 2));

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

    /// <summary>
    /// Explains why the native engine would not load. Whisper.net reports every cause as
    /// "Cannot load the library on this platform", and there are two very different ones:
    /// the DLL is missing (a packaging problem) or it is present but Windows refuses to
    /// load it (usually the VC++ runtime). Naming only the second sent a real user
    /// chasing a redistributable that was already installed, so check first.
    /// </summary>
    internal static string DescribeNativeLoadFailure()
    {
        var rid = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "win-arm64",
            Architecture.X86 => "win-x86",
            _ => "win-x64"
        };

        var expected = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "whisper.dll");

        if (!File.Exists(expected))
        {
            return "Nao foi possivel carregar o motor Whisper: a biblioteca nativa nao foi " +
                   $"encontrada em '{expected}'. Isso indica um problema de empacotamento â€” " +
                   "a pasta 'runtimes' precisa acompanhar o executavel.";
        }

        return "Nao foi possivel carregar o motor Whisper: a biblioteca nativa existe em " +
               $"'{expected}' mas o Windows recusou carrega-la. A causa mais comum e a falta " +
               "do Microsoft Visual C++ 2015-2022 Redistributable (x64): " +
               "winget install Microsoft.VCRedist.2015+.x64 " +
               "ou https://aka.ms/vs/17/release/vc_redist.x64.exe";
    }

    /// <summary>
    /// Turns the configured model directory into an absolute path that does not depend on
    /// the process working directory.
    ///
    /// "./models" used to resolve against the CWD, so launching the executable from
    /// another folder â€” <c>&amp; "C:\...\ZefaIA.App.exe"</c> from a home directory, for
    /// instance â€” silently re-downloaded 141 MB into that folder instead of finding the
    /// copy shipped next to the app.
    ///
    /// Relative paths now resolve next to the executable, falling back to per-user app
    /// data when that folder is read-only (an install under Program Files).
    /// </summary>
    internal static string ResolveModelDirectory(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            configuredPath = "./models";

        if (Path.IsPathRooted(configuredPath))
            return configuredPath;

        var besideExecutable = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, configuredPath));

        try
        {
            Directory.CreateDirectory(besideExecutable);

            // Creating the directory succeeds on a read-only parent in some cases, so
            // probe for write access the only way that is reliable: write something.
            var probe = Path.Combine(besideExecutable, $".write-probe-{Guid.NewGuid():N}");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);

            return besideExecutable;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            var perUser = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ZefaIA", "models");
            Directory.CreateDirectory(perUser);
            return perUser;
        }
    }

    private static async Task<string> EnsureModelAsync(string modelPath, string modelSize, CancellationToken ct)
    {
        modelPath = ResolveModelDirectory(modelPath);

        var modelFile = Path.Combine(modelPath, $"ggml-{modelSize}.bin");

        if (File.Exists(modelFile))
            return modelFile;

        var modelType = modelSize switch
        {
            "tiny" => Whisper.net.Ggml.GgmlType.Tiny,
            "base" => Whisper.net.Ggml.GgmlType.Base,
            "small" => Whisper.net.Ggml.GgmlType.Small,
            "medium" => Whisper.net.Ggml.GgmlType.Medium,
            "large" => Whisper.net.Ggml.GgmlType.LargeV3,
            _ => Whisper.net.Ggml.GgmlType.Base
        };

        using var modelStream = await Whisper.net.Ggml.WhisperGgmlDownloader
            .GetGgmlModelAsync(modelType, cancellationToken: ct);
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
