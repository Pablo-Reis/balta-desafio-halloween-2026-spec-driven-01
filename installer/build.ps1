<#
.SYNOPSIS
    Gera o pacote do Monitor de produtividade (self-contained, win-x64) em artifacts\publish.
    Não exige o .NET instalado na máquina de destino.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'artifacts\publish'

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet publish (Join-Path $root 'src\Productivity.App\Productivity.App.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:DebugType=None -p:DebugSymbols=false `
    -o $out
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar a aplicação.' }

Write-Host "Pacote gerado em $out" -ForegroundColor Green
