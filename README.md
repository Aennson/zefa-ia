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
5. **Sugestoes** — Claude API com prompt caching, sob demanda por `Ctrl+Shift+Space` ou automaticamente apos silencio
6. **Overlay** — WPF topmost, clicavel e redimensionavel, excluido de captura de tela (`WDA_EXCLUDEFROMCAPTURE`)

## Requisitos

### Sistema

- Windows 10 1903+ (necessario para WASAPI loopback e `SetWindowDisplayAffinity`)
- **Visual C++ 2015-2022 Redistributable (x64)** — as DLLs nativas do Whisper sao compiladas com MSVC e nao carregam sem ele:
  ```powershell
  winget install Microsoft.VCRedist.2015+.x64
  ```
- .NET 8 SDK apenas para compilar ([download](https://dotnet.microsoft.com/download/dotnet/8.0)) — o app publicado e self-contained

### API Keys (opcionais)

| Servico | Para que serve | Variavel de ambiente equivalente |
|---------|----------------|----------------------------------|
| Claude (Anthropic) | Sugestoes via LLM | `ANTHROPIC_API_KEY` |
| ElevenLabs | STT alternativo ao Whisper local | `ELEVENLABS_API_KEY` |

> **ElevenLabs e STT, nao LLM.** Ela substitui o Whisper na transcricao; quem gera
> as sugestoes e sempre o Claude. Configurar ElevenLabs sem `ANTHROPIC_API_KEY`
> melhora a transcricao mas nao liga as sugestoes.

> O Whisper local funciona **sem nenhuma API key** — roda 100% offline.
> Sem `ANTHROPIC_API_KEY` o app inicia normalmente em modo so-transcricao:
> grava, salva no historico e exporta; apenas as sugestoes ficam desligadas.

Configure em **Configuracoes → Chaves de API**, com botao para testar a chave
contra o servico antes de depender dela numa reuniao.

As chaves ficam **criptografadas com DPAPI** dentro de `settings.json`, amarradas a
conta do Windows: um arquivo copiado para outra maquina ou outro usuario nao
descriptografa e o app se comporta como se nao houvesse chave. Nada de segredo em
texto puro no disco.

Variavel de ambiente continua valendo como alternativa, para quem prefere nao
guardar a chave. Como o app roda na bandeja, defina no nivel do usuario para que
valha ao abrir pelo Explorer:

```powershell
[Environment]::SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-ant-...", "User")
```

**O campo em Configuracoes tem prioridade sobre a variavel** — quem acabou de colar
uma chave na tela espera que ela valha, e nao teria como descobrir uma variavel
definida meses atras. Deixe o campo em branco para usar a variavel; a tela mostra
de onde cada chave esta vindo.

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

# Rodar testes (586 no total; 6 sao opt-in por variavel de ambiente)
dotnet test

# Executar
dotnet run --project src/ZefaIA.App

# Publicar (self-contained, single-file)
dotnet publish src/ZefaIA.App -c Release -p:PublishProfile=win-x64

# Gerar o instalador (requer Inno Setup 6)
pwsh installer/build-installer.ps1
```

### Modelo Whisper

Na primeira reuniao com STT Whisper, o modelo e baixado automaticamente (~142MB para `base`),
**sem barra de progresso** — a primeira reuniao demora alguns minutos.

O modelo vai para `<pasta do executavel>/models`, com fallback para
`%LOCALAPPDATA%\ZefaIA\models` quando a pasta de instalacao e somente-leitura.
Caminhos relativos em `appsettings.json` resolvem a partir do executavel, nao do
diretorio de trabalho — abrir o `.exe` por caminho completo de outra pasta nao
faz o modelo ser baixado de novo.

Para evitar a espera, coloque um `ggml-<modelo>.bin` ja baixado nessa pasta antes da primeira reuniao.

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
│   ├── ZefaIA.Core/           # Interfaces, models, eventos, triggers (domain)
│   ├── ZefaIA.Audio/          # Captura NAudio, resampling, AEC, pipeline Rx
│   ├── ZefaIA.STT/            # Providers de STT (Whisper, ElevenLabs)
│   ├── ZefaIA.LLM/            # Cliente LLM (Claude API), prompt, orquestracao
│   ├── ZefaIA.Overlay/        # Janelas WPF e tema visual (Themes/)
│   ├── ZefaIA.Persistence/    # SQLite, recorder de reuniao, exportacao
│   └── ZefaIA.App/            # Orquestracao, system tray, bootstrap, crash reports
├── tests/
│   ├── ZefaIA.Audio.Tests/       # 44 testes de audio
│   ├── ZefaIA.STT.Tests/         # 100 testes de STT, idioma, chave e caminho do modelo
│   ├── ZefaIA.Overlay.Tests/     # 115 testes de overlay, layout, settings, chaves e historico
│   ├── ZefaIA.LLM.Tests/         # 88 testes de LLM, triggers e orquestracao
│   ├── ZefaIA.Persistence.Tests/ # 73 testes de SQLite, recorder e export
│   ├── ZefaIA.App.Tests/         # 141 testes de orquestracao, chaves e resiliencia
│   └── ZefaIA.Integration.Tests/ #  25 testes ponta a ponta do pipeline
├── installer/                 # Script Inno Setup e build do instalador
└── docs/
    ├── PROJECT-SPEC.md        # Especificacao completa
    ├── USAGE.md               # Guia de uso e troubleshooting
    ├── sprint-1/ a sprint-6/  # Plano de sprints e tasks
    └── tests/
        ├── E2E-COVERAGE.md    # Cobertura ponta a ponta e lacunas conhecidas
        ├── WINDOWS-TEST-RUN.md# Primeira execucao real em Windows e defeitos achados
        └── sprint-1/ a sprint-6/
```

## Configuracao

Edite `src/ZefaIA.App/appsettings.json`:

```json
{
  "Audio": { "BufferSizeMs": 100, "SampleRate": 16000, "Channels": 1 },
  "STT": {
    "ActiveProvider": "WhisperLocal",
    "WhisperLocal": {
      "ModelSize": "base",
      "Language": "auto",
      "UseGPU": false,
      "ModelPath": "./models"
    },
    "ElevenLabs": { "ApiKeyEnvVar": "ELEVENLABS_API_KEY", "Language": "auto" }
  },
  "Triggers": { "SilenceThresholdMs": 1500, "CooldownMs": 10000, "MaxRequestsPerMinute": 4 },
  "Overlay": { "Opacity": 0.85, "FontSize": 14, "Position": "BottomRight" }
}
```

> A secao `LLM` do `appsettings.json` **nao e lida por ninguem** hoje:
> o `MeetingOrchestrator` monta o `LLMSessionConfig` a partir dos defaults do
> record em `ZefaIA.Core/Models/LLMModels.cs` (`claude-sonnet-5`, 1024 tokens).
> Os valores estao sincronizados para nao enganar, mas editar o arquivo nao muda
> nada ate a ligacao ser feita. Ver [`docs/tests/E2E-COVERAGE.md`](docs/tests/E2E-COVERAGE.md).

O que a tela de **Configuracoes** grava (`%APPDATA%\ZefaIA\settings.json`) tem
precedencia sobre o `appsettings.json` para provedor de STT, modelo, idioma,
perfil do usuario e overlay.

### Trocar provider de STT

Configuracoes → **Provedor** → `Whisper Local` ou `ElevenLabs`. Vale a partir da proxima reuniao.

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
| STT cloud | ElevenLabs Scribe v2 Realtime | WebSocket |
| LLM | Claude API (`claude-sonnet-5`) | - |
| Testes | xUnit + Moq + Xunit.StaFact | 2.9.2 / 4.20.72 |
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
| — | Validacao real em Windows | Concluido |

### Sprint 1 — Captura de Audio
Captura dual (mic + loopback), resampling automatico (48kHz float32 stereo → 16kHz int16 mono), echo cancellation com filtro NLMS, pipeline reativo com System.Reactive, e exportacao WAV para debug.

### Sprint 2 — Speech-to-Text
Whisper local via whisper.net com buffer de audio e VAD, ElevenLabs via WebSocket com reconnection automatica, TranscriptionEngine conectando streams ao STT, diarizacao por stream (mic="Eu", loopback="Interlocutor"), factory com hot-swap de providers.

### Sprint 3 — Overlay WPF
Janela topmost excluida de captura via `SetWindowDisplayAffinity`, mini controles (copiar, fixar, dispensar), display de transcricao live com ObservableCollection, area de sugestoes com streaming de markdown, SimpleMarkdownParser (bold/italic/listas → WPF Inlines), Settings UI completa (STT, perfil, overlay), AppSettings com persistencia JSON.

### Sprint 4 — Integracao LLM
ClaudeLLMClient com SSE streaming e prompt caching (`cache_control` ephemeral), retry com backoff em 429/500, PromptBuilder com perfil do usuario e contexto de reuniao, SilenceTrigger com deteccao RMS e cooldown, HotkeyTrigger global via Win32 `RegisterHotKey`, SuggestionStreamPipeline com maquina de estados e filtragem de `[SEM SUGESTAO]`, SuggestionOrchestrator com rate limiting e deduplicacao.

### Sprint 5 — Persistencia e Config
Persistencia SQLite via ADO.NET puro (`Microsoft.Data.Sqlite`, sem EF Core) com tres tabelas e cascade delete garantido por `Foreign Keys=True`, MeetingRecorder que persiste transcricoes em batch durante a reuniao e faz flush no encerramento, dialogo de nova reuniao com templates (1:1, Standup, Review, Custom) e inicio rapido, tela de historico com busca full-text na transcricao e exclusao com confirmacao, LanguageDetector que agrega o idioma dos segmentos e adapta os labels de speaker, exportacao TXT (com sugestoes inline) e JSON.

### Sprint 6 — Integracao e Polish
MeetingOrchestrator montando o grafo audio -> STT -> LLM -> overlay -> persistencia por reuniao, StageRunner com startup fail-fast + rollback e shutdown best-effort (uma stage que falha nao impede o flush da persistencia), app tray-only com estado visual, degradacao para modo so-transcricao quando falta a API key, RetryPolicy com backoff e jitter, HealthTracker por componente, scrubber de segredos e crash reports locais, instrumentacao de latencia por estagio com percentis, installer Inno Setup self-contained e guia de uso.

### Validacao real em Windows
Primeira execucao de verdade do projeto: o build foi consertado (nao compilava), a suite inteira passou a rodar, foram criados os testes ponta a ponta, e a API do Claude e a da ElevenLabs foram exercitadas contra os servicos reais. Os defeitos encontrados estao documentados em [`docs/tests/WINDOWS-TEST-RUN.md`](docs/tests/WINDOWS-TEST-RUN.md) e [`docs/tests/E2E-COVERAGE.md`](docs/tests/E2E-COVERAGE.md).

## Testes

```bash
# Rodar todos os testes
dotnet test

# Rodar testes de um modulo
dotnet test tests/ZefaIA.Audio.Tests
dotnet test tests/ZefaIA.Integration.Tests

# Rodar teste especifico
dotnet test --filter "EchoCancellerTests.Process_WithMatchingReference_ReducesEcho"
```

| Projeto | Testes | Cobertura |
|---------|--------|-----------|
| ZefaIA.Audio.Tests | 44 | Resampler, captura real (mic e loopback), AEC, pipeline, WAV |
| ZefaIA.STT.Tests | 100 | Factory, Whisper, ElevenLabs, engine, timeline, idioma, resolucao de chave e do caminho do modelo |
| ZefaIA.Overlay.Tests | 115 | Layout das janelas, interacao e captura do overlay, markdown, settings, criptografia e validacao de chaves, historico |
| ZefaIA.LLM.Tests | 88 | Claude client, prompt builder, triggers, pipeline, orchestrator |
| ZefaIA.Persistence.Tests | 73 | Repositorio SQLite, cascade delete, recorder, exportacao TXT/JSON |
| ZefaIA.App.Tests | 141 | Stage runner, bootstrap, tray, retry, health, scrubber, latencia, plumbing das chaves |
| ZefaIA.Integration.Tests | 25 | Pipeline ponta a ponta: audio → STT → trigger → LLM → overlay → SQLite → export |
| **Total** | **586** | **580 passando, 6 opt-in** |

### Testes opt-in

Nao rodam por padrao porque dependem de download grande, chave paga ou rede.
Ligue por variavel de ambiente:

| Variavel | O que executa |
|----------|---------------|
| `ZEFA_RUN_WHISPER_INTEGRATION=1` | Baixa o modelo base e transcreve de verdade |
| `ZEFA_RUN_ELEVENLABS_INTEGRATION=1` | Transcreve fala sintetizada contra a API real (precisa de `ELEVENLABS_API_KEY`) |
| `ZEFA_RUN_ANTHROPIC_INTEGRATION=1` | Chama a API do Claude de verdade (precisa de `ANTHROPIC_API_KEY`) |

Testes que dependem de hardware de audio usam `[RequiresAudioDeviceFact]`, que
sonda os endpoints WASAPI em runtime: executam onde ha microfone e alto-falante,
e pulam com motivo onde nao ha.

## Decisoes Tecnicas

- **Local-first** — Todo processamento local exceto chamadas de API (ElevenLabs STT e Claude LLM)
- **Diarizacao sem ML** — Separacao de speakers pela origem do stream (mic vs loopback), sem modelos extras
- **Echo cancellation por software** — Filtro NLMS ao inves de depender de AEC do driver de audio
- **Provider abstraction** — `ISTTProvider` permite trocar Whisper por ElevenLabs (ou qualquer outro) via config
- **Reactive streams** — System.Reactive para buffering, backpressure e composicao de streams de audio
- **Overlay clicavel por padrao** — click-through existe como API, mas nao ligado: e tudo-ou-nada por janela, entao um botao que o ligasse nunca conseguiria desliga-lo
- **Alias de modelo sem data** — `claude-sonnet-5` em vez de um snapshot datado, que expira silenciosamente
- **Chaves criptografadas com DPAPI** — `settings.json` fica em `%APPDATA%`, e viaja em backups e pedidos de suporte; a chave nao viaja junto
- **LGPD** — Dados de reuniao sao locais e por sessao; o usuario decide se salva

## Limitacoes Conhecidas

- **Sugestao automatica por silencio depende do audio do sistema.** O `SilenceTrigger`
  observa o stream de loopback, e o WASAPI nao entrega nada quando nada esta tocando.
  Falando sozinho ao microfone, use `Ctrl+Shift+Space`.
- **So o atalho de sugestao esta ligado.** `Ctrl+Shift+Z` e `Ctrl+Shift+C` aparecem
  nas Configuracoes mas ainda nao sao registrados.
- **O registro do atalho falha em silencio** se outro app ja tiver a combinacao.
- **O tamanho do overlay nao persiste** entre execucoes do app.
- **Uma chave trocada so vale na proxima reuniao** — a reuniao em andamento segue
  com o cliente que ja estava aberto.
- **A secao `LLM` do `appsettings.json` nao esta ligada** (ver Configuracao).
- **`ZefaIA.LLM` fala com a API da Anthropic via `HttpClient` cru**; existe SDK oficial para C#.

## Licenca

Projeto privado. Todos os direitos reservados.

## Autor

**Aennson** (aennson@gmail.com)
Co-autoria: Claude (AI assistant)
