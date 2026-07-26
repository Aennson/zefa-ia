namespace ZefaIA.Core.Resilience;

public enum HealthState
{
    Healthy,
    Degraded,
    Failed
}

/// <summary>
/// Tracks per-component failure streaks so the app can tell a transient blip
/// (retry silently) from a persistent fault (tell the user). A component only
/// surfaces to the user once it crosses the degraded threshold, which keeps the
/// overlay quiet during the normal reconnect churn of a flaky mic.
/// </summary>
public sealed class HealthTracker
{
    private readonly Dictionary<string, ComponentHealth> _components = new();
    private readonly object _lock = new();

    public int DegradedThreshold { get; init; } = 2;
    public int FailedThreshold { get; init; } = 5;

    /// <summary>Raised when a component's state changes, not on every failure.</summary>
    public event Action<string, HealthState>? OnHealthChanged;

    public IReadOnlyDictionary<string, ComponentHealth> Components
    {
        get { lock (_lock) return new Dictionary<string, ComponentHealth>(_components); }
    }

    public HealthState OverallState
    {
        get
        {
            lock (_lock)
            {
                if (_components.Count == 0) return HealthState.Healthy;
                if (_components.Values.Any(c => c.State == HealthState.Failed)) return HealthState.Failed;
                if (_components.Values.Any(c => c.State == HealthState.Degraded)) return HealthState.Degraded;
                return HealthState.Healthy;
            }
        }
    }

    public void RecordSuccess(string component)
    {
        HealthState? changedTo = null;

        lock (_lock)
        {
            var health = GetOrCreate(component);
            var previous = health.State;

            health.ConsecutiveFailures = 0;
            health.State = HealthState.Healthy;
            health.LastSuccess = DateTime.UtcNow;
            health.LastError = null;

            if (previous != HealthState.Healthy)
                changedTo = HealthState.Healthy;
        }

        if (changedTo.HasValue)
            OnHealthChanged?.Invoke(component, changedTo.Value);
    }

    public void RecordFailure(string component, string? error = null)
    {
        HealthState? changedTo = null;

        lock (_lock)
        {
            var health = GetOrCreate(component);
            var previous = health.State;

            health.ConsecutiveFailures++;
            health.TotalFailures++;
            health.LastFailure = DateTime.UtcNow;
            health.LastError = error;

            health.State = health.ConsecutiveFailures >= FailedThreshold
                ? HealthState.Failed
                : health.ConsecutiveFailures >= DegradedThreshold
                    ? HealthState.Degraded
                    : HealthState.Healthy;

            if (previous != health.State)
                changedTo = health.State;
        }

        if (changedTo.HasValue)
            OnHealthChanged?.Invoke(component, changedTo.Value);
    }

    public HealthState GetState(string component)
    {
        lock (_lock)
            return _components.TryGetValue(component, out var h) ? h.State : HealthState.Healthy;
    }

    public void Reset(string component)
    {
        lock (_lock) _components.Remove(component);
    }

    public void ResetAll()
    {
        lock (_lock) _components.Clear();
    }

    private ComponentHealth GetOrCreate(string component)
    {
        if (!_components.TryGetValue(component, out var health))
        {
            health = new ComponentHealth { Name = component };
            _components[component] = health;
        }
        return health;
    }
}

public sealed class ComponentHealth
{
    public string Name { get; init; } = "";
    public HealthState State { get; set; } = HealthState.Healthy;
    public int ConsecutiveFailures { get; set; }
    public int TotalFailures { get; set; }
    public DateTime? LastSuccess { get; set; }
    public DateTime? LastFailure { get; set; }
    public string? LastError { get; set; }
}
