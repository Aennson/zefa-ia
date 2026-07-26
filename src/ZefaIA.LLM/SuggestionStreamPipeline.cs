using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.LLM;

public enum SuggestionState
{
    Idle,
    Thinking,
    Streaming,
    Complete,
    Error
}

public sealed class SuggestionStreamPipeline : IDisposable
{
    private readonly ILogger<SuggestionStreamPipeline> _logger;
    private SuggestionState _state = SuggestionState.Idle;
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private CancellationTokenSource? _currentCts;
    private bool _disposed;

    private const string NoSuggestionMarker = "[SEM SUGESTAO]";

    public SuggestionState State => _state;

    public event Action? OnThinkingStarted;
    public event Action<string>? OnTokenReceived;
    public event Action? OnComplete;
    public event Action<string>? OnError;
    public event Action<SuggestionState>? OnStateChanged;

    public SuggestionStreamPipeline(ILogger<SuggestionStreamPipeline>? logger = null)
    {
        _logger = logger ?? NullLogger<SuggestionStreamPipeline>.Instance;
    }

    public async Task RequestSuggestionAsync(
        ILLMSession session,
        string recentTranscript,
        SuggestionContext context,
        CancellationToken ct = default)
    {
        if (!await _requestLock.WaitAsync(0, ct))
        {
            _logger.LogDebug("Suggestion request queued — another is in progress");
            await _requestLock.WaitAsync(ct);
        }

        try
        {
            _currentCts?.Cancel();
            _currentCts?.Dispose();
            _currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var linkedCt = _currentCts.Token;

            SetState(SuggestionState.Thinking);
            OnThinkingStarted?.Invoke();

            var fullText = new System.Text.StringBuilder();

            await foreach (var token in session.GetSuggestionStreamAsync(recentTranscript, context, linkedCt))
            {
                if (_state == SuggestionState.Thinking)
                    SetState(SuggestionState.Streaming);

                fullText.Append(token);

                if (IsNoSuggestion(fullText.ToString()))
                {
                    SetState(SuggestionState.Complete);
                    OnComplete?.Invoke();
                    return;
                }

                OnTokenReceived?.Invoke(token);
            }

            SetState(SuggestionState.Complete);
            OnComplete?.Invoke();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            SetState(SuggestionState.Idle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during suggestion streaming");
            SetState(SuggestionState.Error);
            OnError?.Invoke(ex.Message);
        }
        finally
        {
            _requestLock.Release();
        }
    }

    public void Cancel()
    {
        _currentCts?.Cancel();
    }

    internal static bool IsNoSuggestion(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Equals(NoSuggestionMarker, StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith(NoSuggestionMarker, StringComparison.OrdinalIgnoreCase);
    }

    private void SetState(SuggestionState newState)
    {
        if (_state == newState) return;
        _state = newState;
        OnStateChanged?.Invoke(newState);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _currentCts?.Cancel();
        _currentCts?.Dispose();
        _requestLock.Dispose();
    }
}
