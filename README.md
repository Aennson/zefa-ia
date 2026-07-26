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
| Claude (Anthropic) | `ANTHROPIC_API_KEY` | Sugestoes via LLM (Sprint 4+) |
| ElevenLabs | `ELEVENLABS_API_KEY` | STT alternativo ao Whisper local |

> O Whisper local funciona **sem nenhuma API key** — roda 100% offline.

## Build

```bash
# Clonar
git clone https://github.com/aennson/zefa-ia.git
cd zefa-ia

# Compilar
dotnet build

# Rodar testes (140+ testes, os de hardware sao Skip automaticamente)
dotnet test

# Executar
dotnet run --project src/ZefaIA.App
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
│   ├── ZefaIA.Persistence/    # Armazenamento SQLite
│   └── ZefaIA.App/            # Aplicacao WPF principal, DI, config
├── tests/
│   ├── ZefaIA.Audio.Tests/    # 49 testes de audio
│   ├── ZefaIA.STT.Tests/      # 62 testes de STT
│   ├── ZefaIA.Overlay.Tests/  # 30 testes de overlay e settings
│   ├── ZefaIA.LLM.Tests/      # Testes do LLM
│   └── ZefaIA.Integration.Tests/
└── docs/
    ├── PROJECT-SPEC.md        # Especificacao completa
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
| Persistencia | SQLite | - |

## Roadmap (Sprints)

| Sprint | Tema | Status |
|--------|------|--------|
| 1 | Captura de Audio | Concluido |
| 2 | Speech-to-Text | Concluido |
| 3 | Overlay WPF | Concluido |
| 4 | Integracao LLM | Pendente |
| 5 | Persistencia e Config | Pendente |
| 6 | Integracao e Polish | Pendente |

### Sprint 1 — Captura de Audio
Captura dual (mic + loopback), resampling automatico (48kHz float32 stereo → 16kHz int16 mono), echo cancellation com filtro NLMS, pipeline reativo com System.Reactive, e exportacao WAV para debug.

### Sprint 2 — Speech-to-Text
Whisper local via whisper.net com buffer de audio e VAD, ElevenLabs via WebSocket com reconnection automatica, TranscriptionEngine conectando streams ao STT, diarizacao por stream (mic="Eu", loopback="Interlocutor"), factory com hot-swap de providers.

### Sprint 3 — Overlay WPF
Janela topmost click-through com `WS_EX_TRANSPARENT`/`WS_EX_LAYERED`, excluida de captura via `SetWindowDisplayAffinity`, mini controles (copiar, fixar, dispensar) com hit-testing seletivo, display de transcricao live com ObservableCollection, area de sugestoes com streaming de markdown, SimpleMarkdownParser (bold/italic/listas → WPF Inlines), Settings UI completa (STT, perfil, overlay), AppSettings com persistencia JSON.

### Sprint 4 — Integracao LLM
Claude API com prompt caching, triggers por silencio e hotkey, streaming de sugestoes para o overlay.

### Sprint 5 — Persistencia e Config
SQLite para sessoes de reuniao, editor de perfil, contexto de reuniao, UI de configuracoes.

### Sprint 6 — Integracao e Polish
Fluxo end-to-end, system tray, installer, performance tuning para latencia < 2s.

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
| ZefaIA.Audio.Tests | 49 | Resampler, captura, AEC, pipeline, WAV |
| ZefaIA.STT.Tests | 62 | Factory, Whisper, ElevenLabs, engine, timeline, config |
| ZefaIA.Overlay.Tests | 30 | Models, NativeMethods, controller, markdown parser, AppSettings |
| ZefaIA.LLM.Tests | 3 | Models e defaults |
| **Total** | **140+** | |

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
