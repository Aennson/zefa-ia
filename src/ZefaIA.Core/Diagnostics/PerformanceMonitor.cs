using System.Diagnostics;
using System.Text;

namespace ZefaIA.Core.Diagnostics;

/// <summary>
/// Names of the pipeline stages measured against the Sprint 6 latency budget.
/// </summary>
public static class PipelineStages
{
    public const string AudioCapture = "audio.capture";
    public const string BufferToStt = "stt.dispatch";
    public const string SttProcessing = "stt.processing";
    public const string TriggerDetection = "trigger.detection";
    public const string LlmFirstToken = "llm.first_token";
    public const string OverlayRender = "overlay.render";

    /// <summary>The stages that add up to the user-visible end-to-end latency.</summary>
    public static readonly string[] EndToEndPath =
    [
        AudioCapture, BufferToStt, SttProcessing,
        TriggerDetection, LlmFirstToken, OverlayRender
    ];
}

/// <summary>
/// Collects per-stage latency across the pipeline and reports whether the
/// end-to-end budget is being met. Targets come from the Sprint 6 spec.
/// </summary>
public sealed class PerformanceMonitor
{
    private readonly Dictionary<string, LatencyTracker> _trackers;

    public TimeSpan EndToEndTarget { get; init; } = TimeSpan.FromMilliseconds(2000);

    public PerformanceMonitor(int capacityPerStage = 1000)
    {
        _trackers = new Dictionary<string, LatencyTracker>
        {
            [PipelineStages.AudioCapture] = new(PipelineStages.AudioCapture, capacityPerStage)
            { Target = TimeSpan.FromMilliseconds(100) },
            [PipelineStages.BufferToStt] = new(PipelineStages.BufferToStt, capacityPerStage)
            { Target = TimeSpan.FromMilliseconds(50) },
            [PipelineStages.SttProcessing] = new(PipelineStages.SttProcessing, capacityPerStage)
            { Target = TimeSpan.FromMilliseconds(500) },
            [PipelineStages.TriggerDetection] = new(PipelineStages.TriggerDetection, capacityPerStage)
            { Target = TimeSpan.FromMilliseconds(200) },
            [PipelineStages.LlmFirstToken] = new(PipelineStages.LlmFirstToken, capacityPerStage)
            { Target = TimeSpan.FromMilliseconds(1000) },
            [PipelineStages.OverlayRender] = new(PipelineStages.OverlayRender, capacityPerStage)
            { Target = TimeSpan.FromMilliseconds(50) }
        };
    }

    public IReadOnlyDictionary<string, LatencyTracker> Trackers => _trackers;

    public LatencyTracker GetTracker(string stage)
    {
        if (!_trackers.TryGetValue(stage, out var tracker))
        {
            tracker = new LatencyTracker(stage);
            _trackers[stage] = tracker;
        }
        return tracker;
    }

    public void Record(string stage, double milliseconds) => GetTracker(stage).Record(milliseconds);

    public void Record(string stage, TimeSpan elapsed) => GetTracker(stage).Record(elapsed);

    /// <summary>
    /// Times a block and records it. Use as:
    /// <c>using (monitor.Measure(PipelineStages.SttProcessing)) { ... }</c>
    /// </summary>
    public StageTimer Measure(string stage) => new(this, stage);

    /// <summary>
    /// Sum of each stage's p95 along the end-to-end path. This is deliberately
    /// pessimistic: it assumes the slow cases line up, which is the number worth
    /// defending against rather than the average.
    /// </summary>
    public double EndToEndP95Ms =>
        PipelineStages.EndToEndPath.Sum(stage => GetTracker(stage).P95);

    public bool MeetsEndToEndTarget => EndToEndP95Ms <= EndToEndTarget.TotalMilliseconds;

    public void Reset()
    {
        foreach (var tracker in _trackers.Values) tracker.Reset();
    }

    public string BuildReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Zefa IA - Latencia do Pipeline ===");

        foreach (var stage in PipelineStages.EndToEndPath)
            sb.AppendLine(GetTracker(stage).GetSnapshot().Format());

        var extras = _trackers.Keys.Except(PipelineStages.EndToEndPath).OrderBy(k => k);
        foreach (var stage in extras)
            sb.AppendLine(_trackers[stage].GetSnapshot().Format());

        sb.AppendLine();
        sb.AppendLine($"End-to-end (soma dos p95): {EndToEndP95Ms:F1}ms / alvo {EndToEndTarget.TotalMilliseconds:F0}ms " +
                      $"-> {(MeetsEndToEndTarget ? "OK" : "EXCEDIDO")}");

        return sb.ToString();
    }
}

/// <summary>Times a stage for the lifetime of the struct.</summary>
public readonly struct StageTimer : IDisposable
{
    private readonly PerformanceMonitor _monitor;
    private readonly string _stage;
    private readonly long _startTicks;

    internal StageTimer(PerformanceMonitor monitor, string stage)
    {
        _monitor = monitor;
        _stage = stage;
        _startTicks = Stopwatch.GetTimestamp();
    }

    public void Dispose()
    {
        var elapsed = Stopwatch.GetElapsedTime(_startTicks);
        _monitor.Record(_stage, elapsed);
    }
}
