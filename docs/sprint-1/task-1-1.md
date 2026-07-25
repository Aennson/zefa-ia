# Task 1-1: Setup do Projeto

## Descrição
Criar a estrutura da solution .NET 8, projetos, dependências base, e configuração inicial.

## Skills
- `init` — inicializar CLAUDE.md com documentação do codebase
- `update-config` — configurar settings do Claude Code para o projeto

## Entregáveis
- Solution `ZefaIA.sln` com todos os projetos criados
- Dependências NuGet instaladas (NAudio, etc.)
- CLAUDE.md na raiz com contexto do projeto
- `.editorconfig` com padrões do projeto
- Build funcional (`dotnet build` sem erros)

## Estrutura a Criar
```
ZefaIA/
├── ZefaIA.sln
├── src/
│   ├── ZefaIA.Core/           # Interfaces, models, events
│   ├── ZefaIA.Audio/          # NAudio capture
│   ├── ZefaIA.STT/            # STT providers
│   ├── ZefaIA.LLM/            # LLM client
│   ├── ZefaIA.Overlay/        # WPF overlay
│   ├── ZefaIA.Persistence/    # SQLite
│   └── ZefaIA.App/            # Main app (WPF)
├── tests/
│   ├── ZefaIA.Audio.Tests/
│   ├── ZefaIA.STT.Tests/
│   ├── ZefaIA.LLM.Tests/
│   └── ZefaIA.Integration.Tests/
└── docs/
```

## Dependências NuGet
- `NAudio` (≥ 2.2) — audio capture
- `Microsoft.Extensions.DependencyInjection` — DI
- `Microsoft.Extensions.Configuration` — config
- `Microsoft.Extensions.Logging` — logging
- `System.Reactive` — reactive extensions para event streams
- `xunit` + `xunit.runner.visualstudio` + `Moq` — testes

## Critérios de Aceite
- [ ] `dotnet build` compila sem erros
- [ ] `dotnet test` roda (mesmo sem testes ainda)
- [ ] Estrutura de pastas conforme especificado
- [ ] CLAUDE.md criado e preciso

## Testes
- Smoke test: build compila
- Smoke test: test runner executa
