# Testes Ponta a Ponta (E2E)

Projeto: `tests/ZefaIA.Integration.Tests`
Estado: **25 testes, todos passando** (última execução: 2026-07-27, Windows 11 x64)

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
| 15a | `PressingTheHotkeyProducesASuggestionWithNoAudioPlaying` | **`Ctrl+Shift+Space` gera sugestão com o loopback em silêncio** — o caso que o gatilho de silêncio não atende |
| 15b | `PressingTheHotkeyTwiceAsksAgainEvenWithTheSameTranscript` | Deduplicação não engole um pedido explícito |
| 15c | `HotkeyWithoutAnLlmDoesNothingRatherThanFailing` | Atalho sem LLM não derruba a reunião |
| 15d | `StoppingTheMeetingReleasesTheHotkey` | Atalho após o teardown não alcança o grafo destruído |
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
| `MeetingOrchestrator` deixa de registrar o hotkey | Testes 15a/15b falharam por sugestão nenhuma chegar ao overlay |
| `SuggestionOrchestrator` aplica dedup também ao hotkey | Teste 15b falhou — o segundo pedido explícito foi engolido |

Todas revertidas após a verificação.

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

## A integração com a API da Anthropic

Esta seção começou como "lacuna conhecida": a API nunca havia sido chamada de verdade.
**Isso foi fechado em 2026-07-27** — ver "Verificação contra a API real", no fim. O
histórico dos achados fica registrado abaixo porque explica decisões do código atual.

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

### Problemas identificados por inspeção

| # | Achado | Estado |
|---|---|---|
| L-01 | **Modelo padrão já retirado.** `ModelId = "claude-sonnet-4-20250514"` — snapshot datado cuja retirada (15/06/2026) já passou; requisições retornariam 404. | **Corrigido** → `claude-sonnet-5` |
| L-02 | **ID com sufixo de data**, que é o que permitiu L-01 acontecer silenciosamente. | **Corrigido** — alias sem data, com teste que barra regressão (`LLMSessionConfig_ModelIdIsAnUndatedAlias`) |
| L-03 | **`Temperature = 0.7f` era config morta** — nunca serializada. Virou armadilha: nos modelos atuais `temperature` é rejeitado com 400, então "consertar" passando o campo adiante quebraria. | **Corrigido** — removida de `LLMSessionConfig` e do `appsettings.json` |
| L-04 | **Header beta obsoleto** `anthropic-beta: prompt-caching-2024-07-31`; prompt caching é GA. | **Corrigido** — header removido, `cache_control` mantido |
| L-05 | **`MaxTokens = 512`** truncaria sugestões, ainda mais com o tokenizer novo do Sonnet 5 (~30% mais tokens para o mesmo texto). | **Corrigido** → 1024 |
| L-06 | **HTTP cru em vez do SDK oficial** (`Anthropic` para C#). Migrar tiraria da nossa manutenção o parsing de SSE, headers de versão/beta e retry. | **Em aberto** — dívida técnica |
| L-07 | **A seção `LLM` do `appsettings.json` não é lida por ninguém.** `MeetingOrchestrator` monta `new LLMSessionConfig(systemPrompt, session.Agenda)` com os defaults do record. Editar o arquivo não muda nada — duas fontes de verdade, uma delas falsa. | **Em aberto** — valores sincronizados e seção marcada como não-vinculada |

### Consequência de comportamento da troca de modelo

No Sonnet 4, **omitir** o campo `thinking` significava "sem thinking". No Claude Sonnet 5,
omitir significa **thinking adaptativo ligado**. Como os tokens de thinking contam contra
`MaxTokens` e atrasam o primeiro token visível, a troca de modelo sozinha teria truncado
as sugestões e travado o overlay ao vivo.

Por isso o cliente agora envia `thinking: {"type": "disabled"}` **explicitamente** —
preservando o comportamento atual em vez de herdar um default novo por acidente.

> **A avaliar depois:** ligar thinking adaptativo com `effort: "low"` pode melhorar a
> qualidade das sugestões. É um trade-off de qualidade × latência que precisa ser medido
> num cenário real de reunião, não decidido no papel.

### Verificação contra a API real

`tests/ZefaIA.LLM.Tests/ClaudeLLMClientLiveApiTests.cs` — 3 testes opt-in que batem em
`api.anthropic.com` de verdade. Todo o restante do projeto usa `HttpMessageHandler`
mockado, o que prova que o cliente interpreta o que **achamos** que a API devolve, não
que a API aceita o que enviamos.

| Teste | O que pega |
|---|---|
| `LiveApi_DefaultConfig_IsAcceptedAndStreamsTokens` | Config de produção aceita; stream SSE produz tokens não-vazios |
| `LiveApi_ConfiguredModelExists` | 404 por modelo retirado — exatamente a falha do L-01 |
| `LiveApi_PromptCachingStillWorksWithoutTheBetaHeader` | `cache_control` rejeitado se a remoção do header beta (L-04) estiver errada |

```powershell
$env:ANTHROPIC_API_KEY = "sk-ant-..."
$env:ZEFA_RUN_ANTHROPIC_INTEGRATION = "1"
dotnet test tests/ZefaIA.LLM.Tests --filter "FullyQualifiedName~LiveApi"
```

**Executados em 2026-07-27 com uma chave real: os 3 passaram.** Isso fecha o L-01
(o modelo `claude-sonnet-5` existe e é aceito), confirma que o corpo montado pelo
cliente é válido, e confirma o L-04 (`cache_control` continua funcionando sem o
header beta). O app também foi exercitado ponta a ponta contra a API de verdade,
gerando sugestões reais no overlay.

A chave usada foi de teste e não está em lugar nenhum do repositório — os testes a
leem de `ANTHROPIC_API_KEY` e pulam sozinhos quando ela não existe.

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
