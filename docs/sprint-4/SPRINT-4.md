# Sprint 4 — LLM Integration

## Objetivo
Integrar Claude API para gerar sugestões contextuais em tempo real, com prompt caching, triggers por silêncio e hotkey, e rendering streaming no overlay.

## Entregável
App funcional que captura áudio, transcreve, e quando acionado (silêncio ou hotkey) gera sugestão contextual via Claude que aparece no overlay com streaming text.

## Critérios de Aceite
- [ ] Claude API integrado com prompt caching
- [ ] System prompt inclui perfil + contexto da reunião
- [ ] Trigger por silêncio detecta pausa e dispara sugestão
- [ ] Trigger por hotkey funciona globalmente
- [ ] Sugestão aparece no overlay com streaming
- [ ] Latência total (trigger → primeiro token no overlay) < 2s
- [ ] Custo por reunião de 1h < US$0.50

## Tasks
| Task | Descrição | Estimativa |
|------|-----------|------------|
| 4-1 | Claude API client com prompt caching | 4h |
| 4-2 | System prompt: perfil + contexto de reunião | 3h |
| 4-3 | Silence detection trigger (VAD) | 3h |
| 4-4 | Hotkey trigger global | 2h |
| 4-5 | Streaming response no overlay | 3h |
| 4-6 | Orquestração de triggers e rate limiting | 3h |

## Dependências Externas
- Anthropic .NET SDK ou HTTP client direto
- Claude API key
- Sprint 2 (transcrição) e Sprint 3 (overlay) concluídos
