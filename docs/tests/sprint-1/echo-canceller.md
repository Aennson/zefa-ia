# Testes: EchoCanceller

**Arquivo fonte:** `src/ZefaIA.Audio/EchoCanceller.cs`
**Arquivo de teste:** `tests/ZefaIA.Audio.Tests/EchoCancellerTests.cs`
**Classe de teste:** `EchoCancellerTests`

## Motivacao

O `EchoCanceller` usa um filtro adaptativo NLMS (Normalized Least Mean Squares) para remover o eco do loopback que vaza no microfone. Sem AEC, o STT receberia falas duplicadas. Os testes usam sinais sinteticos (ondas senoidais) para validar a reducao de eco sem precisar de hardware.

## Helpers

- `GenerateSineWave(frequency, amplitude, sampleRate, durationMs)` — gera PCM int16 de uma onda senoidal. Usado para simular audio de referencia (loopback) e eco (mic).
- `CalculateRms(pcm)` — calcula o RMS (Root Mean Square) de um buffer PCM. Usado para medir energia do sinal antes e depois do AEC.

## Testes

### 1. `Process_WithoutReference_ReturnsOriginal`
- **Tipo:** Unit
- **O que testa:** Processar audio do mic sem ter alimentado referencia (loopback) retorna audio do mesmo tamanho
- **Como funciona:** Cria AEC, gera 100ms de senoidal 440Hz, chama `Process()` sem `FeedReference()`. Verifica que o resultado tem mesmo tamanho que input.
- **Por que existe:** No inicio da sessao ou se o loopback falhar, nao ha referencia. O AEC deve passar o audio do mic sem crashar. Graceful degradation.
- **Execucao:** `dotnet test --filter "EchoCancellerTests.Process_WithoutReference_ReturnsOriginal"`

### 2. `Process_WithMatchingReference_ReducesEcho`
- **Tipo:** Unit
- **O que testa:** Com referencia alimentada, o eco e reduzido (RMS do output < RMS do input)
- **Como funciona:**
  1. Gera referencia (senoidal 440Hz, amplitude 0.8) simulando audio do loopback
  2. Gera eco no mic (mesma frequencia, amplitude 0.3) simulando o que o mic capta
  3. Alimenta referencia multiplas vezes para treinar o filtro adaptativo (5 iteracoes)
  4. Processa uma ultima vez e compara RMS antes vs depois
  5. Verifica que `processedRms < originalRms`
- **Por que existe:** Teste central do AEC. Se o filtro adaptativo nao convergir, o eco permanecera e o STT transcreverá falas duplicadas.
- **Execucao:** `dotnet test --filter "EchoCancellerTests.Process_WithMatchingReference_ReducesEcho"`

### 3. `Disabled_ReturnsOriginalData`
- **Tipo:** Unit
- **O que testa:** Com `IsEnabled=false`, o AEC retorna o buffer original intacto
- **Como funciona:** Cria AEC, desabilita, processa audio, verifica que retorno e identico ao input.
- **Por que existe:** O usuario pode desabilitar AEC via Settings se estiver causando artefatos. O toggle deve ser transparente.
- **Execucao:** `dotnet test --filter "EchoCancellerTests.Disabled_ReturnsOriginalData"`

### 4. `Reset_ClearsFilter`
- **Tipo:** Unit
- **O que testa:** `Reset()` limpa o filtro adaptativo e o buffer de referencia
- **Como funciona:** Alimenta referencia, processa (treina filtro), chama `Reset()`, processa novamente. Verifica que o resultado tem tamanho correto (filtro voltou ao estado inicial).
- **Por que existe:** Entre reunioes, o filtro deve ser resetado — o perfil acustico muda (headset diferente, sala diferente). Sem reset, o filtro antigo causaria artefatos.
- **Execucao:** `dotnet test --filter "EchoCancellerTests.Reset_ClearsFilter"`

### 5. `Process_DoesNotClip`
- **Tipo:** Unit
- **O que testa:** Audio processado nao contem valores fora do range int16 [-32768, 32767]
- **Como funciona:** Gera sinal alto (amplitude 0.99), processa, verifica cada sample do resultado com `Assert.InRange`.
- **Por que existe:** O processo de subtracao adaptativa pode gerar valores fora do range se nao houver clamping. Clipping causaria estouros sonoros.
- **Execucao:** `dotnet test --filter "EchoCancellerTests.Process_DoesNotClip"`

### 6. `Dispose_MultipleCallsDoNotThrow`
- **Tipo:** Unit
- **O que testa:** Dispose duplo e seguro
- **Como funciona:** Dispose chamado 2x sem excecao.
- **Por que existe:** Padrao IDisposable — seguranca no shutdown.
- **Execucao:** `dotnet test --filter "EchoCancellerTests.Dispose_MultipleCallsDoNotThrow"`
