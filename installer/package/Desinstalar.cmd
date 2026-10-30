@echo off
chcp 65001 >nul
title Desinstalar - Monitor de produtividade
set /p KEEP=Deseja manter seus dados e historico? (S/N): 
if /i "%KEEP%"=="N" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\uninstall.ps1" -RemoveData
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\uninstall.ps1"
)
echo.
pause