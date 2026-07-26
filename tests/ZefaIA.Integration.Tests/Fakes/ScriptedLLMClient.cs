using ZefaIA.Core.Interfaces;
using ZefaIA.Core.Models;

namespace ZefaIA.Integration.Tests.Fakes;

/// <summary>
/// An <see cref="ILLMClient"/> that replays a scripted token stream instead of calling
/// the Anthropic API.
///
/// Calling the real API from an automated test would make the suite depend on a paid
/// key, on network availability, and on non-deterministic model output. What the
/// pipeline actually needs to be correct about is everything *around* the call —
/// prompt assembly, trigger gating, token fan-out to overlay and persistence, error
/// handling — and that is exactly what this fake keeps under test. Validating the real
/// request/response against the live API is tracked separately; see
/// docs/tests/E2E-COVERAGE.md.
/// </summary>
public sealed class ScriptedLLMClient : ILLMClient
{
    private readonly Func<string, IEnumerable<string>> _respond;

    /// <summary>Prompts the pipeline sent, in order, for asserting what was requested.</summary>
    public List<string> ReceivedTranscripts { get; } = new();
    public List<LLMSessionConfig> CreatedSessions { get; } = new();

    /// <param name="respond">
    /// Maps the recent transcript to the tokens streamed back. Defaults to a fixed
    /// two-token reply, deliberately split so the streaming path is exercised.
    /// </param>
    public ScriptedLLMClient(Func<string, IEnumerable<string>>? respond = null)
    {
        _respond = respond ?? (_ => new[] { "Sugestao: ", "pergunte sobre o prazo." });
    }

    public Task<ILLMSession> CreateSessionAsync(LLMSessionConfig config, CancellationToken ct = default)
    {
        CreatedSessions.Add(config);
        return Task.FromResult<ILLMSession>(new ScriptedLLMSession(this, _respond));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class ScriptedLLMSession : ILLMSession
    {
        private readonly ScriptedLLMClient _owner;
        private readonly Func<string, IEnumerable<string>> _respond;

        public LLMSessionMetrics Metrics { get; } = new();

        public ScriptedLLMSession(ScriptedLLMClient owner, Func<string, IEnumerable<string>> respond)
        {
            _owner = owner;
            _respond = respond;
        }

        public async IAsyncEnumerable<string> GetSuggestionStreamAsync(
            string recentTranscript,
            SuggestionContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            _owner.ReceivedTranscripts.Add(recentTranscript);
            Metrics.TotalRequests++;

            foreach (var token in _respond(recentTranscript))
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return token;
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
