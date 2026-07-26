# Testes: StageRunner

**Arquivo fonte:** `src/ZefaIA.App/Pipeline/StageRunner.cs`, `DelegateStage.cs`
**Arquivo de teste:** `tests/ZefaIA.App.Tests/StageRunnerTests.cs`
**Classe de teste:** `StageRunnerTests`
**Total:** 19 testes

## Motivacao

`StageRunner` existe para tornar testavel a parte mais critica e menos observavel
do app: a ordem de startup e shutdown. Sem essa abstracao, verificar "a
persistencia faz flush mesmo se o audio falhar ao parar" exigiria hardware de
audio real.

Os testes usam duas stages falsas — `RecordingStage`, que anota cada chamada em
uma lista compartilhada, e `FailingStage`, que lanca sob demanda. Isso permite
assertar a **sequencia exata** de chamadas, nao apenas o resultado final.

As duas politicas sao deliberadamente diferentes:
- **Startup e fail-fast** — se uma stage nao sobe, as anteriores sao revertidas e
  o app nunca fica meio-iniciado
- **Shutdown e best-effort** — toda stage recebe `StopAsync` mesmo que outra
  falhe, porque uma excecao no meio nao pode pular o flush da transcricao

## Testes

### 1-4. Ordem de startup
- `StartAsync_StartsStagesInRegistrationOrder`
- `StartAsync_NoStages_Succeeds`
- `StartAsync_WhenAlreadyRunning_Throws`
- `Add_WhileRunning_Throws`
- **O que testa:** Stages sobem na ordem de registro (que e a ordem de
  dependencia). Registrar stage com o runner rodando lanca — evita o bug de uma
  stage adicionada tarde nunca ser iniciada nem parada.
- **Execucao:** `dotnet test --filter "StageRunnerTests.StartAsync_Starts|StageRunnerTests.Add_While"`

### 5-8. Ordem de shutdown
- `StopAsync_StopsStagesInReverseOrder`
- `StopAsync_ClearsStartedStages`
- `StopAsync_WithoutStart_DoesNotThrow`
- `StartStopStart_CanRestart`
- **O que testa:** Shutdown e o espelho do startup. Como a persistencia e
  registrada primeiro, ela e a **ultima** a parar — exatamente o que a sequencia
  do spec pede, para que o flush final aconteca com todo o resto ja quieto.
  `StartStopStart` garante que o runner e reutilizavel entre reunioes.
- **Execucao:** `dotnet test --filter "StageRunnerTests.StopAsync|StageRunnerTests.StartStopStart"`

### 9-13. Falha no startup e rollback
- `StartAsync_StageThrows_WrapsInPipelineStartupException`
- `StartAsync_StageThrows_RollsBackAlreadyStartedStages`
- `StartAsync_FailedStart_LeavesRunnerNotRunning`
- `StartAsync_RollbackStopAlsoFails_StillThrowsStartupException`
- `StartAsync_AfterFailedStart_CanRetry`
- **O que testa:** O rollback e o ponto sutil. `RollsBackAlreadyStartedStages`
  assere a sequencia completa `start:A, start:B, stop:B, stop:A` — as stages que
  subiram sao derrubadas na ordem inversa, e a que falhou nunca teve `StopAsync`
  chamado (parar algo que nunca iniciou costuma lancar).

  `RollbackStopAlsoFails` cobre o cenario aninhado: a stage que falha ao reverter
  nao pode mascarar a causa original — a excecao que chega ao usuario ainda
  nomeia a stage que realmente impediu o startup.
- **Execucao:** `dotnet test --filter "StageRunnerTests.StartAsync_Stage|StageRunnerTests.StartAsync_Failed|StageRunnerTests.StartAsync_Rollback|StageRunnerTests.StartAsync_After"`

### 14-16. Isolamento de erro no shutdown
- `StopAsync_StageThrows_StillStopsRemainingStages`
- `StopAsync_MultipleFailures_AggregatesAll`
- `StopAsync_StageThrows_RunnerStillMarkedStopped`
- **O que testa:** O teste central do sprint. Com stages A(ok), B(falha ao parar),
  C(ok), o log resultante e `stop:C, stop:A` — B lancou, mas **A ainda parou**.
  Como a persistencia e sempre a stage A (registrada primeiro), esse teste e o
  que garante que uma falha no audio nao custa a transcricao da reuniao.

  As excecoes viram `AggregateException` lancada **depois** de todas as tentativas,
  nunca durante.
- **Execucao:** `dotnet test --filter "StageRunnerTests.StopAsync_Stage|StageRunnerTests.StopAsync_Multiple"`

### 17-19. DelegateStage
- `DelegateStage_InvokesStartAndStop`
- `DelegateStage_Sync_WrapsSynchronousComponents`
- `DelegateStage_PropagatesCancellationToken`
- **O que testa:** O adaptador que encaixa os componentes dos Sprints 1-5 (cada um
  com sua propria forma de start/stop) no ciclo de vida em stages, sem precisar de
  uma classe wrapper por componente. O `CancellationToken` precisa chegar intacto
  para que um startup lento seja cancelavel.
- **Execucao:** `dotnet test --filter "StageRunnerTests.DelegateStage"`

## Cobertura manual

`MeetingOrchestrator` — que monta o grafo real e o entrega ao `StageRunner` — nao
tem testes automatizados: instanciar `AudioCaptureEngine`, providers de STT e a
janela de overlay exige hardware de audio e uma STA thread. A logica de
sequenciamento que ele delega esta coberta acima; o wiring em si e verificado
rodando o app.
