# Testes: RetryPolicy, HealthTracker e SensitiveDataScrubber

**Arquivos fonte:** `src/ZefaIA.Core/Resilience/*.cs`, `src/ZefaIA.App/CrashReporter.cs`
**Arquivos de teste:** `tests/ZefaIA.App.Tests/ResilienceTests.cs`, `SensitiveDataScrubberTests.cs`
**Total:** 30 + 26 = 56 testes

---

## RetryPolicy (17 testes)

### Motivacao

Retry mal feito e pior que nenhum: retentar erro permanente desperdicia tempo e
dinheiro, e retentar sem jitter transforma uma falha em tempestade. Os testes
usam `InitialDelay = TimeSpan.Zero` para rodar rapido, exceto os de backoff que
verificam o calculo do atraso sem realmente esperar.

### Testes

#### 1-7. Execucao
- `ExecuteAsync_SucceedsFirstTry_CallsOnce`
- `ExecuteAsync_FailsThenSucceeds_Retries`
- `ExecuteAsync_AlwaysFails_ThrowsLastException`
- `ExecuteAsync_NonRetryableException_DoesNotRetry`
- `ExecuteAsync_VoidOverload_Runs`
- `ExecuteAsync_None_NeverRetries`
- `ExecuteAsync_PassesCancellationToken`
- **O que testa:** Sucesso na primeira nao paga custo de retry.
  `NonRetryableException` e importante: com `ShouldRetry` filtrando por tipo, um
  `ArgumentException` (bug de codigo) falha na hora em vez de tentar 3x.
  `AlwaysFails` verifica que a excecao propagada e a da **ultima** tentativa,
  nao a da primeira — e a que reflete o estado atual do sistema.
- **Execucao:** `dotnet test --filter "RetryPolicyTests.ExecuteAsync"`

#### 8-14. Backoff e jitter
- `GetDelay_GrowsExponentially`
- `GetDelay_CapsAtMaxDelay`
- `GetDelay_WithJitter_StaysWithinBand`
- `GetDelay_WithJitter_ProducesVariedValues`
- `GetDelay_NeverNegative`
- `GetDelay_AttemptZeroOrNegative_TreatedAsFirst`
- `ForTransientHttp_RetriesHttpButNotArgument`
- **O que testa:** Progressao 1s → 2s → 4s com `JitterFactor = 0`, teto em
  `MaxDelay`. `StaysWithinBand` roda 50 vezes e confirma que o jitter de 20% em
  torno de 2000ms fica em [1600, 2400] — sem essa faixa o jitter poderia estourar
  o `MaxDelay` silenciosamente.

  `NeverNegative` usa `JitterFactor = 1.0` (jitter maximo) para forcar o caso em
  que o offset negativo excede o valor base; `Task.Delay` com valor negativo lanca.
- **Execucao:** `dotnet test --filter "RetryPolicyTests.GetDelay|RetryPolicyTests.ForTransientHttp"`

---

## HealthTracker (13 testes)

### Motivacao

O objetivo nao e contar falhas, e **decidir quando incomodar o usuario**. Um
microfone que reconecta tres vezes em dois segundos nao merece popup; um que
falha vinte vezes seguidas merece. O tracker separa esses dois casos por
threshold, e o evento so dispara na **transicao** de estado.

### Testes

#### 1-4. Thresholds
- `GetState_UnknownComponent_IsHealthy`
- `RecordFailure_BelowThreshold_StaysHealthy`
- `RecordFailure_AtDegradedThreshold_BecomesDegraded`
- `RecordFailure_AtFailedThreshold_BecomesFailed`
- **Execucao:** `dotnet test --filter "HealthTrackerTests.RecordFailure_At|HealthTrackerTests.RecordFailure_Below"`

