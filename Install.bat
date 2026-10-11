@echo off
setlocal

set "INSTALLER=%~dp0Install.ps1"
if not exist "%INSTALLER%" (
    echo Install.ps1 was not found beside this file.
    pause
    exit /b 1
)

start "" powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File "%INSTALLER%"
exit /b 0
