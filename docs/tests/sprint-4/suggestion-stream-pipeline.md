# Testes: SuggestionStreamPipeline

**Arquivo fonte:** `src/ZefaIA.LLM/SuggestionStreamPipeline.cs`
**Arquivo de teste:** `tests/ZefaIA.LLM.Tests/SuggestionStreamPipelineTests.cs`
**Classe de teste:** `SuggestionStreamPipelineTests`

## Motivacao

`SuggestionStreamPipeline` conecta o streaming do LLM ao overlay, gerenciando estados e filtrando [SEM SUGESTAO]. Testar garante transicoes de estado corretas e filtragem.

## Testes

### 1. `InitialState_IsIdle`
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "SuggestionStreamPipelineTests.InitialState"`

### 2. `RequestSuggestion_TransitionsThinkingToStreamingToComplete`
- **Tipo:** Unit
- **O que testa:** Maquina de estados completa
- **Execucao:** `dotnet test --filter "SuggestionStreamPipelineTests.RequestSuggestion_Transitions"`

### 3. `RequestSuggestion_EmitsTokens`
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "SuggestionStreamPipelineTests.RequestSuggestion_EmitsTokens"`

### 4-5. ThinkingStarted e OnComplete events
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "SuggestionStreamPipelineTests.RequestSuggestion_Fires"`

### 6. `RequestSuggestion_NoSuggestion_FilteredOut`
- **Tipo:** Unit
- **O que testa:** [SEM SUGESTAO] nao emite tokens
- **Execucao:** `dotnet test --filter "SuggestionStreamPipelineTests.RequestSuggestion_NoSuggestion"`

### 7. `RequestSuggestion_ApiError_TransitionsToError`
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "SuggestionStreamPipelineTests.RequestSuggestion_ApiError"`

### 8-11. `IsNoSuggestion_*`
- **Tipo:** Unit
- **O que testa:** Deteccao exata, com whitespace, parcial, texto normal
- **Execucao:** `dotnet test --filter "SuggestionStreamPipelineTests.IsNoSuggestion"`

### 12. `Dispose_MultipleCallsDoNotThrow`
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "SuggestionStreamPipelineTests.Dispose"`
