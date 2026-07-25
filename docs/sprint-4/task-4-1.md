# Task 4-1: Claude API Client com Prompt Caching

## Descrição
Implementar cliente para Claude API com suporte a prompt caching. O system prompt (perfil + contexto) é cacheado; apenas o delta da transcrição é enviado a cada request.

## Skills
- `claude-api` — referência completa da API Claude (OBRIGATÓRIO)
- `security-review` — tratamento seguro de API key
- `simplify` — manter client enxuto

## Dependências
- Task 1-1 (projeto ZefaIA.LLM existe)

## Entregáveis
- `ClaudeLLMClient : ILLMClient` em `ZefaIA.LLM`
- Prompt caching configurado (system prompt com `cache_control`)
- Streaming de resposta via SSE
- Rate limiting e retry com backoff
- Métricas: tokens usados, cache hits, latência

## Interface
```csharp
public interface ILLMClient : IAsyncDisposable
{
    Task<LLMSession> CreateSessionAsync(LLMSessionConfig config, CancellationToken ct = default);
}

public class LLMSession : IAsyncDisposable
{
    // System prompt is cached; only new transcript is sent per request
    IAsyncEnumerable<string> GetSuggestionStreamAsync(
        string recentTranscript,
        SuggestionContext context,
        CancellationToken ct = default);
    
    LLMSessionMetrics Metrics { get; }
}

public record LLMSessionConfig(
    string SystemPrompt,       // cached
    string MeetingContext,     // cached with system prompt
    string ModelId,            // claude-sonnet-4-20250514
    int MaxTokens,
    float Temperature
);
```

## Prompt Caching Strategy
```json
{
  "model": "claude-sonnet-4-20250514",
  "system": [
    {
      "type": "text",
      "text": "<profile + meeting context - rarely changes>",
      "cache_control": {"type": "ephemeral"}
    }
  ],
  "messages": [
    {"role": "user", "content": "<recent transcript delta>"}
  ]
}
```

## Critérios de Aceite
- [ ] Request para Claude API funciona
- [ ] Prompt caching está ativo (verificar headers de resposta)
- [ ] Streaming funciona (tokens chegam incrementalmente)
- [ ] API key lida de env var `ANTHROPIC_API_KEY`
- [ ] Retry com backoff em erros 429/500
- [ ] Métricas de tokens e cache hits são rastreadas
- [ ] Timeout configurável (default 30s)

## Testes
- Unit: request é formatado corretamente com cache_control
- Unit: streaming parser processa SSE events
- Unit: retry logic funciona para 429 e 500
- Unit: métricas são atualizadas após cada request
- Integration: (requer API key) enviar prompt e receber resposta streaming
