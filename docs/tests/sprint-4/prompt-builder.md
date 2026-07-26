# Testes: PromptBuilder

**Arquivo fonte:** `src/ZefaIA.LLM/PromptBuilder.cs`
**Arquivo de teste:** `tests/ZefaIA.LLM.Tests/PromptBuilderTests.cs`
**Classe de teste:** `PromptBuilderTests`

## Motivacao

`PromptBuilder` monta o system prompt com perfil do usuario e contexto da reuniao. Testar garante que o prompt e montado corretamente e fica dentro do limite de tokens.

## Testes

### 1. `Build_WithoutProfile_ReturnsCorePrompt`
- **Tipo:** Unit
- **O que testa:** Prompt base sem secoes de perfil/reuniao
- **Execucao:** `dotnet test --filter "PromptBuilderTests.Build_WithoutProfile_ReturnsCorePrompt"`

### 2. `Build_WithProfile_IncludesUserInfo`
- **Tipo:** Unit
- **O que testa:** Nome, cargo, expertise, tom
- **Execucao:** `dotnet test --filter "PromptBuilderTests.Build_WithProfile_IncludesUserInfo"`

### 3. `Build_WithMeetingContext_IncludesMeetingInfo`
- **Tipo:** Unit
- **O que testa:** Pauta, objetivo, participantes
- **Execucao:** `dotnet test --filter "PromptBuilderTests.Build_WithMeetingContext_IncludesMeetingInfo"`

### 4. `Build_WithProfileAndMeeting_IncludesBoth`
- **Tipo:** Unit
- **O que testa:** Ambas secoes presentes
- **Execucao:** `dotnet test --filter "PromptBuilderTests.Build_WithProfileAndMeeting_IncludesBoth"`

### 5-6. Contexto adicional e secao vazia
- **Tipo:** Unit
- **Execucao:** `dotnet test --filter "PromptBuilderTests.Build_With"`

### 7-8. Core prompt rules e emoji prefixes
- **Tipo:** Unit
- **O que testa:** Regras de sugestao e categorias com emoji
- **Execucao:** `dotnet test --filter "PromptBuilderTests.Build_CorePrompt"`

### 9. `Build_PromptUnder4000Tokens`
- **Tipo:** Unit
- **O que testa:** Prompt completo fica abaixo de 4000 tokens estimados
- **Execucao:** `dotnet test --filter "PromptBuilderTests.Build_PromptUnder4000Tokens"`

### 10. `Build_IsIdempotent`
- **Tipo:** Unit
- **O que testa:** Chamadas repetidas produzem mesmo resultado
- **Execucao:** `dotnet test --filter "PromptBuilderTests.Build_IsIdempotent"`
