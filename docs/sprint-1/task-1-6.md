# Task 1-6: Pipeline de Áudio com Eventos

## Descrição
Refinar o pipeline de áudio usando System.Reactive (Rx) para criar um stream reativo de chunks processados, pronto para alimentar o STT no sprint seguinte.

## Skills
- `simplify` — revisar e simplificar o pipeline
- `security-review` — revisão final do sprint 1

## Dependências
- Tasks 1-2 a 1-5 concluídas

## Entregáveis
- Pipeline reativo: `AudioCaptureEngine.AudioStream` como `IObservable<AudioChunkEventArgs>`
- Buffering configurável (tamanho do chunk em ms)
- Backpressure handling (se consumer for lento, não acumula indefinidamente)
- Métricas básicas (chunks/s, latência, buffer size)
- Logging estruturado

## Pipeline Flow
```
[MicSource] ──► [AEC] ──► [Resample] ──► [Buffer 100ms] ──► IObservable<AudioChunk>
[LoopbackSource] ──────► [Resample] ──► [Buffer 100ms] ──► IObservable<AudioChunk>
```

## Critérios de Aceite
- [ ] Observable emite chunks de tamanho configurável (default 100ms)
- [ ] Subscribers podem filtrar por `AudioSourceType`
- [ ] Backpressure: buffer > 5s gera warning e dropa chunks antigos
- [ ] Métricas acessíveis via propriedades
- [ ] Todos os testes do sprint passam
- [ ] Memory: sem leak após 10 minutos de captura

## Testes
- Unit: observable emite chunks no tamanho correto
- Unit: backpressure dropa chunks antigos quando buffer cheio
- Unit: múltiplos subscribers recebem os mesmos chunks
- Integration: pipeline completo roda por 60s sem erros
- Performance: latência de captura → evento < 150ms (p99)
