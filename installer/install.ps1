<#
.SYNOPSIS
    Instala o Monitor de produtividade para o usuário atual (sem administrador).
.DESCRIPTION
    Copia o programa para %LOCALAPPDATA%\Programs\ProductivityMonitor, cria atalho no Menu Iniciar
    (e opcionalmente na Área de Trabalho) e abre o programa.
    - Dentro do pacote .zip (pasta "app" ao lado de "scripts"): instala o que está no pacote.
    - No repositório: recompila o pacote a cada execução (-SkipBuild reaproveita o existente).
.EXAMPLE
    .\installer\install.ps1 -Desktop
#>
param(
    [switch]$Desktop,
    [switch]$NoLaunch,
    [switch]$SkipBuild,
    [switch]$NoShortcut,
    [string]$InstallDir
)
$ErrorActionPreference = 'Stop'

$exeName = 'ProductivityMonitor.exe'
$parent = Split-Path -Parent $PSScriptRoot
$bundled = Join-Path $parent 'app'

if (Test-Path (Join-Path $bundled $exeName)) {
    # Layout do pacote distribuível (.zip): scripts\install.ps1 + app\
    $package = $bundled
}
else {
    # Layout do repositório: installer\install.ps1 + artifacts\publish
    $package = Join-Path $parent 'artifacts\publish'
    # Sempre recompila para nunca instalar um pacote desatualizado.
    if (-not $SkipBuild -or -not (Test-Path (Join-Path $package $exeName))) {
        Write-Host 'Gerando pacote a partir do código atual...' -ForegroundColor Yellow
        & (Join-Path $PSScriptRoot 'build.ps1')
    }
}

$target = if ($InstallDir) { $InstallDir } else { Join-Path $env:LOCALAPPDATA 'Programs\ProductivityMonitor' }
$targetExe = Join-Path $target $exeName
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Monitor de produtividade.lnk'
$desktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Monitor de produtividade.lnk'

# Encerra apenas a instância instalada neste destino para permitir a atualização dos arquivos.
Get-Process -Name 'ProductivityMonitor' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $targetExe } | Stop-Process -Force
Start-Sleep -Milliseconds 500

if (Test-Path $target) { Remove-Item $target -Recurse -Force }
New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item (Join-Path $package '*') $target -Recurse -Force

function New-Shortcut($path) {
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($path)
    $link.TargetPath = $targetExe
    $link.WorkingDirectory = $target
    $link.Description = 'Monitor de produtividade'
    $link.Save()
}

if (-not $NoShortcut) {
    New-Shortcut $startMenu
    if ($Desktop) { New-Shortcut $desktopLink }
}

Write-Host "Instalado em $target" -ForegroundColor Green
if (-not $NoShortcut) { Write-Host 'Atalho criado no Menu Iniciar: "Monitor de produtividade".' }

if (-not $NoLaunch) { Start-Process $targetExe }
