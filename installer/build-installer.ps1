<#
.SYNOPSIS
    Builds the Zefa IA installer end to end: test, publish, package.

.DESCRIPTION
    Run from anywhere; paths resolve relative to the repository root.
    Requires the .NET 8 SDK and Inno Setup 6 (iscc.exe on PATH or at the
    default install location).

.EXAMPLE
    pwsh installer/build-installer.ps1
    pwsh installer/build-installer.ps1 -SkipTests
#>
[CmdletBinding()]
param(
    [switch]$SkipTests,
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $repoRoot "src/ZefaIA.App/ZefaIA.App.csproj"
$publishDir = Join-Path $repoRoot "src/ZefaIA.App/bin/publish/win-x64"
$issScript = Join-Path $PSScriptRoot "zefa-ia.iss"
$outputDir = Join-Path $PSScriptRoot "output"

function Write-Step($message) {
    Write-Host ""
    Write-Host "==> $message" -ForegroundColor Cyan
}

function Resolve-Iscc {
    $onPath = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )
    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) { return $candidate }
    }

    throw "Inno Setup 6 nao encontrado. Instale de https://jrsoftware.org/isdl.php ou adicione iscc.exe ao PATH."
}

Write-Step "Verificando pre-requisitos"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK nao encontrado. Instale o .NET 8 SDK."
}
$iscc = Resolve-Iscc
Write-Host "  dotnet: $((Get-Command dotnet).Source)"
Write-Host "  iscc:   $iscc"

if (-not $SkipTests) {
    Write-Step "Rodando testes"
    dotnet test (Join-Path $repoRoot "ZefaIA.sln") --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "Testes falharam; instalador nao foi gerado." }
}

Write-Step "Limpando publicacao anterior"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

Write-Step "Publicando self-contained single-file (win-x64)"
dotnet publish $appProject -p:PublishProfile=win-x64 --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou." }

$exe = Join-Path $publishDir "ZefaIA.App.exe"
if (-not (Test-Path $exe)) { throw "Executavel nao encontrado em $exe" }
$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "  ZefaIA.App.exe: $sizeMb MB"

Write-Step "Gerando instalador"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
& $iscc "/DAppVersion=$Version" $issScript
if ($LASTEXITCODE -ne 0) { throw "Inno Setup falhou." }

$installer = Join-Path $outputDir "ZefaIA-Setup-$Version.exe"
Write-Step "Concluido"
Write-Host "  $installer" -ForegroundColor Green
if (Test-Path $installer) {
    $installerMb = [math]::Round((Get-Item $installer).Length / 1MB, 1)
    Write-Host "  Tamanho: $installerMb MB"
}
