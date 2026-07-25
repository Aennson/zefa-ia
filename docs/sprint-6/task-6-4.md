# Task 6-4: Performance Profiling e Otimização

## Descrição
Medir e otimizar latência end-to-end para atingir o target de < 2s. Identificar e eliminar gargalos.

## Skills
- `simplify` — eliminar overhead desnecessário
- `dataviz` — visualizar métricas de performance

## Dependências
- Task 6-1 (app integrado e funcional)

## Entregáveis
- Dashboard de métricas de performance (console ou overlay debug)
- Profiling de cada estágio do pipeline
- Otimizações identificadas e aplicadas
- Benchmark documentado

## Métricas a Medir
| Estágio | Target | Medição |
|---------|--------|---------|
| Audio capture → buffer | < 100ms | Stopwatch |
| Buffer → STT start | < 50ms | Stopwatch |
| STT processing | < 500ms | Stopwatch |
| Trigger detection | < 200ms | Stopwatch |
| LLM request → first token | < 1000ms | Stopwatch |
| Token → overlay render | < 50ms | Stopwatch |
| **Total end-to-end** | **< 2000ms** | Sum |

## Otimizações Potenciais
- Buffer size tuning (menor = menos latência, mais CPU)
- Whisper: modelo menor para frases curtas
- Claude: model selection (Haiku para velocidade vs Sonnet para qualidade)
- Overlay: virtualização de lista para muitos segmentos
- Memory: object pooling para AudioChunks
- GC: configurar Server GC para .NET

## Critérios de Aceite
- [ ] Latência end-to-end < 2s (p95)
- [ ] Sem memory leak após 2h
- [ ] CPU < 15% idle (apenas captura, sem STT ativo)
- [ ] CPU < 40% durante transcrição ativa
- [ ] Benchmark documentado com números reais

## Testes
- Performance: benchmark de cada estágio
- Stress: 2h de uso contínuo — verificar memory e CPU
- Comparison: latência com Whisper tiny vs base
