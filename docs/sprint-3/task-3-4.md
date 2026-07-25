# Task 3-4: Display de Transcrição Live

## Descrição
Exibir transcrição em tempo real no overlay, com diferenciação visual de speakers e scroll automático.

## Skills
- `artifact-design` — design do layout de transcrição
- `simplify` — manter rendering eficiente

## Dependências
- Task 3-1 (overlay), Task 2-3 (pipeline de transcrição)

## Entregáveis
- Área de transcrição scrollável no overlay
- Diferenciação visual: "Eu" (cor A) vs "Interlocutor" (cor B)
- Segmentos parciais em itálico/opaco, finais em normal
- Auto-scroll para último segmento
- Limite de segmentos visíveis (últimos N, para performance)
- Timestamps opcionais

## Layout
```
┌─────────────────────────────────────┐
│ [≡]  Zefa IA          [📌] [📋] [✕]│
├─────────────────────────────────────┤
│ 10:32 [Interlocutor]                │
│ Então o que acham do cronograma?    │
│                                     │
│ 10:32 [Eu]                          │
│ Acho viável, mas precisamos         │
│ validar com o time de backend...    │  ← parcial (opaco)
│                                     │
└─────────────────────────────────────┘
```

## Critérios de Aceite
- [ ] Transcrição aparece em tempo real no overlay
- [ ] Speakers diferenciados por cor
- [ ] Parciais aparecem em estilo diferente de finais
- [ ] Auto-scroll funciona
- [ ] Performance: 100+ segmentos sem lag visual
- [ ] Fonte legível em diferentes resoluções de tela

## Testes
- Manual: falar no mic e ver transcrição aparecer
- Manual: tocar áudio e ver transcrição do loopback
- Manual: verificar diferenciação de cores
- Performance: adicionar 200 segmentos e verificar que scroll é fluido
