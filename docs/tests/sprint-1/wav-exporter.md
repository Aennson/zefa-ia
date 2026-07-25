# Testes: WavExporter

**Arquivo fonte:** `src/ZefaIA.Audio/WavExporter.cs`
**Arquivo de teste:** `tests/ZefaIA.Audio.Tests/WavExporterTests.cs`
**Classe de teste:** `WavExporterTests`

## Motivacao

`WavExporter` e uma utilidade para salvar chunks PCM em formato WAV para verificacao manual (ouvir o audio capturado). Usado durante desenvolvimento e debugging para validar que a captura e o resampling estao corretos.

## Testes

### 1. `WriteWav_CreatesValidWavFile`
- **Tipo:** Unit (filesystem)
- **O que testa:** O arquivo WAV gerado tem header valido e dados corretos
- **Como funciona:**
  1. Cria arquivo temporario
  2. Prepara 2 chunks de 4 bytes cada (8 bytes total de dados PCM)
  3. Chama `WavExporter.WriteWav()` com os chunks
  4. Le o arquivo gerado e verifica:
     - Tamanho > 44 bytes (44 = header WAV minimo)
     - Bytes 0-3: "RIFF" (magic number WAV)
     - Bytes 8-11: "WAVE" (format identifier)
     - Bytes 40-43: tamanho dos dados = 8 (2 chunks * 4 bytes)
  5. Deleta o arquivo temporario no `finally`
- **Por que existe:** Um WAV invalido nao abre em players de audio, impossibilitando a verificacao manual. O header WAV tem uma estrutura rigida — qualquer byte errado torna o arquivo ilegivel.
- **Execucao:** `dotnet test --filter "WavExporterTests.WriteWav_CreatesValidWavFile"`
