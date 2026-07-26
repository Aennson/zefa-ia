# Testes Ponta a Ponta (E2E)

Projeto: `tests/ZefaIA.Integration.Tests`
Estado: **21 testes, todos passando** (última execução: 2026-07-26, Windows 11 x64)

Antes disto o projeto existia mas continha apenas um `Placeholder.cs` — cada componente
tinha teste isolado, mas a **costura entre eles nunca havia sido exercitada**.

## O que é realmente exercitado

Os testes dirigem o `MeetingOrchestrator` **de produção**. Só as fronteiras de processo
são substituídas; tudo entre elas é o código que roda em produção.

```
FakeAudioSource ─┐
                 ├─▶ AudioCaptureEngine ─▶ AudioPipeline ─▶ EchoCanceller
                 │        (real)              (real)          (real)
                 │                               │
                 │                               ├──▶ TranscriptionEngine ─▶ [STT falso]
                 │                               │         (real)                │
                 │                               │                               ▼
                 │                               │      TranscriptionTimeline ─ LanguageDetector
                 │                               │              (real)              (real)
                 │                               │                    │
                 │                               └──▶ SilenceTrigger ─┘
                 │                                       (real)
                 │                                          │
                 │                                          ▼
                 │                              SuggestionOrchestrator ─▶ [LLM falso]
                 │                                       (real)                │
                 │                                          │                  ▼
                 │                              SuggestionStreamPipeline ◀─────┘
                 │                                       (real)
                 │                                          │
                 └──────────────────────────────────────────┼──▶ [Overlay falso]
                                                            │
                                                            ▼
                                                    MeetingRecorder ─▶ SqliteMeetingRepository
                                                        (real)            (real, arquivo .db)
                                                                                 │
                                                                                 ▼
                                                                         SessionExporter (real)
```

**Real e sob teste:** `MeetingOrchestrator`, `StageRunner`, `AudioCaptureEngine`,
`AudioPipeline`, `EchoCanceller`, `TranscriptionEngine`, `TranscriptionTimeline`,
`LanguageDetector`, `SilenceTrigger`, `SuggestionOrchestrator`,
`SuggestionStreamPipeline`, `PromptBuilder`, `MeetingRecorder`,
`SqliteMeetingRepository` (banco em arquivo de verdade), `SessionExporter`.

**Substituído, por ser externo ao processo:**

| Fronteira | Substituto | Por quê |
|---|---|---|
| Dispositivos de áudio (WASAPI) | `FakeAudioSource` | Permite PCM exato em timestamps exatos. A captura real já é validada em `ZefaIA.Audio.Tests` com hardware (ver `WINDOWS-TEST-RUN.md`). |
| Modelo de fala (Whisper/ElevenLabs) | `ScriptedSTTProvider` | Asserções sobre texto conhecido. A inferência real é validada por `WhisperSTTProviderTests.Integration_TranscribesAudio`. |
| API da Anthropic | `ScriptedLLMClient` | **Ver "Lacuna conhecida" abaixo.** |
| Janela WPF | `RecordingOverlayController` | Exige thread STA e desktop. O controller real tem testes próprios em `ZefaIA.Overlay.Tests`. |

## Cobertura por cenário

