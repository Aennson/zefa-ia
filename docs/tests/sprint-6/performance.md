# Testes: LatencyTracker e PerformanceMonitor

**Arquivos fonte:** `src/ZefaIA.Core/Diagnostics/LatencyTracker.cs`, `PerformanceMonitor.cs`
**Arquivo de teste:** `tests/ZefaIA.App.Tests/PerformanceTests.cs`
**Total:** 26 + 11 = 37 testes

## Motivacao

O criterio de aceite do Sprint 6 e "latencia end-to-end < 2s (p95)". Duas coisas
precisam estar certas para esse numero significar algo: o calculo de percentil e
o fato de o buffer de amostras nao crescer indefinidamente numa reuniao de duas
horas (que tambem e criterio de aceite — "sem memory leak apos 2h").

O gate e **p95, nao media**, de proposito: uma pausa de GC nao pode reprovar o
build, mas um estagio consistentemente lento tem que reprovar.

---

## LatencyTracker (26 testes)

### 1-8. Gravacao e buffer circular
- `SampleCount_Initially_IsZero`
- `Record_IncrementsSampleCount`
- `Record_TimeSpanOverload_ConvertsToMilliseconds`
- `Record_NegativeValue_IsIgnored` / `Record_NaN_IsIgnored`
- `Record_BeyondCapacity_KeepsWindowBounded`
- `Record_BeyondCapacity_KeepsMostRecentSamples`
- `Constructor_ZeroCapacity_Throws`
- **O que testa:** 100 amostras num tracker de capacidade 5 deixam exatamente 5 —
  o buffer circular e o que sustenta o requisito de nao vazar memoria.
  `KeepsMostRecentSamples` verifica que a substituicao descarta a **mais antiga**:
  com capacidade 3 e entradas 1,2,3,100, a janela vira {2,3,100} e o `Min` passa
  a ser 2. Um bug de indice aqui manteria as amostras erradas e reportaria
  latencia de dez minutos atras.

  `NaN` e negativos sao ignorados em vez de gravados: um `Stopwatch` mal usado
  produz esses valores e eles envenenariam todos os percentis.
- **Execucao:** `dotnet test --filter "LatencyTrackerTests.Record|LatencyTrackerTests.SampleCount|LatencyTrackerTests.Constructor"`

### 9-19. Estatisticas e percentis
- `Average_ComputesMean` / `Average_NoSamples_IsZero`
- `MinMax_ReflectExtremes`
- `Percentile_UnsortedInput_StillCorrect`
- `Percentile_HundredSamples_NearestRank`
- `Percentile_SingleSample_ReturnsThatSample`
- `Percentile_NoSamples_IsZero`
- `Percentile_Zero_ReturnsMinimum` / `Percentile_Hundred_ReturnsMaximum`
- `Percentile_OutOfRange_Throws` (Theory: -1, 101)
- `Percentile_IsRobustToOutliers`
- **O que testa:** Com 1..100 gravados, p50=50, p95=95, p99=99 — a validacao do
  metodo nearest-rank. `UnsortedInput` confirma que a ordenacao acontece na
  leitura, nao na escrita (gravar e caminho quente; ordenar la custaria caro).

  Os extremos 0 e 100 sao onde o off-by-one aparece: `Math.Ceiling(0/100 * n) - 1`
  daria indice -1 sem o `Clamp`.

  `IsRobustToOutliers` e o teste conceitual: 99 amostras de 10ms mais uma de
  10000ms mantem p50 em 10 enquanto a media sobe — a demonstracao de por que o
  gate usa percentil.
- **Execucao:** `dotnet test --filter "LatencyTrackerTests.Percentile|LatencyTrackerTests.Average|LatencyTrackerTests.MinMax"`

