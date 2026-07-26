namespace ZefaIA.App.Pipeline;

/// <summary>
/// One lifecycle-managed piece of the meeting pipeline. Stages are started in
/// registration order and stopped in reverse, so a stage may depend on anything
/// registered before it.
/// </summary>
public interface IPipelineStage
{
    string Name { get; }

    Task StartAsync(CancellationToken ct = default);

    Task StopAsync();
}
