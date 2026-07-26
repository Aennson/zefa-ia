# Testes: AppBootstrapper e TrayIconController

**Arquivos fonte:** `src/ZefaIA.App/AppBootstrapper.cs`, `TrayIconController.cs`
**Arquivos de teste:** `tests/ZefaIA.App.Tests/AppBootstrapperTests.cs`, `TrayIconControllerTests.cs`
**Total:** 7 + 10 = 17 testes

---

## AppBootstrapper (7 testes)

### Motivacao

O app tem duas fontes de configuracao que se sobrepoem: `appsettings.json`
(padroes de deploy, ao lado do executavel) e `settings.json` do usuario
(`%APPDATA%`, editavel pela UI de Configuracoes). A regra e "o usuario ganha, mas
so quando de fato escolheu algo" — e essa distincao entre "vazio" e "escolhido"
e o que os testes fixam.

`BindSttSettings` e `internal static` e puro, entao da para testar a precedencia
inteira sem tocar em disco. O resto do bootstrap (abrir SQLite, criar overlay)
depende de I/O e da UI thread e e verificado rodando o app.

### Testes

#### 1-3. Precedencia
- `BindSttSettings_NoUserOverrides_UsesConfigurationValues`
- `BindSttSettings_UserSettingsWinOverConfiguration`
- `BindSttSettings_EmptyConfiguration_UsesDefaults`
- **O que testa:** Com o usuario em branco, vale o `appsettings.json`; com valor
  escolhido, vale o do usuario; sem nenhum dos dois, valem os defaults do codigo
  (`WhisperLocal`, modelo `base`, `./models`).
- **Execucao:** `dotnet test --filter "AppBootstrapperTests.BindSttSettings_No|AppBootstrapperTests.BindSttSettings_User|AppBootstrapperTests.BindSttSettings_Empty"`

#### 4. Idioma compartilhado
- `BindSttSettings_LanguageAppliesToBothProviders`
- **O que testa:** O idioma e um campo so na UI, mas alimenta dois blocos de
  configuracao. Trocar de provedor nao pode ressuscitar o idioma antigo.
- **Execucao:** `dotnet test --filter "AppBootstrapperTests.BindSttSettings_Language"`

#### 5-6. Casos de borda da precedencia
- `BindSttSettings_UseGpuAlwaysFromUserSettings`
- `BindSttSettings_WhitespaceUserProvider_FallsBackToConfiguration`
- **O que testa:** Os dois lados da distincao "vazio vs escolhido".

  `UseGPU` e booleano vindo de checkbox — nao existe "nao preenchido", entao o
  valor do usuario sempre vale, inclusive `false`. O teste fixa isso com o config
  em `true` e o usuario em `false`, esperando `false`.

  Ja um provedor em branco (`"   "`) significa "nao escolhi", nao "quero string
  vazia" — por isso cai de volta no config. Sem o `IsNullOrWhiteSpace`, um campo
  de texto com espaco quebraria o parse do enum no startup.
- **Execucao:** `dotnet test --filter "AppBootstrapperTests.BindSttSettings_UseGpu|AppBootstrapperTests.BindSttSettings_Whitespace"`

#### 7. Caminho de configuracao
- `SettingsPath_LivesUnderZefaIAAppData`
- **Execucao:** `dotnet test --filter "AppBootstrapperTests.SettingsPath"`

---

## TrayIconController (10 testes)

### Motivacao

Sem janela principal, a bandeja e a unica UI sempre presente — o icone e o
tooltip sao a unica forma de o usuario saber se esta gravando. Os testes cobrem o
mapeamento puro estado → apresentacao; o `NotifyIcon` em si precisa de message
pump e fica na verificacao manual.

### Testes

#### 1-2. Tooltip
- `BuildTooltip_DescribesEachState` (Theory, 5 estados)
- `BuildTooltip_StaysWithinWindowsTooltipLimit`
- **O que testa:** Cada estado tem texto proprio e reconhecivel. O limite de 63
  caracteres nao e estetico: `NotifyIcon.Text` **lanca** acima disso, e o crash
  aconteceria no meio de uma transicao de estado. O teste percorre todos os
  estados do enum, entao um estado novo com texto longo reprova na hora.
- **Execucao:** `dotnet test --filter "TrayIconControllerTests.BuildTooltip"`

#### 3-6. Cores de estado
- `GetStateColor_RecordingIsRed`
- `GetStateColor_IdleDiffersFromRecording`
- `GetStateColor_TransitionalStatesShareAmber`
- `GetStateColor_EveryStateIsOpaque`
- **O que testa:** Gravando e vermelho (canal R dominante) e visivelmente
  diferente de ocioso — a distincao que evita gravar sem perceber. `Starting` e
  `Stopping` compartilham ambar por serem ambos transitorios.

  `EveryStateIsOpaque` percorre o enum verificando alfa 255: um icone
  semitransparente some no fundo da barra de tarefas em temas claros.
- **Execucao:** `dotnet test --filter "TrayIconControllerTests.GetStateColor"`

## Cobertura manual

- Menu de contexto: Nova Reuniao, Parar, Configuracoes, Historico, Sair
- Itens habilitam/desabilitam conforme o estado (Parar so com reuniao ativa)
- Duplo-clique inicia quando ocioso, para quando gravando
- Icone muda de cor ao iniciar e encerrar
- App continua na bandeja ao fechar janelas de dialogo

### Nota sobre o HICON

`Icon.FromHandle` **nao** assume posse do handle. Como o icone e recriado a cada
transicao de estado, usar o retorno direto vazaria um HICON por transicao. O
codigo clona o icone e destroi o handle com `DestroyIcon`. Nao ha teste
automatizado para o vazamento — verificar com o contador de GDI Objects no
Gerenciador de Tarefas durante uma sessao longa.
