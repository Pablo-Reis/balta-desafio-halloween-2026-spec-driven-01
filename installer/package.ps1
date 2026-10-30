<#
.SYNOPSIS
    Gera o pacote distribuível: artifacts\ProductivityMonitor-<versão>.zip
.DESCRIPTION
    O usuário final extrai o .zip e executa "Instalar.cmd" (duplo clique). Não precisa de .NET,
    administrador nem internet: o pacote é autocontido (win-x64).
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

[xml]$csproj = Get-Content (Join-Path $root 'src\Productivity.App\Productivity.App.csproj')
$version = ($csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { $version = '1.0.0' }

& (Join-Path $PSScriptRoot 'build.ps1')

$name = "ProductivityMonitor-$version"
$stage = Join-Path $root "artifacts\$name"
$zip = Join-Path $root "artifacts\$name.zip"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }

New-Item -ItemType Directory -Force (Join-Path $stage 'scripts') | Out-Null
Copy-Item (Join-Path $root 'artifacts\publish') (Join-Path $stage 'app') -Recurse
Copy-Item (Join-Path $PSScriptRoot 'install.ps1') (Join-Path $stage 'scripts')
Copy-Item (Join-Path $PSScriptRoot 'uninstall.ps1') (Join-Path $stage 'scripts')
Copy-Item (Join-Path $PSScriptRoot 'package\*') $stage -Recurse

Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Remove-Item $stage -Recurse -Force

$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Pacote pronto: $zip ($size MB)" -ForegroundColor Green
