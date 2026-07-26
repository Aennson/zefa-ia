# Zefa IA

Assistente de reunioes em tempo real para Windows. Captura audio dual (microfone + loopback do sistema), transcreve fala ao vivo, e gera sugestoes contextuais via LLM — exibidas em overlay invisivel para compartilhamento de tela.

## Visao Geral

```
[Microfone] ──► Audio Capture ──► STT Provider ──► Transcription Engine ──► LLM Client ──► Overlay
[Loopback]  ──►   (NAudio)    ──► (ISTTProvider) ──►   (texto diarizado)  ──►  (Claude)  ──►  (WPF)
                                                                                    ▲
                                                                          [Perfil + Contexto]
```

**Como funciona:**

1. **Captura dual** — Microfone via NAudio `WaveInEvent` + sistema via WASAPI `WasapiLoopbackCapture`
2. **Echo cancellation** — Filtro adaptativo NLMS remove eco do loopback que vaza no microfone
3. **Transcricao** — Whisper local (whisper.net) ou ElevenLabs Scribe v2 (WebSocket), plugaveis via interface
4. **Diarizacao** — Mic = "Eu", Loopback = "Interlocutor" (sem ML, baseado na origem do stream)
5. **Sugestoes** — Claude API com prompt caching, trigger por silencio (~1.5s) ou hotkey
6. **Overlay** — WPF topmost com click-through, excluido de captura de tela (`WDA_EXCLUDEFROMCAPTURE`)

## Requisitos

### Sistema

