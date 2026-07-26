using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.LLM;

public sealed class SuggestionOrchestrator : IDisposable
{
    private readonly ILLMSession _session;
    private readonly SuggestionStreamPipeline _pipeline;
    private readonly OrchestratorConfig _config;
    private readonly ILogger<SuggestionOrchestrator> _logger;
    private readonly List<ITriggerStrategy> _triggers = new();
    private readonly Queue<DateTime> _requestTimestamps = new();
    private string _lastTranscriptHash = "";
    private bool _disposed;

    public SuggestionStreamPipeline Pipeline => _pipeline;
    public OrchestratorMetrics Metrics { get; } = new();

    public SuggestionOrchestrator(
        ILLMSession session,
        SuggestionStreamPipeline pipeline,
        OrchestratorConfig? config = null,
        ILogger<SuggestionOrchestrator>? logger = null)
    {
        _session = session;
        _pipeline = pipeline;
        _config = config ?? new OrchestratorConfig();
        _logger = logger ?? NullLogger<SuggestionOrchestrator>.Instance;
    }

    public void RegisterTrigger(ITriggerStrategy trigger)
    {
        trigger.Triggered += OnTriggerFired;
        _triggers.Add(trigger);
    }

    public Func<TimeSpan, string>? TranscriptProvider { get; set; }

    private async void OnTriggerFired(object? sender, TriggerEventArgs args)
    {
        try
        {
            await HandleTriggerAsync(args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling trigger {Trigger}", args.TriggerName);
        }
    }

    internal async Task HandleTriggerAsync(TriggerEventArgs args)
    {
        if (!IsRateLimitAllowed())
        {
            _logger.LogDebug("Rate limit exceeded, skipping trigger");
            Metrics.RateLimitedCount++;
            return;
        }

        var transcript = TranscriptProvider?.Invoke(args.TranscriptWindow) ?? "";
        if (string.IsNullOrWhiteSpace(transcript))
        {
            _logger.LogDebug("No transcript available, skipping");
            return;
        }

        if (IsDuplicate(transcript))
        {
            _logger.LogDebug("Duplicate transcript, skipping");
            Metrics.DeduplicatedCount++;
            return;
        }

        RecordRequest();
        _lastTranscriptHash = ComputeHash(transcript);
        Metrics.TotalRequests++;

        var context = new SuggestionContext(transcript, args.Reason, args.TranscriptWindow);
        await _pipeline.RequestSuggestionAsync(_session, transcript, context);

        UpdateCostEstimate();
    }

    internal bool IsRateLimitAllowed()
    {
        PruneOldTimestamps();
        return _requestTimestamps.Count < _config.MaxRequestsPerMinute;
    }

    private void PruneOldTimestamps()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-1);
        while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < cutoff)
            _requestTimestamps.Dequeue();
    }

    private void RecordRequest()
    {
        _requestTimestamps.Enqueue(DateTime.UtcNow);
    }

    internal bool IsDuplicate(string transcript)
    {
        return ComputeHash(transcript) == _lastTranscriptHash && _lastTranscriptHash != "";
    }

    internal static string ComputeHash(string text)
    {
        var hash = 0;
        foreach (var c in text)
            hash = (hash * 31 + c) & 0x7FFFFFFF;
        return hash.ToString("X8");
    }

    private void UpdateCostEstimate()
    {
        var metrics = _session.Metrics;
        var inputCost = metrics.TotalInputTokens * 0.003 / 1000;
        var outputCost = metrics.TotalOutputTokens * 0.015 / 1000;
        var cacheCost = metrics.CacheHits * 0.0003 / 1000;
        Metrics.EstimatedCostUsd = inputCost + outputCost - cacheCost;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var trigger in _triggers)
        {
            trigger.Triggered -= OnTriggerFired;
        }
        _triggers.Clear();
    }
}

public record OrchestratorConfig
{
    public int MaxRequestsPerMinute { get; init; } = 4;
    public TimeSpan DefaultTranscriptWindow { get; init; } = TimeSpan.FromSeconds(60);
}

public class OrchestratorMetrics
{
    public int TotalRequests { get; set; }
    public int RateLimitedCount { get; set; }
    public int DeduplicatedCount { get; set; }
    public double EstimatedCostUsd { get; set; }
}
