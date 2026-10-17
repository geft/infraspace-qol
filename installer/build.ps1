#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = (Get-Content -LiteralPath (Join-Path $root 'config.json') -Raw | ConvertFrom-Json).version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid mod version in config.json.' }
$modOutput = Join-Path $root 'bin/Release/net472'
$modVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $modOutput 'InfraSpaceQoL.dll')).Version.ToString(3)
if ($modVersion -ne $version) { throw "Mod DLL is $modVersion but config.json is $version. Build the mod first." }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe' }
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler was not found.' }
$output = Join-Path $PSScriptRoot 'bin/Release'
$intermediate = Join-Path $PSScriptRoot 'obj'
New-Item -ItemType Directory -Path $output, $intermediate -Force | Out-Null
$versionSource = Join-Path $intermediate 'Version.cs'
@"
using System.Reflection;
[assembly: AssemblyVersion("$version.0")]
[assembly: AssemblyFileVersion("$version.0")]
[assembly: AssemblyInformationalVersion("$version")]
"@ | Set-Content -LiteralPath $versionSource -Encoding utf8
& $compiler /nologo /target:winexe /optimize+ "/out:$(Join-Path $output 'InfraSpaceQoLInstaller.exe')" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'Program.cs') (Join-Path $PSScriptRoot 'SettingsEditor.cs') (Join-Path $root 'src/SettingsOptions.cs') $versionSource
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
foreach ($file in @('InfraSpaceQoL.dll', '0Harmony.dll', 'settings.json')) {
    Copy-Item -LiteralPath (Join-Path $modOutput $file) -Destination $output -Force
}
Copy-Item -LiteralPath (Join-Path $root 'config.json') -Destination $output -Force
Write-Output "Built installer $version with matching mod files: $(Join-Path $output 'InfraSpaceQoLInstaller.exe')"
