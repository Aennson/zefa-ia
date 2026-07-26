# Testes: SuggestionOrchestrator

**Arquivo fonte:** `src/ZefaIA.LLM/SuggestionOrchestrator.cs`
**Arquivo de teste:** `tests/ZefaIA.LLM.Tests/SuggestionOrchestratorTests.cs`
**Classe de teste:** `SuggestionOrchestratorTests`

## Motivacao

`SuggestionOrchestrator` coordena triggers (silencio + hotkey), aplica rate limiting e deduplicacao. Testar garante controle de custos e prevencao de spam.

## Testes

### 1. `HandleTrigger_WithTranscript_MakesRequest`
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "SuggestionOrchestratorTests.HandleTrigger_WithTranscript_MakesRequest"`

### 2. `HandleTrigger_EmptyTranscript_SkipsRequest`
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "SuggestionOrchestratorTests.HandleTrigger_EmptyTranscript"`

### 3. `HandleTrigger_NoTranscriptProvider_SkipsRequest`
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "SuggestionOrchestratorTests.HandleTrigger_NoTranscriptProvider"`

### 4. `HandleTrigger_DuplicateTranscript_SkipsSecond`
- **Tipo:** Unit
- **O que testa:** Deduplicacao por hash
- **Execucao:** `dotnet test --filter "SuggestionOrchestratorTests.HandleTrigger_DuplicateTranscript"`

### 5. `HandleTrigger_DifferentTranscripts_ProcessesBoth`
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "SuggestionOrchestratorTests.HandleTrigger_DifferentTranscripts"`

### 6. `RateLimiter_BlocksExcessRequests`
- **Tipo:** Unit
- **O que testa:** MaxRequestsPerMinute respeitado
- **Execucao:** `dotnet test --filter "SuggestionOrchestratorTests.RateLimiter"`

### 7-10. Rate limit, hash, config, metrics
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "SuggestionOrchestratorTests"`

### 11-12. RegisterTrigger e Dispose
- **Tipo:** Unit
- **O que testa:** Subscription e cleanup de eventos
- **Execucao:** `dotnet test --filter "SuggestionOrchestratorTests.RegisterTrigger"`
