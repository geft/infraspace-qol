@echo off
setlocal

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo .NET Framework C# compiler was not found.
    exit /b 1
)

set "OUT=%~dp0bin\Release"
if not exist "%OUT%" mkdir "%OUT%"

"%CSC%" /nologo /target:winexe /optimize+ /out:"%OUT%\InfraSpaceQoLInstaller.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "%~dp0Program.cs"
if errorlevel 1 exit /b 1

echo Built %OUT%\InfraSpaceQoLInstaller.exe
