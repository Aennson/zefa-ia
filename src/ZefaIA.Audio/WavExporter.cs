namespace ZefaIA.Audio;

public static class WavExporter
{
    public static void WriteWav(string filePath, List<byte[]> pcmChunks, int sampleRate = 16000, int bitsPerSample = 16, int channels = 1)
    {
        var totalDataSize = pcmChunks.Sum(c => c.Length);
        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream);

        var byteRate = sampleRate * channels * bitsPerSample / 8;
        var blockAlign = (short)(channels * bitsPerSample / 8);

        writer.Write("RIFF"u8);
        writer.Write(36 + totalDataSize);
        writer.Write("WAVE"u8);

        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write((short)bitsPerSample);

        writer.Write("data"u8);
        writer.Write(totalDataSize);

        foreach (var chunk in pcmChunks)
            writer.Write(chunk);
    }
}
