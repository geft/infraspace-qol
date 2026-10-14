#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$DotNetPath = 'dotnet',
    [string]$ManagedDir = 'C:\Program Files (x86)\Steam\steamapps\common\InfraSpace\InfraSpace_Data\Managed',
    [string]$HarmonyPath
)

$ErrorActionPreference = 'Stop'
$sdkList = & $DotNetPath --list-sdks
if ($LASTEXITCODE -ne 0 -or !$sdkList) { throw 'A .NET SDK is required. Use -DotNetPath to select its dotnet.exe.' }
if (!(Test-Path -LiteralPath (Join-Path $ManagedDir 'old.dll'))) { throw 'Use -ManagedDir to select the game Managed directory.' }
if (!$HarmonyPath) {
    $candidates = @()
    if ($env:ISModKitDir) { $candidates += Join-Path $env:ISModKitDir 'ExampleMods/CryoChambers/0Harmony.dll' }
    $candidates += Join-Path $PSScriptRoot 'bin/Release/net472/0Harmony.dll'
    $HarmonyPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$HarmonyPath -or !(Test-Path -LiteralPath $HarmonyPath)) { throw 'Use -HarmonyPath to select the ISModKit 0Harmony.dll.' }
$HarmonyPath = (Resolve-Path -LiteralPath $HarmonyPath).Path
$pwshPath = (Get-Process -Id $PID).Path
& $DotNetPath build (Join-Path $PSScriptRoot 'InfraSpaceQoL.csproj') -c Release "-p:InfraSpaceManagedDir=$ManagedDir" "-p:HarmonyPath=$HarmonyPath"
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
& $pwshPath -NoProfile -File (Join-Path $PSScriptRoot 'tests/Regression.ps1') -ManagedDir $ManagedDir
if ($LASTEXITCODE -ne 0) { throw 'Regression checks failed.' }
& $pwshPath -NoProfile -File (Join-Path $PSScriptRoot 'installer/build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
