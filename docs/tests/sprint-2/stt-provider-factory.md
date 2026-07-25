# Testes: STTProviderFactory

**Arquivo fonte:** `src/ZefaIA.STT/STTProviderFactory.cs`
**Arquivo de teste:** `tests/ZefaIA.STT.Tests/STTProviderFactoryTests.cs`
**Classe de teste:** `STTProviderFactoryTests`

## Motivacao

`STTProviderFactory` e o ponto de criacao de providers STT. A factory usa registro explicito para desacoplar a criacao do uso. Testar garante que o provider correto e instanciado por tipo e que tipos nao registrados geram erro claro.

## Testes

### 1. `Create_RegisteredProvider_ReturnsInstance`
- **Tipo:** Unit
- **O que testa:** Factory cria provider quando tipo esta registrado
- **Como funciona:** Registra mock de `ISTTProvider` para `WhisperLocal`, chama `Create()`. Verifica instancia nao-null e tipo correto.
- **Por que existe:** Garante que a factory retorna o provider registrado sem erro.
- **Execucao:** `dotnet test --filter "STTProviderFactoryTests.Create_RegisteredProvider_ReturnsInstance"`

### 2. `Create_UnregisteredProvider_ThrowsNotSupportedException`
- **Tipo:** Unit
- **O que testa:** Factory lanca excecao para tipo nao registrado
- **Como funciona:** Tenta criar `ElevenLabs` sem registrar. Verifica `NotSupportedException` com nome do tipo na mensagem.
- **Por que existe:** Sem este teste, uma config com tipo invalido poderia causar `NullReferenceException` silenciosa em vez de erro descritivo.
- **Execucao:** `dotnet test --filter "STTProviderFactoryTests.Create_UnregisteredProvider_ThrowsNotSupportedException"`

### 3. `Register_OverwritesPreviousCreator`
- **Tipo:** Unit
- **O que testa:** Registrar mesmo tipo duas vezes sobrescreve o anterior
- **Como funciona:** Registra dois mocks diferentes para `WhisperLocal`, cria provider, verifica que o segundo e usado.
- **Por que existe:** Permite hot-swap de implementacoes e testes com mocks.
- **Execucao:** `dotnet test --filter "STTProviderFactoryTests.Register_OverwritesPreviousCreator"`

### 4. `IsRegistered_ReturnsTrueForRegisteredType`
- **Tipo:** Unit
- **O que testa:** `IsRegistered` reflete estado correto
- **Como funciona:** Registra `ElevenLabs`, verifica true. Verifica `WhisperLocal` (nao registrado) retorna false.
- **Por que existe:** Util para UI e validacao antes de tentar criar provider.
- **Execucao:** `dotnet test --filter "STTProviderFactoryTests.IsRegistered_ReturnsTrueForRegisteredType"`

### 5. `TranscriptionResult_ComputesFullTextAndDuration`
- **Tipo:** Unit
- **O que testa:** Propriedades computadas de `TranscriptionResult`
- **Como funciona:** Cria resultado com 2 segmentos. Verifica `FullText` concatena textos, `Duration` calcula corretamente, e `DetectedLanguage` usa primeiro segmento.
- **Por que existe:** `TranscriptionResult` sera usado pelo LLM (Sprint 4) para gerar contexto. Texto ou duracao incorretos causariam sugestoes fora de contexto.
- **Execucao:** `dotnet test --filter "STTProviderFactoryTests.TranscriptionResult_ComputesFullTextAndDuration"`

### 6. `TranscriptionResult_EmptySegments_HasDefaults`
- **Tipo:** Unit
- **O que testa:** Resultado vazio tem defaults seguros
- **Como funciona:** Cria `TranscriptionResult` sem segmentos. Verifica FullText vazio, Duration zero, DetectedLanguage "unknown".
- **Por que existe:** Evita `NullReferenceException` ou `IndexOutOfRange` quando nao ha transcrições.
- **Execucao:** `dotnet test --filter "STTProviderFactoryTests.TranscriptionResult_EmptySegments_HasDefaults"`
