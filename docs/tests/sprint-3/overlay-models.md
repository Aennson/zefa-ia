# Testes: OverlayModels

**Arquivo fonte:** `src/ZefaIA.Overlay/OverlayModels.cs`
**Arquivo de teste:** `tests/ZefaIA.Overlay.Tests/OverlayModelsTests.cs`
**Classe de teste:** `OverlayModelsTests`

## Motivacao

Os models do overlay (`OverlaySettings`, `TranscriptionDisplayItem`, `SuggestionDisplayItem`) definem a configuracao visual e os dados exibidos. Testar defaults garante que o overlay funciona out-of-the-box sem configuracao.

## Testes

### 1. `OverlaySettings_HasCorrectDefaults`
- **Tipo:** Unit
- **O que testa:** Defaults de `OverlaySettings`
- **Como funciona:** Cria instancia, verifica Opacity=0.85, FontSize=14, Position=BottomRight, AutoHideSeconds=30, ExcludeFromCapture=true.
- **Por que existe:** Defaults errados causariam overlay invisivel (opacity=0), ilegivel (font=0) ou visivel em screen sharing.
- **Execucao:** `dotnet test --filter "OverlayModelsTests.OverlaySettings_HasCorrectDefaults"`

### 2. `OverlayPosition_HasAllValues`
- **Tipo:** Unit
- **O que testa:** Enum `OverlayPosition` tem 5 valores
- **Como funciona:** Verifica TopLeft, TopRight, BottomLeft, BottomRight e Center.
- **Por que existe:** Garante que todas as posicoes sao mapeadas na UI de settings.
- **Execucao:** `dotnet test --filter "OverlayModelsTests.OverlayPosition_HasAllValues"`

### 3-4. `TranscriptionDisplayItem` defaults e properties
- **Tipo:** Unit
- **O que testa:** Construcao e valores dos display items
- **Execucao:** `dotnet test --filter "OverlayModelsTests.TranscriptionDisplayItem"`

### 5-6. `SuggestionDisplayItem` defaults e properties
- **Tipo:** Unit
- **O que testa:** Construcao dos items de sugestao
- **Execucao:** `dotnet test --filter "OverlayModelsTests.SuggestionDisplayItem"`

### 7. `OverlaySettings_MutableProperties`
- **Tipo:** Unit
- **O que testa:** Todas as propriedades sao atribuiveis
- **Execucao:** `dotnet test --filter "OverlayModelsTests.OverlaySettings_MutableProperties"`
