using Xunit;
using ZefaIA.Audio;

namespace ZefaIA.Audio.Tests;

public class EchoCancellerTests
{
    private static byte[] GenerateSineWave(float frequency, float amplitude, int sampleRate, int durationMs)
    {
        int sampleCount = sampleRate * durationMs / 1000;
        var result = new byte[sampleCount * 2];
        for (int i = 0; i < sampleCount; i++)
        {
            var value = (short)(amplitude * short.MaxValue * Math.Sin(2 * Math.PI * frequency * i / sampleRate));
            BitConverter.TryWriteBytes(result.AsSpan(i * 2), value);
        }
        return result;
    }

    private static float CalculateRms(byte[] pcm)
    {
        int sampleCount = pcm.Length / 2;
        double sumSquares = 0;
        for (int i = 0; i < sampleCount; i++)
        {
            float sample = BitConverter.ToInt16(pcm, i * 2) / (float)short.MaxValue;
            sumSquares += sample * sample;
        }
        return (float)Math.Sqrt(sumSquares / sampleCount);
    }

    [Fact]
    public void Process_WithoutReference_ReturnsOriginal()
    {
        var aec = new EchoCanceller(sampleRate: 16000, filterLengthMs: 50);
        var mic = GenerateSineWave(440, 0.5f, 16000, 100);

        var result = aec.Process(mic);

        Assert.Equal(mic.Length, result.Length);
        aec.Dispose();
    }

    [Fact]
    public void Process_WithMatchingReference_ReducesEcho()
    {
        var aec = new EchoCanceller(sampleRate: 16000, filterLengthMs: 50, stepSize: 0.05f);

        var reference = GenerateSineWave(440, 0.8f, 16000, 100);
        var echoInMic = GenerateSineWave(440, 0.3f, 16000, 100);

        aec.FeedReference(reference);
        aec.FeedReference(reference);
        aec.FeedReference(reference);

        for (int i = 0; i < 5; i++)
        {
            aec.FeedReference(reference);
            aec.Process(echoInMic);
        }

        aec.FeedReference(reference);
        var processed = aec.Process(echoInMic);

        var originalRms = CalculateRms(echoInMic);
        var processedRms = CalculateRms(processed);

        Assert.True(processedRms < originalRms,
            $"Expected echo reduction: original RMS={originalRms:F4}, processed RMS={processedRms:F4}");

        aec.Dispose();
    }

    [Fact]
    public void Disabled_ReturnsOriginalData()
    {
        var aec = new EchoCanceller(sampleRate: 16000);
        aec.IsEnabled = false;

        var mic = GenerateSineWave(440, 0.5f, 16000, 100);
        var result = aec.Process(mic);

        Assert.Equal(mic, result);
        aec.Dispose();
    }

    [Fact]
    public void Reset_ClearsFilter()
    {
        var aec = new EchoCanceller(sampleRate: 16000, filterLengthMs: 50);
        var reference = GenerateSineWave(440, 0.8f, 16000, 100);

        aec.FeedReference(reference);
        aec.Process(reference);
        aec.Reset();

        var mic = GenerateSineWave(440, 0.5f, 16000, 100);
        var result = aec.Process(mic);
        Assert.Equal(mic.Length, result.Length);

        aec.Dispose();
    }

    [Fact]
    public void Process_DoesNotClip()
    {
        var aec = new EchoCanceller(sampleRate: 16000, filterLengthMs: 50);
        var loudSignal = GenerateSineWave(440, 0.99f, 16000, 100);

        aec.FeedReference(loudSignal);
        var result = aec.Process(loudSignal);

        int sampleCount = result.Length / 2;
        for (int i = 0; i < sampleCount; i++)
        {
            var sample = BitConverter.ToInt16(result, i * 2);
            Assert.InRange(sample, short.MinValue, short.MaxValue);
        }

        aec.Dispose();
    }

    [Fact]
    public void Dispose_MultipleCallsDoNotThrow()
    {
        var aec = new EchoCanceller();
        aec.Dispose();
        aec.Dispose();
    }
}
