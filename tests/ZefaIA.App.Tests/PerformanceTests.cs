using Xunit;
using ZefaIA.Core.Diagnostics;

namespace ZefaIA.App.Tests;

public class LatencyTrackerTests
{
    #region Recording

    [Fact]
    public void SampleCount_Initially_IsZero()
    {
        Assert.Equal(0, new LatencyTracker("test").SampleCount);
    }

    [Fact]
    public void Record_IncrementsSampleCount()
    {
        var tracker = new LatencyTracker("test");

        tracker.Record(10);
        tracker.Record(20);

        Assert.Equal(2, tracker.SampleCount);
    }

    [Fact]
    public void Record_TimeSpanOverload_ConvertsToMilliseconds()
    {
        var tracker = new LatencyTracker("test");

        tracker.Record(TimeSpan.FromMilliseconds(250));

        Assert.Equal(250, tracker.Average, precision: 3);
    }

    [Fact]
    public void Record_NegativeValue_IsIgnored()
    {
        var tracker = new LatencyTracker("test");

        tracker.Record(-5);

        Assert.Equal(0, tracker.SampleCount);
    }

    [Fact]
    public void Record_NaN_IsIgnored()
    {
        var tracker = new LatencyTracker("test");

        tracker.Record(double.NaN);

        Assert.Equal(0, tracker.SampleCount);
    }

    [Fact]
    public void Record_BeyondCapacity_KeepsWindowBounded()
    {
        var tracker = new LatencyTracker("test", capacity: 5);

        for (var i = 0; i < 100; i++) tracker.Record(i);

        Assert.Equal(5, tracker.SampleCount);
        Assert.Equal(5, tracker.Capacity);
    }

    [Fact]
    public void Record_BeyondCapacity_KeepsMostRecentSamples()
    {
        var tracker = new LatencyTracker("test", capacity: 3);

        tracker.Record(1);
        tracker.Record(2);
        tracker.Record(3);
        tracker.Record(100);

        // The oldest sample (1) was overwritten, so the window is {2, 3, 100}.
        Assert.Equal(2, tracker.Min);
        Assert.Equal(100, tracker.Max);
    }

