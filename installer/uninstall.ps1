<#
.SYNOPSIS
    Remove o Monitor de produtividade do usuário atual.
.PARAMETER RemoveData
    Também apaga banco de dados e logs em %LOCALAPPDATA%\ProductivityMonitor.
#>
param([switch]$RemoveData)
$ErrorActionPreference = 'Stop'

$target = Join-Path $env:LOCALAPPDATA 'Programs\ProductivityMonitor'
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Monitor de produtividade.lnk'
$desktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Monitor de produtividade.lnk'

Get-Process -Name 'ProductivityMonitor' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

foreach ($path in @($target, $startMenu, $desktopLink)) {
    if (Test-Path $path) { Remove-Item $path -Recurse -Force }
}

# Remove o início automático com o Windows, se estiver ativo.
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'ProductivityMonitor' -ErrorAction SilentlyContinue

if ($RemoveData) {
    $data = Join-Path $env:LOCALAPPDATA 'ProductivityMonitor'
    if (Test-Path $data) { Remove-Item $data -Recurse -Force }
}

Write-Host 'Monitor de produtividade removido.' -ForegroundColor Green
if (-not $RemoveData) { Write-Host 'Seus dados foram mantidos (use -RemoveData para apagá-los).' }
