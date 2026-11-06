<#
.SYNOPSIS
    Desinstala o Monitor de produtividade (chamado pela lista de programas do Windows).
.DESCRIPTION
    Fica gravado na pasta do programa. Pergunta se deve manter os dados, pede permissão de
    administrador quando a pasta exige e remove arquivos, atalhos e registro.
.PARAMETER Silent
    Sem perguntas e sem janelas. Mantém os dados, a menos que -RemoveData seja informado.
.PARAMETER RemoveData
    Também apaga banco de dados e logs em %LOCALAPPDATA%\ProductivityMonitor.
.PARAMETER Sandbox
    Usa os nomes da instalação de teste (não mexe na instalação real).
#>
param(
    [switch]$Silent,
    [switch]$RemoveData,
    [switch]$Sandbox,
    [switch]$Confirmed,
    [switch]$FromTemp,
    [string]$InstallDir
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

$product = 'Monitor de produtividade'
if (-not $InstallDir) { $InstallDir = $PSScriptRoot }
$InstallDir = $InstallDir.TrimEnd('\')
$keyName = if ($Sandbox) { 'ProductivityMonitor.Sandbox' } else { 'ProductivityMonitor' }
$shortcut = if ($Sandbox) { "$product (teste).lnk" } else { "$product.lnk" }

function Show-Message($text, $icon = 'Information') {
    if (-not $Silent) {
        [void][System.Windows.Forms.MessageBox]::Show($text, $product, 'OK', $icon)
    }
}

function Test-Admin {
    ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-NeedsAdmin {
    if (Test-Admin) { return $false }
    try {
        $probe = Join-Path $InstallDir ".uninstall-$([guid]::NewGuid().ToString('N')).tmp"
        New-Item -ItemType File -Path $probe -ErrorAction Stop | Out-Null
        Remove-Item $probe -Force
        return $false
    }
    catch { return $true }
}

function Get-SelfArguments {
    $list = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden', '-File', "`"$PSCommandPath`"", '-FromTemp', '-InstallDir', "`"$InstallDir`"")
    if ($Silent) { $list += '-Silent' }
    if ($RemoveData) { $list += '-RemoveData' }
    if ($Sandbox) { $list += '-Sandbox' }
    if ($Confirmed) { $list += '-Confirmed' }
    return $list
}

# 1) Este script está dentro da pasta que será apagada: continua a partir de uma cópia temporária.
if (-not $FromTemp) {
    $temp = Join-Path $env:TEMP "pm-uninstall-$([guid]::NewGuid().ToString('N')).ps1"
    Copy-Item $PSCommandPath $temp
    $args1 = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden', '-File', "`"$temp`"", '-FromTemp', '-InstallDir', "`"$InstallDir`"")
    if ($Silent) { $args1 += '-Silent' }
    if ($RemoveData) { $args1 += '-RemoveData' }
    if ($Sandbox) { $args1 += '-Sandbox' }
    Start-Process powershell.exe -ArgumentList $args1
    exit 0
}

# 2) Confirmações
if (-not $Silent -and -not $Confirmed) {
    $remove = [System.Windows.Forms.MessageBox]::Show("Deseja remover o $product deste computador?", $product, 'YesNo', 'Question')
    if ($remove -ne 'Yes') { exit 0 }

    $keep = [System.Windows.Forms.MessageBox]::Show(
        "Deseja manter seus dados (demandas, anotações e configurações)?`n`nSim: mantém, e eles voltam se você reinstalar.`nNão: apaga tudo permanentemente.",
        $product, 'YesNoCancel', 'Question')
    if ($keep -eq 'Cancel') { exit 0 }
    if ($keep -eq 'No') { $RemoveData = [switch]$true }
    $Confirmed = [switch]$true
}

# 3) Permissão de administrador quando a pasta exige (ex.: Arquivos de Programas)
if (Test-NeedsAdmin) {
    try {
        Start-Process powershell.exe -Verb RunAs -ArgumentList (Get-SelfArguments)
    }
    catch {
        Show-Message 'A permissão de administrador é necessária para remover o programa desta pasta. Nada foi alterado.' 'Warning'
    }
    exit 0
}

# 4) Remoção
try {
    $prefix = $InstallDir + '\'
    Get-Process -Name 'ProductivityMonitor' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) } |
        Stop-Process -Force
    Start-Sleep -Milliseconds 800

    for ($i = 0; $i -lt 5 -and (Test-Path $InstallDir); $i++) {
        try { Remove-Item $InstallDir -Recurse -Force -ErrorAction Stop }
        catch { Start-Sleep -Seconds 1 }
    }
    if (Test-Path $InstallDir) { throw "Não foi possível remover a pasta $InstallDir (algum arquivo está em uso)." }

    $links = @(
        (Join-Path ([Environment]::GetFolderPath('CommonPrograms')) $shortcut),
        (Join-Path ([Environment]::GetFolderPath('Programs')) $shortcut),
        (Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) $shortcut),
        (Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) $shortcut)
    )
    foreach ($link in $links) { if (Test-Path $link) { Remove-Item $link -Force -ErrorAction SilentlyContinue } }

    foreach ($hive in 'HKLM', 'HKCU') {
        $path = "${hive}:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$keyName"
        if (Test-Path $path) { Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue }
    }

    if (-not $Sandbox) {
        $run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
        $value = (Get-ItemProperty $run -Name ProductivityMonitor -ErrorAction SilentlyContinue).ProductivityMonitor
        if ($value -and $value.ToString().IndexOf($InstallDir, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            Remove-ItemProperty -Path $run -Name ProductivityMonitor -ErrorAction SilentlyContinue
        }

        if ($RemoveData) {
            $data = Join-Path $env:LOCALAPPDATA 'ProductivityMonitor'
            if (Test-Path $data) { Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue }
        }
    }

    $kept = if ($RemoveData -or $Sandbox) { '' } else { "`n`nSeus dados foram mantidos em %LOCALAPPDATA%\ProductivityMonitor." }
    Show-Message "O $product foi removido.$kept"
}
catch {
    Show-Message "Não foi possível concluir a remoção:`n$($_.Exception.Message)" 'Error'
    exit 1
}
finally {
    # Remove esta cópia temporária depois que o PowerShell encerrar.
    if ($PSCommandPath -and $PSCommandPath.StartsWith($env:TEMP, [StringComparison]::OrdinalIgnoreCase)) {
        Start-Process cmd.exe -WindowStyle Hidden -ArgumentList "/c ping 127.0.0.1 -n 3 >nul & del /f /q `"$PSCommandPath`""
    }
}
