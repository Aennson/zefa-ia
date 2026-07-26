# Execução Real de Testes no Windows

Primeira execução real da suíte em máquina Windows. Antes disto, todas as sprints
tinham sido implementadas e os testes escritos, mas **nada havia sido compilado nem
executado** — a solução não compilava.

- **Data:** 2026-07-26
- **Máquina:** Windows 11 Pro 10.0.26200, x64
- **Áudio:** Realtek Audio (Speakers + Microphone Array), Intel Display Audio
- **SDK:** .NET 8.0.423 (instalado durante esta execução — ver F-01)

## Resultado final

| Projeto | Passou | Falhou | Pulado | Total |
|---|---:|---:|---:|---:|
| ZefaIA.App.Tests | 132 | 0 | 0 | 132 |
| ZefaIA.LLM.Tests | 81 | 0 | 1 | 82 |
| ZefaIA.STT.Tests | 80 | 0 | 2 | 82 |
| ZefaIA.Persistence.Tests | 73 | 0 | 0 | 73 |
| ZefaIA.Audio.Tests | 44 | 0 | 0 | 44 |
| ZefaIA.Overlay.Tests | 44 | 0 | 0 | 44 |
| ZefaIA.Integration.Tests | 0 | 0 | 0 | 0 |
| **Total** | **454** | **0** | **3** | **457** |

Estado inicial: **0 testes executáveis** (solução não compilava, 5 erros de restore).

### Os 3 testes ainda não executados

Nenhum é bloqueio de código; todos dependem de recurso externo. Deixaram de ser
`Skip` fixo e agora são opt-in por variável de ambiente, então são executáveis
sob demanda em vez de código morto.

| Teste | Como executar |
|---|---|
| `WhisperSTTProviderTests.Integration_TranscribesAudio` | `ZEFA_RUN_WHISPER_INTEGRATION=1` — **executado e aprovado** nesta sessão (ver V-03); fica desligado por padrão porque baixa ~150 MB |
| `ElevenLabsSTTProviderTests.Integration_ConnectsAndTranscribes` | `ZEFA_RUN_ELEVENLABS_INTEGRATION=1` + `ELEVENLABS_API_KEY` (chave paga — não disponível) |
| `ClaudeLLMClientTests` (integração) | `ANTHROPIC_API_KEY` (não disponível) |

### Validações executadas de verdade neste hardware

| ID | O que foi validado | Resultado |
|---|---|---|
| V-01 | Captura de microfone real via WASAPI (`MicrophoneSource`) | Passou |
| V-02 | Captura de loopback real via WASAPI (`LoopbackSource`) | Passou |
| V-03 | Whisper end-to-end: download do modelo + inferência nativa | Passou |
| V-04 | Publish self-contained (`win-x64`) | Gerado com sucesso |
| V-05 | App publicado inicia, cria `%APPDATA%\ZefaIA\meetings.db`, permanece responsivo, encerra limpo | Passou, sem crash report |

---

## Falhas encontradas e corrigidas

### Bloqueios de ambiente

**F-01 — .NET SDK não instalado.** Nem `dotnet` nem Visual Studio na máquina. O
instalador do winget precisa de elevação (UAC) e foi abandonado; resolvido com o
script oficial `dotnet-install.ps1`, que instala em `%LOCALAPPDATA%\Microsoft\dotnet`
sem admin.

> **Nota:** `dotnet` não está no `PATH` do sistema. Para usar em novo terminal:
> `$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"`

**F-02 — Visual C++ 2015-2022 Redistributable ausente.** Causa raiz da falha do
Whisper (ver F-14). Instalado com `winget install Microsoft.VCRedist.2015+.x64`.

### Erros de compilação (a solução não compilava)

| ID | Defeito | Correção |
|---|---|---|
| F-03 | `Whisper.net.Ggml` referenciado como **pacote NuGet** — não existe; é um *namespace* dentro de `Whisper.net`. Quebrava o restore de 5 projetos | Removida a `PackageReference` inválida |
| F-04 | `WhisperNet.*` usado como namespace; o correto é `Whisper.net.*` | Corrigidos os 10 pontos de uso |
| F-05 | `builder.WithNoGPU()` não existe na API do Whisper.net 1.7.3 | Trocado por `RuntimeOptions.Instance.SetUseGpu(useGpu)`, que é o controle real (process-wide, antes de carregar a lib nativa) |
| F-06 | `new WhisperGgmlDownloader()` — a classe é `static`; e a assinatura de `GetGgmlModelAsync` tem `QuantizationType` como 2º parâmetro | Chamada estática com `cancellationToken:` nomeado |
| F-07 | `AllowUnsafeBlocks` ausente em Core, Overlay e App, exigido pelo gerador `[LibraryImport]` | Adicionado nos 3 `.csproj`, com comentário do motivo |
| F-08 | `ZefaIA.Audio` usava `ILogger<T>` sem referenciar `Microsoft.Extensions.Logging.Abstractions` | Pacote adicionado |
| F-09 | `ApiBaseUrl` era `private` em `ClaudeLLMClient` mas usado por `ClaudeLLMSession` | Promovido a `internal` |
| F-10 | `UseWPF`/`UseWindowsForms` substituem o conjunto de *implicit usings* e removem `System.IO` — `Path`/`File`/`Directory` não resolviam em App, Overlay e seus testes | `<Using Include="System.IO" />` (e `System.Net.Http`) nos `.csproj` afetados |
| F-11 | `Application` e `MessageBox` ambíguos entre `System.Windows` e `System.Windows.Forms` | Qualificados explicitamente em `App.xaml.cs` |
| F-12 | 13 arquivos de teste sem `using Xunit;` | Adicionado |
| F-13 | Testes acessavam membros `internal` (`SessionExporter.FormatText`, `SilenceTrigger.CalculateRMS`, `NativeMethods`, `ClaudeLLMSession`, DTOs do ElevenLabs) sem `InternalsVisibleTo` | Declarado em Core, LLM, STT, Persistence e Overlay |
| F-14 | `TreatWarningsAsErrors` transformava em erro: campos `_disposed` nunca lidos, evento `PartialReceived` nunca usado, `async` sem `await`, código inalcançável, e uso de `.Wait()` em teste (xUnit1031) | Corrigido caso a caso — os `_disposed` agora realmente guardam o dispose |

