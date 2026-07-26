# Testes: OverlayController

**Arquivo fonte:** `src/ZefaIA.Overlay/OverlayController.cs`
**Arquivo de teste:** `tests/ZefaIA.Overlay.Tests/OverlayControllerTests.cs`
**Classe de teste:** `OverlayControllerTests`

## Motivacao

`OverlayController` orquestra a conexao entre TranscriptionEngine e OverlayWindow. Os testes validam instanciacao, settings e lifecycle sem precisar de display grafico.

## Testes

### 1. `Constructor_CreatesWindowWithDefaultSettings`
- **Tipo:** Unit
- **O que testa:** Controller cria window com settings padrao
- **Execucao:** `dotnet test --filter "OverlayControllerTests.Constructor_CreatesWindowWithDefaultSettings"`

### 2. `Constructor_AppliesCustomSettings`
- **Tipo:** Unit
- **O que testa:** Settings customizados sao passados para window
- **Execucao:** `dotnet test --filter "OverlayControllerTests.Constructor_AppliesCustomSettings"`

### 3. `SetSpeakerNames_UpdatesNames`
- **Tipo:** Unit
- **O que testa:** Nomes de speakers sao configuraveis
- **Execucao:** `dotnet test --filter "OverlayControllerTests.SetSpeakerNames_UpdatesNames"`

### 4. `Dispose_MultipleCallsDoNotThrow`
- **Tipo:** Unit
- **O que testa:** Dispose duplo e seguro
- **Execucao:** `dotnet test --filter "OverlayControllerTests.Dispose_MultipleCallsDoNotThrow"`
