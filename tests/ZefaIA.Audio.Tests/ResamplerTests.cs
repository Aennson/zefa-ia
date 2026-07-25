using Xunit;
using ZefaIA.Audio;

namespace ZefaIA.Audio.Tests;

public class ResamplerTests
{
    [Fact]
    public void ResampleToTarget_SameFormat_ReturnsSameData()
    {
        var input = new byte[] { 0x00, 0x40, 0xFF, 0x3F };
        var result = Resampler.ResampleToTarget(input, 16000, 1, 16);
        Assert.Equal(input, result);
    }

    [Fact]
    public void ConvertFloat32ToInt16_ClampsCorrectly()
    {
        var floatBytes = new byte[4];
        BitConverter.TryWriteBytes(floatBytes, 0.5f);

        var result = Resampler.ConvertFloat32ToInt16(floatBytes);

        Assert.Equal(2, result.Length);
        var sample = BitConverter.ToInt16(result, 0);
        Assert.True(sample > 0);
    }

    [Fact]
    public void ConvertFloat32ToInt16_ClipsAtBounds()
    {
        var floatBytes = new byte[4];
        BitConverter.TryWriteBytes(floatBytes, 2.0f);

        var result = Resampler.ConvertFloat32ToInt16(floatBytes);
        var sample = BitConverter.ToInt16(result, 0);

        Assert.Equal(short.MaxValue, sample);
    }

    [Fact]
    public void ResampleToTarget_StereoToMono_HalvesSamples()
    {
        var stereo16k = new byte[16];
        for (int i = 0; i < 4; i++)
        {
            BitConverter.TryWriteBytes(stereo16k.AsSpan(i * 4), (short)(1000 * (i + 1)));
            BitConverter.TryWriteBytes(stereo16k.AsSpan(i * 4 + 2), (short)(1000 * (i + 1)));
        }

        var result = Resampler.ResampleToTarget(stereo16k, 16000, 2, 16);

        Assert.Equal(stereo16k.Length / 2, result.Length);
    }

    [Fact]
    public void ResampleToTarget_48kTo16k_ReducesSamples()
    {
        var sampleCount = 4800;
        var input = new byte[sampleCount * 2];
        for (int i = 0; i < sampleCount; i++)
        {
            var value = (short)(Math.Sin(2 * Math.PI * 440 * i / 48000) * short.MaxValue * 0.5);
            BitConverter.TryWriteBytes(input.AsSpan(i * 2), value);
        }

        var result = Resampler.ResampleToTarget(input, 48000, 1, 16);

        var expectedSamples = (int)(sampleCount * (16000.0 / 48000));
        var actualSamples = result.Length / 2;
        Assert.InRange(actualSamples, expectedSamples - 2, expectedSamples + 2);
    }

    [Fact]
    public void ResampleToTarget_Float32Input_Converts()
    {
        var floatSamples = 4;
        var input = new byte[floatSamples * 4];
        for (int i = 0; i < floatSamples; i++)
            BitConverter.TryWriteBytes(input.AsSpan(i * 4), 0.25f);

        var result = Resampler.ResampleToTarget(input, 16000, 1, 32);

        Assert.Equal(floatSamples * 2, result.Length);
        var sample = BitConverter.ToInt16(result, 0);
        Assert.True(sample > 0);
    }
}
