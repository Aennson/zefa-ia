using Xunit;
using ZefaIA.App.Pipeline;

namespace ZefaIA.App.Tests;

public class StageRunnerTests
{
    #region Start ordering

    [Fact]
    public async Task StartAsync_StartsStagesInRegistrationOrder()
    {
        var log = new List<string>();
        var runner = new StageRunner()
            .Add(new RecordingStage("A", log))
            .Add(new RecordingStage("B", log))
            .Add(new RecordingStage("C", log));

        await runner.StartAsync();

        Assert.Equal(new[] { "start:A", "start:B", "start:C" }, log);
        Assert.True(runner.IsRunning);
    }

    [Fact]
    public async Task StartAsync_NoStages_Succeeds()
    {
        var runner = new StageRunner();
        await runner.StartAsync();

        Assert.True(runner.IsRunning);
        Assert.Empty(runner.StartedStages);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_Throws()
    {
        var runner = new StageRunner().Add(new RecordingStage("A", new List<string>()));
        await runner.StartAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.StartAsync());
    }

    [Fact]
    public async Task Add_WhileRunning_Throws()
    {
        var runner = new StageRunner();
        await runner.StartAsync();

        Assert.Throws<InvalidOperationException>(
            () => runner.Add(new RecordingStage("Late", new List<string>())));
    }

    #endregion

    #region Stop ordering

    [Fact]
    public async Task StopAsync_StopsStagesInReverseOrder()
    {
        var log = new List<string>();
        var runner = new StageRunner()
            .Add(new RecordingStage("A", log))
            .Add(new RecordingStage("B", log))
            .Add(new RecordingStage("C", log));

        await runner.StartAsync();
        log.Clear();
        await runner.StopAsync();

        Assert.Equal(new[] { "stop:C", "stop:B", "stop:A" }, log);
        Assert.False(runner.IsRunning);
    }

    [Fact]
    public async Task StopAsync_ClearsStartedStages()
    {
        var runner = new StageRunner().Add(new RecordingStage("A", new List<string>()));

        await runner.StartAsync();
        Assert.Single(runner.StartedStages);

        await runner.StopAsync();
        Assert.Empty(runner.StartedStages);
    }

    [Fact]
    public async Task StopAsync_WithoutStart_DoesNotThrow()
    {
        var runner = new StageRunner().Add(new RecordingStage("A", new List<string>()));
        await runner.StopAsync();

        Assert.False(runner.IsRunning);
    }

    [Fact]
    public async Task StartStopStart_CanRestart()
    {
        var log = new List<string>();
        var runner = new StageRunner().Add(new RecordingStage("A", log));

        await runner.StartAsync();
        await runner.StopAsync();
        await runner.StartAsync();

        Assert.True(runner.IsRunning);
        Assert.Equal(new[] { "start:A", "stop:A", "start:A" }, log);
    }

    #endregion

    #region Startup failure and rollback

