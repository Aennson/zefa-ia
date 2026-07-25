namespace ZefaIA.Audio;

public static class Resampler
{
    public const int TargetSampleRate = 16000;
    public const int TargetBitsPerSample = 16;
    public const int TargetChannels = 1;

    public static byte[] ResampleToTarget(byte[] input, int sourceSampleRate, int sourceChannels, int sourceBitsPerSample)
    {
        if (sourceSampleRate == TargetSampleRate && sourceChannels == TargetChannels && sourceBitsPerSample == TargetBitsPerSample)
            return input;

        var samples = ConvertToFloat(input, sourceBitsPerSample);

        if (sourceChannels > 1)
            samples = MixToMono(samples, sourceChannels);

        if (sourceSampleRate != TargetSampleRate)
            samples = ResampleLinear(samples, sourceSampleRate, TargetSampleRate);

        return ConvertToInt16Bytes(samples);
    }

    public static byte[] ConvertFloat32ToInt16(byte[] float32Data)
    {
        var sampleCount = float32Data.Length / 4;
        var result = new byte[sampleCount * 2];

        for (int i = 0; i < sampleCount; i++)
        {
            var sample = BitConverter.ToSingle(float32Data, i * 4);
            sample = Math.Clamp(sample, -1.0f, 1.0f);
            var int16Sample = (short)(sample * short.MaxValue);
            BitConverter.TryWriteBytes(result.AsSpan(i * 2), int16Sample);
        }

        return result;
    }

    private static float[] ConvertToFloat(byte[] input, int bitsPerSample)
    {
        return bitsPerSample switch
        {
            16 => ConvertInt16ToFloat(input),
            32 => ConvertFloat32ToFloatArray(input),
            _ => throw new NotSupportedException($"Unsupported bits per sample: {bitsPerSample}")
        };
    }

    private static float[] ConvertInt16ToFloat(byte[] input)
    {
        var sampleCount = input.Length / 2;
        var result = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            result[i] = BitConverter.ToInt16(input, i * 2) / (float)short.MaxValue;
        }
        return result;
    }

    private static float[] ConvertFloat32ToFloatArray(byte[] input)
    {
        var sampleCount = input.Length / 4;
        var result = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            result[i] = BitConverter.ToSingle(input, i * 4);
        }
        return result;
    }

    private static float[] MixToMono(float[] samples, int channels)
    {
        var monoCount = samples.Length / channels;
        var result = new float[monoCount];
        for (int i = 0; i < monoCount; i++)
        {
            float sum = 0;
            for (int ch = 0; ch < channels; ch++)
                sum += samples[i * channels + ch];
            result[i] = sum / channels;
        }
        return result;
    }

    private static float[] ResampleLinear(float[] input, int sourceRate, int targetRate)
    {
        double ratio = (double)sourceRate / targetRate;
        int outputLength = (int)(input.Length / ratio);
        var result = new float[outputLength];

        for (int i = 0; i < outputLength; i++)
        {
            double srcIndex = i * ratio;
            int srcFloor = (int)srcIndex;
            double frac = srcIndex - srcFloor;

            if (srcFloor + 1 < input.Length)
                result[i] = (float)(input[srcFloor] * (1 - frac) + input[srcFloor + 1] * frac);
            else if (srcFloor < input.Length)
                result[i] = input[srcFloor];
        }

        return result;
    }

    private static byte[] ConvertToInt16Bytes(float[] samples)
    {
        var result = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            var clamped = Math.Clamp(samples[i], -1.0f, 1.0f);
            var int16Sample = (short)(clamped * short.MaxValue);
            BitConverter.TryWriteBytes(result.AsSpan(i * 2), int16Sample);
        }
        return result;
    }
}
