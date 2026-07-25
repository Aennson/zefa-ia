# Task 4-6: Orquestração de Triggers e Rate Limiting

## Descrição
Coordenar múltiplos triggers (silêncio + hotkey), aplicar rate limiting para controlar custos, e gerenciar a janela de transcrição enviada ao LLM.

## Skills
- `simplify` — manter orquestração clara
- `security-review` — revisão final do sprint 4

## Dependências
- Tasks 4-3, 4-4, 4-5 concluídas

## Entregáveis
- `SuggestionOrchestrator` em `ZefaIA.Core`
- Rate limiter: máximo N requests por minuto (configurável, default 4)
- Transcript window: envia últimos N segundos/segmentos ao LLM
- Deduplicação: não enviar transcrição idêntica duas vezes
- Prioridade: hotkey > silêncio (hotkey ignora cooldown)
- Métricas: requests/min, custo estimado, cache hit rate

## Transcript Window Strategy
```
Último trigger: 10:35:00
Transcript window: últimos 60s de transcrição

Envio ao LLM:
[10:34:05] [Interlocutor] E sobre o orçamento?
[10:34:12] [Eu] Estamos dentro do previsto.
[10:34:20] [Interlocutor] Mas o fornecedor aumentou 15%.
[10:34:35] [Eu] Precisamos renegociar...
[10:34:50] -- silêncio de 1.5s -- TRIGGER
```

## Critérios de Aceite
- [ ] Rate limiter respeita máximo configurado
- [ ] Hotkey bypassa cooldown mas não rate limit
- [ ] Transcript window envia contexto suficiente (configurável)
- [ ] Transcrição idêntica não é enviada duas vezes
- [ ] Métricas de custo são atualizadas após cada request
- [ ] Sprint 4 completo: fluxo trigger → transcrição → LLM → overlay funciona end-to-end

## Testes
- Unit: rate limiter bloqueia após N requests/min
- Unit: hotkey bypassa cooldown
- Unit: transcript window captura segmentos corretos
- Unit: deduplicação funciona
- Integration: fluxo completo trigger → sugestão no overlay
