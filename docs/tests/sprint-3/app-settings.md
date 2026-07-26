# Testes: AppSettings

**Arquivo fonte:** `src/ZefaIA.Overlay/AppSettings.cs`
**Arquivo de teste:** `tests/ZefaIA.Overlay.Tests/AppSettingsTests.cs`
**Classe de teste:** `AppSettingsTests`

## Motivacao

`AppSettings` e a classe de configuracao unificada do app. Serializa/deserializa para JSON e persiste em disco. Testar garante que as preferencias do usuario sobrevivem entre sessoes.

## Testes

### 1. `HasCorrectDefaults`
- **Tipo:** Unit
- **O que testa:** Todos os defaults da classe
- **Como funciona:** Cria instancia, verifica cada campo: SttProvider="WhisperLocal", ModelSize="base", Opacity=0.85, etc.
- **Por que existe:** Defaults definem a experiencia do primeiro uso.
- **Execucao:** `dotnet test --filter "AppSettingsTests.HasCorrectDefaults"`

### 2. `ToJson_ProducesValidJson`
- **Tipo:** Unit
- **O que testa:** Serializacao JSON contem campos corretos
- **Execucao:** `dotnet test --filter "AppSettingsTests.ToJson_ProducesValidJson"`

### 3. `FromJson_ParsesCorrectly`
- **Tipo:** Unit
- **O que testa:** Deserializacao restaura todos os campos
- **Execucao:** `dotnet test --filter "AppSettingsTests.FromJson_ParsesCorrectly"`

### 4. `FromJson_InvalidJson_ReturnsDefaults`
- **Tipo:** Unit
- **O que testa:** JSON incompleto usa defaults
- **Execucao:** `dotnet test --filter "AppSettingsTests.FromJson_InvalidJson_ReturnsDefaults"`

### 5. `RoundTrip_PreservesAllFields`
- **Tipo:** Unit
- **O que testa:** Serialize → Deserialize preserva todos os 17 campos
- **Como funciona:** Cria settings com todos os campos customizados, serializa, deserializa, compara cada um.
- **Por que existe:** Garante que nenhum campo e perdido na persistencia.
- **Execucao:** `dotnet test --filter "AppSettingsTests.RoundTrip_PreservesAllFields"`

### 6. `SaveAndLoad_FilePersistence`
- **Tipo:** Unit (filesystem)
- **O que testa:** Save/Load em arquivo real
- **Como funciona:** Salva em temp file, carrega de volta, verifica valores. Deleta no finally.
- **Execucao:** `dotnet test --filter "AppSettingsTests.SaveAndLoad_FilePersistence"`

### 7. `LoadAsync_FileNotExists_ReturnsDefaults`
- **Tipo:** Unit
- **O que testa:** Arquivo inexistente retorna defaults
- **Execucao:** `dotnet test --filter "AppSettingsTests.LoadAsync_FileNotExists_ReturnsDefaults"`

### 8. `ToJson_EnumSerializesAsString`
- **Tipo:** Unit
- **O que testa:** Enums serializam como string (nao int)
- **Como funciona:** Position=Center serializa como "Center", nao "4".
- **Por que existe:** JSON legivel para edicao manual.
- **Execucao:** `dotnet test --filter "AppSettingsTests.ToJson_EnumSerializesAsString"`
