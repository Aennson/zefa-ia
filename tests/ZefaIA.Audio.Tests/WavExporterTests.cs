using Xunit;
using ZefaIA.Audio;

namespace ZefaIA.Audio.Tests;

public class WavExporterTests
{
    [Fact]
    public void WriteWav_CreatesValidWavFile()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            var chunks = new List<byte[]>
            {
                new byte[] { 0x00, 0x10, 0xFF, 0x0F },
                new byte[] { 0x00, 0x20, 0xFF, 0x1F }
            };

            WavExporter.WriteWav(tempPath, chunks);

            var bytes = File.ReadAllBytes(tempPath);

            Assert.True(bytes.Length > 44);
            Assert.Equal((byte)'R', bytes[0]);
            Assert.Equal((byte)'I', bytes[1]);
            Assert.Equal((byte)'F', bytes[2]);
            Assert.Equal((byte)'F', bytes[3]);
            Assert.Equal((byte)'W', bytes[8]);
            Assert.Equal((byte)'A', bytes[9]);
            Assert.Equal((byte)'V', bytes[10]);
            Assert.Equal((byte)'E', bytes[11]);

            var dataSize = BitConverter.ToInt32(bytes, 40);
            Assert.Equal(8, dataSize);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
