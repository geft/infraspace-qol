@echo off
where pwsh >nul 2>nul
if errorlevel 1 (
    echo PowerShell 7 is required. Install it from https://aka.ms/powershell-release
    pause
    exit /b 1
)
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
set "BUILD_RESULT=%errorlevel%"
pause
exit /b %BUILD_RESULT%
