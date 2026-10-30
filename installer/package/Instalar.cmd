@echo off
chcp 65001 >nul
title Instalar - Monitor de produtividade
if not exist "%~dp0app\ProductivityMonitor.exe" (
    echo Nao foi possivel encontrar os arquivos do programa.
    echo Extraia TODO o conteudo do .zip para uma pasta e execute este arquivo novamente.
    echo.
    pause
    exit /b 1
)
echo Instalando o Monitor de produtividade...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\install.ps1" -Desktop
if errorlevel 1 (
    echo.
    echo A instalacao falhou. Consulte a mensagem acima.
) else (
    echo.
    echo Instalacao concluida. O programa foi aberto e fica na bandeja do Windows.
)
echo.
pause