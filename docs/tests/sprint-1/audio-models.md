# Testes: AudioModels

**Arquivo fonte:** `src/ZefaIA.Core/Models/AudioModels.cs`
**Arquivo de teste:** `tests/ZefaIA.Audio.Tests/AudioModelsTests.cs`
**Classe de teste:** `AudioModelsTests`

## Motivacao

Os models de audio (`AudioChunkEventArgs`, `SpeakerLabel`, `AudioSourceState`) sao records/enums usados em todo o pipeline. Testar sua construcao garante que os valores default e a imutabilidade dos records funcionam como esperado. Como sao a base de toda comunicacao entre componentes, erros aqui se propagariam silenciosamente.

## Testes

### 1. `AudioChunkEventArgs_CreatesCorrectly`
- **Tipo:** Unit
- **O que testa:** Construcao do record `AudioChunkEventArgs` com todos os campos
- **Como funciona:** Cria uma instancia com dados PCM, sample rate 16000, timestamp de 1s e source Microphone. Verifica que cada propriedade retorna o valor passado no construtor.
- **Por que existe:** Garante que o record armazena corretamente os dados de um chunk de audio — se um campo fosse reordenado ou renomeado, este teste quebraria imediatamente.
- **Execucao:** `dotnet test --filter "AudioModelsTests.AudioChunkEventArgs_CreatesCorrectly"`

### 2. `SpeakerLabel_Me_ReturnsCorrectDefaults`
- **Tipo:** Unit
- **O que testa:** O factory method `SpeakerLabel.Me()` sem parametros
- **Como funciona:** Chama `SpeakerLabel.Me()` e verifica que `DisplayName` e "Eu" e `Source` e `AudioSourceType.Microphone`.
- **Por que existe:** O label default para o usuario e usado na diarizacao (Sprint 2). Se o default mudar acidentalmente, a transcrição mostraria labels errados.
- **Execucao:** `dotnet test --filter "AudioModelsTests.SpeakerLabel_Me_ReturnsCorrectDefaults"`

### 3. `SpeakerLabel_Other_ReturnsCorrectDefaults`
- **Tipo:** Unit
- **O que testa:** O factory method `SpeakerLabel.Other()` sem parametros
- **Como funciona:** Chama `SpeakerLabel.Other()` e verifica que `DisplayName` e "Interlocutor" e `Source` e `AudioSourceType.Loopback`.
- **Por que existe:** Mesma razao que o teste anterior, mas para o interlocutor (audio vindo do loopback).
- **Execucao:** `dotnet test --filter "AudioModelsTests.SpeakerLabel_Other_ReturnsCorrectDefaults"`

### 4. `SpeakerLabel_CustomName_Works`
- **Tipo:** Unit
- **O que testa:** O factory method `SpeakerLabel.Me("Joao")` com nome customizado
- **Como funciona:** Passa "Joao" como parametro e verifica que `DisplayName` reflete o nome passado.
- **Por que existe:** O usuario podera configurar seu nome nas Settings (Sprint 3). Este teste garante que nomes customizados sao aceitos.
- **Execucao:** `dotnet test --filter "AudioModelsTests.SpeakerLabel_CustomName_Works"`

### 5. `AudioSourceState_HasAllExpectedValues`
- **Tipo:** Unit
- **O que testa:** O enum `AudioSourceState` contem os valores criticos (Idle, Capturing, Error)
- **Como funciona:** Usa `Enum.GetValues<AudioSourceState>()` e verifica com `Assert.Contains` que os tres estados criticos existem.
- **Por que existe:** O enum e usado para maquina de estado do audio source. Se alguem remover um valor, o pipeline quebraria sem erro claro.
- **Execucao:** `dotnet test --filter "AudioModelsTests.AudioSourceState_HasAllExpectedValues"`
