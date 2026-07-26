# Testes: ClaudeLLMClient

**Arquivo fonte:** `src/ZefaIA.LLM/ClaudeLLMClient.cs`
**Arquivo de teste:** `tests/ZefaIA.LLM.Tests/ClaudeLLMClientTests.cs`
**Classe de teste:** `ClaudeLLMClientTests`

## Motivacao

`ClaudeLLMClient` e o cliente HTTP para a API do Claude com SSE streaming, prompt caching e retry. Testar garante request formatting correto, parsing de SSE, e metricas de cache.

## Testes

### 1. `Constructor_ThrowsWhenNoApiKey`
- **Tipo:** Unit
- **O que testa:** Falha quando ANTHROPIC_API_KEY nao existe
- **Execucao:** `dotnet test --filter "ClaudeLLMClientTests.Constructor_ThrowsWhenNoApiKey"`

### 2. `CreateSession_ReturnsSession`
- **Tipo:** Unit
- **O que testa:** Sessao criada com metricas zeradas
- **Execucao:** `dotnet test --filter "ClaudeLLMClientTests.CreateSession_ReturnsSession"`

### 3-5. `BuildRequestBody_*`
- **Tipo:** Unit
- **O que testa:** Request JSON inclui cache_control ephemeral, model, max_tokens
- **Execucao:** `dotnet test --filter "ClaudeLLMClientTests.BuildRequestBody"`

### 6-9. `ParseSSEData_*`
- **Tipo:** Unit
- **O que testa:** Parsing de content_block_delta, message_start, message_delta, JSON invalido
- **Execucao:** `dotnet test --filter "ClaudeLLMClientTests.ParseSSEData"`

### 10-12. `ParseSSEStream_*`
- **Tipo:** Unit
- **O que testa:** Extracao de tokens de stream SSE, stream vazio, linhas nao-data
- **Execucao:** `dotnet test --filter "ClaudeLLMClientTests.ParseSSEStream"`

### 13-15. `UpdateCacheMetrics_*`
- **Tipo:** Unit
- **O que testa:** Contagem de input tokens, cache hits, output tokens
- **Execucao:** `dotnet test --filter "ClaudeLLMClientTests.UpdateCacheMetrics"`

### 16. `Integration_StreamsRealResponse` (Skip)
- **Tipo:** Integration
- **O que testa:** Streaming real com API key
- **Execucao:** `dotnet test --filter "ClaudeLLMClientTests.Integration_StreamsRealResponse"`
