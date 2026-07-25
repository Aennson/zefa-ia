# Task 5-6: Exportação de Sessão

## Descrição
Permitir exportar transcrição e sugestões de uma reunião em formatos úteis.

## Skills
- `simplify` — manter exportação direta
- `security-review` — revisão final do sprint 5

## Dependências
- Task 5-4 (tela de histórico)

## Entregáveis
- Exportação para TXT (transcrição formatada com speakers e timestamps)
- Exportação para JSON (dados estruturados completos)
- Botão de exportar na tela de histórico
- Diálogo de "Salvar como" do Windows

## Formato TXT
```
Reunião: Sync Semanal
Data: 2026-07-25 14:00
Duração: 45 min

---

[14:00:05] [Interlocutor] Olá, vamos começar?
[14:00:08] [Eu] Vamos sim.
[14:00:15] [Interlocutor] Primeiro ponto: orçamento do Q3.

  💡 Sugestão (14:00:22): O orçamento do Q3 está 12% acima do Q2.
     Considere mencionar a meta de redução de 5%.

[14:00:25] [Eu] Sobre o orçamento...
```

## Critérios de Aceite
- [ ] TXT exporta formatado e legível
- [ ] JSON exporta dados completos e parseáveis
- [ ] Diálogo de salvar abre no local correto
- [ ] Arquivo gerado é válido e não corrompido
- [ ] Sugestões aparecem inline no TXT

## Testes
- Unit: formatação TXT produz output esperado
- Unit: JSON é deserializável
- Manual: exportar reunião e abrir arquivo
