# Skills Registry — Regras de Carregamento por Task

Este documento define quais skills devem ser carregadas para cada tipo de task.
**Regra imutável:** toda task DEVE carregar as skills listadas antes de iniciar a execução.

## Skills por Tipo de Atividade

### Código C# / Implementação
- `init` — na primeira task de cada sprint (setup de projeto)
- `claude-api` — quando envolver integração com Claude API
- `security-review` — ao finalizar cada sprint (revisão de segurança)
- `simplify` — após implementação, antes de fechar a task (limpeza de código)
- `review` — ao criar PR de sprint

### Documentação
- `artifact-design` — para diagramas e visualizações
- `dataviz` — para dashboards de métricas/performance

### Testes
- `run` — para executar e validar o app
- `simplify` — para garantir testes limpos

### DevOps / Config
- `update-config` — para configurações do Claude Code
- `session-start-hook` — para hooks de inicialização

### Revisão Final de Sprint
- `security-review` — segurança do código alterado
- `simplify` — simplificação e limpeza
- `review` — revisão geral do PR

## Regra de Ouro

> Cada task no seu arquivo .md lista suas skills obrigatórias na seção `## Skills`.
> O executor DEVE carregar essas skills via `/skill-name` antes de iniciar o trabalho.
> Não pular. Não substituir. Não postergar.
