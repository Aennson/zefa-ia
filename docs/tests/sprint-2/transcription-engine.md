# Testes: TranscriptionEngine

**Arquivo fonte:** `src/ZefaIA.STT/TranscriptionEngine.cs`
**Arquivo de teste:** `tests/ZefaIA.STT.Tests/TranscriptionEngineTests.cs`
**Classe de teste:** `TranscriptionEngineTests`

## Motivacao

`TranscriptionEngine` e o orquestrador que conecta os streams de audio (mic e loopback) aos provedores STT correspondentes. Ele merge as transcricoes em um unico observable com atribuicao de source correta. Os testes usam mocks e `Subject<T>` do System.Reactive para simular streams sem hardware.

## Helpers

- `CreateEngine()` — cria `TranscriptionEngine` com mocks de `ISTTProvider` para mic e loopback. Retorna tupla `(engine, micMock, loopMock)`.

## Testes

### 1. `Start_SubscribesToBothStreams`
- **Tipo:** Unit
- **O que testa:** Start faz subscribe em ambos os observables
- **Como funciona:** Cria engine, inicia com `Subject<AudioChunkEventArgs>`, verifica `HasObservers` em ambos.
- **Por que existe:** Se um stream nao tiver subscriber, chunks seriam perdidos silenciosamente.
- **Execucao:** `dotnet test --filter "TranscriptionEngineTests.Start_SubscribesToBothStreams"`

### 2. `Start_AlreadyStarted_Throws`
- **Tipo:** Unit
- **O que testa:** Duplo start lanca excecao
- **Como funciona:** Inicia duas vezes, verifica `InvalidOperationException` na segunda.
- **Por que existe:** Dupla subscricao duplicaria processamento e consumiria 2x recursos.
- **Execucao:** `dotnet test --filter "TranscriptionEngineTests.Start_AlreadyStarted_Throws"`

### 3. `MicChunks_RouteToMicProvider`
- **Tipo:** Unit
- **O que testa:** Chunks de mic vao para o provider de mic
- **Como funciona:** Emite chunk no micStream, aguarda 50ms, verifica que micMock recebeu e loopMock nao.
- **Por que existe:** Roteamento errado misturaria speakers — mic seria processado como interlocutor.
- **Execucao:** `dotnet test --filter "TranscriptionEngineTests.MicChunks_RouteToMicProvider"`

### 4. `LoopbackChunks_RouteToLoopbackProvider`
- **Tipo:** Unit
- **O que testa:** Chunks de loopback vao para o provider de loopback
- **Como funciona:** Analogo ao teste anterior, mas emite no loopStream. Verifica loopMock.
- **Por que existe:** Complemento do teste de roteamento — garante ambas as direcoes.
- **Execucao:** `dotnet test --filter "TranscriptionEngineTests.LoopbackChunks_RouteToLoopbackProvider"`

### 5. `SegmentReceived_FromMicProvider_EmitsOnTranscriptionStream`
- **Tipo:** Unit
- **O que testa:** Segmentos finais do mic provider aparecem no TranscriptionStream
- **Como funciona:** Levanta evento `SegmentReceived` no micMock via `mock.Raise()`. Subscribe no TranscriptionStream e verifica segmento recebido com Source=Microphone.
- **Por que existe:** Teste central da integracao provider → engine. Se eventos nao propagassem, a UI nao mostraria transcricoes.
- **Execucao:** `dotnet test --filter "TranscriptionEngineTests.SegmentReceived_FromMicProvider_EmitsOnTranscriptionStream"`

### 6. `SegmentReceived_FromLoopbackProvider_EmitsWithLoopbackSource`
- **Tipo:** Unit
- **O que testa:** Segmentos do loopback provider marcam Source=Loopback
- **Como funciona:** Levanta `SegmentReceived` no loopMock, verifica Source no segmento recebido.
- **Por que existe:** Garante atribuicao correta de source para diarizacao.
- **Execucao:** `dotnet test --filter "TranscriptionEngineTests.SegmentReceived_FromLoopbackProvider_EmitsWithLoopbackSource"`

### 7. `PartialReceived_EmitsAsNonFinal`
- **Tipo:** Unit
- **O que testa:** Segmentos parciais sao emitidos com `IsFinal=false`
- **Como funciona:** Levanta evento `PartialReceived` no micMock. Verifica que o segmento no TranscriptionStream tem `IsFinal=false`.
- **Por que existe:** A UI mostra parciais em italico e finais em texto normal. Se parciais viessem como finais, textos em progresso seriam commitados prematuramente.
- **Execucao:** `dotnet test --filter "TranscriptionEngineTests.PartialReceived_EmitsAsNonFinal"`

### 8. `Metrics_TracksSegments`
- **Tipo:** Unit
- **O que testa:** Metricas contam segmentos finais e parciais
- **Como funciona:** Emite 1 final + 1 parcial, verifica contadores.
- **Por que existe:** Metricas serao exibidas no overlay (Sprint 3) e usadas para diagnostico.
- **Execucao:** `dotnet test --filter "TranscriptionEngineTests.Metrics_TracksSegments"`

### 9. `Metrics_InitializeToZero`
- **Tipo:** Unit
- **O que testa:** Metricas iniciam zeradas
- **Como funciona:** Cria `TranscriptionEngineMetrics`, verifica todos os campos em 0.
- **Por que existe:** Valores iniciais errados causariam displays enganosos na UI.
- **Execucao:** `dotnet test --filter "TranscriptionEngineTests.Metrics_InitializeToZero"`

### 10. `Stop_UnsubscribesStreams`
- **Tipo:** Unit
- **O que testa:** Stop remove subscribers dos streams
- **Como funciona:** Start, Stop, verifica `HasObservers=false` em ambos subjects.
- **Por que existe:** Subscriptions ativas apos stop continuariam processando audio inutilmente.
- **Execucao:** `dotnet test --filter "TranscriptionEngineTests.Stop_UnsubscribesStreams"`

### 11. `Dispose_MultipleCallsDoNotThrow`
- **Tipo:** Unit
- **O que testa:** Dispose duplo e seguro
- **Como funciona:** Dispose chamado 2x sem excecao.
- **Por que existe:** Padrao IDisposable.
- **Execucao:** `dotnet test --filter "TranscriptionEngineTests.Dispose_MultipleCallsDoNotThrow"`

### 12. `FlushAsync_FlushBothProviders`
- **Tipo:** Unit
- **O que testa:** FlushAsync chama Flush em ambos providers
- **Como funciona:** Chama `FlushAsync()`, verifica que ambos mocks receberam a chamada.
- **Por que existe:** Flush forca processamento de audio buffered. Se um provider nao recebesse, audio residual seria perdido ao fim da sessao.
- **Execucao:** `dotnet test --filter "TranscriptionEngineTests.FlushAsync_FlushBothProviders"`
