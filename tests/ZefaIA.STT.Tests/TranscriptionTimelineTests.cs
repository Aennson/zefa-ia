using Xunit;
using ZefaIA.Core.Models;

namespace ZefaIA.STT.Tests;

public class TranscriptionTimelineTests
{
    private static TranscriptionSegment MakeSegment(string text, double startSec, double endSec, AudioSourceType source)
    {
        return new TranscriptionSegment(
            text, "pt", 0.9f,
            TimeSpan.FromSeconds(startSec),
            TimeSpan.FromSeconds(endSec),
            source, true);
    }

    [Fact]
    public void Add_MicSegment_LabeledAsMe()
    {
        var timeline = new TranscriptionTimeline();
        timeline.Add(MakeSegment("Olá", 0, 1, AudioSourceType.Microphone));

        var result = timeline.GetTimeline();
        Assert.Single(result);
        Assert.Equal("Eu", result[0].Speaker.DisplayName);
    }

    [Fact]
    public void Add_LoopbackSegment_LabeledAsOther()
    {
        var timeline = new TranscriptionTimeline();
        timeline.Add(MakeSegment("Tudo bem?", 1, 3, AudioSourceType.Loopback));

        var result = timeline.GetTimeline();
        Assert.Single(result);
        Assert.Equal("Interlocutor", result[0].Speaker.DisplayName);
    }

    [Fact]
    public void GetTimeline_OrdersByStartTime()
    {
        var timeline = new TranscriptionTimeline();
        timeline.Add(MakeSegment("Segundo", 3, 5, AudioSourceType.Microphone));
        timeline.Add(MakeSegment("Primeiro", 0, 2, AudioSourceType.Loopback));
        timeline.Add(MakeSegment("Terceiro", 6, 8, AudioSourceType.Microphone));

        var result = timeline.GetTimeline();
        Assert.Equal(3, result.Count);
        Assert.Equal("Primeiro", result[0].Segment.Text);
        Assert.Equal("Segundo", result[1].Segment.Text);
        Assert.Equal("Terceiro", result[2].Segment.Text);
    }

    [Fact]
    public void GetTimeline_OverlappingSegments_PreservesBoth()
    {
        var timeline = new TranscriptionTimeline();
        timeline.Add(MakeSegment("Eu falo", 2, 5, AudioSourceType.Microphone));
        timeline.Add(MakeSegment("Ele fala", 3, 6, AudioSourceType.Loopback));

        var result = timeline.GetTimeline();
        Assert.Equal(2, result.Count);
        Assert.Equal("Eu falo", result[0].Segment.Text);
        Assert.Equal("Ele fala", result[1].Segment.Text);
    }

    [Fact]
    public void GetTimelineWindow_FiltersCorrectly()
    {
        var timeline = new TranscriptionTimeline();
        timeline.Add(MakeSegment("Before", 0, 2, AudioSourceType.Microphone));
        timeline.Add(MakeSegment("Inside", 5, 8, AudioSourceType.Loopback));
        timeline.Add(MakeSegment("After", 12, 15, AudioSourceType.Microphone));

        var result = timeline.GetTimelineWindow(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10));
        Assert.Single(result);
        Assert.Equal("Inside", result[0].Segment.Text);
    }

    [Fact]
    public void ConfigurableLabels_ChangeDisplayNames()
    {
        var timeline = new TranscriptionTimeline
        {
            MicSpeaker = SpeakerLabel.Me("João"),
            LoopbackSpeaker = SpeakerLabel.Other("Maria")
        };

        timeline.Add(MakeSegment("Oi Maria", 0, 1, AudioSourceType.Microphone));
        timeline.Add(MakeSegment("Oi João", 1, 2, AudioSourceType.Loopback));

        var result = timeline.GetTimeline();
        Assert.Equal("João", result[0].Speaker.DisplayName);
        Assert.Equal("Maria", result[1].Speaker.DisplayName);
    }

    [Fact]
    public void FormatTimeline_ProducesFormattedOutput()
    {
        var timeline = new TranscriptionTimeline();
        timeline.Add(MakeSegment("Hello", 0, 1, AudioSourceType.Microphone));
        timeline.Add(MakeSegment("Hi there", 2, 4, AudioSourceType.Loopback));

        var output = timeline.FormatTimeline();
        Assert.Contains("[Eu]", output);
        Assert.Contains("[Interlocutor]", output);
        Assert.Contains("Hello", output);
        Assert.Contains("Hi there", output);
    }

    [Fact]
    public void DiarizedSegment_Format_IncludesTimestamp()
    {
        var segment = MakeSegment("Test", 65, 67, AudioSourceType.Microphone);
        var diarized = new DiarizedSegment(segment, SpeakerLabel.Me());

        var formatted = diarized.Format();
        Assert.Equal("[01:05] [Eu] Test", formatted);
    }

    [Fact]
    public void DiarizedSegment_FormatWithConfidence_IncludesPercentage()
    {
        var segment = new TranscriptionSegment(
            "Test", "pt", 0.95f,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(12),
            AudioSourceType.Loopback, true);
        var diarized = new DiarizedSegment(segment, SpeakerLabel.Other());

        var formatted = diarized.FormatWithConfidence();
        Assert.Contains("95", formatted);
        Assert.Contains("[Interlocutor]", formatted);
    }

    [Fact]
    public void Clear_RemovesAllSegments()
    {
        var timeline = new TranscriptionTimeline();
        timeline.Add(MakeSegment("A", 0, 1, AudioSourceType.Microphone));
        timeline.Add(MakeSegment("B", 1, 2, AudioSourceType.Loopback));

        Assert.Equal(2, timeline.SegmentCount);

        timeline.Clear();

        Assert.Equal(0, timeline.SegmentCount);
        Assert.Empty(timeline.GetTimeline());
    }

    [Fact]
    public void SpeakerLabel_FactoryMethods_SetSourceCorrectly()
    {
        var me = SpeakerLabel.Me("Alice");
        var other = SpeakerLabel.Other("Bob");

        Assert.Equal(AudioSourceType.Microphone, me.Source);
        Assert.Equal("Alice", me.DisplayName);
        Assert.Equal(AudioSourceType.Loopback, other.Source);
        Assert.Equal("Bob", other.DisplayName);
    }
}
