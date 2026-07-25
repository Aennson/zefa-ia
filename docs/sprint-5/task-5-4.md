# Task 5-4: Tela de Histórico de Reuniões

## Descrição
Criar tela para visualizar reuniões anteriores, com transcrição e sugestões, e opção de deletar.

## Skills
- `artifact-design` — design do histórico
- `simplify` — manter UI simples

## Dependências
- Tasks 5-1, 5-2 (dados persistidos)

## Entregáveis
- Tela de lista de reuniões (WPF Window)
- Detalhes de reunião: transcrição completa + sugestões
- Busca por texto na transcrição
- Deletar reunião (com confirmação)
- Ordenação por data

## Critérios de Aceite
- [ ] Lista mostra reuniões com título, data, duração
- [ ] Click abre transcrição completa
- [ ] Sugestões aparecem inline na transcrição
- [ ] Busca encontra texto na transcrição
- [ ] Delete remove sessão e todos os dados associados
- [ ] Confirmação antes de deletar

## Testes
- Manual: abrir histórico, navegar reuniões, deletar uma
- Unit: busca encontra texto correto
- Unit: delete cascade funciona
