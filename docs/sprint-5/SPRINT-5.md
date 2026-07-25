# Sprint 5 — Persistence & Configuration

## Objetivo
Persistir sessões de reunião localmente (SQLite), implementar fluxo de início de reunião com contexto, e consolidar todas as configurações.

## Entregável
App que salva transcrições + sugestões por reunião, permite revisar sessões anteriores, e tem fluxo completo de setup pré-reunião.

## Critérios de Aceite
- [ ] Reuniões são salvas localmente em SQLite
- [ ] Usuário pode revisar transcrição e sugestões de reuniões anteriores
- [ ] Fluxo de início de reunião solicita contexto/agenda
- [ ] Configurações são persistidas e aplicadas corretamente
- [ ] Dados podem ser deletados pelo usuário (LGPD)
- [ ] Auto-detect de idioma funciona

## Tasks
| Task | Descrição | Estimativa |
|------|-----------|------------|
| 5-1 | Modelo de dados e SQLite setup | 3h |
| 5-2 | Salvar/carregar transcrição e sugestões | 3h |
| 5-3 | Fluxo de início de reunião (contexto/agenda) | 3h |
| 5-4 | Tela de histórico de reuniões | 3h |
| 5-5 | Detecção automática de idioma | 2h |
| 5-6 | Exportação de sessão (TXT/JSON) | 2h |

## Dependências Externas
- Microsoft.Data.Sqlite (NuGet)
- Entity Framework Core SQLite (ou Dapper)
