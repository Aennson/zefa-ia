namespace ZefaIA.App.Pipeline;

/// <summary>
/// Adapts the existing components (which predate IPipelineStage and each expose
/// their own start/stop shape) to the staged lifecycle without a wrapper class per
/// component.
/// </summary>
public sealed class DelegateStage : IPipelineStage
{
    private readonly Func<CancellationToken, Task> _start;
    private readonly Func<Task> _stop;

    public string Name { get; }

    public DelegateStage(
        string name,
        Func<CancellationToken, Task> start,
        Func<Task> stop)
    {
        Name = name;
        _start = start;
        _stop = stop;
    }

    /// <summary>Wraps components whose start and stop are both synchronous.</summary>
    public static DelegateStage Sync(string name, Action start, Action stop) =>
        new(name,
            _ => { start(); return Task.CompletedTask; },
            () => { stop(); return Task.CompletedTask; });

    public Task StartAsync(CancellationToken ct = default) => _start(ct);

    public Task StopAsync() => _stop();
}
