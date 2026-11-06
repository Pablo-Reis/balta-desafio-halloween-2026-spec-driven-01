<#
.SYNOPSIS
    Gera o instalador de arquivo único: artifacts\ProductivityMonitor-Setup-<versão>.exe
.DESCRIPTION
    1. Publica o programa autocontido (win-x64) e junta o desinstalador.
    2. Compacta tudo em app.zip.
    3. Publica o assistente de instalação como um único .exe com o app.zip embutido.
    O usuário final só precisa executar esse arquivo: ele abre o assistente, copia o programa para
    "Arquivos de Programas", cria atalhos e registra o programa na lista de aplicativos do Windows.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

[xml]$csproj = Get-Content (Join-Path $root 'src\Productivity.App\Productivity.App.csproj')
$version = ($csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { $version = '1.0.0' }

# 1) Programa
& (Join-Path $PSScriptRoot 'build.ps1')

$work = Join-Path $root 'artifacts\payload'
$stage = Join-Path $work 'app'
$zip = Join-Path $work 'app.zip'
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Force $work | Out-Null
Copy-Item (Join-Path $root 'artifacts\publish') $stage -Recurse
Copy-Item (Join-Path $PSScriptRoot 'uninstall.ps1') $stage

# 2) Pacote
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Write-Host ("Pacote do programa: {0} MB" -f [math]::Round((Get-Item $zip).Length / 1MB, 1))

# 3) Instalador de arquivo único
$setupOut = Join-Path $root 'artifacts\setup'
if (Test-Path $setupOut) { Remove-Item $setupOut -Recurse -Force }
dotnet publish (Join-Path $root 'src\Productivity.Installer\Productivity.Installer.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None -p:DebugSymbols=false `
    "-p:Version=$version" "-p:PayloadZip=$zip" `
    -o $setupOut
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar o instalador.' }

$exe = Join-Path $root "artifacts\ProductivityMonitor-Setup-$version.exe"
Copy-Item (Join-Path $setupOut 'ProductivityMonitor.Setup.exe') $exe -Force
Remove-Item $work -Recurse -Force

Write-Host ("Instalador pronto: {0} ({1} MB)" -f $exe, [math]::Round((Get-Item $exe).Length / 1MB, 1)) -ForegroundColor Green