#### 5-6. Recuperacao
- `RecordSuccess_ResetsFailureStreak`
- `RecordSuccess_KeepsTotalFailureCount`
- **O que testa:** Sucesso zera o streak consecutivo mas preserva `TotalFailures`
  — o streak decide o estado, o total serve para diagnostico ("reconectou 40
  vezes nesta reuniao" e um sintoma mesmo com o componente saudavel agora).
- **Execucao:** `dotnet test --filter "HealthTrackerTests.RecordSuccess"`

#### 7-9. Eventos de transicao
- `OnHealthChanged_FiresOnlyOnTransition`
- `OnHealthChanged_FiresOnRecovery`
- `OnHealthChanged_RepeatedSuccess_DoesNotRefire`
- **O que testa:** Tres falhas com threshold 2 disparam **um** evento, nao tres.
  Sem isso o overlay piscaria a cada chunk de audio perdido.
- **Execucao:** `dotnet test --filter "HealthTrackerTests.OnHealthChanged"`

#### 10-13. Agregacao e reset
- `OverallState_NoComponents_IsHealthy`
- `OverallState_ReflectsWorstComponent`
- `RecordFailure_StoresLastError` / `RecordSuccess_ClearsLastError`
- `Reset` / `ResetAll` / `ComponentsAreTrackedIndependently`
- **O que testa:** O estado geral e o pior entre os componentes. Componentes sao
  independentes: mic degradado nao contamina o loopback.
- **Execucao:** `dotnet test --filter "HealthTrackerTests.OverallState|HealthTrackerTests.Reset|HealthTrackerTests.Components"`

---

## SensitiveDataScrubber (21 testes)

### Motivacao

Este e o unico componente do projeto onde um falso negativo tem custo direto:
uma chave da Anthropic vazada em arquivo de crash e cobravel por quem a
encontrar. Os testes cobrem as duas direcoes — **redigir o que e segredo** e
**nao redigir o que nao e**, porque um scrubber agressivo demais torna os logs
inuteis e leva o usuario a desliga-lo.

### Testes

#### 1-6. Chaves de API
- `Scrub_AnthropicKey_IsRedacted` / `Scrub_AnthropicKey_CaseInsensitive`
- `Scrub_ElevenLabsKey_IsRedacted`
- `Scrub_BearerToken_IsRedacted`
- `Scrub_ApiKeyAssignments_AreRedacted` (Theory, 4 formatos)
- `Scrub_MultipleSecretsInOneString_AllRedacted`
- **O que testa:** Formatos `sk-ant-*`, `sk_*`, `Bearer <token>` e as variacoes de
  atribuicao (`x-api-key:`, `"api_key":`, `ApiKey=`, `api-key :`). O teste de
  multiplos segredos garante que o replace e global, nao apenas na primeira
  ocorrencia.
- **Execucao:** `dotnet test --filter "SensitiveDataScrubberTests.Scrub_Anthropic|SensitiveDataScrubberTests.Scrub_ElevenLabs|SensitiveDataScrubberTests.Scrub_Bearer|SensitiveDataScrubberTests.Scrub_ApiKey|SensitiveDataScrubberTests.Scrub_Multiple"`

#### 7-9. Caminhos com nome de usuario
- `ScrubUserPaths_WindowsUserDirectory_IsAnonymized`
- `ScrubUserPaths_PreservesDriveLetter`
- `ScrubUserPaths_NoUserPath_LeavesInputAlone`
- **O que testa:** `C:\Users\joaosilva\...` vira `C:\Users\%USER%\...`. O nome da
  conta do Windows e dado pessoal sob LGPD e aparece em toda mensagem de erro de
  I/O. O resto do caminho e preservado — anonimizar sem destruir a utilidade
  para debug.
- **Execucao:** `dotnet test --filter "SensitiveDataScrubberTests.ScrubUserPaths"`

#### 10-13. Entrada inofensiva (testes negativos)
- `Scrub_Null_ReturnsEmpty` / `Scrub_Empty_ReturnsEmpty`
- `Scrub_OrdinaryMessage_IsUnchanged`
- `Scrub_ShortTokenLikeString_NotOverRedacted`
- **O que testa:** `"Audio device disconnected, retrying in 2s"` passa intacta.
  `sk_short` (abaixo do comprimento minimo) nao e redigida — sem esse piso, o
  padrao pegaria fragmentos de palavras comuns e transformaria o log em
  `[REDACTED]`.
- **Execucao:** `dotnet test --filter "SensitiveDataScrubberTests.Scrub_Ordinary|SensitiveDataScrubberTests.Scrub_Short|SensitiveDataScrubberTests.Scrub_Null|SensitiveDataScrubberTests.Scrub_Empty"`

#### 14-18. Excecoes
- `ScrubException_Null_ReturnsEmpty`
- `ScrubException_IncludesTypeAndMessage`
- `ScrubException_RedactsSecretInMessage`
- `ScrubException_IncludesInnerException`
- `ScrubException_RedactsSecretInInnerException`
- **O que testa:** A recursao em `InnerException` preserva a cadeia completa —
  e o scrubbing desce junto. Um `HttpRequestException` embrulhado costuma trazer
  a chave na inner, que e exatamente onde um scrubber ingenuo nao olharia.
- **Execucao:** `dotnet test --filter "SensitiveDataScrubberTests.ScrubException"`

---

## CrashReporter (5 testes)

- `BuildReport_ContainsEnvironmentContext`
- `BuildReport_StatesThatNothingIsTransmitted`
- `BuildReport_RedactsSecrets`
- `Report_WritesFileToGivenDirectory`
- `Report_KeepsOnlyMostRecentReports`

**O que testa:** O relatorio traz SO, versao do .NET e arquitetura (contexto
minimo para diagnostico) e passa pelo scrubber. `KeepsOnlyMostRecentReports`
verifica a poda: um crash em loop poderia encher o disco do usuario.

O texto afirmando que o arquivo nao e enviado a lugar nenhum e verificado por
teste porque e uma promessa ao usuario, nao um detalhe cosmetico.

**Execucao:** `dotnet test --filter "CrashReporterTests"`

## Cobertura manual

Os handlers globais (`DispatcherUnhandledException`, `AppDomain.UnhandledException`,
`TaskScheduler.UnobservedTaskException`) nao tem teste automatizado — disparar
cada canal de forma confiavel em processo de teste e fragil. Verificacao manual:
provocar uma excecao em cada contexto e confirmar que o arquivo aparece em
`%APPDATA%\ZefaIA\crashes\`.
