$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$intermediate = Join-Path $root 'installer/obj'
New-Item -ItemType Directory -Path $intermediate -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe' }
$executable = Join-Path $intermediate 'InstallerSettingsSmoke.exe'
& $compiler /nologo /target:exe /main:InstallerSettingsSmoke "/out:$executable" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll (Join-Path $root 'installer/Program.cs') (Join-Path $root 'installer/SettingsEditor.cs') (Join-Path $root 'src/SettingsOptions.cs') (Join-Path $PSScriptRoot 'InstallerSettingsSmoke.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer smoke compilation failed.' }
& $executable (Join-Path $root 'installer/bin/Release') (Join-Path $intermediate 'installer-preview.png')
if ($LASTEXITCODE -ne 0) { throw 'Installer settings smoke failed.' }
