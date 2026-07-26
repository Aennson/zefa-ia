using System.Text.Json;
using Xunit;
using ZefaIA.Core.Models;

namespace ZefaIA.STT.Tests;

public class ElevenLabsSTTProviderTests
{
    [Fact]
    public void Provider_HasCorrectIdentity()
    {
        var provider = new ElevenLabsSTTProvider();

        Assert.Equal("elevenlabs-scribe", provider.ProviderId);
        Assert.Equal(STTProviderType.ElevenLabs, provider.Type);
        Assert.Contains("pt", provider.SupportedLanguages);
        Assert.Contains("en", provider.SupportedLanguages);
    }

    [Fact]
    public async Task ProcessAudio_BeforeInit_Throws()
    {
        var provider = new ElevenLabsSTTProvider();
        var chunk = new AudioChunkEventArgs(
            new byte[320], 16000, TimeSpan.Zero, AudioSourceType.Microphone);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.ProcessAudioAsync(chunk));
    }

    [Fact]
    public async Task InitializeAsync_NoApiKey_Throws()
    {
        var provider = new ElevenLabsSTTProvider();
        var config = new STTProviderConfig
        {
            ProviderType = STTProviderType.ElevenLabs,
            Language = "auto",
            Options = new Dictionary<string, string>
            {
                ["ApiKeyEnvVar"] = "ZEFA_TEST_NONEXISTENT_KEY_12345"
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.InitializeAsync(config));
        Assert.Contains("API key not found", ex.Message);
    }

    [Fact]
    public void ProcessResponse_FinalTranscript_EmitsSegmentReceived()
    {
        var provider = new ElevenLabsSTTProvider();
        var segments = new List<TranscriptionSegment>();
        provider.SegmentReceived += (_, e) => segments.Add(e.Segment);

        var json = JsonSerializer.Serialize(new
        {
            type = "transcript",
            text = "Olá tudo bem",
            is_final = true,
            language = "pt",
            confidence = 0.95,
            start_time = 1.0,
            end_time = 3.0
        });

        provider.ProcessResponse(json);

        Assert.Single(segments);
        Assert.Equal("Olá tudo bem", segments[0].Text);
        Assert.Equal("pt", segments[0].Language);
        Assert.True(segments[0].IsFinal);
        Assert.Equal(0.95f, segments[0].Confidence, 0.01f);
    }

    [Fact]
    public void ProcessResponse_PartialTranscript_EmitsPartialReceived()
    {
        var provider = new ElevenLabsSTTProvider();
        var partials = new List<TranscriptionSegment>();
        provider.PartialReceived += (_, e) => partials.Add(e.Segment);

        var json = JsonSerializer.Serialize(new
        {
            type = "transcript",
            text = "Olá",
            is_final = false,
            language = "pt",
            confidence = 0.5
        });

        provider.ProcessResponse(json);

        Assert.Single(partials);
        Assert.Equal("Olá", partials[0].Text);
        Assert.False(partials[0].IsFinal);
    }

    [Fact]
    public void ProcessResponse_NonTranscriptType_Ignored()
    {
        var provider = new ElevenLabsSTTProvider();
        var segments = new List<TranscriptionSegment>();
        provider.SegmentReceived += (_, e) => segments.Add(e.Segment);
        provider.PartialReceived += (_, e) => segments.Add(e.Segment);

        var json = JsonSerializer.Serialize(new { type = "info", message = "connected" });
        provider.ProcessResponse(json);

        Assert.Empty(segments);
    }

    [Fact]
    public void ProcessResponse_EmptyText_Ignored()
    {
        var provider = new ElevenLabsSTTProvider();
        var segments = new List<TranscriptionSegment>();
        provider.SegmentReceived += (_, e) => segments.Add(e.Segment);

        var json = JsonSerializer.Serialize(new
        {
            type = "transcript",
            text = "  ",
            is_final = true
        });

        provider.ProcessResponse(json);
        Assert.Empty(segments);
    }

    [Fact]
    public void ProcessResponse_InvalidJson_DoesNotThrow()
    {
        var provider = new ElevenLabsSTTProvider();
        provider.ProcessResponse("not valid json {{{");
    }

    [Fact]
    public void AudioMessage_SerializesCorrectly()
    {
        var message = new ElevenLabsAudioMessage
        {
            Audio = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            SampleRate = 16000
        };

        var json = JsonSerializer.Serialize(message, JsonContext.Default.ElevenLabsAudioMessage);

        Assert.Contains("\"audio\"", json);
        Assert.Contains("\"sample_rate\":16000", json);
        Assert.Contains(Convert.ToBase64String(new byte[] { 1, 2, 3 }), json);
    }

    [Fact]
    public async Task DisposeAsync_MultipleCallsDoNotThrow()
    {
        var provider = new ElevenLabsSTTProvider();
        await provider.DisposeAsync();
        await provider.DisposeAsync();
    }

    [Fact]
    public async Task ProcessAudio_AfterDispose_Throws()
    {
        var provider = new ElevenLabsSTTProvider();
        await provider.DisposeAsync();

        var chunk = new AudioChunkEventArgs(
            new byte[320], 16000, TimeSpan.Zero, AudioSourceType.Microphone);

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => provider.ProcessAudioAsync(chunk));
    }

    [OptInFact("ZEFA_RUN_ELEVENLABS_INTEGRATION", "Requires a paid ELEVENLABS_API_KEY")]
    public async Task Integration_ConnectsAndTranscribes()
    {
        await using var provider = new ElevenLabsSTTProvider();
        var config = new STTProviderConfig
        {
            ProviderType = STTProviderType.ElevenLabs,
            Language = "en",
            Options = new Dictionary<string, string>
            {
                ["ApiKeyEnvVar"] = "ELEVENLABS_API_KEY"
            }
        };

        await provider.InitializeAsync(config);

        var pcm = new byte[3200]; // 100ms of 16kHz mono int16
        var chunk = new AudioChunkEventArgs(pcm, 16000, TimeSpan.Zero, AudioSourceType.Microphone);
        await provider.ProcessAudioAsync(chunk);
    }
}
