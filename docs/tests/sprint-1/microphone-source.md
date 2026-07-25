# Testes: MicrophoneSource

**Arquivo fonte:** `src/ZefaIA.Audio/MicrophoneSource.cs`
**Arquivo de teste:** `tests/ZefaIA.Audio.Tests/MicrophoneSourceTests.cs`
**Classe de teste:** `MicrophoneSourceTests`

## Motivacao

`MicrophoneSource` implementa `IAudioSource` usando NAudio `WaveInEvent` para capturar audio do microfone. Os testes validam a construcao do objeto, seguranca do `Dispose`, e (quando hardware esta disponivel) o ciclo de vida completo de captura.

## Testes

### 1. `Constructor_SetsProperties`
- **Tipo:** Unit
- **O que testa:** Construcao do `MicrophoneSource` com device index e nome
- **Como funciona:** Cria com `deviceIndex=0` e `displayName="Test Mic"`. Verifica `SourceId="mic-0"`, `DisplayName="Test Mic"`, `Type=Microphone`. Envolto em try/catch para ambientes sem microfone.
- **Por que existe:** Garante que as propriedades de identificacao sao corretamente definidas — usadas pelo `AudioCaptureEngine` para logging e roteamento.
- **Execucao:** `dotnet test --filter "MicrophoneSourceTests.Constructor_SetsProperties"`

### 2. `Dispose_DoesNotThrow`
- **Tipo:** Unit
- **O que testa:** Chamadas multiplas a `Dispose()` nao lancam excecao
- **Como funciona:** Cria um `MicrophoneSource`, chama `Dispose()` duas vezes consecutivas. Se alguma lancar excecao, o teste falha.
- **Por que existe:** O padrao IDisposable exige que multiplos Dispose sejam safe. Em shutdown do app, Dispose pode ser chamado por DI e manualmente — se lançar, o app crasha ao fechar.
- **Execucao:** `dotnet test --filter "MicrophoneSourceTests.Dispose_DoesNotThrow"`

### 3. `Type_IsMicrophone`
- **Tipo:** Unit
- **O que testa:** Propriedade `Type` sempre retorna `AudioSourceType.Microphone`
- **Como funciona:** Cria instancia e verifica `Type == AudioSourceType.Microphone`.
- **Por que existe:** O `AudioCaptureEngine` e `AudioPipeline` usam `Type` para rotear chunks. Se retornasse `Loopback`, o audio do mic seria processado como audio do sistema.
- **Execucao:** `dotnet test --filter "MicrophoneSourceTests.Type_IsMicrophone"`

### 4. `StartAsync_RaisesStateChangedToCapturing` *(Skip: requer hardware)*
- **Tipo:** Hardware / Integration
- **O que testa:** Ciclo de vida: Start emite estados `Starting` e `Capturing`
- **Como funciona:** Inicia captura real, aguarda 100ms, para, e verifica que os estados foram emitidos em ordem.
- **Por que existe:** Valida que o NAudio `WaveInEvent` inicia corretamente e que os eventos de estado sao emitidos — essencial para a UI mostrar status.
- **Execucao:** `dotnet test --filter "MicrophoneSourceTests.StartAsync_RaisesStateChangedToCapturing"` (apenas em Windows com microfone)

### 5. `StartAsync_EmitsAudioChunks` *(Skip: requer hardware)*
- **Tipo:** Hardware / Integration
- **O que testa:** Captura real produz chunks de audio com formato correto
- **Como funciona:** Inicia captura, aguarda 500ms, para, e verifica que chunks foram emitidos com `SampleRate=16000` e `Source=Microphone`.
- **Por que existe:** Teste end-to-end do fluxo NAudio → evento → dados PCM. Sem este teste, bugs de formato so apareceriam ao testar com STT.
- **Execucao:** `dotnet test --filter "MicrophoneSourceTests.StartAsync_EmitsAudioChunks"` (apenas em Windows com microfone)
