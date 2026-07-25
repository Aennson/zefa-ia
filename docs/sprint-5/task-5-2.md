# Task 5-2: Salvar/Carregar Transcrição e Sugestões

## Descrição
Integrar o pipeline de transcrição e sugestões com a persistência SQLite. Salvar em tempo real durante a reunião.

## Skills
- `simplify` — manter integração direta

## Dependências
- Task 5-1 (repositório existe), Tasks 2-3, 4-6 (pipelines funcionam)

## Entregáveis
- `MeetingRecorder` que subscribe nos pipelines e persiste
- Batch insert (a cada 5 segmentos ou 5s, o que vier primeiro)
- Flush no encerramento da reunião
- Indicador visual de "gravando" no overlay

## Critérios de Aceite
- [ ] Transcrições são salvas durante a reunião
- [ ] Sugestões são salvas com contexto que as gerou
- [ ] Batch insert não causa pausa perceptível
- [ ] Flush final garante que nada se perde ao encerrar
- [ ] Indicador de gravação é visível no overlay
- [ ] Performance: insert não adiciona latência ao pipeline

## Testes
- Unit: batch insert dispara no threshold correto
- Unit: flush salva buffer restante
- Integration: 60s de reunião simulada gera entries no DB
