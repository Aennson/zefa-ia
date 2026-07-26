using Xunit;
using ZefaIA.Core.Resilience;

namespace ZefaIA.App.Tests;

public class RetryPolicyTests
{
    #region Execution

    [Fact]
    public async Task ExecuteAsync_SucceedsFirstTry_CallsOnce()
    {
        var calls = 0;
        var policy = FastPolicy();

        var result = await policy.ExecuteAsync(_ =>
        {
            calls++;
            return Task.FromResult(42);
        });

        Assert.Equal(42, result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExecuteAsync_FailsThenSucceeds_Retries()
    {
        var calls = 0;
        var policy = FastPolicy();

        var result = await policy.ExecuteAsync(_ =>
        {
            calls++;
            if (calls < 3) throw new InvalidOperationException("transient");
            return Task.FromResult("ok");
        });

        Assert.Equal("ok", result);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task ExecuteAsync_AlwaysFails_ThrowsLastException()
    {
        var calls = 0;
        var policy = FastPolicy();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            policy.ExecuteAsync<int>(_ =>
            {
                calls++;
                throw new InvalidOperationException($"attempt {calls}");
            }));

        Assert.Equal(3, calls);
        Assert.Equal("attempt 3", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_NonRetryableException_DoesNotRetry()
    {
        var calls = 0;
        var policy = new RetryPolicy
        {
            MaxAttempts = 3,
            InitialDelay = TimeSpan.Zero,
            ShouldRetry = ex => ex is HttpRequestException
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            policy.ExecuteAsync<int>(_ =>
            {
                calls++;
                throw new ArgumentException("permanent");
            }));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExecuteAsync_VoidOverload_Runs()
    {
        var calls = 0;
        var policy = FastPolicy();

        await policy.ExecuteAsync(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExecuteAsync_None_NeverRetries()
    {
        var calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RetryPolicy.None.ExecuteAsync<int>(_ =>
            {
                calls++;
                throw new InvalidOperationException();
            }));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken received = default;

        await FastPolicy().ExecuteAsync(ct =>
        {
            received = ct;
            return Task.FromResult(0);
        }, cts.Token);

        Assert.Equal(cts.Token, received);
    }

    #endregion

    #region Backoff

    [Fact]
    public void GetDelay_GrowsExponentially()
    {
        var policy = new RetryPolicy
        {
            InitialDelay = TimeSpan.FromSeconds(1),
            BackoffMultiplier = 2.0,
            JitterFactor = 0,
            MaxDelay = TimeSpan.FromMinutes(1)
        };

        Assert.Equal(1000, policy.GetDelay(1).TotalMilliseconds, precision: 0);
        Assert.Equal(2000, policy.GetDelay(2).TotalMilliseconds, precision: 0);
        Assert.Equal(4000, policy.GetDelay(3).TotalMilliseconds, precision: 0);
    }

    [Fact]
    public void GetDelay_CapsAtMaxDelay()
    {
        var policy = new RetryPolicy
        {
            InitialDelay = TimeSpan.FromSeconds(1),
            BackoffMultiplier = 2.0,
            JitterFactor = 0,
            MaxDelay = TimeSpan.FromSeconds(5)
        };

        Assert.Equal(5000, policy.GetDelay(10).TotalMilliseconds, precision: 0);
    }

    [Fact]
    public void GetDelay_WithJitter_StaysWithinBand()
    {
        var policy = new RetryPolicy
        {
            InitialDelay = TimeSpan.FromSeconds(1),
            BackoffMultiplier = 2.0,
            JitterFactor = 0.2,
            MaxDelay = TimeSpan.FromMinutes(1)
        };

        for (var i = 0; i < 50; i++)
        {
            var delay = policy.GetDelay(2).TotalMilliseconds;
            Assert.InRange(delay, 1600, 2400);
        }
    }

    [Fact]
    public void GetDelay_WithJitter_ProducesVariedValues()
    {
        var policy = new RetryPolicy { InitialDelay = TimeSpan.FromSeconds(1), JitterFactor = 0.2 };

        var values = Enumerable.Range(0, 20)
            .Select(_ => policy.GetDelay(1).TotalMilliseconds)
            .Distinct()
            .Count();

        // Jitter exists so simultaneous failures do not retry in lockstep.
        Assert.True(values > 1, "jitter should spread retry delays");
    }

    [Fact]
    public void GetDelay_NeverNegative()
    {
        var policy = new RetryPolicy { InitialDelay = TimeSpan.FromMilliseconds(1), JitterFactor = 1.0 };

        for (var i = 0; i < 50; i++)
            Assert.True(policy.GetDelay(1) >= TimeSpan.Zero);
    }

    [Fact]
    public void GetDelay_AttemptZeroOrNegative_TreatedAsFirst()
    {
        var policy = new RetryPolicy { InitialDelay = TimeSpan.FromSeconds(1), JitterFactor = 0 };

        Assert.Equal(policy.GetDelay(1), policy.GetDelay(0));
        Assert.Equal(policy.GetDelay(1), policy.GetDelay(-5));
    }

    [Fact]
    public void ForTransientHttp_RetriesHttpButNotArgument()
    {
        var policy = RetryPolicy.ForTransientHttp;

        Assert.True(policy.ShouldRetry(new HttpRequestException()));
        Assert.True(policy.ShouldRetry(new TaskCanceledException()));
        Assert.False(policy.ShouldRetry(new ArgumentException()));
    }

    #endregion

    private static RetryPolicy FastPolicy() => new()
    {
        MaxAttempts = 3,
        InitialDelay = TimeSpan.Zero,
        JitterFactor = 0
    };
}

public class HealthTrackerTests
{
    [Fact]
    public void GetState_UnknownComponent_IsHealthy()
    {
        var tracker = new HealthTracker();
        Assert.Equal(HealthState.Healthy, tracker.GetState("mic"));
    }

    [Fact]
    public void RecordFailure_BelowThreshold_StaysHealthy()
    {
        var tracker = new HealthTracker { DegradedThreshold = 3 };

        tracker.RecordFailure("mic");
        tracker.RecordFailure("mic");

        Assert.Equal(HealthState.Healthy, tracker.GetState("mic"));
    }

    [Fact]
    public void RecordFailure_AtDegradedThreshold_BecomesDegraded()
    {
        var tracker = new HealthTracker { DegradedThreshold = 2, FailedThreshold = 5 };

        tracker.RecordFailure("mic");
        tracker.RecordFailure("mic");

        Assert.Equal(HealthState.Degraded, tracker.GetState("mic"));
    }

    [Fact]
    public void RecordFailure_AtFailedThreshold_BecomesFailed()
    {
        var tracker = new HealthTracker { DegradedThreshold = 2, FailedThreshold = 4 };

        for (var i = 0; i < 4; i++)
            tracker.RecordFailure("mic");

        Assert.Equal(HealthState.Failed, tracker.GetState("mic"));
    }

    [Fact]
    public void RecordSuccess_ResetsFailureStreak()
    {
        var tracker = new HealthTracker { DegradedThreshold = 2 };

        tracker.RecordFailure("mic");
        tracker.RecordFailure("mic");
        tracker.RecordSuccess("mic");

        Assert.Equal(HealthState.Healthy, tracker.GetState("mic"));
        Assert.Equal(0, tracker.Components["mic"].ConsecutiveFailures);
    }

    [Fact]
    public void RecordSuccess_KeepsTotalFailureCount()
    {
        var tracker = new HealthTracker();

        tracker.RecordFailure("mic");
        tracker.RecordFailure("mic");
        tracker.RecordSuccess("mic");

        Assert.Equal(2, tracker.Components["mic"].TotalFailures);
    }

    [Fact]
    public void OnHealthChanged_FiresOnlyOnTransition()
    {
        var tracker = new HealthTracker { DegradedThreshold = 2, FailedThreshold = 10 };
        var events = new List<(string, HealthState)>();
        tracker.OnHealthChanged += (name, state) => events.Add((name, state));

        tracker.RecordFailure("mic");   // still healthy, no event
        tracker.RecordFailure("mic");   // -> degraded, one event
        tracker.RecordFailure("mic");   // still degraded, no event

        Assert.Single(events);
        Assert.Equal(("mic", HealthState.Degraded), events[0]);
    }

    [Fact]
    public void OnHealthChanged_FiresOnRecovery()
    {
        var tracker = new HealthTracker { DegradedThreshold = 1 };
        var events = new List<HealthState>();
        tracker.OnHealthChanged += (_, state) => events.Add(state);

        tracker.RecordFailure("mic");
        tracker.RecordSuccess("mic");

        Assert.Equal(new[] { HealthState.Degraded, HealthState.Healthy }, events);
    }

    [Fact]
    public void OnHealthChanged_RepeatedSuccess_DoesNotRefire()
    {
        var tracker = new HealthTracker();
        var count = 0;
        tracker.OnHealthChanged += (_, _) => count++;

        tracker.RecordSuccess("mic");
        tracker.RecordSuccess("mic");

        Assert.Equal(0, count);
    }

    [Fact]
    public void OverallState_NoComponents_IsHealthy()
    {
        Assert.Equal(HealthState.Healthy, new HealthTracker().OverallState);
    }

    [Fact]
    public void OverallState_ReflectsWorstComponent()
    {
        var tracker = new HealthTracker { DegradedThreshold = 1, FailedThreshold = 2 };

        tracker.RecordSuccess("audio");
        tracker.RecordFailure("mic");
        Assert.Equal(HealthState.Degraded, tracker.OverallState);

        tracker.RecordFailure("stt");
        tracker.RecordFailure("stt");
        Assert.Equal(HealthState.Failed, tracker.OverallState);
    }

    [Fact]
    public void RecordFailure_StoresLastError()
    {
        var tracker = new HealthTracker();
        tracker.RecordFailure("mic", "device disconnected");

        Assert.Equal("device disconnected", tracker.Components["mic"].LastError);
    }

    [Fact]
    public void RecordSuccess_ClearsLastError()
    {
        var tracker = new HealthTracker();
        tracker.RecordFailure("mic", "boom");
        tracker.RecordSuccess("mic");

        Assert.Null(tracker.Components["mic"].LastError);
    }

    [Fact]
    public void Reset_ForgetsComponent()
    {
        var tracker = new HealthTracker { DegradedThreshold = 1 };
        tracker.RecordFailure("mic");
        tracker.Reset("mic");

        Assert.Equal(HealthState.Healthy, tracker.GetState("mic"));
        Assert.Empty(tracker.Components);
    }

    [Fact]
    public void ResetAll_ClearsEverything()
    {
        var tracker = new HealthTracker { DegradedThreshold = 1 };
        tracker.RecordFailure("mic");
        tracker.RecordFailure("stt");
        tracker.ResetAll();

        Assert.Empty(tracker.Components);
        Assert.Equal(HealthState.Healthy, tracker.OverallState);
    }

    [Fact]
    public void ComponentsAreTrackedIndependently()
    {
        var tracker = new HealthTracker { DegradedThreshold = 1 };

        tracker.RecordFailure("mic");

        Assert.Equal(HealthState.Degraded, tracker.GetState("mic"));
        Assert.Equal(HealthState.Healthy, tracker.GetState("loopback"));
    }
}
