using Moq;
using Xunit;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.STT.Tests;

public class STTServiceManagerTests
{
    [Fact]
    public void DefaultFactory_RegistersBothProviders()
    {
        var factory = STTServiceManager.CreateDefaultFactory();

        Assert.True(factory.IsRegistered(STTProviderType.WhisperLocal));
        Assert.True(factory.IsRegistered(STTProviderType.ElevenLabs));
    }

    [Fact]
    public async Task InitializeActiveProvider_WhisperLocal_CreatesProvider()
    {
        var mockProvider = new Mock<ISTTProvider>();
        mockProvider.Setup(p => p.ProviderId).Returns("whisper-local");
        mockProvider.Setup(p => p.Type).Returns(STTProviderType.WhisperLocal);

        var factory = new STTProviderFactory();
        factory.Register(STTProviderType.WhisperLocal, () => mockProvider.Object);

        var settings = new STTSettings { ActiveProvider = "WhisperLocal" };
        var manager = new STTServiceManager(factory, settings);

        var provider = await manager.InitializeActiveProviderAsync();

        Assert.NotNull(provider);
        Assert.Equal("whisper-local", provider.ProviderId);
        Assert.Same(provider, manager.ActiveProvider);

        mockProvider.Verify(p => p.InitializeAsync(
            It.Is<STTProviderConfig>(c => c.ProviderType == STTProviderType.WhisperLocal),
            It.IsAny<CancellationToken>()), Times.Once);

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task InitializeActiveProvider_ElevenLabs_CreatesProvider()
    {
        var mockProvider = new Mock<ISTTProvider>();
        mockProvider.Setup(p => p.ProviderId).Returns("elevenlabs-scribe");
        mockProvider.Setup(p => p.Type).Returns(STTProviderType.ElevenLabs);

        var factory = new STTProviderFactory();
        factory.Register(STTProviderType.ElevenLabs, () => mockProvider.Object);

        var settings = new STTSettings { ActiveProvider = "ElevenLabs" };
        var manager = new STTServiceManager(factory, settings);

        var provider = await manager.InitializeActiveProviderAsync();

        Assert.Equal("elevenlabs-scribe", provider.ProviderId);

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task InitializeActiveProvider_InvalidName_Throws()
    {
        var factory = new STTProviderFactory();
        var settings = new STTSettings { ActiveProvider = "NonExistent" };
        var manager = new STTServiceManager(factory, settings);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.InitializeActiveProviderAsync());
        Assert.Contains("Invalid STT provider", ex.Message);

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task SwapProvider_DisposesOldAndCreatesNew()
    {
        var whisperMock = new Mock<ISTTProvider>();
        whisperMock.Setup(p => p.ProviderId).Returns("whisper-local");
        whisperMock.Setup(p => p.Type).Returns(STTProviderType.WhisperLocal);

        var elevenMock = new Mock<ISTTProvider>();
        elevenMock.Setup(p => p.ProviderId).Returns("elevenlabs-scribe");
        elevenMock.Setup(p => p.Type).Returns(STTProviderType.ElevenLabs);

        var factory = new STTProviderFactory();
        factory.Register(STTProviderType.WhisperLocal, () => whisperMock.Object);
        factory.Register(STTProviderType.ElevenLabs, () => elevenMock.Object);

        var settings = new STTSettings { ActiveProvider = "WhisperLocal" };
        var manager = new STTServiceManager(factory, settings);

        await manager.InitializeActiveProviderAsync();
        Assert.Equal("whisper-local", manager.ActiveProvider!.ProviderId);

        var newProvider = await manager.SwapProviderAsync(STTProviderType.ElevenLabs);

        whisperMock.Verify(p => p.DisposeAsync(), Times.Once);
        Assert.Equal("elevenlabs-scribe", newProvider.ProviderId);
        Assert.Same(newProvider, manager.ActiveProvider);

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task Dispose_DisposesActiveProvider()
    {
        var mockProvider = new Mock<ISTTProvider>();
        mockProvider.Setup(p => p.Type).Returns(STTProviderType.WhisperLocal);

        var factory = new STTProviderFactory();
        factory.Register(STTProviderType.WhisperLocal, () => mockProvider.Object);

        var settings = new STTSettings();
        var manager = new STTServiceManager(factory, settings);
        await manager.InitializeActiveProviderAsync();

        await manager.DisposeAsync();

        mockProvider.Verify(p => p.DisposeAsync(), Times.Once);
        Assert.Null(manager.ActiveProvider);
    }

    [Fact]
    public void STTSettings_HasCorrectDefaults()
    {
        var settings = new STTSettings();

        Assert.Equal("WhisperLocal", settings.ActiveProvider);
        Assert.Equal("base", settings.WhisperLocal.ModelSize);
        Assert.Equal("auto", settings.WhisperLocal.Language);
        Assert.False(settings.WhisperLocal.UseGPU);
        Assert.Equal("./models", settings.WhisperLocal.ModelPath);
        Assert.Equal(2500, settings.WhisperLocal.BufferMs);
    }

    [Fact]
    public void ElevenLabsSettings_HasCorrectDefaults()
    {
        var settings = new ElevenLabsSettings();

        Assert.Equal("ELEVENLABS_API_KEY", settings.ApiKeyEnvVar);
        Assert.Equal("auto", settings.Language);
        Assert.True(settings.VadEnabled);
    }

    [Fact]
    public async Task Validation_InvalidWhisperModelSize_Throws()
    {
        var mockProvider = new Mock<ISTTProvider>();
        mockProvider.Setup(p => p.Type).Returns(STTProviderType.WhisperLocal);

        var factory = new STTProviderFactory();
        factory.Register(STTProviderType.WhisperLocal, () => mockProvider.Object);

        var settings = new STTSettings
        {
            ActiveProvider = "WhisperLocal",
            WhisperLocal = new WhisperLocalSettings { ModelSize = "gigantic" }
        };

        var manager = new STTServiceManager(factory, settings);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.InitializeActiveProviderAsync());
        Assert.Contains("Invalid Whisper model size", ex.Message);

        await manager.DisposeAsync();
    }
}
