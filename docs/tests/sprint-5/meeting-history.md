# Testes: MeetingHistoryWindow

**Arquivo fonte:** `src/ZefaIA.Overlay/MeetingHistoryWindow.xaml.cs`
**Arquivo de teste:** `tests/ZefaIA.Overlay.Tests/MeetingHistoryTests.cs`
**Classe de teste:** `MeetingHistoryTests`
**Total:** 6 testes

## Motivacao

A tela de historico e a unica forma de o usuario revisar reunioes passadas e exercer o direito de exclusao (LGPD). Os testes focam em `SessionListItem`, o view-model que traduz `MeetingSession` para o que aparece na lista — e onde moram os casos de borda de formatacao. A janela WPF em si (busca, delete com confirmacao, painel de detalhe) e validada manualmente por exigir STA thread.

## Testes

### 1. Mapeamento completo
- `SessionListItem_From_MapsAllFields`
- **O que testa:** Id, titulo, data no formato `dd/MM/yyyy HH:mm`, duracao e participantes fazem a traducao correta
- **Execucao:** `dotnet test --filter "MeetingHistoryTests.SessionListItem_From_MapsAllFields"`

### 2-3. Titulo ausente
- `SessionListItem_From_EmptyTitle_ShowsFallback`
- `SessionListItem_From_WhitespaceTitle_ShowsFallback`
- **O que testa:** Sessao sem titulo aparece como `Reuniao #<id>` em vez de uma linha em branco na lista. O caso de whitespace existe porque `string.IsNullOrWhiteSpace` e usado no lugar de `IsNullOrEmpty` — um titulo com espacos vem do usuario apertando espaco no campo e saindo.
- **Execucao:** `dotnet test --filter "MeetingHistoryTests.SessionListItem_From_EmptyTitle|MeetingHistoryTests.SessionListItem_From_WhitespaceTitle"`

### 4. Reuniao em andamento
- `SessionListItem_From_NoEndedAt_ShowsEmAndamento`
- **O que testa:** Sem `EndedAt`, `Duration` e `TimeSpan.Zero` e a UI mostra "Em andamento" em vez de "0min" — distingue uma reuniao ativa de uma que terminou instantaneamente
- **Execucao:** `dotnet test --filter "MeetingHistoryTests.SessionListItem_From_NoEndedAt"`

### 5. Participantes ausentes
- `SessionListItem_From_NoParticipants_ShowsDash`
- **O que testa:** Campo vazio vira "-" no cabecalho de detalhe
- **Execucao:** `dotnet test --filter "MeetingHistoryTests.SessionListItem_From_NoParticipants"`

### 6. Duracao longa
- `SessionListItem_From_LongDuration_ShowsMinutes`
- **O que testa:** Reuniao de 2h15 mostra "135min" — a lista usa minutos totais para manter a coluna estreita; o formato com horas fica no export TXT
- **Execucao:** `dotnet test --filter "MeetingHistoryTests.SessionListItem_From_LongDuration"`

## Cobertura manual

Verificados manualmente por dependerem da janela WPF:
- Busca incremental no `TextChanged` — texto vazio restaura a lista completa a partir do cache `_allSessions` sem ir ao banco
- Painel de detalhe intercala transcricoes e sugestoes por `Timestamp`, com sugestoes em bloco destacado
- Delete pede confirmacao via `MessageBox` e, ao confirmar, remove a sessao do banco (com cascade), do cache e da lista
- Botoes de export TXT/JSON abrem o dialogo "Salvar como" do Windows
