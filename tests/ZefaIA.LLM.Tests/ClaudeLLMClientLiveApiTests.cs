using Xunit;
using ZefaIA.Core.Models;
using ZefaIA.LLM;

namespace ZefaIA.LLM.Tests;

/// <summary>
/// The only tests here that actually talk to api.anthropic.com. Everything else in this
/// project runs against a mocked HttpMessageHandler, which proves the client parses what
/// we *think* the API returns — not that the API accepts what we send.
///
/// This is what catches a retired model id, a rejected parameter, or a changed stream
/// format. Run it after any change to the model, the request body, or the headers:
///
///   $env:ANTHROPIC_API_KEY = "sk-ant-..."
///   $env:ZEFA_RUN_ANTHROPIC_INTEGRATION = "1"
///   dotnet test tests/ZefaIA.LLM.Tests --filter "FullyQualifiedName~LiveApi"
/// </summary>
public class ClaudeLLMClientLiveApiTests
{
    private const string OptIn = "ZEFA_RUN_ANTHROPIC_INTEGRATION";
    private const string Requirement = "Calls the real Anthropic API and needs ANTHROPIC_API_KEY";

    [OptInFact(OptIn, Requirement)]
    public async Task LiveApi_DefaultConfig_IsAcceptedAndStreamsTokens()
    {
        // The default config is the one production uses. If the model id has been
        // retired or a parameter is no longer accepted, this is where it surfaces.
        var config = new LLMSessionConfig(
            SystemPrompt: "Voce e um assistente de reuniao. Responda em no maximo 10 palavras.",
            MeetingContext: "Teste de integracao");

        await using var client = new ClaudeLLMClient();
        await using var session = await client.CreateSessionAsync(config);

        var tokens = new List<string>();
        var context = new SuggestionContext("teste", TriggerReason.Manual, TimeSpan.FromSeconds(60));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await foreach (var token in session.GetSuggestionStreamAsync(
            "Participante: qual e a capital da Franca?", context, cts.Token))
        {
            tokens.Add(token);
        }

        Assert.NotEmpty(tokens);
        Assert.False(string.IsNullOrWhiteSpace(string.Concat(tokens)),
            "the stream produced only empty tokens — check whether thinking blocks are " +
            "being parsed as text deltas");
    }

    [OptInFact(OptIn, Requirement)]
    public async Task LiveApi_ConfiguredModelExists()
    {
        // A 404 here means the model id is wrong or retired — the exact failure that
        // claude-sonnet-4-20250514 would have produced once its retirement date passed.
        var config = new LLMSessionConfig("Responda apenas: OK", "Teste", MaxTokens: 16);

        await using var client = new ClaudeLLMClient();
        await using var session = await client.CreateSessionAsync(config);

        var context = new SuggestionContext("", TriggerReason.Manual, TimeSpan.FromSeconds(10));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var exception = await Record.ExceptionAsync(async () =>
        {
            await foreach (var _ in session.GetSuggestionStreamAsync("Diga OK", context, cts.Token))
            {
                // Draining is enough; the assertion is that nothing threw.
            }
        });

        Assert.Null(exception);
    }

    [OptInFact(OptIn, Requirement)]
    public async Task LiveApi_PromptCachingStillWorksWithoutTheBetaHeader()
    {
        // The prompt-caching beta header was removed because the feature is GA. If that
        // was wrong, cache_control would be rejected and this request would fail.
        // A system prompt below the cacheable minimum simply will not cache, which is
        // why this asserts the request succeeds rather than asserting a cache hit.
        var config = new LLMSessionConfig(
            SystemPrompt: string.Join(" ", Enumerable.Repeat(
                "Voce e um assistente de reunioes corporativas em portugues do Brasil.", 200)),
            MeetingContext: "Teste de cache",
            MaxTokens: 32);

        await using var client = new ClaudeLLMClient();
        await using var session = await client.CreateSessionAsync(config);

        var context = new SuggestionContext("", TriggerReason.Manual, TimeSpan.FromSeconds(10));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var tokens = new List<string>();
        await foreach (var token in session.GetSuggestionStreamAsync("Diga OK", context, cts.Token))
            tokens.Add(token);

        Assert.NotEmpty(tokens);
    }
}
