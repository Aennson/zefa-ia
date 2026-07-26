# Instalador do Zefa IA

## Pre-requisitos

| Ferramenta | Versao | Onde obter |
|-----------|--------|------------|
| .NET SDK | 8.0+ | https://dotnet.microsoft.com/download/dotnet/8.0 |
| Inno Setup | 6.x | https://jrsoftware.org/isdl.php |

## Gerar o instalador

```powershell
pwsh installer/build-installer.ps1
```

O script roda os testes, publica em self-contained single-file e empacota.
Para pular os testes durante iteracao local:

```powershell
pwsh installer/build-installer.ps1 -SkipTests
pwsh installer/build-installer.ps1 -Version 1.1.0
```

Saida: `installer/output/ZefaIA-Setup-<versao>.exe`

## Fazer manualmente

```powershell
dotnet publish src/ZefaIA.App -p:PublishProfile=win-x64
iscc installer/zefa-ia.iss
```

## Decisoes de packaging

**Self-contained** — o instalador carrega o runtime .NET junto (~80 MB), entao
funciona em maquina limpa sem instalar o .NET separado. O preco e o tamanho.

**Sem trimming** — `PublishTrimmed` fica em `false`. WPF resolve XAML por
reflection, e o trimmer remove tipos que so sao referenciados dessa forma; o
build passa e o app quebra em runtime. Nao vale o risco pelos ~20 MB.

**ReadyToRun ligado** — pre-compila IL para codigo nativo. Aumenta o arquivo mas
reduz o tempo de startup, que tem alvo de < 5s no criterio de aceite.

**Modelo Whisper baixado no primeiro uso** — bundlar o modelo `base` levaria o
instalador de ~80 MB para ~220 MB. Como o download e automatico e acontece uma
vez so, fica fora do pacote.

**PrivilegesRequired=lowest** — instala em `%LOCALAPPDATA%\Programs` por padrao,
sem prompt de UAC. O usuario pode elevar para instalar em `Program Files` pela
propria caixa de dialogo.

## Dados do usuario

O desinstalador remove apenas a pasta do app. O historico de reunioes e as
configuracoes ficam em `%APPDATA%\ZefaIA` e so sao apagados se o usuario
confirmar no prompt ao final da desinstalacao.

## Estado de verificacao

O script e o `.iss` ainda **nao foram executados** — exigem host Windows com o
SDK .NET e o Inno Setup. Ao rodar pela primeira vez, confira:

- [ ] `dotnet publish` conclui e gera `ZefaIA.App.exe`
- [ ] `iscc` compila sem erro (atencao ao caminho de `docs/USAGE.md` na secao `[Files]`)
- [ ] Instalacao em VM limpa, sem .NET instalado, e o app abre
- [ ] Atalho no Menu Iniciar funciona
- [ ] Auto-start marcado -> app sobe apos reboot
- [ ] Desinstalar remove a pasta do app e pergunta sobre os dados