    [Fact]
    public void Constructor_ZeroCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LatencyTracker("test", capacity: 0));
    }

    #endregion

    #region Statistics

    [Fact]
    public void Average_ComputesMean()
    {
        var tracker = new LatencyTracker("test");

        tracker.Record(10);
        tracker.Record(20);
        tracker.Record(30);

        Assert.Equal(20, tracker.Average, precision: 3);
    }

    [Fact]
    public void Average_NoSamples_IsZero()
    {
        Assert.Equal(0, new LatencyTracker("test").Average);
    }

    [Fact]
    public void MinMax_ReflectExtremes()
    {
        var tracker = new LatencyTracker("test");

        tracker.Record(50);
        tracker.Record(10);
        tracker.Record(90);

        Assert.Equal(10, tracker.Min);
        Assert.Equal(90, tracker.Max);
    }

    [Fact]
    public void Percentile_UnsortedInput_StillCorrect()
    {
        var tracker = new LatencyTracker("test");

        foreach (var v in new[] { 50.0, 10.0, 90.0, 30.0, 70.0 })
            tracker.Record(v);

        Assert.Equal(50, tracker.P50);
    }

    [Fact]
    public void Percentile_HundredSamples_NearestRank()
    {
        var tracker = new LatencyTracker("test", capacity: 200);

        for (var i = 1; i <= 100; i++) tracker.Record(i);

        Assert.Equal(50, tracker.P50);
        Assert.Equal(95, tracker.P95);
        Assert.Equal(99, tracker.P99);
    }

    [Fact]
    public void Percentile_SingleSample_ReturnsThatSample()
    {
        var tracker = new LatencyTracker("test");
        tracker.Record(42);

        Assert.Equal(42, tracker.P50);
        Assert.Equal(42, tracker.P95);
        Assert.Equal(42, tracker.P99);
    }

    [Fact]
    public void Percentile_NoSamples_IsZero()
    {
        var tracker = new LatencyTracker("test");

        Assert.Equal(0, tracker.P50);
        Assert.Equal(0, tracker.P95);
    }

    [Fact]
    public void Percentile_Zero_ReturnsMinimum()
    {
        var tracker = new LatencyTracker("test");
        tracker.Record(10);
        tracker.Record(20);

        Assert.Equal(10, tracker.Percentile(0));
    }

    [Fact]
    public void Percentile_Hundred_ReturnsMaximum()
    {
        var tracker = new LatencyTracker("test");
        tracker.Record(10);
        tracker.Record(20);

        Assert.Equal(20, tracker.Percentile(100));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Percentile_OutOfRange_Throws(double percentile)
    {
        var tracker = new LatencyTracker("test");

        Assert.Throws<ArgumentOutOfRangeException>(() => tracker.Percentile(percentile));
    }

    [Fact]
    public void Percentile_IsRobustToOutliers()
    {
        var tracker = new LatencyTracker("test", capacity: 200);

        for (var i = 0; i < 99; i++) tracker.Record(10);
        tracker.Record(10000);

        // One 10s spike must not drag p50 up; that is why p95 is the gate, not average.
        Assert.Equal(10, tracker.P50);
        Assert.True(tracker.Average > 10);
    }

    #endregion

    #region Targets

    [Fact]
    public void MeetsTarget_NoTargetSet_IsTrue()
    {
        var tracker = new LatencyTracker("test");
        tracker.Record(99999);

        Assert.True(tracker.MeetsTarget);
    }

    [Fact]
    public void MeetsTarget_NoSamples_IsTrue()
    {
        var tracker = new LatencyTracker("test") { Target = TimeSpan.FromMilliseconds(100) };

        Assert.True(tracker.MeetsTarget);
    }

    [Fact]
    public void MeetsTarget_P95WithinTarget_IsTrue()
    {
        var tracker = new LatencyTracker("test", 200) { Target = TimeSpan.FromMilliseconds(100) };

        for (var i = 0; i < 100; i++) tracker.Record(50);

        Assert.True(tracker.MeetsTarget);
    }

    [Fact]
    public void MeetsTarget_P95ExceedsTarget_IsFalse()
    {
        var tracker = new LatencyTracker("test", 200) { Target = TimeSpan.FromMilliseconds(100) };

        for (var i = 0; i < 100; i++) tracker.Record(150);

        Assert.False(tracker.MeetsTarget);
    }

    [Fact]
    public void MeetsTarget_ToleratesRareSpikes()
    {
        var tracker = new LatencyTracker("test", 200) { Target = TimeSpan.FromMilliseconds(100) };

        for (var i = 0; i < 96; i++) tracker.Record(50);
        for (var i = 0; i < 4; i++) tracker.Record(5000);

        // 4% of samples over budget still passes a p95 gate — by design.
        Assert.True(tracker.MeetsTarget);
    }

    #endregion

    #region Snapshot and reset

    [Fact]
    public void Reset_ClearsSamples()
    {
        var tracker = new LatencyTracker("test");
        tracker.Record(10);
        tracker.Reset();

        Assert.Equal(0, tracker.SampleCount);
        Assert.Equal(0, tracker.Average);
    }

    [Fact]
    public void GetSnapshot_CapturesStatistics()
    {
        var tracker = new LatencyTracker("stage.x", 200) { Target = TimeSpan.FromMilliseconds(100) };
        for (var i = 1; i <= 100; i++) tracker.Record(i);

        var snapshot = tracker.GetSnapshot();

        Assert.Equal("stage.x", snapshot.Name);
        Assert.Equal(100, snapshot.SampleCount);
        Assert.Equal(50, snapshot.P50Ms);
        Assert.Equal(95, snapshot.P95Ms);
        Assert.Equal(100, snapshot.TargetMs);
        Assert.True(snapshot.MeetsTarget);
    }

    [Fact]
    public void Snapshot_Format_MarksExceededTarget()
    {
        var tracker = new LatencyTracker("stage.slow", 200) { Target = TimeSpan.FromMilliseconds(10) };
        for (var i = 0; i < 100; i++) tracker.Record(500);

        var formatted = tracker.GetSnapshot().Format();

        Assert.Contains("stage.slow", formatted);
        Assert.Contains("EXCEDIDO", formatted);
    }

    #endregion
}

