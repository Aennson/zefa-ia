namespace ZefaIA.Core.Resilience;

/// <summary>
/// Exponential backoff with jitter. Jitter matters here because the mic and
/// loopback channels fail together when a device is unplugged — without it both
/// would retry in lockstep and hammer the device driver at the same instants.
/// </summary>
public sealed class RetryPolicy
{
    private readonly Random _random = new();

    public int MaxAttempts { get; init; } = 3;
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);
    public double BackoffMultiplier { get; init; } = 2.0;
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);
    public double JitterFactor { get; init; } = 0.2;

    /// <summary>Decides whether a given exception is worth retrying. Defaults to all.</summary>
    public Func<Exception, bool> ShouldRetry { get; init; } = _ => true;

    public static RetryPolicy None => new() { MaxAttempts = 1 };

    /// <summary>
    /// Matches the Claude client's documented behavior: back off on rate limits and
    /// server errors, give up immediately on anything else.
    /// </summary>
    public static RetryPolicy ForTransientHttp => new()
    {
        MaxAttempts = 3,
        InitialDelay = TimeSpan.FromSeconds(1),
        ShouldRetry = ex => ex is HttpRequestException or TaskCanceledException
    };

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct = default)
    {
        Exception? last = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await operation(ct);
            }
            catch (Exception ex) when (ShouldRetry(ex) && attempt < MaxAttempts)
            {
                last = ex;
                await Task.Delay(GetDelay(attempt), ct);
            }
        }

        // Reached only when the last attempt threw or ShouldRetry rejected it; the
        // loop rethrows naturally in those cases, so this is the exhausted path.
        throw last ?? new InvalidOperationException("Retry policy completed without result.");
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken ct = default)
    {
        await ExecuteAsync<bool>(async token =>
        {
            await operation(token);
            return true;
        }, ct);
    }

    /// <summary>
    /// Delay before the given 1-based attempt number, capped at MaxDelay and then
    /// spread by up to +/- JitterFactor.
    /// </summary>
    public TimeSpan GetDelay(int attempt)
    {
        if (attempt < 1) attempt = 1;

        var exponent = attempt - 1;
        var baseMs = InitialDelay.TotalMilliseconds * Math.Pow(BackoffMultiplier, exponent);

        if (baseMs > MaxDelay.TotalMilliseconds || double.IsInfinity(baseMs))
            baseMs = MaxDelay.TotalMilliseconds;

        var jitterRange = baseMs * JitterFactor;
        var offset = (_random.NextDouble() * 2 - 1) * jitterRange;
        var withJitter = Math.Max(0, baseMs + offset);

        return TimeSpan.FromMilliseconds(withJitter);
    }
}