| # | Teste | O que valida |
|---|---|---|
| 1 | `StartMeeting_BringsEveryStageUpAndCreatesTheSessionRow` | Todos os estágios sobem em ordem; a linha da sessão existe assim que o estado é `Running` |
| 2 | `StartMeeting_Twice_IsRejected` | Reentrância bloqueada sem corromper o estado |
| 3 | `StopMeeting_ReturnsToIdleAndClosesTheSession` | `EndedAt` gravado, estado volta a `Idle` |
| 4 | `StopMeeting_WhenIdle_IsANoOp` | Parar sem reunião não quebra |
| 5 | `StartStopStart_ReusesTheGraphAndKeepsBothSessions` | Grafo reconstruído entre reuniões; histórico preservado |
| 6 | `AudioFlowsThroughToTranscriptOverlayAndDatabase` | **Caminho principal:** áudio → overlay → SQLite |
| 7 | `MicAndLoopbackAreTranscribedIndependentlyAndLabelledPerSpeaker` | Dois provedores STT distintos; roteamento por canal; rótulos "Eu"/"Interlocutor" |
| 8 | `PartialSegmentsReachTheOverlayButAreNeverPersisted` | Parciais aparecem na UI mas não entram no histórico |
| 9 | `EchoCancellationRunsOnTheMicPathWhileLoopbackPassesThrough` | Loopback é a referência (intocado); mic passa pelo cancelador |
| 10 | `SilenceAfterSpeechTriggersASuggestionThatReachesOverlayAndHistory` | **Caminho principal:** silêncio → trigger → LLM → overlay → SQLite, com o transcript certo no prompt |
| 11 | `SilenceWithNothingSaidYetProducesNoSuggestion` | Sem fala anterior, não gasta requisição |
| 12 | `NoSuggestionMarkerIsSwallowedInsteadOfRenderedOrStored` | Marcador `[SEM SUGESTAO]` fragmentado não vaza para a UI nem para o banco |
| 13 | `WhenTheLlmFailsTheMeetingKeepsTranscribing` | Falha do LLM é reportada mas não derruba a transcrição |
| 14 | `WithoutAnLlmClientTheMeetingStillTranscribesAndRecords` | Modo degradado sem `ANTHROPIC_API_KEY` |
| 15 | `RateLimitingStopsASecondSuggestionInsideTheCooldown` | Rate limit do orquestrador respeitado |
| 16 | `DetectedLanguageRenamesTheSpeakersOnTheOverlay` | Detecção de idioma (en) renomeia para "Me"/"Other" |
| 17 | `PortugueseKeepsTheDefaultSpeakerLabels` | pt-BR mantém "Eu"/"Interlocutor" |
| 18 | `AFinishedMeetingExportsToTextAndJsonWithItsRealContent` | Export TXT e JSON com conteúdo real da reunião |
| 19 | `DeletingAMeetingRemovesItsTranscriptAndSuggestions` | Cascade delete no SQLite |
| 20 | `DisposingMidMeetingStopsItAndFlushesWhatWasTranscribed` | Crash/saída no meio não perde transcrição |
| 21 | `StoppingReleasesTheAudioSourcesAndTheOverlaySubscription` | Teardown solta as assinaturas |

### Verificação por mutação

Um E2E verde que não falha quando o produto quebra não vale nada. Duas mutações foram
injetadas temporariamente para confirmar que a suíte detecta regressões:

| Mutação | Resultado |
|---|---|
| `SuggestionStreamPipeline` volta a emitir tokens avidamente | Teste 12 falhou com `Collection: ["[SEM"]` |
| `MeetingRecorder` atribui tudo a "Eu" | Teste 7 falhou com `Expected "Interlocutor", Actual "Eu"` |

Ambas revertidas após a verificação.

### Mudanças de produção que o E2E exigiu

Duas costuras mínimas, porque o orquestrador instanciava suas dependências externas
diretamente e não havia como testá-lo sem hardware e sem desktop:

- **`IOverlayController`** (`src/ZefaIA.Overlay/IOverlayController.cs`) — extraído de
  `OverlayController`. `AppServices.Overlay` passa a depender do comportamento, não da
  janela WPF.
- **`AppServices.AudioSourceFactory`** — o orquestrador fazia `new MicrophoneSource()` /
  `new LoopbackSource()` embutidos. Agora vêm de uma factory cujo padrão é exatamente
  esse par real.

Nenhuma das duas altera o comportamento em produção.

---

## Lacuna conhecida: a API da Anthropic nunca foi exercitada de verdade

**Decisão:** adiado para uma etapa própria, a pedido. Este bloco existe para que não
se perca.

### O que já está coberto (com mock/fake)

- `ClaudeLLMClientTests` — parsing de SSE, retry com backoff, montagem do corpo da
  requisição, cache de prompt, tratamento de erro (mocks de `HttpMessageHandler`).
