# Testes: HotkeyTrigger

**Arquivo fonte:** `src/ZefaIA.Core/Triggers/HotkeyTrigger.cs`
**Arquivo de teste:** `tests/ZefaIA.LLM.Tests/HotkeyTriggerTests.cs`
**Classe de teste:** `HotkeyTriggerTests`

## Motivacao

`HotkeyTrigger` registra hotkeys globais via Win32 para disparar sugestoes on-demand. Testar garante parsing de strings, registro de hotkeys e processamento de WM_HOTKEY.

## Testes

### 1. `TriggerName_IsHotkeyTrigger`
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "HotkeyTriggerTests.TriggerName"`

### 2. `RegisterHotkey_ReturnsIncrementingIds`
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "HotkeyTriggerTests.RegisterHotkey"`

### 3. `ProcessMessage_RegisteredHotkey_FiresTriggered`
- **Tipo:** Unit
- **O que testa:** WM_HOTKEY com ID registrado dispara evento
- **Execucao:** `dotnet test --filter "HotkeyTriggerTests.ProcessMessage_RegisteredHotkey"`

### 4. `ProcessMessage_UnregisteredId_ReturnsFalse`
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "HotkeyTriggerTests.ProcessMessage_UnregisteredId"`

### 5. `ProcessMessage_AfterUnregister_ReturnsFalse`
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "HotkeyTriggerTests.ProcessMessage_AfterUnregister"`

### 6. `ProcessMessage_SetsTranscriptWindowFromBinding`
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "HotkeyTriggerTests.ProcessMessage_SetsTranscriptWindow"`

### 7-10. `ParseHotkeyString_*`
- **Tipo:** Unit
- **O que testa:** Ctrl+Shift+Space, Ctrl+Shift+Z, Alt+C, case insensitive
- **Execucao:** `dotnet test --filter "HotkeyTriggerTests.ParseHotkeyString"`

### 11-13. Binding defaults, modifiers flags, dispose
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "HotkeyTriggerTests.HotkeyBinding"`
