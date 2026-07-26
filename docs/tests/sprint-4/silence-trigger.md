# Testes: SilenceTrigger

**Arquivo fonte:** `src/ZefaIA.Core/Triggers/SilenceTrigger.cs`
**Arquivo de teste:** `tests/ZefaIA.LLM.Tests/SilenceTriggerTests.cs`
**Classe de teste:** `SilenceTriggerTests`

## Motivacao

`SilenceTrigger` detecta silencio no audio loopback para disparar sugestoes automaticamente. Testar garante que a deteccao funciona corretamente com cooldown e checagem de transcricao recente.

## Testes

### 1-4. `CalculateRMS_*`
- **Tipo:** Unit
- **O que testa:** Calculo de RMS em audio silencioso, alto, vazio, byte unico
- **Execucao:** `dotnet test --filter "SilenceTriggerTests.CalculateRMS"`

### 5. `TriggerName_IsSilenceTrigger`
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "SilenceTriggerTests.TriggerName"`

### 6. `OnAudioChunk_LoudAudio_DoesNotTrigger`
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "SilenceTriggerTests.OnAudioChunk_LoudAudio"`

### 7. `OnAudioChunk_SilenceWithoutTranscription_DoesNotTrigger`
- **Tipo:** Unit
- **O que testa:** Nao dispara sem transcricao recente
- **Execucao:** `dotnet test --filter "SilenceTriggerTests.OnAudioChunk_SilenceWithoutTranscription"`

### 8. `OnAudioChunk_SilenceWithTranscription_Triggers`
- **Tipo:** Unit
- **O que testa:** Dispara com silencio + transcricao recente
- **Execucao:** `dotnet test --filter "SilenceTriggerTests.OnAudioChunk_SilenceWithTranscription"`

### 9. `OnAudioChunk_CooldownPreventsRefire`
- **Tipo:** Unit
- **O que testa:** Cooldown impede disparos repetidos
- **Execucao:** `dotnet test --filter "SilenceTriggerTests.OnAudioChunk_CooldownPreventsRefire"`

### 10. `OnAudioChunk_LoudAfterSilence_ResetsSilenceTimer`
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "SilenceTriggerTests.OnAudioChunk_LoudAfterSilence"`

### 11. `Config_HasCorrectDefaults`
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "SilenceTriggerTests.Config_HasCorrectDefaults"`

### 12. `Dispose_MultipleCallsDoNotThrow`
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "SilenceTriggerTests.Dispose"`