- E2E testes 10–15 — todo o comportamento **em volta** da chamada: montagem do prompt,
  gating por trigger, rate limit, fan-out de tokens para overlay e persistência,
  degradação quando o LLM falha ou está ausente.

### O que **não** está coberto

Nenhuma requisição real foi feita à `api.anthropic.com`. Não há validação de que o
corpo montado é aceito, de que os headers estão corretos, nem de que o formato do
stream SSE corresponde ao que a API realmente devolve hoje.

### Problemas já identificados por inspeção — verificar nesta etapa

Encontrados ao ler o código durante a implementação do E2E. Nenhum é pego pelos testes
atuais justamente porque nenhum teste fala com a API real.

| # | Achado | Onde | Risco |
|---|---|---|---|
| L-01 | **Modelo padrão provavelmente já retirado.** `ModelId = "claude-sonnet-4-20250514"`. Esse modelo estava marcado para retirada em **15/06/2026** — data já passada. Requisições retornariam **404**. Substituto indicado: `claude-sonnet-5`. | `src/ZefaIA.Core/Models/LLMModels.cs:6` | **Alto — provável falha total do LLM em produção** |
| L-02 | **ID de modelo com sufixo de data.** Aliases sem data (`claude-sonnet-5`) evitam exatamente esse tipo de expiração silenciosa. | `LLMModels.cs:6` | Médio |
| L-03 | **`Temperature = 0.7f` é configuração morta.** O campo existe em `LLMSessionConfig` mas `ClaudeRequest` nunca o serializa. Pior: nos modelos atuais `temperature` é **rejeitado com 400** — se alguém "corrigir" passando o campo adiante, quebra. | `LLMModels.cs:8` vs `ClaudeLLMClient.cs:264` | Médio (armadilha) |
| L-04 | **Header beta de prompt caching obsoleto.** `anthropic-beta: prompt-caching-2024-07-31` é enviado em toda requisição; prompt caching é GA há tempo e não precisa mais de header. | `ClaudeLLMClient.cs:23,39` | Baixo |
| L-05 | **`MaxTokens = 512`** pode truncar sugestões mais longas no meio. Vale revisar junto com o modelo. | `LLMModels.cs:7` | Baixo |
| L-06 | **HTTP cru em vez do SDK oficial.** Existe pacote `Anthropic` para C#. Migrar tiraria da nossa manutenção o parsing de SSE, os headers de versão/beta e o retry. | `src/ZefaIA.LLM/ClaudeLLMClient.cs` | Dívida técnica |

### O que fazer nessa etapa

1. Resolver L-01 primeiro — é o único com risco de deixar o produto sem sugestões hoje.
   Confirmar o status do modelo antes de escolher o substituto.
2. Reavaliar L-03/L-04/L-05 à luz da API atual (parâmetros de sampling, thinking,
   headers beta mudaram desde que este código foi escrito).
3. Decidir sobre L-06 (SDK oficial) antes de investir em mais código de transporte.
4. Criar um teste de integração real, opt-in por variável de ambiente, no mesmo padrão
   já usado em `ZefaIA.STT.Tests`:

   ```csharp
   [OptInFact("ZEFA_RUN_ANTHROPIC_INTEGRATION", "Requires a real ANTHROPIC_API_KEY")]
   public async Task RealApi_StreamsASuggestion() { ... }
   ```

   Deve validar, contra a API de verdade: requisição aceita (sem 400/404), stream SSE
   parseado, `stop_reason` tratado, e uso de tokens reportado.

## Como executar

```powershell
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
dotnet test tests/ZefaIA.Integration.Tests
```

Os testes usam banco SQLite em arquivo temporário (`%TEMP%\zefa_e2e_*.db`), removido no
teardown. Não precisam de hardware de áudio, desktop, rede nem chave de API.

> **Sobre os tempos:** o pipeline é assíncrono ponta a ponta (buffer Rx por tempo, STT
> fire-and-forget, escrita em lote no SQLite). Os testes usam `WaitForAsync` com polling
> em vez de `Task.Delay` fixo — um sleep fixo seria instável ou lento demais. A suíte
> roda em ~13s.
