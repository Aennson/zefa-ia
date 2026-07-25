using System.Collections.Concurrent;
using ZefaIA.Core.Models;

namespace ZefaIA.STT;

public class TranscriptionTimeline
{
    private readonly ConcurrentBag<DiarizedSegment> _segments = new();
    private SpeakerLabel _micLabel = SpeakerLabel.Me();
    private SpeakerLabel _loopbackLabel = SpeakerLabel.Other();

    public SpeakerLabel MicSpeaker
    {
        get => _micLabel;
        set => _micLabel = value;
    }

    public SpeakerLabel LoopbackSpeaker
    {
        get => _loopbackLabel;
        set => _loopbackLabel = value;
    }

    public void Add(TranscriptionSegment segment)
    {
        var label = segment.Source == AudioSourceType.Microphone ? _micLabel : _loopbackLabel;
        _segments.Add(new DiarizedSegment(segment, label));
    }

    public IReadOnlyList<DiarizedSegment> GetTimeline()
    {
        return _segments
            .OrderBy(s => s.Segment.StartTime)
            .ThenBy(s => s.Segment.Source)
            .ToList();
    }

    public IReadOnlyList<DiarizedSegment> GetTimelineWindow(TimeSpan from, TimeSpan to)
    {
        return _segments
            .Where(s => s.Segment.StartTime >= from && s.Segment.EndTime <= to)
            .OrderBy(s => s.Segment.StartTime)
            .ThenBy(s => s.Segment.Source)
            .ToList();
    }

    public string FormatTimeline()
    {
        var timeline = GetTimeline();
        return string.Join(Environment.NewLine,
            timeline.Select(s => s.Format()));
    }

    public string FormatTimelineWindow(TimeSpan from, TimeSpan to)
    {
        var timeline = GetTimelineWindow(from, to);
        return string.Join(Environment.NewLine,
            timeline.Select(s => s.Format()));
    }

    public int SegmentCount => _segments.Count;

    public void Clear() => _segments.Clear();
}

public record DiarizedSegment(
    TranscriptionSegment Segment,
    SpeakerLabel Speaker
)
{
    public string Format() =>
        $"[{Segment.StartTime:mm\\:ss}] [{Speaker.DisplayName}] {Segment.Text}";

    public string FormatWithConfidence() =>
        $"[{Segment.StartTime:mm\\:ss}] [{Speaker.DisplayName}] {Segment.Text} ({Segment.Confidence:P0})";
}
