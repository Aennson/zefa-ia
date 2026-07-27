# Zefa IA — Documentacao de Testes

## Estrutura

```
docs/tests/
├── README.md              # Este arquivo
├── sprint-1/
│   ├── audio-models.md
│   ├── resampler.md
│   ├── microphone-source.md
│   ├── loopback-source.md
│   ├── audio-device-enumerator.md
│   ├── audio-capture-engine.md
│   ├── echo-canceller.md
│   ├── audio-pipeline.md
│   ├── wav-exporter.md
│   ├── transcription-models.md
│   └── llm-models.md
├── sprint-2/
├── sprint-3/
...
```

## Convencoes

- Um arquivo `.md` por classe/componente testado
- Cada arquivo lista todos os testes, seu motivo, como sao executados, e o que validam
- Testes marcados com `Skip` indicam dependencia de hardware (Windows audio device)
- Framework: **xUnit** com **Moq** para mocking

## Estado atual da suite

**525 testes: 519 passando, 6 opt-in.** Ultima execucao completa em Windows 11 x64.

| Projeto | Testes |
|---|---:|
| ZefaIA.App.Tests | 132 |
| ZefaIA.STT.Tests | 90 |
| ZefaIA.LLM.Tests | 88 |
| ZefaIA.Overlay.Tests | 73 |
| ZefaIA.Persistence.Tests | 73 |
| ZefaIA.Audio.Tests | 44 |
| ZefaIA.Integration.Tests | 25 |

## Testes ponta a ponta (E2E)

Ver [`E2E-COVERAGE.md`](E2E-COVERAGE.md) — 25 testes em `tests/ZefaIA.Integration.Tests`
dirigem o `MeetingOrchestrator` de producao (audio -> STT -> trigger -> LLM -> overlay
-> SQLite -> export), com apenas as fronteiras de processo substituidas.

Esse documento tambem registra a **lacuna de integracao real com a API da Anthropic**,
adiada para uma etapa propria, com os 6 problemas ja identificados por inspecao.

## Ultima execucao real

Ver [`WINDOWS-TEST-RUN.md`](WINDOWS-TEST-RUN.md) — primeira execucao em Windows real
(2026-07-26): **454 passando, 0 falhando, 3 opt-in**. Documenta os 22 defeitos
encontrados e corrigidos, incluindo os de produto (lock do SQLite, perda de
`DateTimeKind`, vazamento do marcador `[SEM SUGESTAO]`, instalador sem checagem do
VC++ Redistributable).

## Pre-requisitos

```powershell
winget install Microsoft.DotNet.SDK.8
winget install Microsoft.VCRedist.2015+.x64   # obrigatorio para o Whisper nativo
```

## Como Executar

```powershell
# Todos os testes
dotnet test

# Testes de um projeto especifico
dotnet test tests/ZefaIA.Audio.Tests

# Incluindo o teste de integracao do Whisper (baixa ~150 MB na primeira vez)
$env:ZEFA_RUN_WHISPER_INTEGRATION = "1"; dotnet test tests/ZefaIA.STT.Tests
```

## Categorias de Teste

| Tipo | Descricao | Ambiente |
|------|-----------|----------|
| **Unit** | Testa logica isolada com mocks | Qualquer (CI/local) |
| **Integration** | Testa interacao entre componentes reais | Qualquer (CI/local) |
| **Hardware** | Requer dispositivos de audio Windows | Roda onde ha hardware; pula sozinho onde nao ha |
| **Opt-in** | Precisa de download grande ou chave de API paga | Ligado por variavel de ambiente |

### Como o skip funciona

Nao ha mais `Skip` fixo (que tornava o teste codigo morto). Dois atributos decidem
em runtime:

- `[RequiresAudioDeviceFact]` (`ZefaIA.Audio.Tests`) — sonda os endpoints WASAPI.
  Executa numa maquina com microfone/alto-falante, pula com motivo numa sem.
  Aceita `AudioEndpoint.Capture` / `AudioEndpoint.Render` para exigir so um lado.
- `[OptInFact("VAR", "motivo")]` (`ZefaIA.STT.Tests`) — executa quando a variavel de
  ambiente esta em `1`/`true`; caso contrario pula informando como liga-la.
