using ZefaIA.Core.Models;

namespace ZefaIA.Core.Interfaces;

public interface ILLMClient : IAsyncDisposable
{
    Task<ILLMSession> CreateSessionAsync(LLMSessionConfig config, CancellationToken ct = default);
}

public interface ILLMSession : IAsyncDisposable
{
    IAsyncEnumerable<string> GetSuggestionStreamAsync(
        string recentTranscript,
        SuggestionContext context,
        CancellationToken ct = default);

    LLMSessionMetrics Metrics { get; }
}
