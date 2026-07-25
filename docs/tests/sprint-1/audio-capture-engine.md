# Testes: AudioCaptureEngine

**Arquivo fonte:** `src/ZefaIA.Audio/AudioCaptureEngine.cs`
**Arquivo de teste:** `tests/ZefaIA.Audio.Tests/AudioCaptureEngineTests.cs`
**Classe de teste:** `AudioCaptureEngineTests`

## Motivacao

`AudioCaptureEngine` orquestra multiplos `IAudioSource` (mic + loopback) em paralelo, unificando os chunks de audio em um unico `IObservable<AudioChunkEventArgs>`. Os testes usam **Moq** para simular sources sem hardware, validando logica de orquestracao, tolerancia a falhas e lifecycle.

## Helper

`CreateMockSource(AudioSourceType type)` — cria um `Mock<IAudioSource>` com ID, nome, tipo, e metodos Start/Stop retornando `Task.CompletedTask`. Usado por todos os testes.

## Testes

### 1. `AddSource_AddsToActiveSources`
- **Tipo:** Unit (mock)
- **O que testa:** `AddSource()` registra o source na lista `ActiveSources`
- **Como funciona:** Cria engine, adiciona mock de Microphone, verifica que `ActiveSources` tem 1 item com tipo correto.
- **Por que existe:** Garante que sources sao registradas antes de iniciar. Se `AddSource` nao adicionasse, `StartAsync` nao teria nada para iniciar.
- **Execucao:** `dotnet test --filter "AudioCaptureEngineTests.AddSource_AddsToActiveSources"`

### 2. `StartAsync_StartsAllSources`
- **Tipo:** Unit (mock)
- **O que testa:** `StartAsync()` chama `StartAsync()` em cada source adicionada
- **Como funciona:** Adiciona mocks de mic e loopback, chama `StartAsync()`, usa `Verify` do Moq para confirmar que cada source recebeu `StartAsync` exatamente 1 vez. Verifica `IsRunning=true`.
- **Por que existe:** Garante que o engine nao "esquece" nenhuma source — se uma nao for started, o usuario perderia audio de um lado.
- **Execucao:** `dotnet test --filter "AudioCaptureEngineTests.StartAsync_StartsAllSources"`

### 3. `StopAsync_StopsAllSources`
- **Tipo:** Unit (mock)
- **O que testa:** `StopAsync()` chama `StopAsync()` em cada source
- **Como funciona:** Start, depois Stop, depois `Verify` que cada source recebeu `StopAsync` 1 vez. Verifica `IsRunning=false`.
- **Por que existe:** Sem stop correto, o NAudio continuaria capturando em background, consumindo CPU e memoria.
- **Execucao:** `dotnet test --filter "AudioCaptureEngineTests.StopAsync_StopsAllSources"`

### 4. `StartAsync_NoSources_Throws`
- **Tipo:** Unit
- **O que testa:** Iniciar sem sources adicionadas lanca `InvalidOperationException`
- **Como funciona:** Cria engine vazio, chama `StartAsync()`, verifica excecao.
- **Por que existe:** Iniciar sem sources seria um bug silencioso — o app pareceria funcionar mas nao capturaria nada.
- **Execucao:** `dotnet test --filter "AudioCaptureEngineTests.StartAsync_NoSources_Throws"`

### 5. `AddSource_WhileRunning_Throws`
- **Tipo:** Unit
- **O que testa:** Adicionar source com engine ja rodando lanca excecao
- **Como funciona:** Adiciona mic, inicia engine, tenta adicionar loopback — `InvalidOperationException`.
- **Por que existe:** Adicionar sources em runtime criaria race conditions no Observable. A restricao forca configuracao antes de iniciar.
- **Execucao:** `dotnet test --filter "AudioCaptureEngineTests.AddSource_WhileRunning_Throws"`

### 6. `AudioStream_EmitsChunksFromBothSources`
- **Tipo:** Unit (mock)
- **O que testa:** O `IObservable<AudioChunkEventArgs>` unificado recebe chunks de ambas as sources
- **Como funciona:** Subscribe no `AudioStream`, usa `mock.Raise()` para simular eventos `AudioChunkReceived` de mic e loopback. Verifica que 2 chunks chegaram com os tipos corretos.
- **Por que existe:** Teste central — valida que o Observable unificado funciona, que e a base de todo o pipeline downstream (STT, triggers).
- **Execucao:** `dotnet test --filter "AudioCaptureEngineTests.AudioStream_EmitsChunksFromBothSources"`

### 7. `FailedSource_DoesNotPreventOtherFromStarting`
- **Tipo:** Unit (mock)
- **O que testa:** Falha ao iniciar uma source nao impede as outras de funcionar
- **Como funciona:** Configura mock de mic para lancar excecao no `StartAsync`. Adiciona mic (falha) e loopback (funciona). Chama `StartAsync()` e verifica que loopback foi iniciado mesmo com mic falhando. `IsRunning=true`.
- **Por que existe:** Em cenarios reais, o mic pode estar em uso por outro app. O usuario ainda quer captura de loopback funcionando. Sem tolerancia a falhas, uma source derrubaria tudo.
- **Execucao:** `dotnet test --filter "AudioCaptureEngineTests.FailedSource_DoesNotPreventOtherFromStarting"`

### 8. `Dispose_DisposesAllSources`
- **Tipo:** Unit (mock)
- **O que testa:** `Dispose()` do engine chama `Dispose()` em cada source
- **Como funciona:** Adiciona 2 mocks, disposa engine, usa `Verify` para confirmar `Dispose` em cada um.
- **Por que existe:** Sources NAudio mantem handles nativos (WASAPI). Sem dispose, haveria leak de recursos do sistema operacional.
- **Execucao:** `dotnet test --filter "AudioCaptureEngineTests.Dispose_DisposesAllSources"`