public class PerformanceMonitorTests
{
    [Fact]
    public void Constructor_RegistersEveryEndToEndStage()
    {
        var monitor = new PerformanceMonitor();

        foreach (var stage in PipelineStages.EndToEndPath)
            Assert.True(monitor.Trackers.ContainsKey(stage), $"missing tracker for {stage}");
    }

    [Fact]
    public void StageTargets_SumToTheEndToEndBudget()
    {
        var monitor = new PerformanceMonitor();

        var sum = PipelineStages.EndToEndPath
            .Sum(s => monitor.GetTracker(s).Target.TotalMilliseconds);

        // 100 + 50 + 500 + 200 + 1000 + 50 = 1900ms, inside the 2000ms budget.
        Assert.Equal(1900, sum);
        Assert.True(sum <= monitor.EndToEndTarget.TotalMilliseconds);
    }

    [Fact]
    public void Record_RoutesToNamedStage()
    {
        var monitor = new PerformanceMonitor();

        monitor.Record(PipelineStages.SttProcessing, 123);

        Assert.Equal(1, monitor.GetTracker(PipelineStages.SttProcessing).SampleCount);
        Assert.Equal(0, monitor.GetTracker(PipelineStages.LlmFirstToken).SampleCount);
    }

    [Fact]
    public void GetTracker_UnknownStage_CreatesOnDemand()
    {
        var monitor = new PerformanceMonitor();

        var tracker = monitor.GetTracker("custom.stage");
        tracker.Record(5);

        Assert.Equal(1, monitor.GetTracker("custom.stage").SampleCount);
    }

    [Fact]
    public void EndToEndP95_SumsStageP95s()
    {
        var monitor = new PerformanceMonitor();

        foreach (var stage in PipelineStages.EndToEndPath)
            monitor.Record(stage, 100);

        Assert.Equal(600, monitor.EndToEndP95Ms, precision: 3);
    }

    [Fact]
    public void EndToEndP95_NoSamples_IsZero()
    {
        Assert.Equal(0, new PerformanceMonitor().EndToEndP95Ms);
    }

    [Fact]
    public void MeetsEndToEndTarget_WithinBudget_IsTrue()
    {
        var monitor = new PerformanceMonitor();

        foreach (var stage in PipelineStages.EndToEndPath)
            monitor.Record(stage, 50);

        Assert.True(monitor.MeetsEndToEndTarget);
    }

    [Fact]
    public void MeetsEndToEndTarget_OverBudget_IsFalse()
    {
        var monitor = new PerformanceMonitor();

        foreach (var stage in PipelineStages.EndToEndPath)
            monitor.Record(stage, 500);

        // 6 stages * 500ms = 3000ms, over the 2000ms budget.
        Assert.False(monitor.MeetsEndToEndTarget);
    }

    [Fact]
    public void Measure_RecordsElapsedTime()
    {
        var monitor = new PerformanceMonitor();

        using (monitor.Measure(PipelineStages.OverlayRender))
        {
            Thread.Sleep(20);
        }

        var tracker = monitor.GetTracker(PipelineStages.OverlayRender);
        Assert.Equal(1, tracker.SampleCount);
        Assert.True(tracker.Max >= 15, $"expected at least ~20ms, got {tracker.Max}ms");
    }

    [Fact]
    public void Reset_ClearsEveryStage()
    {
        var monitor = new PerformanceMonitor();
        foreach (var stage in PipelineStages.EndToEndPath)
            monitor.Record(stage, 100);

        monitor.Reset();

        Assert.Equal(0, monitor.EndToEndP95Ms);
    }

    [Fact]
    public void BuildReport_ListsEveryStageAndVerdict()
    {
        var monitor = new PerformanceMonitor();
        foreach (var stage in PipelineStages.EndToEndPath)
            monitor.Record(stage, 10);

        var report = monitor.BuildReport();

        foreach (var stage in PipelineStages.EndToEndPath)
            Assert.Contains(stage, report);

        Assert.Contains("End-to-end", report);
        Assert.Contains("OK", report);
    }

    [Fact]
    public void BuildReport_IncludesCustomStages()
    {
        var monitor = new PerformanceMonitor();
        monitor.Record("custom.stage", 10);

        Assert.Contains("custom.stage", monitor.BuildReport());
    }
}
