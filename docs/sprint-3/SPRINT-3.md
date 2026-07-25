# Sprint 3 — Overlay UI

## Objetivo
Criar o overlay WPF click-through com mini controles, invisível ao compartilhamento de tela, que exibe transcrição ao vivo e (futuramente) sugestões do LLM.

## Entregável
Overlay funcional que exibe transcrição em tempo real, com controles de copiar/descartar/pin, excluído de screen capture.

## Critérios de Aceite
- [ ] Overlay aparece topmost sobre qualquer janela
- [ ] Click-through funciona (clicks passam para janela abaixo)
- [ ] Mini controles respondem a hover/click
- [ ] Overlay NÃO aparece em screenshots ou compartilhamento de tela
- [ ] Transcrição ao vivo aparece no overlay
- [ ] Posição e tamanho são persistidos entre sessões
- [ ] Visual limpo, legível, não intrusivo

## Tasks
| Task | Descrição | Estimativa |
|------|-----------|------------|
| 3-1 | Overlay window base (topmost, transparente, click-through) | 3h |
| 3-2 | SetWindowDisplayAffinity (excluir de capture) | 2h |
| 3-3 | Mini controles (copiar, descartar, pin, drag) | 4h |
| 3-4 | Display de transcrição live no overlay | 3h |
| 3-5 | Área de sugestões do LLM (preparação) | 3h |
| 3-6 | Settings UI básica (provider, perfil, atalhos) | 4h |

## Dependências Externas
- WPF (.NET 8)
- Win32 interop (SetWindowLong, SetWindowDisplayAffinity)
