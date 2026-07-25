# Task 3-5: Área de Sugestões do LLM

## Descrição
Preparar a área do overlay que exibirá sugestões do LLM (Sprint 4). Layout separado da transcrição, com rendering de streaming text.

## Skills
- `artifact-design` — design da área de sugestões
- `simplify` — manter separação clara transcrição vs sugestão

## Dependências
- Task 3-4 concluída (transcrição no overlay)

## Entregáveis
- Área de sugestão separada da transcrição (abaixo ou em tab)
- Rendering de texto streaming (caractere por caractere, como ChatGPT)
- Indicador de "pensando..." enquanto LLM processa
- Suporte a markdown básico (bold, itálico, listas)
- Histórico de sugestões anteriores (scroll up)

## Layout
```
┌─────────────────────────────────────┐
│ [≡]  Zefa IA          [📌] [📋] [✕]│
├─────────────────────────────────────┤
│ [Transcrição]  [Sugestões]          │  ← tabs
├─────────────────────────────────────┤
│                                     │
│ 💡 Sugestão:                        │
│ Considere mencionar que o prazo     │
│ de backend depende da API do        │
│ parceiro estar pronta até dia 15... │  ← streaming render
│                                     │
│ ────────────────────────────        │
│ 💡 Anterior:                        │
│ O orçamento mencionado está 20%     │
│ acima do último quarter...          │
│                                     │
└─────────────────────────────────────┘
```

## Critérios de Aceite
- [ ] Área de sugestão é visualmente distinta da transcrição
- [ ] Texto streaming renderiza caractere por caractere
- [ ] Indicador "pensando" aparece durante processamento
- [ ] Markdown básico renderiza (bold, itálico)
- [ ] Sugestões anteriores ficam no histórico
- [ ] Copiar funciona na sugestão ativa

## Testes
- Unit: rendering de streaming text funciona com delays variados
- Unit: markdown parser converte bold/itálico/listas
- Manual: simular sugestão longa com streaming text
- Manual: verificar que tab switching funciona
