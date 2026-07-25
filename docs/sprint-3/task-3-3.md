# Task 3-3: Mini Controles

## Descrição
Adicionar controles interativos ao overlay: copiar texto, descartar sugestão, pin (manter visível), e drag para reposicionar. A área de controles NÃO é click-through.

## Skills
- `artifact-design` — design dos controles
- `simplify` — manter UI mínima

## Dependências
- Task 3-1 concluída (overlay base)

## Entregáveis
- Botões: Copiar, Descartar, Pin/Unpin
- Área de drag para mover o overlay
- Hit-test: área de controles responde a click, resto é click-through
- Animações sutis de hover/press
- Ícones (embutidos, sem dependência externa)

## Hit-Test Strategy
```
┌─────────────────────────────────────┐
│ [≡ drag handle]     [📌] [📋] [✕] │  ← Interativo (não click-through)
├─────────────────────────────────────┤
│                                     │
│   Texto de transcrição/sugestão     │  ← Click-through
│                                     │
└─────────────────────────────────────┘
```

O truque: ao invés de `WS_EX_TRANSPARENT` na janela toda, usar `WndProc` com `WM_NCHITTEST` retornando `HTTRANSPARENT` para a área de conteúdo e `HTCLIENT` para a barra de controles.

## Comportamento dos Controles
- **Copiar (📋)**: copia o texto visível para o clipboard
- **Descartar (✕)**: esconde a sugestão/transcrição atual
- **Pin (📌)**: toggle — quando pinado, texto não some automaticamente
- **Drag (≡)**: arrastar para reposicionar o overlay

## Critérios de Aceite
- [ ] Botões aparecem na barra superior do overlay
- [ ] Copiar coloca texto no clipboard
- [ ] Descartar esconde o conteúdo atual
- [ ] Pin mantém conteúdo visível (não auto-dismiss)
- [ ] Drag permite mover o overlay
- [ ] Clicks na área de conteúdo passam para janela abaixo
- [ ] Hover mostra feedback visual nos botões

## Testes
- Manual: copiar texto e colar em notepad
- Manual: descartar esconde conteúdo
- Manual: pin mantém conteúdo após timeout
- Manual: drag move overlay
- Manual: click na área de texto passa para janela abaixo
