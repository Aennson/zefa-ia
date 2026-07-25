# Testes: Resampler

**Arquivo fonte:** `src/ZefaIA.Audio/Resampler.cs`
**Arquivo de teste:** `tests/ZefaIA.Audio.Tests/ResamplerTests.cs`
**Classe de teste:** `ResamplerTests`

## Motivacao

O `Resampler` converte audio de qualquer formato (float32/int16, mono/stereo, qualquer sample rate) para o formato padrao do pipeline: PCM 16kHz 16-bit mono. Erros aqui causam audio distorcido, clipping, ou crash no STT. E o componente mais critico para a qualidade do audio capturado.

## Testes

### 1. `ResampleToTarget_SameFormat_ReturnsSameData`
- **Tipo:** Unit
- **O que testa:** Audio que ja esta no formato alvo (16kHz, mono, 16-bit) passa sem modificacao
- **Como funciona:** Passa um buffer de 4 bytes com sample rate 16000, 1 canal, 16 bits. Verifica que o retorno e identico ao input (mesma referencia ou bytes iguais).
- **Por que existe:** Otimizacao — se o audio ja esta no formato certo, nao deve haver processamento desnecessario que adicione latencia.
- **Execucao:** `dotnet test --filter "ResamplerTests.ResampleToTarget_SameFormat_ReturnsSameData"`

### 2. `ConvertFloat32ToInt16_ClampsCorrectly`
- **Tipo:** Unit
- **O que testa:** Conversao de um sample float32 (0.5f) para int16
- **Como funciona:** Cria 4 bytes representando o float 0.5, converte, e verifica que o resultado tem 2 bytes e o valor int16 e positivo (proporcional a 0.5 * 32767).
- **Por que existe:** O loopback WASAPI captura em float32. Se a conversao para int16 estiver errada, o audio fica distorcido ou silencioso.
- **Execucao:** `dotnet test --filter "ResamplerTests.ConvertFloat32ToInt16_ClampsCorrectly"`

### 3. `ConvertFloat32ToInt16_ClipsAtBounds`
- **Tipo:** Unit
- **O que testa:** Valores float acima de 1.0 sao clampados a `short.MaxValue` (32767)
- **Como funciona:** Passa float 2.0 (fora do range [-1, 1]) e verifica que o resultado e exatamente `short.MaxValue`.
- **Por que existe:** Audio sem clamping causa overflow em int16, gerando estouros sonoros (pops/clicks) que corrompem a transcrição.
- **Execucao:** `dotnet test --filter "ResamplerTests.ConvertFloat32ToInt16_ClipsAtBounds"`

### 4. `ResampleToTarget_StereoToMono_HalvesSamples`
- **Tipo:** Unit
- **O que testa:** Conversao stereo (2 canais) para mono reduz o numero de samples pela metade
- **Como funciona:** Cria 16 bytes de audio stereo 16-bit (4 samples * 2 canais * 2 bytes). Converte para mono e verifica que o resultado tem metade do tamanho (8 bytes = 4 samples mono).
- **Por que existe:** O STT espera mono. Se stereo passasse direto, cada sample seria interpretado como dois, causando audio acelerado e ilegivel.
- **Execucao:** `dotnet test --filter "ResamplerTests.ResampleToTarget_StereoToMono_HalvesSamples"`

### 5. `ResampleToTarget_48kTo16k_ReducesSamples`
- **Tipo:** Unit
- **O que testa:** Downsampling de 48kHz para 16kHz reduz samples na proporcao 3:1
- **Como funciona:** Gera 4800 samples de onda senoidal 440Hz a 48kHz. Converte para 16kHz e verifica que o resultado tem aproximadamente 1600 samples (tolerancia de +-2).
- **Por que existe:** A maioria dos dispositivos de audio Windows opera a 48kHz. Sem downsampling correto, o Whisper receberia audio 3x mais lento (pitch grave).
- **Execucao:** `dotnet test --filter "ResamplerTests.ResampleToTarget_48kTo16k_ReducesSamples"`

### 6. `ResampleToTarget_Float32Input_Converts`
- **Tipo:** Unit
- **O que testa:** Pipeline completo: float32 input a 16kHz converte para int16
- **Como funciona:** Cria 4 samples float32 (0.25f cada), converte, e verifica que o resultado tem 8 bytes (4 samples * 2 bytes/sample int16) com valores positivos.
- **Por que existe:** Testa o path completo de conversao quando o input e float32 mas ja esta no sample rate correto — cenario real de loopback com sample rate 16kHz.
- **Execucao:** `dotnet test --filter "ResamplerTests.ResampleToTarget_Float32Input_Converts"`
