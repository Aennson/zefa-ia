# Task 5-3: Fluxo de Início de Reunião

## Descrição
Criar o fluxo de setup pré-reunião onde o usuário informa contexto, agenda, e objetivo antes de iniciar a captura.

## Skills
- `artifact-design` — design do fluxo
- `simplify` — manter fluxo rápido (< 30s para iniciar)

## Dependências
- Task 5-1 (modelo de sessão), Task 3-6 (settings UI)

## Entregáveis
- Diálogo de "Nova Reunião" (WPF Window)
- Campos: título (opcional), agenda, objetivo, participantes
- Templates de contexto (1:1, standup, review, custom)
- Botão "Iniciar" que cria sessão e liga captura
- Atalho rápido: iniciar sem contexto (usar defaults)

## Layout
```
┌─────────────────────────────────────────┐
│           🎙️ Nova Reunião               │
├─────────────────────────────────────────┤
│ Título: [                            ]  │
│                                         │
│ Template: [1:1 ▼] [Standup] [Review]    │
│                                         │
│ Agenda/Contexto:                        │
│ [                                    ]  │
│ [                                    ]  │
│                                         │
│ Objetivo:                               │
│ [                                    ]  │
│                                         │
│ [Iniciar Reunião]    [Início Rápido]    │
└─────────────────────────────────────────┘
```

## Critérios de Aceite
- [ ] Diálogo abre antes de iniciar captura
- [ ] Templates preenchem campos automaticamente
- [ ] "Início Rápido" pula o diálogo
- [ ] Contexto é passado para o system prompt do LLM
- [ ] Sessão é criada no SQLite ao iniciar
- [ ] Título default: data + hora se não informado

## Testes
- Manual: preencher formulário e verificar que contexto chega ao LLM
- Manual: início rápido funciona sem preencher nada
- Unit: templates preenchem campos corretamente
- Unit: sessão é criada com dados do formulário
