# Task 4-2: System Prompt — Perfil + Contexto de Reunião

## Descrição
Construir o system prompt que define o comportamento do assistente, incorporando o perfil do usuário e o contexto específico da reunião.

## Skills
- `claude-api` — melhores práticas de prompting
- `simplify` — manter prompt conciso e efetivo

## Dependências
- Task 4-1 (client existe), Task 3-6 (perfil configurável)

## Entregáveis
- `PromptBuilder` em `ZefaIA.LLM`
- Template de system prompt com placeholders
- Integração com perfil do Settings
- Campo de contexto de reunião (preenchido antes de cada meeting)
- Prompt testado e refinado para qualidade de sugestões

## System Prompt Template
```
Você é Zefa, uma assistente de reuniões em tempo real. Seu papel é fornecer 
sugestões discretas e úteis durante a conversa.

## Sobre o Usuário
Nome: {profile.Name}
Cargo: {profile.Role}
Expertise: {profile.Expertise}
Tom preferido: {profile.Tone}
{profile.AdditionalContext}

## Contexto desta Reunião
{meetingContext.Agenda}
{meetingContext.Objective}
{meetingContext.Participants}

## Regras
1. Sugestões devem ser CURTAS (2-3 frases no máximo)
2. Foque em: dados relevantes, contra-argumentos, pontos esquecidos, riscos
3. NÃO repita o que já foi dito
4. Se não tiver sugestão útil, responda com [SEM SUGESTÃO]
5. Adapte o idioma ao idioma da conversa
6. Priorize acionáveis sobre observações genéricas

## Formato
Responda APENAS com a sugestão, sem preâmbulos. Se relevante, use:
- 💡 para sugestão/insight
- ⚠️ para risco/cuidado
- 📊 para dado/métrica relevante
```

## Critérios de Aceite
- [ ] System prompt incorpora perfil do usuário
- [ ] Contexto de reunião é injetado corretamente
- [ ] Prompt produz sugestões curtas e relevantes
- [ ] [SEM SUGESTÃO] é retornado quando não há valor a agregar
- [ ] Prompt funciona em PT-BR e EN
- [ ] Tamanho total do system prompt < 4000 tokens (para caching eficiente)

## Testes
- Unit: PromptBuilder monta prompt correto com dados do perfil
- Unit: placeholders são substituídos
- Unit: prompt sem contexto de reunião ainda funciona
- Manual: testar com transcrição real e avaliar qualidade das sugestões
