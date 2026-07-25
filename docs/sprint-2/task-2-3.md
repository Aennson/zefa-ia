# Task 2-3: Pipeline de Transcrição em Tempo Real

## Descrição
Conectar o AudioCaptureEngine (Sprint 1) ao STT Provider, criando um pipeline reativo que emite transcrições em tempo real.

## Skills
- `simplify` — simplificar pipeline reativo

## Dependências
- Task 1-6 (pipeline de áudio) e Task 2-2 (Whisper provider)

## Entregáveis
- `TranscriptionEngine` em `ZefaIA.Core` que orquestra audio → STT
- Observable de segmentos de transcrição
- Dois providers STT instanciados (um para mic, um para loopback)
- Métricas: latência end-to-end, segmentos/min
- App console que demonstra transcrição live

## Arquitetura
```
AudioCaptureEngine
  ├── MicStream ──► WhisperProvider[mic] ──► TranscriptionSegment(Source=Mic)
  └── LoopbackStream ──► WhisperProvider[loopback] ──► TranscriptionSegment(Source=Loopback)
                                                          │
TranscriptionEngine.TranscriptionStream ◄─────────────────┘
```

## Critérios de Aceite
- [ ] Transcrição aparece no console em tempo real
- [ ] Mic e loopback transcrevem independentemente
- [ ] Segmentos parciais atualizam e segmentos finais commitam
- [ ] Latência total (áudio → texto no console) < 2s
- [ ] Pipeline não acumula backlog (métricas estáveis)
- [ ] Graceful: falha do STT não derruba o áudio

## Testes
- Unit: TranscriptionEngine roteia chunks para providers corretos
- Unit: segmentos de ambos providers são merged na stream de saída
- Integration: 30s de captura + transcrição sem erros
- Performance: métricas de latência dentro do target
