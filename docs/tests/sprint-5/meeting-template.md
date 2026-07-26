# Testes: MeetingTemplate (Fluxo de Nova Reuniao)

**Arquivo fonte:** `src/ZefaIA.Overlay/MeetingTemplate.cs`, `src/ZefaIA.Overlay/NewMeetingWindow.xaml.cs`
**Arquivo de teste:** `tests/ZefaIA.Overlay.Tests/MeetingTemplateTests.cs`
**Classe de teste:** `MeetingTemplateTests`
**Total:** 8 testes

## Motivacao

O contexto preenchido no dialogo de nova reuniao alimenta o system prompt do LLM. Templates errados ou vazios degradam a qualidade das sugestoes durante toda a reuniao. Como a `NewMeetingWindow` e uma `Window` do WPF (exige STA thread e message pump), os testes cobrem a camada de dados `MeetingTemplate`; o comportamento da janela em si e validado manualmente.

## Testes

### 1, 8. Registro de templates
- `All_ContainsFourTemplates`
- `All_OrderIsOneOnOne_Standup_Review_Custom`
- **O que testa:** A ordem de `MeetingTemplate.All` importa: os botoes no XAML usam `Tag="0"` a `Tag="3"` como indice nessa lista. Se a ordem mudar sem atualizar o XAML, o botao "Standup" passa a aplicar o template errado — este teste trava o contrato.
- **Execucao:** `dotnet test --filter "MeetingTemplateTests.All"`

### 2-4. Conteudo dos templates predefinidos
- `OneOnOne_HasCorrectName`
- `Standup_HasCorrectName`
- `Review_HasCorrectName`
- **O que testa:** Cada template tem nome correto e agenda/objetivo nao vazios — um template que so preenche o nome nao ajuda o LLM
- **Execucao:** `dotnet test --filter "MeetingTemplateTests.OneOnOne|MeetingTemplateTests.Standup|MeetingTemplateTests.Review"`

### 5. Template livre
- `Custom_HasEmptyFields`
- **O que testa:** "Custom" limpa os campos em vez de manter o texto do template anterior — permite recomecar do zero apos clicar em outro template por engano
- **Execucao:** `dotnet test --filter "MeetingTemplateTests.Custom"`

### 6-7. Titulo padrao
- `GenerateDefaultTitle_ContainsDateAndTime`
- `GenerateDefaultTitle_CalledTwiceQuickly_ReturnsSameMinute`
- **O que testa:** Quando o usuario nao informa titulo, o formato e `Reuniao yyyy-MM-dd HH:mm`. A precisao e de minuto (nao segundo), o que torna o valor estavel entre a construcao do resultado e a gravacao no banco.
- **Execucao:** `dotnet test --filter "MeetingTemplateTests.GenerateDefaultTitle"`

## Cobertura manual

O "Inicio Rapido" (`BuildResult(isQuickStart: true)`) descarta agenda, objetivo e participantes mesmo se preenchidos, mantendo apenas o titulo. Esse comportamento e verificado manualmente: abrir o dialogo, preencher a agenda, clicar em "Inicio Rapido" e confirmar que a sessao criada no historico tem agenda vazia.