### 20-24. Avaliacao de alvo
- `MeetsTarget_NoTargetSet_IsTrue` / `MeetsTarget_NoSamples_IsTrue`
- `MeetsTarget_P95WithinTarget_IsTrue` / `MeetsTarget_P95ExceedsTarget_IsFalse`
- `MeetsTarget_ToleratesRareSpikes`
- **O que testa:** `ToleratesRareSpikes` documenta o comportamento por teste: 96
  amostras em 50ms mais 4 em 5000ms **passam** num alvo de 100ms, porque 4% acima
  do orcamento e o que um gate de p95 aceita por definicao. Deixar isso explicito
  evita que alguem "conserte" o teste depois achando que e bug.

  Sem amostras o resultado e `true` — um estagio que nunca rodou nao deve
  reprovar o build.
- **Execucao:** `dotnet test --filter "LatencyTrackerTests.MeetsTarget"`

### 25-26. Snapshot
- `Reset_ClearsSamples`
- `GetSnapshot_CapturesStatistics` / `Snapshot_Format_MarksExceededTarget`
- **Execucao:** `dotnet test --filter "LatencyTrackerTests.Reset|LatencyTrackerTests.GetSnapshot|LatencyTrackerTests.Snapshot"`

---

## PerformanceMonitor (11 testes)

### 1-2. Registro e orcamento
- `Constructor_RegistersEveryEndToEndStage`
- `StageTargets_SumToTheEndToEndBudget`
- **O que testa:** O segundo e o mais valioso do arquivo: soma os alvos
  individuais (100 + 50 + 500 + 200 + 1000 + 50 = 1900ms) e confirma que cabem no
  orcamento de 2000ms. Se alguem afrouxar um alvo de estagio sem revisar o total,
  o teste reprova — o orcamento nao pode ser furado por acumulo silencioso.
- **Execucao:** `dotnet test --filter "PerformanceMonitorTests.Constructor|PerformanceMonitorTests.StageTargets"`

### 3-4. Roteamento por estagio
- `Record_RoutesToNamedStage`
- `GetTracker_UnknownStage_CreatesOnDemand`
- **Execucao:** `dotnet test --filter "PerformanceMonitorTests.Record_Routes|PerformanceMonitorTests.GetTracker"`

### 5-8. Agregacao end-to-end
- `EndToEndP95_SumsStageP95s` / `EndToEndP95_NoSamples_IsZero`
- `MeetsEndToEndTarget_WithinBudget_IsTrue` / `MeetsEndToEndTarget_OverBudget_IsFalse`
- **O que testa:** A soma dos p95 e deliberadamente pessimista — assume que os
  casos lentos de cada estagio coincidem. E o numero que vale a pena defender,
  ainda que na pratica raramente aconteca.
- **Execucao:** `dotnet test --filter "PerformanceMonitorTests.EndToEnd|PerformanceMonitorTests.MeetsEndToEnd"`

### 9. Medicao por escopo
- `Measure_RecordsElapsedTime`
- **O que testa:** O `using (monitor.Measure(stage))` grava o tempo decorrido.
  Unico teste do arquivo com `Thread.Sleep` (20ms) — a assercao usa piso de 15ms
  em vez de igualdade para tolerar a imprecisao do agendador.
- **Execucao:** `dotnet test --filter "PerformanceMonitorTests.Measure"`

### 10-11. Relatorio
- `Reset_ClearsEveryStage`
- `BuildReport_ListsEveryStageAndVerdict` / `BuildReport_IncludesCustomStages`
- **Execucao:** `dotnet test --filter "PerformanceMonitorTests.BuildReport|PerformanceMonitorTests.Reset"`

---

## O que estes testes NAO cobrem

Eles validam o **instrumento**, nao a performance real do app. Os numeros do
criterio de aceite ainda nao existem: obte-los exige rodar em host Windows com
hardware de audio, o que o ambiente de desenvolvimento usado nao tem.

Procedimento para a primeira medicao:

1. Rodar uma reuniao de ~15 min com Whisper `base`, sem GPU
2. Chamar `PerformanceMonitor.BuildReport()` ao encerrar
3. Comparar cada estagio com seu alvo e registrar os numeros aqui
4. Repetir com `tiny` para a comparacao que o spec pede

Pendencias separadas, tambem sem cobertura automatizada:
- **Sem memory leak apos 2h** — precisa de execucao longa com profiler
- **CPU < 15% ocioso / < 40% transcrevendo** — precisa de medicao no host
