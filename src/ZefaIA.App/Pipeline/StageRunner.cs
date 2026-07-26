using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ZefaIA.App.Pipeline;

/// <summary>
/// Drives the startup and shutdown sequences over an ordered list of stages.
///
/// Startup is fail-fast: if a stage throws, the stages already started are rolled
/// back (in reverse) and the exception surfaces to the caller, so the app never
/// sits in a half-started state.
///
/// Shutdown is best-effort: every stage gets its StopAsync called even if an
/// earlier one throws, because a failure while stopping must not strand the
/// remaining stages — that is what would lose the unflushed transcription batch.
/// </summary>
public sealed class StageRunner
{
    private readonly List<IPipelineStage> _stages = new();
    private readonly List<IPipelineStage> _started = new();
    private readonly ILogger<StageRunner> _logger;

    public IReadOnlyList<IPipelineStage> Stages => _stages;
    public IReadOnlyList<IPipelineStage> StartedStages => _started;
    public bool IsRunning { get; private set; }

    public StageRunner(ILogger<StageRunner>? logger = null)
    {
        _logger = logger ?? NullLogger<StageRunner>.Instance;
    }

    public StageRunner Add(IPipelineStage stage)
    {
        if (IsRunning)
            throw new InvalidOperationException("Cannot add stages while running.");

        _stages.Add(stage);
        return this;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (IsRunning)
            throw new InvalidOperationException("Already running.");

        _started.Clear();

        foreach (var stage in _stages)
        {
            try
            {
                await stage.StartAsync(ct);
                _started.Add(stage);
                _logger.LogInformation("Stage started: {Stage}", stage.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stage failed to start: {Stage} — rolling back", stage.Name);
                await RollbackAsync();
                throw new PipelineStartupException(stage.Name, ex);
            }
        }

        IsRunning = true;
        _logger.LogInformation("Pipeline started with {Count} stages", _started.Count);
    }

    /// <summary>
    /// Stops every started stage in reverse order. Exceptions are collected rather
    /// than thrown immediately so one failing stage cannot skip the others; if any
    /// stage failed, an AggregateException is thrown after all have been attempted.
    /// </summary>
    public async Task StopAsync()
    {
        var errors = new List<Exception>();

        for (var i = _started.Count - 1; i >= 0; i--)
        {
            var stage = _started[i];
            try
            {
                await stage.StopAsync();
                _logger.LogInformation("Stage stopped: {Stage}", stage.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stage failed to stop: {Stage} — continuing", stage.Name);
                errors.Add(ex);
            }
        }

        _started.Clear();
        IsRunning = false;

        if (errors.Count > 0)
            throw new AggregateException("One or more stages failed to stop.", errors);

        _logger.LogInformation("Pipeline stopped cleanly");
    }

    private async Task RollbackAsync()
    {
        for (var i = _started.Count - 1; i >= 0; i--)
        {
            try
            {
                await _started[i].StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rollback failed for stage {Stage}", _started[i].Name);
            }
        }

        _started.Clear();
        IsRunning = false;
    }
}

public sealed class PipelineStartupException : Exception
{
    public string StageName { get; }

    public PipelineStartupException(string stageName, Exception inner)
        : base($"Pipeline stage '{stageName}' failed to start.", inner)
    {
        StageName = stageName;
    }
}
