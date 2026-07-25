# Testes: ElevenLabsSTTProvider

**Arquivo fonte:** `src/ZefaIA.STT/ElevenLabsSTTProvider.cs`
**Arquivo de teste:** `tests/ZefaIA.STT.Tests/ElevenLabsSTTProviderTests.cs`
**Classe de teste:** `ElevenLabsSTTProviderTests`

## Motivacao

`ElevenLabsSTTProvider` implementa `ISTTProvider` via WebSocket usando ElevenLabs Scribe v2 Realtime. E o provider alternativo ao Whisper, plugavel via configuracao. Os testes validam identidade, lifecycle, parsing de respostas JSON, e serializacao sem depender de conexao real com a API.

## Testes

### 1. `Provider_HasCorrectIdentity`
- **Tipo:** Unit
- **O que testa:** ProviderId, Type e SupportedLanguages
- **Como funciona:** Cria instancia, verifica `ProviderId="elevenlabs-scribe"`, `Type=ElevenLabs`, suporte a "pt" e "en".
- **Por que existe:** Identidade usada pela factory e pelo TranscriptionEngine.
- **Execucao:** `dotnet test --filter "ElevenLabsSTTProviderTests.Provider_HasCorrectIdentity"`

### 2. `ProcessAudio_BeforeInit_Throws`
- **Tipo:** Unit
- **O que testa:** Uso antes da inicializacao lanca excecao
- **Como funciona:** Tenta processar audio sem inicializar. Verifica `InvalidOperationException`.
- **Por que existe:** Sem init, o WebSocket nao esta conectado.
- **Execucao:** `dotnet test --filter "ElevenLabsSTTProviderTests.ProcessAudio_BeforeInit_Throws"`

### 3. `InitializeAsync_NoApiKey_Throws`
- **Tipo:** Unit
- **O que testa:** API key ausente gera erro claro
- **Como funciona:** Configura env var inexistente, tenta inicializar. Verifica mensagem "API key not found".
- **Por que existe:** Sem API key, a conexao WebSocket falharia com 401 — erro pouco descritivo. Melhor falhar cedo.
- **Execucao:** `dotnet test --filter "ElevenLabsSTTProviderTests.InitializeAsync_NoApiKey_Throws"`

### 4. `ProcessResponse_FinalTranscript_EmitsSegmentReceived`
- **Tipo:** Unit
- **O que testa:** Resposta final do WebSocket emite evento `SegmentReceived`
- **Como funciona:** Chama `ProcessResponse()` com JSON de transcript final. Verifica segmento com texto, idioma, confidence e `IsFinal=true`.
- **Por que existe:** Teste central do parsing — garante que respostas da API sao convertidas corretamente para o modelo interno.
- **Execucao:** `dotnet test --filter "ElevenLabsSTTProviderTests.ProcessResponse_FinalTranscript_EmitsSegmentReceived"`

### 5. `ProcessResponse_PartialTranscript_EmitsPartialReceived`
- **Tipo:** Unit
- **O que testa:** Resposta parcial emite evento `PartialReceived`
- **Como funciona:** JSON com `is_final=false`. Verifica segmento parcial emitido.
- **Por que existe:** Parciais permitem UI responsiva (texto aparece enquanto usuario fala).
- **Execucao:** `dotnet test --filter "ElevenLabsSTTProviderTests.ProcessResponse_PartialTranscript_EmitsPartialReceived"`

### 6. `ProcessResponse_NonTranscriptType_Ignored`
- **Tipo:** Unit
- **O que testa:** Mensagens nao-transcript sao ignoradas
- **Como funciona:** JSON com `type="info"`. Verifica que nenhum evento e emitido.
- **Por que existe:** A API envia mensagens de controle (info, error) que nao sao transcricoes.
- **Execucao:** `dotnet test --filter "ElevenLabsSTTProviderTests.ProcessResponse_NonTranscriptType_Ignored"`

### 7. `ProcessResponse_EmptyText_Ignored`
- **Tipo:** Unit
- **O que testa:** Transcricao com texto vazio e ignorada
- **Como funciona:** JSON com `text="  "`. Verifica que nenhum evento e emitido.
- **Por que existe:** Whisper/ElevenLabs podem emitir segmentos vazios em silencio.
- **Execucao:** `dotnet test --filter "ElevenLabsSTTProviderTests.ProcessResponse_EmptyText_Ignored"`

### 8. `ProcessResponse_InvalidJson_DoesNotThrow`
- **Tipo:** Unit
- **O que testa:** JSON invalido nao causa crash
- **Como funciona:** Passa string invalida para `ProcessResponse()`. Verifica que nao lanca excecao.
- **Por que existe:** Dados corrompidos na rede nao devem derrubar o provider.
- **Execucao:** `dotnet test --filter "ElevenLabsSTTProviderTests.ProcessResponse_InvalidJson_DoesNotThrow"`

### 9. `AudioMessage_SerializesCorrectly`
- **Tipo:** Unit
- **O que testa:** Mensagem de audio serializa no formato esperado pela API
- **Como funciona:** Cria `ElevenLabsAudioMessage` com audio base64 e sample_rate. Verifica JSON output.
- **Por que existe:** Formato incorreto causaria rejeicao pela API.
- **Execucao:** `dotnet test --filter "ElevenLabsSTTProviderTests.AudioMessage_SerializesCorrectly"`

### 10. `DisposeAsync_MultipleCallsDoNotThrow`
- **Tipo:** Unit
- **O que testa:** Dispose duplo e seguro
- **Como funciona:** Dispose chamado 2x sem excecao.
- **Por que existe:** Padrao IAsyncDisposable.
- **Execucao:** `dotnet test --filter "ElevenLabsSTTProviderTests.DisposeAsync_MultipleCallsDoNotThrow"`

### 11. `ProcessAudio_AfterDispose_Throws`
- **Tipo:** Unit
- **O que testa:** Uso apos dispose lanca `ObjectDisposedException`
- **Como funciona:** Dispose, tenta processar. Verifica excecao.
- **Por que existe:** WebSocket ja foi fechado, tentativa de envio crasharia.
- **Execucao:** `dotnet test --filter "ElevenLabsSTTProviderTests.ProcessAudio_AfterDispose_Throws"`

### 12. `Integration_ConnectsAndTranscribes` (Skip)
- **Tipo:** Integration
- **O que testa:** Conexao real com ElevenLabs Scribe
- **Como funciona:** Conecta via WebSocket, envia 100ms de audio PCM.
- **Por que existe:** Validacao end-to-end. Requer API key.
- **Execucao:** `dotnet test --filter "ElevenLabsSTTProviderTests.Integration_ConnectsAndTranscribes"` (requer ELEVENLABS_API_KEY)
