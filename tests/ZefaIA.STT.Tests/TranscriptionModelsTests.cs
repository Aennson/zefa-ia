using Xunit;
using ZefaIA.Core.Models;

namespace ZefaIA.STT.Tests;

public class TranscriptionModelsTests
{
    [Fact]
    public void TranscriptionSegment_CreatesCorrectly()
    {
        var segment = new TranscriptionSegment(
            Text: "Olá, tudo bem?",
            Language: "pt",
            Confidence: 0.95f,
            StartTime: TimeSpan.FromSeconds(1),
            EndTime: TimeSpan.FromSeconds(3),
            Source: AudioSourceType.Loopback,
            IsFinal: true
        );

        Assert.Equal("Olá, tudo bem?", segment.Text);
        Assert.Equal("pt", segment.Language);
        Assert.True(segment.IsFinal);
        Assert.Equal(AudioSourceType.Loopback, segment.Source);
    }

    [Fact]
    public void STTProviderConfig_DefaultsWork()
    {
        var config = new STTProviderConfig
        {
            ProviderType = STTProviderType.WhisperLocal,
            Language = "auto"
        };

        Assert.Empty(config.Options);
        Assert.Equal(STTProviderType.WhisperLocal, config.ProviderType);
    }
}
