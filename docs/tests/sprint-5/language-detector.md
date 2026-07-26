# Testes: LanguageDetector

**Arquivo fonte:** `src/ZefaIA.Core/LanguageDetector.cs`
**Arquivo de teste:** `tests/ZefaIA.STT.Tests/LanguageDetectorTests.cs`
**Classe de teste:** `LanguageDetectorTests`
**Total:** 19 testes

## Motivacao

O idioma detectado controla os labels de speaker e a adaptacao do system prompt. Duas propriedades importam mais que a acuracia bruta: a deteccao precisa **estabilizar** (nao pode ficar alternando entre PT e EN no meio da reuniao, trocando labels a cada segmento) e o **override manual** precisa sempre vencer. Os testes usam `minSamples: 3` para tornar o threshold rapido de atingir.

O detector nao roda um modelo de identificacao de idioma — ele agrega o campo `Language` que o provider de STT ja retorna em cada segmento, o que satisfaz o criterio de "nao adicionar latencia".

## Testes

### 1, 5. Estado inicial e threshold
- `DetectedLanguage_Initial_IsAuto`
- `ProcessSegment_BelowThreshold_NotDetected`
- **O que testa:** Antes de `minSamples` segmentos o resultado e "auto" e `IsDetected` e falso — a UI usa isso para nao trocar labels prematuramente
- **Execucao:** `dotnet test --filter "LanguageDetectorTests.DetectedLanguage_Initial|LanguageDetectorTests.ProcessSegment_BelowThreshold"`

### 2-4. Deteccao por maioria
- `ProcessSegment_PortugueseSegments_DetectsPt`
- `ProcessSegment_EnglishSegments_DetectsEn`
- `ProcessSegment_MixedLanguages_DetectsMajority`
- **O que testa:** Contagem por idioma com desempate pelo mais frequente. O caso misto (2 PT + 1 EN) reflete a realidade: o STT erra segmentos curtos ocasionalmente, e a maioria absorve esse ruido.
- **Execucao:** `dotnet test --filter "LanguageDetectorTests.ProcessSegment_Portuguese|LanguageDetectorTests.ProcessSegment_English|LanguageDetectorTests.ProcessSegment_Mixed"`

### 6. Trava apos deteccao
- `ProcessSegment_AfterLocked_IgnoresNewSegments`
- **O que testa:** Depois de detectar PT com 3 amostras, alimentar 10 segmentos EN **nao** muda o resultado. Sem essa trava, uma reuniao bilingue trocaria os labels de speaker no meio da conversa.
- **Execucao:** `dotnet test --filter "LanguageDetectorTests.ProcessSegment_AfterLocked"`

### 7-9, 19. Override manual
- `SetOverride_OverridesDetection`
- `SetOverride_Auto_ClearsOverride`
- `SetOverride_EmptyString_ClearsOverride`
- `ProcessSegment_WithOverrideSet_Ignored`
- **O que testa:** O override das Settings vence a deteccao automatica em qualquer ordem — definido antes ou depois dos segmentos. Tanto `"auto"` quanto `""` limpam o override e devolvem o controle a deteccao (sao os dois valores que o ComboBox de idioma pode emitir).
- **Execucao:** `dotnet test --filter "LanguageDetectorTests.SetOverride|LanguageDetectorTests.ProcessSegment_WithOverrideSet"`

### 10-11. Evento de notificacao
- `OnLanguageDetected_FiresWhenDetected`
- `OnLanguageDetected_FiresOnOverride`
- **O que testa:** O evento dispara nos dois caminhos (deteccao e override), permitindo que overlay e prompt builder reajam sem polling
- **Execucao:** `dotnet test --filter "LanguageDetectorTests.OnLanguageDetected"`

### 12-15. Labels de speaker por idioma
- `GetSpeakerLabels_Portuguese_ReturnsEuInterlocutor`
- `GetSpeakerLabels_English_ReturnsMeOther`
- `GetSpeakerLabels_Spanish_ReturnsYoInterlocutor`
- `GetSpeakerLabels_French_ReturnsMoiInterlocuteur`
- **O que testa:** Cada idioma suportado mapeia para o par correto; idiomas nao mapeados caem no default portugues
- **Execucao:** `dotnet test --filter "LanguageDetectorTests.GetSpeakerLabels"`

### 16. Normalizacao de codigo
- `NormalizeLanguage_VariousFormats_ReturnsShortCode`
- **O que testa:** `pt-BR`, `en-US` e `EN` colapsam para `pt` e `en`. Sem isso, Whisper retornando `pt` e ElevenLabs retornando `pt-BR` seriam contados como idiomas diferentes e nenhum atingiria o threshold sozinho.
- **Execucao:** `dotnet test --filter "LanguageDetectorTests.NormalizeLanguage"`

### 17. Idioma desconhecido
- `ProcessSegment_UnknownLanguage_Ignored`
- **O que testa:** Segmentos com `"unknown"` ou string vazia nao contam para o threshold — evita travar em um valor invalido
- **Execucao:** `dotnet test --filter "LanguageDetectorTests.ProcessSegment_UnknownLanguage"`

### 18. Reset
- `Reset_ClearsState`
- **O que testa:** Limpa contagens e destrava a deteccao para a proxima reuniao, sem precisar recriar o objeto
- **Execucao:** `dotnet test --filter "LanguageDetectorTests.Reset"`