- Windows 10 1903+ (necessario para WASAPI loopback e `SetWindowDisplayAffinity`)
- .NET 8 SDK ([download](https://dotnet.microsoft.com/download/dotnet/8.0))

### API Keys (opcionais)

| Servico | Variavel de Ambiente | Quando |
|---------|---------------------|--------|
| Claude (Anthropic) | `ANTHROPIC_API_KEY` | Sugestoes via LLM |
| ElevenLabs | `ELEVENLABS_API_KEY` | STT alternativo ao Whisper local |

> O Whisper local funciona **sem nenhuma API key** — roda 100% offline.
> Sem `ANTHROPIC_API_KEY` o app inicia normalmente em modo so-transcricao:
> grava, salva no historico e exporta; apenas as sugestoes ficam desligadas.

## Uso

Guia completo em **[`docs/USAGE.md`](docs/USAGE.md)** — instalacao, configuracao
de perfil, atalhos, escolha de provedor de STT e troubleshooting.

O app nao tem janela principal: apos iniciar, ele vive no system tray.
Botao direito no icone → Nova Reuniao, Historico, Configuracoes.

## Build

```bash
# Clonar
git clone https://github.com/aennson/zefa-ia.git
cd zefa-ia

# Compilar
dotnet build

# Rodar testes (457 testes, os de hardware sao Skip automaticamente)
dotnet test

# Executar
dotnet run --project src/ZefaIA.App

# Gerar o instalador (requer Inno Setup 6)
pwsh installer/build-installer.ps1
```

### Modelo Whisper

Na primeira execucao com STT Whisper, o modelo sera baixado automaticamente (~142MB para `base`).
O download acontece em `./models/` e pode ser configurado em `appsettings.json`.

Modelos disponiveis:

| Modelo | Tamanho | Qualidade | Velocidade |
|--------|---------|-----------|------------|
| tiny | ~75 MB | Basica | Muito rapida |
| base | ~142 MB | Boa | Rapida |
| small | ~466 MB | Muito boa | Moderada |
| medium | ~1.5 GB | Excelente | Lenta |

## Estrutura do Projeto

```
zefa-ia/
├── ZefaIA.sln
├── src/
│   ├── ZefaIA.Core/           # Interfaces, models, eventos (domain)
│   ├── ZefaIA.Audio/          # Captura NAudio, resampling, AEC, pipeline Rx
│   ├── ZefaIA.STT/            # Providers de STT (Whisper, ElevenLabs)
│   ├── ZefaIA.LLM/            # Cliente LLM (Claude API)
│   ├── ZefaIA.Overlay/        # Janela overlay WPF
│   ├── ZefaIA.Persistence/    # SQLite, recorder de reuniao, exportacao
│   └── ZefaIA.App/            # Orquestracao, system tray, bootstrap, crash reports
├── tests/
│   ├── ZefaIA.Audio.Tests/       # 44 testes de audio
│   ├── ZefaIA.STT.Tests/         # 82 testes de STT e deteccao de idioma
│   ├── ZefaIA.Overlay.Tests/     # 44 testes de overlay, settings e historico
│   ├── ZefaIA.LLM.Tests/         # 82 testes de LLM, triggers e orquestracao
│   ├── ZefaIA.Persistence.Tests/ # 73 testes de SQLite, recorder e export
│   ├── ZefaIA.App.Tests/         # 132 testes de orquestracao e resiliencia
│   └── ZefaIA.Integration.Tests/
├── installer/                 # Script Inno Setup e build do instalador
└── docs/
    ├── PROJECT-SPEC.md        # Especificacao completa
    ├── USAGE.md               # Guia de uso e troubleshooting
    ├── sprint-1/ a sprint-6/  # Plano de sprints e tasks
    └── tests/                 # Documentacao de cada teste
```

## Configuracao

Edite `src/ZefaIA.App/appsettings.json`:

```json
{
  "Audio": {
    "BufferSizeMs": 100,
    "SampleRate": 16000,
    "Channels": 1
  },
  "STT": {
    "ActiveProvider": "WhisperLocal",
    "WhisperLocal": {
      "ModelSize": "base",
      "Language": "auto",
      "UseGPU": false,
      "ModelPath": "./models"
    },
    "ElevenLabs": {
      "ApiKeyEnvVar": "ELEVENLABS_API_KEY",
      "Language": "auto"
    }
  },
  "LLM": {
    "ModelId": "claude-sonnet-4-20250514",
    "MaxTokens": 512,
    "Temperature": 0.7
  },
  "Triggers": {
    "SilenceThresholdMs": 1500,
    "CooldownMs": 10000,
    "MaxRequestsPerMinute": 4
  },
  "Overlay": {
    "Opacity": 0.85,
    "FontSize": 14,
    "Position": "BottomRight"
  }
}
```

### Trocar provider de STT

Altere `STT.ActiveProvider` para `"WhisperLocal"` ou `"ElevenLabs"`. O hot-swap tambem pode ser feito em runtime sem reiniciar o app.

## Dados das Reunioes

Transcricoes e sugestoes ficam em um banco SQLite local:

```
%APPDATA%\ZefaIA\meetings.db
```

Nada e enviado para servidor proprio — as unicas chamadas externas sao para a API do Claude (sugestoes) e, se configurado, ElevenLabs (STT). O banco guarda tres tabelas: `MeetingSessions`, `TranscriptionEntries` e `SuggestionEntries`, as duas ultimas com `ON DELETE CASCADE`.

Para exercer o direito de exclusao (LGPD): a tela de historico tem botao "Deletar" com confirmacao, que remove a sessao e todos os dados associados. Deletar o arquivo `meetings.db` apaga todo o historico de uma vez.

Antes de deletar, e possivel exportar a reuniao em TXT (transcricao legivel com sugestoes inline) ou JSON (dados estruturados completos) pela tela de historico.

## Tecnologias

| Componente | Tecnologia | Versao |
|------------|-----------|--------|
| Runtime | .NET 8 | net8.0-windows |
| UI | WPF | - |
| Audio | NAudio | 2.2.1 |
| Streams reativos | System.Reactive | 6.0.1 |
| STT local | Whisper.net | 1.7.3 |
| STT cloud | ElevenLabs Scribe v2 | WebSocket |
| LLM | Claude API (Anthropic) | - |
| Testes | xUnit + Moq | 2.9.2 / 4.20.72 |
| Persistencia | Microsoft.Data.Sqlite | 8.0.8 |

## Roadmap (Sprints)

| Sprint | Tema | Status |
|--------|------|--------|
| 1 | Captura de Audio | Concluido |
| 2 | Speech-to-Text | Concluido |
| 3 | Overlay WPF | Concluido |
| 4 | Integracao LLM | Concluido |
| 5 | Persistencia e Config | Concluido |
| 6 | Integracao e Polish | Concluido |

### Sprint 1 — Captura de Audio
Captura dual (mic + loopback), resampling automatico (48kHz float32 stereo → 16kHz int16 mono), echo cancellation com filtro NLMS, pipeline reativo com System.Reactive, e exportacao WAV para debug.

### Sprint 2 — Speech-to-Text
Whisper local via whisper.net com buffer de audio e VAD, ElevenLabs via WebSocket com reconnection automatica, TranscriptionEngine conectando streams ao STT, diarizacao por stream (mic="Eu", loopback="Interlocutor"), factory com hot-swap de providers.

### Sprint 3 — Overlay WPF
Janela topmost click-through com `WS_EX_TRANSPARENT`/`WS_EX_LAYERED`, excluida de captura via `SetWindowDisplayAffinity`, mini controles (copiar, fixar, dispensar) com hit-testing seletivo, display de transcricao live com ObservableCollection, area de sugestoes com streaming de markdown, SimpleMarkdownParser (bold/italic/listas → WPF Inlines), Settings UI completa (STT, perfil, overlay), AppSettings com persistencia JSON.

### Sprint 4 — Integracao LLM
ClaudeLLMClient com SSE streaming e prompt caching (`cache_control` ephemeral), retry com backoff em 429/500, PromptBuilder com perfil do usuario e contexto de reuniao, SilenceTrigger com deteccao RMS e cooldown, HotkeyTrigger global via Win32 `RegisterHotKey`, SuggestionStreamPipeline com maquina de estados e filtragem de `[SEM SUGESTAO]`, SuggestionOrchestrator com rate limiting e deduplicacao.

### Sprint 5 — Persistencia e Config
Persistencia SQLite via ADO.NET puro (`Microsoft.Data.Sqlite`, sem EF Core) com tres tabelas e cascade delete garantido por `Foreign Keys=True`, MeetingRecorder que persiste transcricoes em batch durante a reuniao e faz flush no encerramento, dialogo de nova reuniao com templates (1:1, Standup, Review, Custom) e inicio rapido, tela de historico com busca full-text na transcricao e exclusao com confirmacao, LanguageDetector que agrega o idioma dos segmentos e adapta os labels de speaker, exportacao TXT (com sugestoes inline) e JSON.

### Sprint 6 — Integracao e Polish
MeetingOrchestrator montando o grafo audio -> STT -> LLM -> overlay -> persistencia por reuniao, StageRunner com startup fail-fast + rollback e shutdown best-effort (uma stage que falha nao impede o flush da persistencia), app tray-only com estado visual, degradacao para modo so-transcricao quando falta a API key, RetryPolicy com backoff e jitter, HealthTracker por componente, scrubber de segredos e crash reports locais, instrumentacao de latencia por estagio com percentis, installer Inno Setup self-contained e guia de uso.

> **Validacao pendente:** o codigo do Sprint 6 nao foi compilado nem executado — o projeto e Windows-only e o ambiente de desenvolvimento usado nao tem o SDK .NET. Faltam os numeros reais do benchmark de latencia (Task 6-4) e a verificacao do instalador em VM limpa (Task 6-5). Checklist em [`installer/README.md`](installer/README.md).

## Testes

```bash
# Rodar todos os testes
dotnet test

# Rodar testes de um modulo
dotnet test tests/ZefaIA.Audio.Tests
dotnet test tests/ZefaIA.STT.Tests

# Rodar teste especifico
dotnet test --filter "EchoCancellerTests.Process_WithMatchingReference_ReducesEcho"
```

Testes que requerem hardware de audio ou API keys sao marcados com `[Fact(Skip = "...")]` e nao executam em CI.

| Projeto | Testes | Cobertura |
|---------|--------|-----------|
| ZefaIA.Audio.Tests | 44 | Resampler, captura, AEC, pipeline, WAV |
| ZefaIA.STT.Tests | 82 | Factory, Whisper, ElevenLabs, engine, timeline, config, deteccao de idioma |
| ZefaIA.Overlay.Tests | 44 | Models, NativeMethods, controller, markdown parser, AppSettings, templates, historico |
| ZefaIA.LLM.Tests | 82 | Claude client, prompt builder, triggers, pipeline, orchestrator |
| ZefaIA.Persistence.Tests | 73 | Repositorio SQLite, cascade delete, recorder, exportacao TXT/JSON |
| ZefaIA.App.Tests | 132 | Stage runner, bootstrap, tray, retry, health, scrubber, latencia |
| **Total** | **457** | |

> Os testes nunca foram executados neste repositorio — o alvo e `net8.0-windows` e
> nao havia SDK .NET no ambiente de desenvolvimento. Os numeros acima contam
> `[Fact]` e casos de `[InlineData]`, nao execucoes. Rode `dotnet test` no Windows
> antes de confiar neles.

## Decisoes Tecnicas

- **Local-first** — Todo processamento local exceto chamadas de API (ElevenLabs STT e Claude LLM)
- **Diarizacao sem ML** — Separacao de speakers pela origem do stream (mic vs loopback), sem modelos extras
- **Echo cancellation por software** — Filtro NLMS ao inves de depender de AEC do driver de audio
- **Provider abstraction** — `ISTTProvider` permite trocar Whisper por ElevenLabs (ou qualquer outro) via config
- **Reactive streams** — System.Reactive para buffering, backpressure e composicao de streams de audio
- **LGPD** — Dados de reuniao sao locais e por sessao; o usuario decide se salva

## Licenca

Projeto privado. Todos os direitos reservados.

## Autor

**Aennson** (aennson@gmail.com)
Co-autoria: Claude (AI assistant)
