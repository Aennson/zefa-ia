# Task 2-2: Whisper Local Provider

## Descrição
Implementar `ISTTProvider` usando Whisper rodando localmente via `Whisper.net` (binding C# do whisper.cpp). Sem dependência de rede.

## Skills
- `simplify` — revisar código após implementação
- `security-review` — garantir que download de modelo é seguro

## Dependências
- Task 2-1 concluída (interface definida)

## Entregáveis
- `WhisperSTTProvider : ISTTProvider` em `ZefaIA.STT`
- Download automático de modelo na primeira execução (base ou small)
- Configuração: modelo (tiny/base/small/medium), idioma (auto/pt/en), GPU vs CPU
- Buffer de áudio com VAD simples (não processar silêncio)
- Emissão de segmentos parciais e finais

## Detalhes Técnicos
- `Whisper.net` wrapa `whisper.cpp` — performance nativa em C/C++
- Modelos: tiny (~75MB), base (~142MB), small (~466MB)
- Para MVP: default `base` (bom equilíbrio custo/qualidade)
- Whisper processa chunks de 30s; precisamos bufferar e processar incrementalmente
- VAD integrado no whisper.cpp (`no_speech_threshold`)
- GPU: suporte CUDA via `Whisper.net.Runtime.Cuda` (opcional)

## Pipeline
```
AudioChunk (100ms) → Buffer (acumula ~2-3s) → VAD check → Whisper process → TranscriptionSegment
```

## Critérios de Aceite
- [ ] Transcreve áudio em PT-BR corretamente
- [ ] Transcreve áudio em EN corretamente
- [ ] Detecta idioma automaticamente
- [ ] Latência < 1.5s do fim da fala até segmento final
- [ ] Não processa silêncio (VAD funciona)
- [ ] Modelo baixa automaticamente se não existir
- [ ] Funciona em CPU (GPU é bônus)
- [ ] Memory: modelo carrega uma vez, sem recargas

## Testes
- Unit: buffer acumula e dispara processamento no threshold correto
- Unit: VAD rejeita chunks de silêncio
- Integration: transcrever WAV de teste e comparar com texto esperado (WER < 20%)
- Performance: processar 10s de áudio em < 2s (CPU, modelo base)
