namespace ZefaIA.Core.Diagnostics;

/// <summary>
/// Rolling latency samples for one pipeline stage. Keeps a bounded window so a
/// two-hour meeting cannot grow the sample buffer without bound, and so the
/// reported percentiles reflect recent behavior rather than the whole session.
/// </summary>
public sealed class LatencyTracker
{
    private readonly double[] _samples;
    private readonly object _lock = new();
    private int _count;
    private int _next;

    public string Name { get; }
    public int Capacity => _samples.Length;

    /// <summary>Target for this stage; <see cref="MeetsTarget"/> compares p95 against it.</summary>
    public TimeSpan Target { get; init; } = TimeSpan.Zero;

    public LatencyTracker(string name, int capacity = 1000)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));

        Name = name;
        _samples = new double[capacity];
    }

    public int SampleCount
    {
        get { lock (_lock) return _count; }
    }

    public void Record(double milliseconds)
    {
        if (double.IsNaN(milliseconds) || milliseconds < 0) return;

        lock (_lock)
        {
            _samples[_next] = milliseconds;
            _next = (_next + 1) % _samples.Length;
            if (_count < _samples.Length) _count++;
        }
    }

    public void Record(TimeSpan elapsed) => Record(elapsed.TotalMilliseconds);

    public double Average
    {
        get
        {
            lock (_lock)
            {
                if (_count == 0) return 0;

                var sum = 0.0;
                for (var i = 0; i < _count; i++) sum += _samples[i];
                return sum / _count;
            }
        }
    }

    public double Min => Snapshot() is { Length: > 0 } s ? s[0] : 0;

    public double Max => Snapshot() is { Length: > 0 } s ? s[^1] : 0;

    public double P50 => Percentile(50);
    public double P95 => Percentile(95);
    public double P99 => Percentile(99);

    /// <summary>
    /// Nearest-rank percentile: the smallest sample at or above the given rank.
    /// Returns 0 when no samples have been recorded.
    /// </summary>
    public double Percentile(double percentile)
    {
        if (percentile is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(percentile), "Must be between 0 and 100.");

        var sorted = Snapshot();
        if (sorted.Length == 0) return 0;

        var rank = (int)Math.Ceiling(percentile / 100.0 * sorted.Length);
        var index = Math.Clamp(rank - 1, 0, sorted.Length - 1);

        return sorted[index];
    }

    /// <summary>True when p95 is within Target, or when no target was set.</summary>
    public bool MeetsTarget =>
        Target == TimeSpan.Zero || SampleCount == 0 || P95 <= Target.TotalMilliseconds;

    public void Reset()
    {
        lock (_lock)
        {
            _count = 0;
            _next = 0;
            Array.Clear(_samples);
        }
    }

    public LatencySnapshot GetSnapshot() => new()
    {
        Name = Name,
        SampleCount = SampleCount,
        AverageMs = Average,
        P50Ms = P50,
        P95Ms = P95,
        P99Ms = P99,
        MinMs = Min,
        MaxMs = Max,
        TargetMs = Target.TotalMilliseconds,
        MeetsTarget = MeetsTarget
    };

    private double[] Snapshot()
    {
        lock (_lock)
        {
            if (_count == 0) return Array.Empty<double>();

            var copy = new double[_count];
            Array.Copy(_samples, copy, _count);
            Array.Sort(copy);
            return copy;
        }
    }
}

public sealed record LatencySnapshot
{
    public string Name { get; init; } = "";
    public int SampleCount { get; init; }
    public double AverageMs { get; init; }
    public double P50Ms { get; init; }
    public double P95Ms { get; init; }
    public double P99Ms { get; init; }
    public double MinMs { get; init; }
    public double MaxMs { get; init; }
    public double TargetMs { get; init; }
    public bool MeetsTarget { get; init; }

    public string Format()
    {
        var target = TargetMs > 0 ? $" (alvo {TargetMs:F0}ms {(MeetsTarget ? "OK" : "EXCEDIDO")})" : "";
        return $"{Name,-24} n={SampleCount,-5} p50={P50Ms,7:F1}ms p95={P95Ms,7:F1}ms p99={P99Ms,7:F1}ms{target}";
    }
}
