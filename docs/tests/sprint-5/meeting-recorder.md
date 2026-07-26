# Testes: MeetingRecorder

**Arquivo fonte:** `src/ZefaIA.Persistence/MeetingRecorder.cs`
**Arquivo de teste:** `tests/ZefaIA.Persistence.Tests/MeetingRecorderTests.cs`
**Classe de teste:** `MeetingRecorderTests`
**Total:** 14 testes

## Motivacao

`MeetingRecorder` e a ponte entre os pipelines em tempo real (transcricao e sugestoes) e o SQLite. O risco principal e perda de dados: transcricoes que ficam no buffer e nunca chegam ao disco. Os testes usam `batchSize: 3` e `batchInterval: 1h` para tornar o disparo por threshold deterministico — o timer nunca dispara durante o teste, entao qualquer escrita observada veio do batch por contagem ou do flush final.

## Testes

### 1-5. Ciclo de vida da gravacao
- `StartAsync_CreatesSession`
- `StartAsync_AlreadyRecording_Throws`
- `StopAsync_SetsEndedAt`
- `StopAsync_WhenNotRecording_ReturnsNull`
- `StartAsync_FiresRecordingStateChanged`
- **O que testa:** `StartAsync` cria a sessao no banco e expoe o Id via `CurrentSessionId`; chamar duas vezes lanca `InvalidOperationException`; `StopAsync` grava `EndedAt` e limpa o estado. O evento `OnRecordingStateChanged` dispara `true` no start e `false` no stop — e o que alimenta o indicador de "gravando" no overlay.
- **Execucao:** `dotnet test --filter "MeetingRecorderTests.StartAsync|MeetingRecorderTests.StopAsync"`

### 6-7. Captura de transcricoes
- `SubscribeToTranscriptions_RecordsFinalSegments`
- `SubscribeToTranscriptions_SetsSpeakerBySource`
- **O que testa:** Apenas segmentos com `IsFinal == true` sao persistidos — parciais do STT sao descartados para nao poluir o historico com texto que ainda vai mudar. O nome do speaker vem da origem do stream: `Microphone` vira "Eu", `Loopback` vira "Interlocutor".
- **Execucao:** `dotnet test --filter "MeetingRecorderTests.SubscribeToTranscriptions"`

### 8-9. Batch e flush
- `BatchFlush_TriggersAtBatchSize`
- `StopAsync_FlushesRemainingBuffer`
- **O que testa:** Ao atingir `batchSize` entradas o buffer e escrito em uma transacao unica (`BatchesWritten == 1`). Com o buffer parcialmente cheio, `StopAsync` faz o flush final — este e o teste que protege contra perda de dados no encerramento da reuniao.
- **Execucao:** `dotnet test --filter "MeetingRecorderTests.BatchFlush|MeetingRecorderTests.StopAsync_Flushes"`

### 10, 13. Metricas
- `Metrics_TracksTranscriptionCount`
- `Metrics_TracksSuggestionCount`
- **O que testa:** Contadores incrementam por item recebido, independente de quando o flush acontece
- **Execucao:** `dotnet test --filter "MeetingRecorderTests.Metrics"`

### 11-12. Sugestoes
- `OnSuggestionReceived_SavesSuggestion`
- `OnSuggestionReceived_WhenNotRecording_Ignored`
- **O que testa:** Sugestoes sao gravadas imediatamente (sem batch, pois sao raras) com o contexto de transcricao que as gerou e os contadores de token. Fora de uma gravacao ativa a chamada e ignorada em vez de lancar excecao — o pipeline de LLM pode emitir eventos apos o stop.
- **Execucao:** `dotnet test --filter "MeetingRecorderTests.OnSuggestionReceived"`

### 14. Dispose
- `DisposeAsync_StopsRecordingIfActive`
- **O que testa:** Descartar o recorder com gravacao ativa faz o stop implicito, garantindo `EndedAt` gravado mesmo se o app fechar sem stop explicito
- **Execucao:** `dotnet test --filter "MeetingRecorderTests.DisposeAsync"`

## Tratamento de falha no flush

Se `AddTranscriptionBatchAsync` lanca, o `catch` reinsere as entradas no inicio do buffer (`_buffer.InsertRange(0, toFlush)`) em vez de descarta-las — a proxima tentativa de flush leva tudo junto. Nao ha teste automatizado para esse caminho porque exigiria um repositorio mock que falha sob demanda; e um candidato a teste de integracao no Sprint 6.
