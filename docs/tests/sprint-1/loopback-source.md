# Testes: LoopbackSource

**Arquivo fonte:** `src/ZefaIA.Audio/LoopbackSource.cs`
**Arquivo de teste:** `tests/ZefaIA.Audio.Tests/LoopbackSourceTests.cs`
**Classe de teste:** `LoopbackSourceTests`

## Motivacao

`LoopbackSource` captura o audio do sistema via `WasapiLoopbackCapture`. O formato de captura varia por hardware (tipicamente float32, stereo, 48kHz), entao o componente precisa fazer conversao automatica para PCM 16kHz mono. Os testes validam a construcao, seguranca e a conversao de formato.

## Testes

### 1. `Constructor_SetsProperties`
- **Tipo:** Unit
- **O que testa:** Construcao com device ID e nome customizados
- **Como funciona:** Cria `LoopbackSource("test-device", "Test Loopback")` e verifica `SourceId="loopback-test-device"`, `DisplayName="Test Loopback"`, `Type=Loopback`.
- **Por que existe:** Garante identificacao correta para logging e roteamento no `AudioCaptureEngine`.
- **Execucao:** `dotnet test --filter "LoopbackSourceTests.Constructor_SetsProperties"`

### 2. `Constructor_DefaultDevice_SetsDefaultId`
- **Tipo:** Unit
- **O que testa:** Construtor sem parametros usa "default" como ID
- **Como funciona:** Cria `LoopbackSource()` e verifica `SourceId="loopback-default"`.
- **Por que existe:** A maioria dos usuarios usara o device padrao. Este teste garante que o ID e previsivel para configuracoes e logs.
- **Execucao:** `dotnet test --filter "LoopbackSourceTests.Constructor_DefaultDevice_SetsDefaultId"`

### 3. `Dispose_DoesNotThrow`
- **Tipo:** Unit
- **O que testa:** Multiplas chamadas a `Dispose()` sao seguras
- **Como funciona:** Dispose chamado 2x sem excecao.
- **Por que existe:** Mesmo motivo que em `MicrophoneSource` — seguranca no shutdown.
- **Execucao:** `dotnet test --filter "LoopbackSourceTests.Dispose_DoesNotThrow"`

### 4. `StopAsync_BeforeStart_DoesNotThrow`
- **Tipo:** Unit
- **O que testa:** Chamar `StopAsync()` sem ter chamado `StartAsync()` e seguro
- **Como funciona:** Cria source, chama `StopAsync()` direto, verifica que a task completa sem excecao.
- **Por que existe:** Em cenarios de erro ou shutdown rapido, Stop pode ser chamado antes de Start. Se lancasse excecao, o shutdown falharia.
- **Execucao:** `dotnet test --filter "LoopbackSourceTests.StopAsync_BeforeStart_DoesNotThrow"`

### 5. `StartAsync_RaisesStateChangedToCapturing` *(Skip: requer hardware)*
- **Tipo:** Hardware / Integration
- **O que testa:** Ciclo de vida completo com estados Starting e Capturing
- **Como funciona:** Inicia captura WASAPI real, aguarda 200ms, para, verifica estados.
- **Por que existe:** Valida integracao com WASAPI — API nativa que pode falhar dependendo do hardware.
- **Execucao:** Apenas Windows com dispositivo de audio ativo

### 6. `StartAsync_EmitsAudioChunks_WhenAudioPlaying` *(Skip: requer hardware + audio ativo)*
- **Tipo:** Hardware / Integration
- **O que testa:** Captura real produz chunks quando ha audio tocando no sistema
- **Como funciona:** Inicia loopback, aguarda 1s (precisa de audio tocando), verifica chunks com `SampleRate=16000` e `Source=Loopback`.
- **Por que existe:** Loopback so produz dados quando ha audio no sistema. Este teste valida o fluxo completo incluindo resampling automatico.
- **Execucao:** Apenas Windows com audio tocando (ex: video no YouTube)

### 7. `Resampler_Handles48kStereoFloat32`
- **Tipo:** Unit
- **O que testa:** Conversao do formato tipico de loopback WASAPI (48kHz, stereo, float32) para o target (16kHz, mono, int16)
- **Como funciona:** Gera 960 samples stereo float32 de onda senoidal 440Hz a 48kHz. Converte e verifica que o resultado tem ~320 samples mono int16 (960 * 16000/48000).
- **Por que existe:** Este e o cenario real mais comum. Se esta conversao falhar, o Whisper receberia audio corrompido. Tolerancia de +-2 samples para arredondamento.
- **Execucao:** `dotnet test --filter "LoopbackSourceTests.Resampler_Handles48kStereoFloat32"`