### Defeitos de produto encontrados pelos testes

**F-15 — SQLite mantinha o arquivo do banco travado após `DisposeAsync`.**
`Microsoft.Data.Sqlite` faz *pooling*: descartar a `SqliteConnection` devolve ao pool
em vez de fechar o handle do arquivo. O `.db` continuava bloqueado, impedindo apagar,
mover ou fazer backup do banco depois que o repositório já não existia. Derrubou 71
testes de Persistence de uma vez.
→ `SqliteConnection.ClearPool()` no `DisposeAsync`.

**F-16 — Datas UTC voltavam do banco convertidas para horário local.** A gravação usa
`ToString("o")` (preserva o offset), mas a leitura usava `DateTime.Parse` sem
`RoundtripKind`. Uma sessão salva às `10:00Z` retornava como `07:00-03:00` — mesmo
instante, mas `Kind=Local`, então todo consumidor que formata ou re-serializa (export
JSON/TXT, overlay) produzia string errada.
→ Helper `ParseTimestamp` com `DateTimeStyles.RoundtripKind` nos 4 pontos de leitura.

**F-17 — Marcador `[SEM SUGESTAO]` vazava para a UI.** O pipeline acumulava o texto
para detectar o marcador, mas repassava cada token assim que chegava. Como o marcador
vem quebrado em vários tokens (`"[SEM"`, `" SUGESTAO]"`), o overlay piscava `[SEM` antes
de o filtro conseguir agir.
→ A saída agora é retida enquanto o texto ainda puder virar o marcador, e liberada
assim que ele for descartado. Inclui o caso da stream terminar no meio do prefixo
(uma resposta literalmente `"[SEM"` precisa chegar à UI).

**F-18 — Instalador não verificava o VC++ Redistributable.** O `.iss` afirmava que
"o app é self-contained, então nenhuma checagem de runtime é necessária" — verdade para
o .NET, falso para as DLLs nativas do Whisper, que são compiladas com MSVC. Em máquina
limpa, o Zefa IA instalava normalmente e a transcrição morria com erro Win32 126.
→ `InitializeSetup` agora detecta o runtime e avisa com as instruções de instalação.

**F-19 — Mensagem de erro do Whisper enganosa.** A mensagem original do Whisper.net
("Cannot load the library on this platform") sugere problema de arquitetura, quando a
causa real é o VC++ ausente.
→ `WhisperSTTProvider` traduz essa falha para uma mensagem acionável.

### Defeitos nos próprios testes

**F-20 — Testes de WPF falhavam por thread não-STA.** 11 testes de Overlay instanciavam
`TextBlock`/janelas fora de thread STA.
→ Pacote `Xunit.StaFact` e troca de `[Fact]` por `[WpfFact]` nas classes que tocam UI.

**F-21 — `LoopbackSource.StartAsync_EmitsAudioChunks_WhenAudioPlaying` era impossível de
passar sozinho.** WASAPI loopback não entrega nada com o endpoint de saída ocioso, e o
teste nunca tocava áudio — dependia de um humano estar com música tocando.
→ O teste agora gera o próprio tom senoidal (`SignalGenerator` + `WaveOutEvent`) durante
a captura, virando determinístico.

**F-22 — Testes de hardware eram `Skip` fixo, portanto código morto.** Nunca rodariam,
nem em máquina com hardware.
→ Atributo `RequiresAudioDeviceFact`, que sonda os endpoints WASAPI em runtime: executa
onde há hardware, pula (com motivo) onde não há. Os 6 testes agora **rodam de verdade**
nesta máquina.

---

## Como reproduzir

```powershell
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"

dotnet build ZefaIA.sln -c Debug
dotnet test  ZefaIA.sln -c Debug

# Incluindo o teste de integração do Whisper (baixa ~150 MB na primeira vez)
$env:ZEFA_RUN_WHISPER_INTEGRATION = "1"
dotnet test tests/ZefaIA.STT.Tests -c Debug
```

Pré-requisitos numa máquina limpa:

```powershell
winget install Microsoft.DotNet.SDK.8
winget install Microsoft.VCRedist.2015+.x64   # obrigatório para o Whisper
```

## Pendências conhecidas

- **`ZefaIA.Integration.Tests` está vazio** (só um `Placeholder.cs`). O projeto existe e
  compila, mas não há nenhum teste ponta-a-ponta cobrindo o pipeline
  áudio → STT → trigger → LLM → overlay → persistência integrado.
- **Integração com a API da Anthropic nunca foi exercitada** contra o serviço real —
  falta `ANTHROPIC_API_KEY`. Os testes cobrem parsing de SSE, retry e montagem do corpo
  com mocks, mas nenhuma requisição real foi feita.
- **`ZefaIA.LLM` fala com a API da Anthropic via `HttpClient` cru.** Existe SDK oficial
  para C# (pacote `Anthropic`); migrar reduziria manutenção de parsing de SSE e headers
  de beta. Fora do escopo desta rodada de correções.
