# Testes: AudioPipeline

**Arquivo fonte:** `src/ZefaIA.Audio/AudioPipeline.cs`
**Arquivo de teste:** `tests/ZefaIA.Audio.Tests/AudioPipelineTests.cs`
**Classe de teste:** `AudioPipelineTests`

## Motivacao

`AudioPipeline` e o ponto de integracao do Sprint 1 — conecta `AudioCaptureEngine` ao `EchoCanceller` usando System.Reactive (Rx), criando streams separados para mic (com AEC aplicado) e loopback. Os testes validam roteamento, stream combinado, metricas e lifecycle usando mocks.

## Helpers

- `CreateEngine()` — cria um `AudioCaptureEngine` com mocks de mic e loopback ja adicionados. Retorna a tupla `(engine, micMock, loopbackMock)`.
- `CreatePcmChunk(samples=160)` — gera 160 samples PCM int16 de onda senoidal 440Hz (10ms de audio a 16kHz).

## Testes

### 1. `Pipeline_RoutesChunksCorrectly`
- **Tipo:** Unit (mock + Rx)
- **O que testa:** Chunks de mic vao para `MicStream` e chunks de loopback vao para `LoopbackStream`
- **Como funciona:**
  1. Cria engine com mocks, instancia `EchoCanceller` e `AudioPipeline`
  2. Subscribe em `MicStream` e `LoopbackStream` separadamente
  3. Inicia engine e pipeline
  4. Simula evento de loopback e mic via `mock.Raise()`
  5. Aguarda 200ms (tempo para Rx Buffer processar)
  6. Verifica que cada lista recebeu chunks do tipo correto
- **Por que existe:** Teste central do roteamento. Se mic fosse para loopback stream ou vice-versa, o STT teria diarizacao invertida (quem fala apareceria como interlocutor e vice-versa).
- **Execucao:** `dotnet test --filter "AudioPipelineTests.Pipeline_RoutesChunksCorrectly"`

### 2. `CombinedStream_ReceivesBothSources`
- **Tipo:** Unit (mock + Rx)
- **O que testa:** `CombinedStream` (merge de Mic + Loopback) recebe chunks de ambas as sources
- **Como funciona:** Igual ao teste anterior, mas subscribe no `CombinedStream`. Verifica que recebeu >= 2 chunks e que ambos os tipos (Mic e Loopback) estao presentes.
- **Por que existe:** O `CombinedStream` sera usado pelo `TranscriptionEngine` (Sprint 2) para alimentar o STT. Se algum source faltasse, a transcrição seria incompleta.
- **Execucao:** `dotnet test --filter "AudioPipelineTests.CombinedStream_ReceivesBothSources"`

### 3. `Metrics_InitializeToZero`
- **Tipo:** Unit
- **O que testa:** `AudioPipelineMetrics` comeca com todos os contadores zerados
- **Como funciona:** Cria instancia de `AudioPipelineMetrics`, verifica que `MicChunksProcessed`, `LoopbackChunksProcessed`, `DroppedChunks` e `AverageLatencyMs` sao todos 0.
- **Por que existe:** Metricas sao expostas na UI (Sprint 3) e usadas para performance tuning (Sprint 6). Valores iniciais errados causariam displays enganosos.
- **Execucao:** `dotnet test --filter "AudioPipelineTests.Metrics_InitializeToZero"`

### 4. `Dispose_StopsPipeline`
- **Tipo:** Unit
- **O que testa:** `Dispose()` para o pipeline e pode ser chamado multiplas vezes
- **Como funciona:** Inicia pipeline, chama Dispose 2x, verifica que nao lanca excecao.
- **Por que existe:** O pipeline mantem subscriptions Rx que, se nao disposadas, continuariam consumindo eventos. Dispose duplo deve ser seguro para o shutdown do app.
- **Execucao:** `dotnet test --filter "AudioPipelineTests.Dispose_StopsPipeline"`
