using Moq;
using Xunit;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.STT.Tests;

public class STTProviderFactoryTests
{
    [Fact]
    public void Create_RegisteredProvider_ReturnsInstance()
    {
        var factory = new STTProviderFactory();
        var mockProvider = new Mock<ISTTProvider>();
        mockProvider.Setup(p => p.Type).Returns(STTProviderType.WhisperLocal);

        factory.Register(STTProviderType.WhisperLocal, () => mockProvider.Object);

        var config = new STTProviderConfig { ProviderType = STTProviderType.WhisperLocal };
        var provider = factory.Create(config);

        Assert.NotNull(provider);
        Assert.Equal(STTProviderType.WhisperLocal, provider.Type);
    }

    [Fact]
    public void Create_UnregisteredProvider_ThrowsNotSupportedException()
    {
        var factory = new STTProviderFactory();
        var config = new STTProviderConfig { ProviderType = STTProviderType.ElevenLabs };

        var ex = Assert.Throws<NotSupportedException>(() => factory.Create(config));
        Assert.Contains("ElevenLabs", ex.Message);
    }

    [Fact]
    public void Register_OverwritesPreviousCreator()
    {
        var factory = new STTProviderFactory();
        var mock1 = new Mock<ISTTProvider>();
        mock1.Setup(p => p.ProviderId).Returns("first");
        var mock2 = new Mock<ISTTProvider>();
        mock2.Setup(p => p.ProviderId).Returns("second");

        factory.Register(STTProviderType.WhisperLocal, () => mock1.Object);
        factory.Register(STTProviderType.WhisperLocal, () => mock2.Object);

        var config = new STTProviderConfig { ProviderType = STTProviderType.WhisperLocal };
        var provider = factory.Create(config);

        Assert.Equal("second", provider.ProviderId);
    }

    [Fact]
    public void IsRegistered_ReturnsTrueForRegisteredType()
    {
        var factory = new STTProviderFactory();
        factory.Register(STTProviderType.ElevenLabs, () => new Mock<ISTTProvider>().Object);

        Assert.True(factory.IsRegistered(STTProviderType.ElevenLabs));
        Assert.False(factory.IsRegistered(STTProviderType.WhisperLocal));
    }

    [Fact]
    public void TranscriptionResult_ComputesFullTextAndDuration()
    {
        var result = new TranscriptionResult
        {
            Segments = new[]
            {
                new TranscriptionSegment("Olá", "pt", 0.9f, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1), AudioSourceType.Microphone, true),
                new TranscriptionSegment("tudo bem?", "pt", 0.85f, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), AudioSourceType.Microphone, true)
            }
        };

        Assert.Equal("Olá tudo bem?", result.FullText);
        Assert.Equal(TimeSpan.FromSeconds(3), result.Duration);
        Assert.Equal("pt", result.DetectedLanguage);
    }

    [Fact]
    public void TranscriptionResult_EmptySegments_HasDefaults()
    {
        var result = new TranscriptionResult();

        Assert.Empty(result.Segments);
        Assert.Equal("", result.FullText);
        Assert.Equal(TimeSpan.Zero, result.Duration);
        Assert.Equal("unknown", result.DetectedLanguage);
    }
}