    [Fact]
    public async Task StartAsync_StageThrows_WrapsInPipelineStartupException()
    {
        var runner = new StageRunner()
            .Add(new RecordingStage("A", new List<string>()))
            .Add(new FailingStage("B", failOnStart: true));

        var ex = await Assert.ThrowsAsync<PipelineStartupException>(() => runner.StartAsync());

        Assert.Equal("B", ex.StageName);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public async Task StartAsync_StageThrows_RollsBackAlreadyStartedStages()
    {
        var log = new List<string>();
        var runner = new StageRunner()
            .Add(new RecordingStage("A", log))
            .Add(new RecordingStage("B", log))
            .Add(new FailingStage("C", failOnStart: true));

        await Assert.ThrowsAsync<PipelineStartupException>(() => runner.StartAsync());

        // A and B started, then rolled back in reverse; C never started.
        Assert.Equal(new[] { "start:A", "start:B", "stop:B", "stop:A" }, log);
    }

    [Fact]
    public async Task StartAsync_FailedStart_LeavesRunnerNotRunning()
    {
        var runner = new StageRunner().Add(new FailingStage("A", failOnStart: true));

        await Assert.ThrowsAsync<PipelineStartupException>(() => runner.StartAsync());

        Assert.False(runner.IsRunning);
        Assert.Empty(runner.StartedStages);
    }

    [Fact]
    public async Task StartAsync_RollbackStopAlsoFails_StillThrowsStartupException()
    {
        var runner = new StageRunner()
            .Add(new FailingStage("A", failOnStart: false, failOnStop: true))
            .Add(new FailingStage("B", failOnStart: true));

        var ex = await Assert.ThrowsAsync<PipelineStartupException>(() => runner.StartAsync());

        // The original startup failure is what surfaces, not the rollback noise.
        Assert.Equal("B", ex.StageName);
        Assert.False(runner.IsRunning);
    }

    [Fact]
    public async Task StartAsync_AfterFailedStart_CanRetry()
    {
        var stage = new FailingStage("A", failOnStart: true);
        var runner = new StageRunner().Add(stage);

        await Assert.ThrowsAsync<PipelineStartupException>(() => runner.StartAsync());

        stage.FailOnStart = false;
        await runner.StartAsync();

        Assert.True(runner.IsRunning);
    }

    #endregion

    #region Shutdown error isolation

    [Fact]
    public async Task StopAsync_StageThrows_StillStopsRemainingStages()
    {
        var log = new List<string>();
        var runner = new StageRunner()
            .Add(new RecordingStage("A", log))
            .Add(new FailingStage("B", failOnStart: false, failOnStop: true))
            .Add(new RecordingStage("C", log));

        await runner.StartAsync();
        log.Clear();

        await Assert.ThrowsAsync<AggregateException>(() => runner.StopAsync());

        // C stopped, B threw, and A still got stopped despite B's failure — this is
        // what protects the persistence flush registered first.
        Assert.Equal(new[] { "stop:C", "stop:A" }, log);
    }

    [Fact]
    public async Task StopAsync_MultipleFailures_AggregatesAll()
    {
        var runner = new StageRunner()
            .Add(new FailingStage("A", failOnStart: false, failOnStop: true))
            .Add(new FailingStage("B", failOnStart: false, failOnStop: true));

        await runner.StartAsync();

        var ex = await Assert.ThrowsAsync<AggregateException>(() => runner.StopAsync());

        Assert.Equal(2, ex.InnerExceptions.Count);
    }

    [Fact]
    public async Task StopAsync_StageThrows_RunnerStillMarkedStopped()
    {
        var runner = new StageRunner()
            .Add(new FailingStage("A", failOnStart: false, failOnStop: true));

        await runner.StartAsync();
        await Assert.ThrowsAsync<AggregateException>(() => runner.StopAsync());

        Assert.False(runner.IsRunning);
        Assert.Empty(runner.StartedStages);
    }

    #endregion

    #region DelegateStage

    [Fact]
    public async Task DelegateStage_InvokesStartAndStop()
    {
        var started = false;
        var stopped = false;

        var stage = new DelegateStage("Test",
            _ => { started = true; return Task.CompletedTask; },
            () => { stopped = true; return Task.CompletedTask; });

        await stage.StartAsync();
        await stage.StopAsync();

        Assert.True(started);
        Assert.True(stopped);
        Assert.Equal("Test", stage.Name);
    }

    [Fact]
    public async Task DelegateStage_Sync_WrapsSynchronousComponents()
    {
        var order = new List<string>();
        var stage = DelegateStage.Sync("Sync",
            () => order.Add("start"),
            () => order.Add("stop"));

        await stage.StartAsync();
        await stage.StopAsync();

        Assert.Equal(new[] { "start", "stop" }, order);
    }

    [Fact]
    public async Task DelegateStage_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken received = default;

        var stage = new DelegateStage("Test",
            ct => { received = ct; return Task.CompletedTask; },
            () => Task.CompletedTask);

        await stage.StartAsync(cts.Token);

        Assert.Equal(cts.Token, received);
    }

    #endregion

    #region Test doubles

    private sealed class RecordingStage : IPipelineStage
    {
        private readonly List<string> _log;

        public string Name { get; }

        public RecordingStage(string name, List<string> log)
        {
            Name = name;
            _log = log;
        }

        public Task StartAsync(CancellationToken ct = default)
        {
            _log.Add($"start:{Name}");
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _log.Add($"stop:{Name}");
            return Task.CompletedTask;
        }
    }

    private sealed class FailingStage : IPipelineStage
    {
        public string Name { get; }
        public bool FailOnStart { get; set; }
        public bool FailOnStop { get; set; }

        public FailingStage(string name, bool failOnStart, bool failOnStop = false)
        {
            Name = name;
            FailOnStart = failOnStart;
            FailOnStop = failOnStop;
        }

        public Task StartAsync(CancellationToken ct = default)
        {
            if (FailOnStart)
                throw new InvalidOperationException($"{Name} failed to start");
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            if (FailOnStop)
                throw new InvalidOperationException($"{Name} failed to stop");
            return Task.CompletedTask;
        }
    }

    #endregion
}
