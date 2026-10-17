#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$packageDirectory = Join-Path $PSScriptRoot 'installer/bin/Release'
$version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'config.json') -Raw | ConvertFrom-Json).version
$files = @('InfraSpaceQoLInstaller.exe', 'InfraSpaceQoL.dll', '0Harmony.dll', 'config.json', 'settings.json') |
    ForEach-Object { Join-Path $packageDirectory $_ }
foreach ($file in $files) {
    if (!(Test-Path -LiteralPath $file)) { throw "Missing package file: $file. Run build.ps1 first." }
}
$builtVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $packageDirectory 'InfraSpaceQoL.dll')).Version.ToString(3)
$installerVersion = (Get-Item -LiteralPath (Join-Path $packageDirectory 'InfraSpaceQoLInstaller.exe')).VersionInfo.FileVersion
$packagedVersion = (Get-Content -LiteralPath (Join-Path $packageDirectory 'config.json') -Raw | ConvertFrom-Json).version
if ($builtVersion -ne $version -or $packagedVersion -ne $version -or $installerVersion -ne "$version.0") {
    throw 'Package versions do not match config.json. Run build.ps1 first.'
}
$instructionsDirectory = Join-Path $PSScriptRoot 'installer/obj'
New-Item -ItemType Directory -Path $instructionsDirectory -Force | Out-Null
$instructions = Join-Path $instructionsDirectory 'INSTALL.txt'
@"
InfraSpace QoL $version for Windows

1. Extract all files from this ZIP into one folder.
2. Close InfraSpace.
3. Run InfraSpaceQoLInstaller.exe, choose your settings, and click Install.
4. Start InfraSpace and enable InfraSpace QoL in the Mods menu.

Keep the installer, both DLLs, config.json, and settings.json together.
No Git checkout, SDK, or build tools are needed.

Updates and user guide: https://github.com/geft/infraspace-qol
"@ | Set-Content -LiteralPath $instructions -Encoding utf8
# Keep this filename stable for GitHub's /releases/latest/download/ link.
$archive = Join-Path $PSScriptRoot 'installer/bin/InfraSpaceQoL-Windows.zip'
Compress-Archive -LiteralPath ($files + $instructions) -DestinationPath $archive -Force
Write-Output "Release download ready: $archive"
