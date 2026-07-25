# Testes: LLMModels

**Arquivo fonte:** `src/ZefaIA.Core/Models/LLMModels.cs`
**Arquivo de teste:** `tests/ZefaIA.LLM.Tests/LLMModelsTests.cs`
**Classe de teste:** `LLMModelsTests`

## Motivacao

Os models do LLM (`LLMSessionConfig`, `TriggerEventArgs`, `LLMSessionMetrics`) definem os contratos para integracao com Claude API (Sprint 4). Testar os defaults garante que o app funciona sem configuracao explicita para todos os campos opcionais.

## Testes

### 1. `LLMSessionConfig_HasCorrectDefaults`
- **Tipo:** Unit
- **O que testa:** Valores default do record `LLMSessionConfig`
- **Como funciona:** Cria config com apenas os campos obrigatorios (`SystemPrompt` e `MeetingContext`). Verifica defaults: `ModelId="claude-sonnet-4-20250514"`, `MaxTokens=512`, `Temperature=0.7f`.
- **Por que existe:** Os defaults definem o comportamento quando o usuario nao configura nada. Se `MaxTokens` defaultasse para 0, o LLM nao geraria resposta. Se `Temperature` fosse 0, as respostas seriam identicas e engessadas.
- **Execucao:** `dotnet test --filter "LLMModelsTests.LLMSessionConfig_HasCorrectDefaults"`

### 2. `TriggerEventArgs_CreatesCorrectly`
- **Tipo:** Unit
- **O que testa:** Construcao do record `TriggerEventArgs`
- **Como funciona:** Cria com name="SilenceTrigger", reason=Silence, window=60s, timestamp=agora. Verifica `Reason` e `TriggerName`.
- **Por que existe:** `TriggerEventArgs` e emitido pelo `ITriggerStrategy` (Sprint 4) e consumido pelo orquestrador de sugestões. Campos errados causariam triggers ignorados ou com contexto insuficiente.
- **Execucao:** `dotnet test --filter "LLMModelsTests.TriggerEventArgs_CreatesCorrectly"`

### 3. `LLMSessionMetrics_InitializesToZero`
- **Tipo:** Unit
- **O que testa:** Metricas iniciam zeradas
- **Como funciona:** Cria `LLMSessionMetrics`, verifica que `TotalRequests`, `CacheHits`, `TotalInputTokens`, `TotalOutputTokens` e `AverageLatencyMs` sao todos 0.
- **Por que existe:** Metricas serao exibidas no overlay e usadas para estimar custo. Valores iniciais errados causariam displays incorretos e alarmes falsos de custo.
- **Execucao:** `dotnet test --filter "LLMModelsTests.LLMSessionMetrics_InitializesToZero"`
