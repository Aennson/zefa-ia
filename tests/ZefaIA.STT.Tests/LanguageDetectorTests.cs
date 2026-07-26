using ZefaIA.Core;
using ZefaIA.Core.Models;

namespace ZefaIA.STT.Tests;

public class LanguageDetectorTests
{
    private readonly LanguageDetector _detector = new(minSamples: 3);

    [Fact]
    public void DetectedLanguage_Initial_IsAuto()
    {
        Assert.Equal("auto", _detector.DetectedLanguage);
        Assert.False(_detector.IsDetected);
    }

    [Fact]
    public void ProcessSegment_PortugueseSegments_DetectsPt()
    {
        FeedSegments("pt", 3);

        Assert.Equal("pt", _detector.DetectedLanguage);
        Assert.True(_detector.IsDetected);
    }

    [Fact]
    public void ProcessSegment_EnglishSegments_DetectsEn()
    {
        FeedSegments("en", 3);

        Assert.Equal("en", _detector.DetectedLanguage);
        Assert.True(_detector.IsDetected);
    }

    [Fact]
    public void ProcessSegment_MixedLanguages_DetectsMajority()
    {
        FeedSegments("pt", 2);
        FeedSegments("en", 1);

        Assert.Equal("pt", _detector.DetectedLanguage);
    }

    [Fact]
    public void ProcessSegment_BelowThreshold_NotDetected()
    {
        FeedSegments("pt", 2);

        Assert.Equal("auto", _detector.DetectedLanguage);
        Assert.False(_detector.IsDetected);
    }

    [Fact]
    public void ProcessSegment_AfterLocked_IgnoresNewSegments()
    {
        FeedSegments("pt", 3);
        FeedSegments("en", 10);

        Assert.Equal("pt", _detector.DetectedLanguage);
    }

    [Fact]
    public void SetOverride_OverridesDetection()
    {
        FeedSegments("pt", 3);
        _detector.SetOverride("en");

        Assert.Equal("en", _detector.DetectedLanguage);
        Assert.True(_detector.IsDetected);
    }

    [Fact]
    public void SetOverride_Auto_ClearsOverride()
    {
        FeedSegments("pt", 3);
        _detector.SetOverride("en");
        _detector.SetOverride("auto");

        Assert.Equal("pt", _detector.DetectedLanguage);
    }

    [Fact]
    public void SetOverride_EmptyString_ClearsOverride()
    {
        _detector.SetOverride("fr");
        _detector.SetOverride("");

        Assert.Equal("auto", _detector.DetectedLanguage);
    }

    [Fact]
    public void OnLanguageDetected_FiresWhenDetected()
    {
        string? detected = null;
        _detector.OnLanguageDetected += lang => detected = lang;

        FeedSegments("pt", 3);

        Assert.Equal("pt", detected);
    }

    [Fact]
    public void OnLanguageDetected_FiresOnOverride()
    {
        string? detected = null;
        _detector.OnLanguageDetected += lang => detected = lang;

        _detector.SetOverride("es");

        Assert.Equal("es", detected);
    }

    [Fact]
    public void GetSpeakerLabels_Portuguese_ReturnsEuInterlocutor()
    {
        FeedSegments("pt", 3);
        var labels = _detector.GetSpeakerLabels();

        Assert.Equal("Eu", labels.Self);
        Assert.Equal("Interlocutor", labels.Other);
    }

    [Fact]
    public void GetSpeakerLabels_English_ReturnsMeOther()
    {
        FeedSegments("en", 3);
        var labels = _detector.GetSpeakerLabels();

        Assert.Equal("Me", labels.Self);
        Assert.Equal("Other", labels.Other);
    }

    [Fact]
    public void GetSpeakerLabels_Spanish_ReturnsYoInterlocutor()
    {
        FeedSegments("es", 3);
        var labels = _detector.GetSpeakerLabels();

        Assert.Equal("Yo", labels.Self);
        Assert.Equal("Interlocutor", labels.Other);
    }

    [Fact]
    public void GetSpeakerLabels_French_ReturnsMoiInterlocuteur()
    {
        FeedSegments("fr", 3);
        var labels = _detector.GetSpeakerLabels();

        Assert.Equal("Moi", labels.Self);
        Assert.Equal("Interlocuteur", labels.Other);
    }

    [Fact]
    public void NormalizeLanguage_VariousFormats_ReturnsShortCode()
    {
        Assert.Equal("pt", LanguageDetector.NormalizeLanguage("pt-BR"));
        Assert.Equal("en", LanguageDetector.NormalizeLanguage("en-US"));
        Assert.Equal("en", LanguageDetector.NormalizeLanguage("EN"));
        Assert.Equal("es", LanguageDetector.NormalizeLanguage("es-ES"));
        Assert.Equal("fr", LanguageDetector.NormalizeLanguage("fr"));
    }

    [Fact]
    public void ProcessSegment_UnknownLanguage_Ignored()
    {
        for (int i = 0; i < 5; i++)
        {
            var seg = MakeSegment("unknown");
            _detector.ProcessSegment(seg);
        }

        Assert.False(_detector.IsDetected);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        FeedSegments("pt", 3);
        Assert.True(_detector.IsDetected);

        _detector.Reset();

        Assert.False(_detector.IsDetected);
        Assert.Equal("auto", _detector.DetectedLanguage);
    }

    [Fact]
    public void ProcessSegment_WithOverrideSet_Ignored()
    {
        _detector.SetOverride("en");
        FeedSegments("pt", 5);

        Assert.Equal("en", _detector.DetectedLanguage);
    }

    private void FeedSegments(string language, int count)
    {
        for (int i = 0; i < count; i++)
            _detector.ProcessSegment(MakeSegment(language));
    }

    private static TranscriptionSegment MakeSegment(string language) => new(
        Text: "test",
        Language: language,
        Confidence: 0.9f,
        StartTime: TimeSpan.Zero,
        EndTime: TimeSpan.FromSeconds(1),
        Source: AudioSourceType.Microphone,
        IsFinal: true
    );
}
