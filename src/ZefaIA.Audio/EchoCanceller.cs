using Microsoft.Extensions.Logging;

namespace ZefaIA.Audio;

public class EchoCanceller : IDisposable
{
    private readonly ILogger<EchoCanceller>? _logger;
    private readonly int _filterLength;
    private readonly float _stepSize;
    private readonly int _sampleRate;

    private float[] _adaptiveFilter;
    private readonly CircularBuffer _referenceBuffer;
    private bool _disposed;

    public bool IsEnabled { get; set; } = true;

    public EchoCanceller(
        int sampleRate = 16000,
        int filterLengthMs = 100,
        float stepSize = 0.01f,
        ILogger<EchoCanceller>? logger = null)
    {
        _sampleRate = sampleRate;
        _filterLength = sampleRate * filterLengthMs / 1000;
        _stepSize = stepSize;
        _logger = logger;

        _adaptiveFilter = new float[_filterLength];
        _referenceBuffer = new CircularBuffer(_filterLength * 2);

        _logger?.LogInformation("Echo canceller initialized: filter={FilterLen} samples, step={Step}",
            _filterLength, _stepSize);
    }

    public void FeedReference(byte[] loopbackPcm)
    {
        if (!IsEnabled) return;

        var samples = PcmToFloat(loopbackPcm);
        _referenceBuffer.Write(samples);
    }

    public byte[] Process(byte[] micPcm)
    {
        if (!IsEnabled) return micPcm;

        var micSamples = PcmToFloat(micPcm);
        var outputSamples = new float[micSamples.Length];

        for (int i = 0; i < micSamples.Length; i++)
        {
            var refSegment = _referenceBuffer.ReadSegment(_filterLength);
            if (refSegment is null)
            {
                outputSamples[i] = micSamples[i];
                continue;
            }

            float echoEstimate = 0;
            for (int j = 0; j < _filterLength && j < refSegment.Length; j++)
                echoEstimate += _adaptiveFilter[j] * refSegment[j];

            float error = micSamples[i] - echoEstimate;
            outputSamples[i] = error;

            float refPower = 0;
            for (int j = 0; j < _filterLength && j < refSegment.Length; j++)
                refPower += refSegment[j] * refSegment[j];

            if (refPower > 1e-10f)
            {
                float normalizedStep = _stepSize / (refPower + 1e-8f);
                for (int j = 0; j < _filterLength && j < refSegment.Length; j++)
                    _adaptiveFilter[j] += normalizedStep * error * refSegment[j];
            }

            _referenceBuffer.Advance(1);
        }

        return FloatToPcm(outputSamples);
    }

    public void Reset()
    {
        Array.Clear(_adaptiveFilter);
        _referenceBuffer.Clear();
        _logger?.LogInformation("Echo canceller reset");
    }

    private static float[] PcmToFloat(byte[] pcm)
    {
        var count = pcm.Length / 2;
        var result = new float[count];
        for (int i = 0; i < count; i++)
            result[i] = BitConverter.ToInt16(pcm, i * 2) / (float)short.MaxValue;
        return result;
    }

    private static byte[] FloatToPcm(float[] samples)
    {
        var result = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            var clamped = Math.Clamp(samples[i], -1.0f, 1.0f);
            BitConverter.TryWriteBytes(result.AsSpan(i * 2), (short)(clamped * short.MaxValue));
        }
        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

internal class CircularBuffer
{
    private readonly float[] _buffer;
    private int _writePos;
    private int _readPos;
    private int _count;

    public CircularBuffer(int capacity)
    {
        _buffer = new float[capacity];
    }

    public void Write(float[] data)
    {
        foreach (var sample in data)
        {
            _buffer[_writePos] = sample;
            _writePos = (_writePos + 1) % _buffer.Length;
            if (_count < _buffer.Length)
                _count++;
        }
    }

    public float[]? ReadSegment(int length)
    {
        if (_count < length) return null;

        var result = new float[length];
        int pos = _readPos;
        for (int i = 0; i < length; i++)
        {
            result[i] = _buffer[pos];
            pos = (pos + 1) % _buffer.Length;
        }
        return result;
    }

    public void Advance(int count)
    {
        _readPos = (_readPos + count) % _buffer.Length;
        _count = Math.Max(0, _count - count);
    }

    public void Clear()
    {
        Array.Clear(_buffer);
        _writePos = 0;
        _readPos = 0;
        _count = 0;
    }
}
