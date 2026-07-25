# Testes: STTServiceManager

**Arquivo fonte:** `src/ZefaIA.STT/STTServiceManager.cs`
**Arquivo de teste:** `tests/ZefaIA.STT.Tests/STTServiceManagerTests.cs`
**Classe de teste:** `STTServiceManagerTests`

## Motivacao

`STTServiceManager` gerencia o lifecycle dos providers STT — inicializacao, hot-swap (troca sem reiniciar o app), e dispose. Usa `STTProviderFactory` para criar providers e `STTSettings` para configuracao tipada. Os testes validam criacao, swap, validacao e defaults.

## Testes

### 1. `DefaultFactory_RegistersBothProviders`
- **Tipo:** Unit
- **O que testa:** Factory padrao registra WhisperLocal e ElevenLabs
- **Como funciona:** Chama `CreateDefaultFactory()`, verifica `IsRegistered` para ambos tipos.
- **Por que existe:** Se um provider nao estiver registrado, o usuario nao conseguira seleciona-lo.
- **Execucao:** `dotnet test --filter "STTServiceManagerTests.DefaultFactory_RegistersBothProviders"`

### 2. `InitializeActiveProvider_WhisperLocal_CreatesProvider`
- **Tipo:** Unit
- **O que testa:** Manager inicializa provider WhisperLocal corretamente
- **Como funciona:** Registra mock, configura `ActiveProvider="WhisperLocal"`. Verifica que `InitializeAsync` e chamado com config correta e `ActiveProvider` e setado.
- **Por que existe:** Teste do fluxo principal de inicializacao.
- **Execucao:** `dotnet test --filter "STTServiceManagerTests.InitializeActiveProvider_WhisperLocal_CreatesProvider"`

### 3. `InitializeActiveProvider_ElevenLabs_CreatesProvider`
- **Tipo:** Unit
- **O que testa:** Manager inicializa provider ElevenLabs
- **Como funciona:** Analogo ao anterior com `ActiveProvider="ElevenLabs"`.
- **Por que existe:** Garante que ambos os caminhos de inicializacao funcionam.
- **Execucao:** `dotnet test --filter "STTServiceManagerTests.InitializeActiveProvider_ElevenLabs_CreatesProvider"`

### 4. `InitializeActiveProvider_InvalidName_Throws`
- **Tipo:** Unit
- **O que testa:** Nome de provider invalido gera erro descritivo
- **Como funciona:** Configura `ActiveProvider="NonExistent"`, tenta inicializar. Verifica `InvalidOperationException` com mensagem clara.
- **Por que existe:** Erro de config deve ser detectado na inicializacao, nao em runtime.
- **Execucao:** `dotnet test --filter "STTServiceManagerTests.InitializeActiveProvider_InvalidName_Throws"`

### 5. `SwapProvider_DisposesOldAndCreatesNew`
- **Tipo:** Unit
- **O que testa:** Hot-swap disposa provider antigo e cria novo
- **Como funciona:** Inicializa WhisperLocal, faz swap para ElevenLabs. Verifica que whisperMock recebeu `DisposeAsync` e `ActiveProvider` aponta para o novo.
- **Por que existe:** Teste central do hot-swap. Se o antigo nao for disposado, os recursos (modelo Whisper ~150MB, WebSocket) ficariam presos.
- **Execucao:** `dotnet test --filter "STTServiceManagerTests.SwapProvider_DisposesOldAndCreatesNew"`

### 6. `Dispose_DisposesActiveProvider`
- **Tipo:** Unit
- **O que testa:** Dispose do manager disposa provider ativo
- **Como funciona:** Inicializa, disposa manager. Verifica que mock recebeu `DisposeAsync` e `ActiveProvider` e null.
- **Por que existe:** Shutdown limpo — liberar recursos nativos (Whisper) e conexoes (WebSocket).
- **Execucao:** `dotnet test --filter "STTServiceManagerTests.Dispose_DisposesActiveProvider"`

### 7. `STTSettings_HasCorrectDefaults`
- **Tipo:** Unit
- **O que testa:** Defaults do `STTSettings`
- **Como funciona:** Cria instancia, verifica: `ActiveProvider="WhisperLocal"`, `ModelSize="base"`, `Language="auto"`, `UseGPU=false`, `ModelPath="./models"`, `BufferMs=2500`.
- **Por que existe:** Defaults definem o comportamento out-of-the-box. Se o default de ModelSize fosse "large", o primeiro uso baixaria ~3GB.
- **Execucao:** `dotnet test --filter "STTServiceManagerTests.STTSettings_HasCorrectDefaults"`

### 8. `ElevenLabsSettings_HasCorrectDefaults`
- **Tipo:** Unit
- **O que testa:** Defaults do `ElevenLabsSettings`
- **Como funciona:** Verifica `ApiKeyEnvVar="ELEVENLABS_API_KEY"`, `Language="auto"`, `VadEnabled=true`.
- **Por que existe:** VAD desabilitado por default causaria processamento desnecessario de silencio.
- **Execucao:** `dotnet test --filter "STTServiceManagerTests.ElevenLabsSettings_HasCorrectDefaults"`

### 9. `Validation_InvalidWhisperModelSize_Throws`
- **Tipo:** Unit
- **O que testa:** ModelSize invalido e rejeitado na validacao
- **Como funciona:** Configura `ModelSize="gigantic"`, tenta inicializar. Verifica `InvalidOperationException` com "Invalid Whisper model size".
- **Por que existe:** Whisper.net nao tem modelo "gigantic" — sem validacao, o download falharia com erro generico.
- **Execucao:** `dotnet test --filter "STTServiceManagerTests.Validation_InvalidWhisperModelSize_Throws"`
