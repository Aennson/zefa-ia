# Testes: WhisperSTTProvider

**Arquivo fonte:** `src/ZefaIA.STT/WhisperSTTProvider.cs`
**Arquivo de teste:** `tests/ZefaIA.STT.Tests/WhisperSTTProviderTests.cs`
**Classe de teste:** `WhisperSTTProviderTests`

## Motivacao

`WhisperSTTProvider` e o provider principal de STT usando Whisper.net (binding C# do whisper.cpp). Roda localmente sem depender de rede. Os testes validam identidade, lifecycle, VAD (Voice Activity Detection), e tratamento de erros sem precisar do modelo real.

## Testes

### 1. `Provider_HasCorrectIdentity`
- **Tipo:** Unit
- **O que testa:** ProviderId, Type e SupportedLanguages
- **Como funciona:** Cria instancia, verifica `ProviderId="whisper-local"`, `Type=WhisperLocal`, e que suporta "pt", "en" e "auto".
- **Por que existe:** A identidade e usada pela factory e pelo TranscriptionEngine para roteamento. Valores errados causariam instanciacao incorreta.
- **Execucao:** `dotnet test --filter "WhisperSTTProviderTests.Provider_HasCorrectIdentity"`

### 2. `ProcessAudio_BeforeInit_Throws`
- **Tipo:** Unit
- **O que testa:** Processar audio sem inicializar lanca excecao
- **Como funciona:** Cria provider, chama `ProcessAudioAsync()` sem chamar `InitializeAsync()`. Verifica `InvalidOperationException`.
- **Por que existe:** Protege contra uso antes da carga do modelo Whisper, que causaria `NullReferenceException`.
- **Execucao:** `dotnet test --filter "WhisperSTTProviderTests.ProcessAudio_BeforeInit_Throws"`

### 3. `FlushAsync_BeforeInit_Throws`
- **Tipo:** Unit
- **O que testa:** Flush sem inicializar lanca excecao
- **Como funciona:** Mesmo padrao do teste anterior, mas com `FlushAsync()`.
- **Por que existe:** Consistencia de lifecycle — todas as operacoes exigem init.
- **Execucao:** `dotnet test --filter "WhisperSTTProviderTests.FlushAsync_BeforeInit_Throws"`

### 4. `InitializeAsync_CalledTwice_Throws`
- **Tipo:** Unit
- **O que testa:** Dupla inicializacao e rejeitada
- **Como funciona:** Tenta inicializar duas vezes (a primeira falha por falta de modelo no ambiente de teste, mas o guarda e verificado).
- **Por que existe:** Carregar modelo Whisper duas vezes desperdicaria memoria (~150-500MB).
- **Execucao:** `dotnet test --filter "WhisperSTTProviderTests.InitializeAsync_CalledTwice_Throws"`

### 5. `IsSilence_DetectsSilentAudio`
- **Tipo:** Unit
- **O que testa:** VAD identifica buffer zerado como silencio
- **Como funciona:** Cria array de 1600 floats zerados, verifica `IsSilence()` retorna true.
- **Por que existe:** Sem VAD, o Whisper processaria silencio desperdicando CPU. Este teste valida o threshold.
- **Execucao:** `dotnet test --filter "WhisperSTTProviderTests.IsSilence_DetectsSilentAudio"`

### 6. `IsSilence_DetectsAudio`
- **Tipo:** Unit
- **O que testa:** VAD identifica onda senoidal como nao-silencio
- **Como funciona:** Gera senoidal 440Hz com amplitude 0.5, verifica `IsSilence()` retorna false.
- **Por que existe:** Complemento do teste anterior — garante que audio real nao e descartado.
- **Execucao:** `dotnet test --filter "WhisperSTTProviderTests.IsSilence_DetectsAudio"`

### 7. `IsSilence_EmptyBuffer_ReturnsSilence`
- **Tipo:** Unit
- **O que testa:** Buffer vazio e tratado como silencio
- **Como funciona:** Passa array vazio para `IsSilence()`, verifica retorno true.
- **Por que existe:** Edge case que poderia causar divisao por zero no calculo de RMS.
- **Execucao:** `dotnet test --filter "WhisperSTTProviderTests.IsSilence_EmptyBuffer_ReturnsSilence"`

### 8. `IsSilence_LowNoise_IsSilent`
- **Tipo:** Unit
- **O que testa:** Ruido de fundo abaixo do threshold e silencio
- **Como funciona:** Gera ruido aleatorio com amplitude ~0.0025 (RNG seed 42). Verifica que VAD trata como silencio.
- **Por que existe:** Microfones captam ruido de fundo constante. O threshold deve filtrar isso.
- **Execucao:** `dotnet test --filter "WhisperSTTProviderTests.IsSilence_LowNoise_IsSilent"`

### 9. `DisposeAsync_MultipleCallsDoNotThrow`
- **Tipo:** Unit
- **O que testa:** Dispose duplo e seguro
- **Como funciona:** Dispose chamado 2x sem excecao.
- **Por que existe:** Padrao IAsyncDisposable — seguranca no shutdown.
- **Execucao:** `dotnet test --filter "WhisperSTTProviderTests.DisposeAsync_MultipleCallsDoNotThrow"`

### 10. `ProcessAudio_AfterDispose_Throws`
- **Tipo:** Unit
- **O que testa:** Usar provider apos dispose lanca `ObjectDisposedException`
- **Como funciona:** Dispose, tenta processar audio. Verifica excecao correta.
- **Por que existe:** Acessar recursos liberados (modelo Whisper) causaria crash.
- **Execucao:** `dotnet test --filter "WhisperSTTProviderTests.ProcessAudio_AfterDispose_Throws"`

### 11. `Integration_TranscribesAudio` (Skip)
- **Tipo:** Integration
- **O que testa:** Pipeline completo de transcricao com modelo real
- **Como funciona:** Inicializa provider com modelo base, envia 3s de audio senoidal, aguarda processamento.
- **Por que existe:** Validacao end-to-end da integracao com whisper.net. Requer download de ~142MB.
- **Execucao:** `dotnet test --filter "WhisperSTTProviderTests.Integration_TranscribesAudio"` (requer modelo)
