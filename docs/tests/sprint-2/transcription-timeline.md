# Testes: TranscriptionTimeline

**Arquivo fonte:** `src/ZefaIA.STT/TranscriptionTimeline.cs`
**Arquivo de teste:** `tests/ZefaIA.STT.Tests/TranscriptionTimelineTests.cs`
**Classe de teste:** `TranscriptionTimelineTests`

## Motivacao

`TranscriptionTimeline` implementa a diarizacao por stream — mapeia `AudioSourceType.Microphone` para "Eu" e `AudioSourceType.Loopback` para "Interlocutor". Mantem uma timeline ordenada cronologicamente com segmentos de ambos speakers. Os testes validam ordenacao, labels, overlaps e formatacao.

## Helpers

- `MakeSegment(text, startSec, endSec, source)` — cria `TranscriptionSegment` com defaults para language="pt" e confidence=0.9.

## Testes

### 1. `Add_MicSegment_LabeledAsMe`
- **Tipo:** Unit
- **O que testa:** Segmento de mic recebe label "Eu"
- **Como funciona:** Adiciona segmento com Source=Microphone, verifica DisplayName="Eu" no resultado.
- **Por que existe:** Mapeamento errado inverteria quem esta falando na UI.
- **Execucao:** `dotnet test --filter "TranscriptionTimelineTests.Add_MicSegment_LabeledAsMe"`

### 2. `Add_LoopbackSegment_LabeledAsOther`
- **Tipo:** Unit
- **O que testa:** Segmento de loopback recebe label "Interlocutor"
- **Como funciona:** Adiciona segmento com Source=Loopback, verifica DisplayName="Interlocutor".
- **Por que existe:** Complemento do teste anterior.
- **Execucao:** `dotnet test --filter "TranscriptionTimelineTests.Add_LoopbackSegment_LabeledAsOther"`

### 3. `GetTimeline_OrdersByStartTime`
- **Tipo:** Unit
- **O que testa:** Timeline ordena segmentos por StartTime
- **Como funciona:** Adiciona 3 segmentos fora de ordem (3s, 0s, 6s). Verifica que GetTimeline retorna na ordem correta.
- **Por que existe:** Segmentos chegam de providers diferentes em tempos diferentes. Sem ordenacao, a conversa pareceria incoerente.
- **Execucao:** `dotnet test --filter "TranscriptionTimelineTests.GetTimeline_OrdersByStartTime"`

### 4. `GetTimeline_OverlappingSegments_PreservesBoth`
- **Tipo:** Unit
- **O que testa:** Segmentos com timestamps sobrepostos sao preservados
- **Como funciona:** Adiciona mic (2-5s) e loopback (3-6s) sobrepostos. Verifica que ambos aparecem.
- **Por que existe:** Quando ambos falam ao mesmo tempo, descartar um perderia informacao.
- **Execucao:** `dotnet test --filter "TranscriptionTimelineTests.GetTimeline_OverlappingSegments_PreservesBoth"`

### 5. `GetTimelineWindow_FiltersCorrectly`
- **Tipo:** Unit
- **O que testa:** Filtro por janela de tempo funciona
- **Como funciona:** Adiciona 3 segmentos (0-2s, 5-8s, 12-15s). Filtra janela 3-10s. Verifica que apenas o do meio aparece.
- **Por que existe:** O LLM (Sprint 4) usa janela de contexto dos ultimos N segundos de transcricao.
- **Execucao:** `dotnet test --filter "TranscriptionTimelineTests.GetTimelineWindow_FiltersCorrectly"`

### 6. `ConfigurableLabels_ChangeDisplayNames`
- **Tipo:** Unit
- **O que testa:** Labels de speaker sao configuraveis
- **Como funciona:** Define MicSpeaker="Joao" e LoopbackSpeaker="Maria". Verifica labels corretos.
- **Por que existe:** O usuario pode querer personalizar nomes na UI do overlay.
- **Execucao:** `dotnet test --filter "TranscriptionTimelineTests.ConfigurableLabels_ChangeDisplayNames"`

### 7. `FormatTimeline_ProducesFormattedOutput`
- **Tipo:** Unit
- **O que testa:** Formatacao de timeline para exibicao
- **Como funciona:** Adiciona 2 segmentos, chama `FormatTimeline()`, verifica presenca de labels e textos.
- **Por que existe:** O output formatado e usado tanto no console demo quanto no overlay.
- **Execucao:** `dotnet test --filter "TranscriptionTimelineTests.FormatTimeline_ProducesFormattedOutput"`

### 8. `DiarizedSegment_Format_IncludesTimestamp`
- **Tipo:** Unit
- **O que testa:** Formato de segmento inclui timestamp mm:ss
- **Como funciona:** Cria segmento em 1m05s, formata, verifica output "[01:05] [Eu] Test".
- **Por que existe:** Timestamps permitem correlacionar transcricao com momento da reuniao.
- **Execucao:** `dotnet test --filter "TranscriptionTimelineTests.DiarizedSegment_Format_IncludesTimestamp"`

### 9. `DiarizedSegment_FormatWithConfidence_IncludesPercentage`
- **Tipo:** Unit
- **O que testa:** Formato com confidence mostra percentual
- **Como funciona:** Segmento com confidence=0.95, formata, verifica "95%" presente.
- **Por que existe:** Confidence baixa indica transcricao duvidosa — util para debugging.
- **Execucao:** `dotnet test --filter "TranscriptionTimelineTests.DiarizedSegment_FormatWithConfidence_IncludesPercentage"`

### 10. `Clear_RemovesAllSegments`
- **Tipo:** Unit
- **O que testa:** Clear limpa todos os segmentos
- **Como funciona:** Adiciona 2 segmentos, chama Clear, verifica count=0 e timeline vazia.
- **Por que existe:** Entre reunioes, a timeline deve ser resetada.
- **Execucao:** `dotnet test --filter "TranscriptionTimelineTests.Clear_RemovesAllSegments"`

### 11. `SpeakerLabel_FactoryMethods_SetSourceCorrectly`
- **Tipo:** Unit
- **O que testa:** Factory methods de `SpeakerLabel` associam Source correto
- **Como funciona:** `Me("Alice")` → Source=Microphone, `Other("Bob")` → Source=Loopback.
- **Por que existe:** O mapeamento Source↔Label e a base da diarizacao.
- **Execucao:** `dotnet test --filter "TranscriptionTimelineTests.SpeakerLabel_FactoryMethods_SetSourceCorrectly"`
