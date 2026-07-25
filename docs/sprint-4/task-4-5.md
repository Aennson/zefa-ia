# Task 4-5: Streaming Response no Overlay

## Descrição
Conectar o streaming do Claude API ao overlay, renderizando a sugestão caractere por caractere conforme chega.

## Skills
- `simplify` — manter pipeline de rendering simples

## Dependências
- Task 4-1 (client streaming), Task 3-5 (área de sugestões)

## Entregáveis
- Pipeline: LLM stream → UI thread → overlay rendering
- Dispatch para UI thread via `Dispatcher.InvokeAsync`
- Animação suave de texto aparecendo
- Tratamento de [SEM SUGESTÃO] (não exibir no overlay)
- Indicadores de estado: pensando → streaming → completo

## Pipeline
```
TriggerEvent → Collect recent transcript → ClaudeLLMClient.GetSuggestionStreamAsync()
    → foreach token: Dispatcher.InvokeAsync → SuggestionPanel.AppendText(token)
    → OnComplete: SuggestionPanel.MarkComplete()
```

## Critérios de Aceite
- [ ] Texto aparece incrementalmente no overlay (não espera resposta completa)
- [ ] Primeiro token aparece em < 2s após trigger
- [ ] [SEM SUGESTÃO] é filtrado (não exibido)
- [ ] Estado visual muda: pensando → streaming → completo
- [ ] Múltiplos triggers enfileiram (não sobrescrevem sugestão em streaming)
- [ ] Erro na API mostra mensagem no overlay, não crasha

## Testes
- Unit: tokens são dispatched para UI thread
- Unit: [SEM SUGESTÃO] é filtrado
- Unit: estados transicionam corretamente
- Manual: ver sugestão aparecer com streaming no overlay
- Performance: latência trigger → primeiro token visível
