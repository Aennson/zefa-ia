# Testes: SqliteMeetingRepository

**Arquivo fonte:** `src/ZefaIA.Persistence/SqliteMeetingRepository.cs`
**Arquivo de teste:** `tests/ZefaIA.Persistence.Tests/SqliteMeetingRepositoryTests.cs`
**Classe de teste:** `SqliteMeetingRepositoryTests`
**Total:** 35 testes

## Motivacao

`SqliteMeetingRepository` e a camada de persistencia de toda a aplicacao. Um bug aqui significa perda de dados de reuniao. Os testes usam um arquivo SQLite temporario por classe de teste (criado no `InitializeAsync`, deletado no `DisposeAsync`), garantindo isolamento total entre execucoes.

## Testes

### 1-2. Inicializacao do schema
- `InitializeAsync_CreatesDatabase`
- `InitializeAsync_Idempotent_DoesNotThrow`
- **Tipo:** Unit (I/O em arquivo temporario)
- **O que testa:** `CREATE TABLE IF NOT EXISTS` permite chamar `InitializeAsync` multiplas vezes sem erro
- **Execucao:** `dotnet test --filter "SqliteMeetingRepositoryTests.InitializeAsync"`

### 3-6. Criacao de sessao
- `CreateSessionAsync_AssignsId`
- `CreateSessionAsync_PersistsAllFields`
- `CreateSessionAsync_WithEndedAt_Persists`
- `CreateSessionAsync_MultipleSessions_IncrementingIds`
- **O que testa:** `last_insert_rowid()` retorna o Id, todos os campos fazem round-trip (incluindo `DateTime` via formato ISO 8601 "o"), e `EndedAt` nulo e persistido como `DBNull`
- **Execucao:** `dotnet test --filter "SqliteMeetingRepositoryTests.CreateSessionAsync"`

### 7-9. Leitura de sessoes
- `GetSessionAsync_NonExistent_ReturnsNull`
- `GetAllSessionsAsync_Empty_ReturnsEmptyList`
- `GetAllSessionsAsync_ReturnsSortedByStartedAtDesc`
- **O que testa:** Ordenacao decrescente por `StartedAt` (mais recente primeiro), essencial para a tela de historico
- **Execucao:** `dotnet test --filter "SqliteMeetingRepositoryTests.GetSession|SqliteMeetingRepositoryTests.GetAllSessions"`

### 10-15. Atualizacao e delecao
- `UpdateSessionAsync_UpdatesAllFields`
- `DeleteSessionAsync_RemovesSession`
- `DeleteSessionAsync_NonExistent_DoesNotThrow`
- `DeleteSessionAsync_CascadesToTranscriptions`
- `DeleteSessionAsync_CascadesToSuggestions`
- `DeleteSessionAsync_LeavesOtherSessionsIntact`
- **O que testa:** Update parcial nao zera campos; delete de Id inexistente e no-op silencioso; deletar uma sessao remove transcricoes e sugestoes associadas sem afetar outras sessoes (requisito LGPD do Sprint 5)
- **Execucao:** `dotnet test --filter "SqliteMeetingRepositoryTests.UpdateSession|SqliteMeetingRepositoryTests.DeleteSession"`

### 13-17. Transcricoes
- `AddTranscriptionAsync_AssignsId`
- `AddTranscriptionAsync_PersistsAllFields`
- `GetTranscriptionsAsync_ReturnsOrderedByTimestamp`
- `GetTranscriptionsAsync_EmptySession_ReturnsEmpty`
- `AddTranscriptionBatchAsync_InsertsAll`
- **O que testa:** Batch insert dentro de uma transacao unica atribui Id a cada entrada; leitura ordenada por `Timestamp` reconstroi a linha do tempo da reuniao
- **Execucao:** `dotnet test --filter "SqliteMeetingRepositoryTests.Transcription"`

### 18-21. Sugestoes
- `AddSuggestionAsync_AssignsId`
- `AddSuggestionAsync_PersistsAllFields`
- `GetSuggestionsAsync_ReturnsOrderedByTimestamp`
- `GetSuggestionsAsync_EmptySession_ReturnsEmpty`
- **O que testa:** Contadores de token (`InputTokens`/`OutputTokens`) fazem round-trip — usados no calculo de custo
- **Execucao:** `dotnet test --filter "SqliteMeetingRepositoryTests.Suggestion"`

### 22-26. Busca
- `SearchSessionsAsync_ByTitle_FindsMatch`
- `SearchSessionsAsync_ByAgenda_FindsMatch`
- `SearchSessionsAsync_ByTranscriptionText_FindsMatch`
- `SearchSessionsAsync_NoMatch_ReturnsEmpty`
- `SearchSessionsAsync_MultipleTranscriptions_NoDuplicateSessions`
- **O que testa:** O `LEFT JOIN` com `SELECT DISTINCT` encontra texto dentro da transcricao sem retornar a mesma sessao varias vezes quando ha multiplos matches
- **Execucao:** `dotnet test --filter "SqliteMeetingRepositoryTests.SearchSessions"`

### 27. Caminho padrao do banco
- `DefaultDbPath_ContainsZefaIA`
- **O que testa:** Banco vive em `%APPDATA%/ZefaIA/meetings.db`
- **Execucao:** `dotnet test --filter "SqliteMeetingRepositoryTests.DefaultDbPath"`

### 28-32. Entidades de dominio
- `MeetingSession_Duration_WithEndedAt_CalculatesCorrectly`
- `MeetingSession_Duration_WithoutEndedAt_ReturnsZero`
- `MeetingSession_Defaults_AreCorrect`
- `TranscriptionEntry_Defaults_AreCorrect`
- `SuggestionEntry_Defaults_AreCorrect`
- **O que testa:** `Duration` computada retorna `TimeSpan.Zero` para reuniao em andamento; strings default como `""` e nao `null` (evita `NullReferenceException` na UI)
- **Execucao:** `dotnet test --filter "SqliteMeetingRepositoryTests.MeetingSession|SqliteMeetingRepositoryTests.TranscriptionEntry|SqliteMeetingRepositoryTests.SuggestionEntry"`

## Nota sobre cascade delete

O schema declara `ON DELETE CASCADE` nas foreign keys de `TranscriptionEntries` e `SuggestionEntries`, mas o SQLite ignora essas regras quando o pragma `foreign_keys` esta desligado. Por isso a connection string e montada com `ForeignKeys = true` explicito em vez de depender do default do provider. Os tres testes de cascade cobrem justamente essa regressao: se alguem remover a flag, `DeleteSessionAsync_CascadesToTranscriptions` falha.
