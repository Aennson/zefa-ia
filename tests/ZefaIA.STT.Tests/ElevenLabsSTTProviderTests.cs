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
            message_type = "committed_transcript_with_timestamps",
            text = "Olá tudo bem",
            language_code = "pt",
            words = new[]
            {
                new { text = "Olá", start = 1.0, end = 1.5 },
                new { text = "bem", start = 2.5, end = 3.0 }
            }
        });

        provider.ProcessResponse(json);

        Assert.Single(segments);
        Assert.Equal("Olá tudo bem", segments[0].Text);
        Assert.Equal("pt", segments[0].Language);
        Assert.True(segments[0].IsFinal);
        Assert.Equal(TimeSpan.FromSeconds(1.0), segments[0].StartTime);
        Assert.Equal(TimeSpan.FromSeconds(3.0), segments[0].EndTime);
    }

    [Fact]
    public void ProcessResponse_PartialTranscript_EmitsPartialReceived()
    {
        var provider = new ElevenLabsSTTProvider();
        var partials = new List<TranscriptionSegment>();
        provider.PartialReceived += (_, e) => partials.Add(e.Segment);

        var json = JsonSerializer.Serialize(new
        {
            message_type = "partial_transcript",
            text = "Olá"
        });

        provider.ProcessResponse(json);

        Assert.Single(partials);
        Assert.Equal("Olá", partials[0].Text);
        Assert.False(partials[0].IsFinal);
    }

    [Fact]
    public void ProcessResponse_InputError_EmitsNothing()
    {
        // This is what the server answered to every chunk while the wire format was
        // wrong. It must never be mistaken for a transcript.
        var provider = new ElevenLabsSTTProvider();
        var segments = new List<TranscriptionSegment>();
        provider.SegmentReceived += (_, e) => segments.Add(e.Segment);
        provider.PartialReceived += (_, e) => segments.Add(e.Segment);

        provider.ProcessResponse(
            """{"message_type":"input_error","error":"Message must be a valid protocol message"}""");

        Assert.Empty(segments);
    }

    [Fact]
    public void ProcessResponse_NonTranscriptType_Ignored()
    {
        var provider = new ElevenLabsSTTProvider();
        var segments = new List<TranscriptionSegment>();
        provider.SegmentReceived += (_, e) => segments.Add(e.Segment);
        provider.PartialReceived += (_, e) => segments.Add(e.Segment);

        var json = JsonSerializer.Serialize(new { message_type = "session_started", session_id = "abc" });
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
            message_type = "committed_transcript",
            text = "  "
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
    public void AudioMessage_MatchesTheRealtimeProtocol()
    {
        var message = new ElevenLabsAudioMessage
        {
            AudioBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            Commit = false,
            SampleRate = 16000
        };

        var json = JsonSerializer.Serialize(message, JsonContext.Default.ElevenLabsAudioMessage);

        // Every one of these is required. Sending {"audio":...,"sample_rate":...} instead
        // got each chunk rejected with "Message must be a valid protocol message".
        Assert.Contains("\"message_type\":\"input_audio_chunk\"", json);
        Assert.Contains("\"audio_base_64\"", json);
        Assert.Contains("\"commit\":false", json);
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

    /// <summary>
    /// Streams real synthesized speech and requires actual words back.
    ///
    /// The previous version of this test sent 100ms of digital silence and asserted
    /// nothing, so it passed for months while the provider spoke the wrong protocol and
    /// the server rejected every single chunk with
    /// {"message_type":"input_error","error":"Message must be a valid protocol message"}.
    /// A test that cannot fail when the feature is completely broken is worse than no
    /// test, because it reads like coverage.
    /// </summary>
    [OptInFact("ZEFA_RUN_ELEVENLABS_INTEGRATION", "Requires a paid ELEVENLABS_API_KEY")]
    public async Task Integration_TranscribesRealSpeech()
    {
        const string spoken = "the quick brown fox jumps over the lazy dog";

        var finals = new List<string>();
        var partials = new List<string>();

        await using var provider = new ElevenLabsSTTProvider();
        provider.SegmentReceived += (_, e) => { lock (finals) finals.Add(e.Segment.Text); };
        provider.PartialReceived += (_, e) => { lock (partials) partials.Add(e.Segment.Text); };

        await provider.InitializeAsync(new STTProviderConfig
        {
            ProviderType = STTProviderType.ElevenLabs,
            Language = "en",
            Options = new Dictionary<string, string> { ["ApiKeyEnvVar"] = "ELEVENLABS_API_KEY" }
        });

        var pcm = SynthesizeSpeechPcm16k(spoken);

        // 100 ms chunks, the same shape the audio pipeline delivers.
        const int chunkBytes = 3200;
        for (int offset = 0; offset < pcm.Length; offset += chunkBytes)
        {
            var size = Math.Min(chunkBytes, pcm.Length - offset);
            var slice = new byte[size];
            Buffer.BlockCopy(pcm, offset, slice, 0, size);

            await provider.ProcessAudioAsync(
                new AudioChunkEventArgs(slice, 16000, TimeSpan.Zero, AudioSourceType.Microphone));
        }

        await provider.FlushAsync();

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            lock (finals) if (finals.Count > 0) break;
            await Task.Delay(200);
        }

        Assert.True(finals.Count > 0,
            $"no final transcript arrived (partials seen: {partials.Count}). " +
            "Check the log for an input_error, which means the wire format is wrong again.");

        var transcript = string.Join(" ", finals).ToLowerInvariant();
        Assert.Contains("brown fox", transcript);
    }

    /// <summary>
    /// Renders text to 16 kHz mono PCM with the Windows speech synthesizer, so the test
    /// carries its own audio instead of depending on a fixture file or a microphone.
    /// </summary>
    private static byte[] SynthesizeSpeechPcm16k(string text)
    {
        using var stream = new MemoryStream();
        using (var synth = new System.Speech.Synthesis.SpeechSynthesizer())
        {
            synth.SetOutputToAudioStream(stream, new System.Speech.AudioFormat.SpeechAudioFormatInfo(
                16000,
                System.Speech.AudioFormat.AudioBitsPerSample.Sixteen,
                System.Speech.AudioFormat.AudioChannel.Mono));
            synth.Speak(text);
        }

        // SetOutputToAudioStream writes raw PCM with no WAV header.
        return stream.ToArray();
    }
}
