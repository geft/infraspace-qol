#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$DotNetPath = 'dotnet',
    [string]$ManagedDir,
    [string]$HarmonyPath
)

$ErrorActionPreference = 'Stop'
if (!(Get-Command $DotNetPath -ErrorAction SilentlyContinue)) {
    throw 'Install the .NET SDK from https://dotnet.microsoft.com/download or select dotnet.exe with -DotNetPath.'
}
$sdkList = & $DotNetPath --list-sdks
if ($LASTEXITCODE -ne 0 -or !$sdkList) { throw 'A .NET SDK is required. Use -DotNetPath to select its dotnet.exe.' }
if (!$ManagedDir) {
    $steamPaths = @((Join-Path ${env:ProgramFiles(x86)} 'Steam'))
    $steamRegistry = Get-ItemProperty 'HKCU:/Software/Valve/Steam' -ErrorAction SilentlyContinue
    if ($steamRegistry.SteamPath) { $steamPaths += $steamRegistry.SteamPath }
    $libraries = @($steamPaths)
    foreach ($steam in ($steamPaths | Select-Object -Unique)) {
        $libraryFile = Join-Path $steam 'steamapps/libraryfolders.vdf'
        if (Test-Path -LiteralPath $libraryFile) {
            foreach ($match in [regex]::Matches([IO.File]::ReadAllText($libraryFile), '"path"\s+"([^"]+)"')) {
                $libraries += $match.Groups[1].Value.Replace('\\', '\')
            }
        }
    }
    $ManagedDir = $libraries | Select-Object -Unique | ForEach-Object {
        Join-Path $_ 'steamapps/common/InfraSpace/InfraSpace_Data/Managed'
    } | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'old.dll') } | Select-Object -First 1
    if (!$ManagedDir) { throw 'InfraSpace was not found. Install it with Steam or pass -ManagedDir pointing to InfraSpace_Data/Managed.' }
}
if (!(Test-Path -LiteralPath (Join-Path $ManagedDir 'old.dll'))) { throw 'Use -ManagedDir to select the game Managed directory.' }
if (!$HarmonyPath) {
    $candidates = @()
    if ($env:ISModKitDir) { $candidates += Join-Path $env:ISModKitDir 'ExampleMods/CryoChambers/0Harmony.dll' }
    $candidates += Join-Path $PSScriptRoot 'bin/Release/net472/0Harmony.dll'
    $HarmonyPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (!$HarmonyPath) {
        $dependencyDirectory = Join-Path $PSScriptRoot 'obj/dependencies'
        New-Item -ItemType Directory -Path $dependencyDirectory -Force | Out-Null
        $HarmonyPath = Join-Path $dependencyDirectory '0Harmony.dll'
        $expectedHash = '19CE60AC3280F72EC1751D36A40CB7E2FECE2934DF8345969DC7FEB83BD633E4'
        if (!(Test-Path -LiteralPath $HarmonyPath) -or (Get-FileHash -LiteralPath $HarmonyPath).Hash -ne $expectedHash) {
            Write-Output 'Downloading Harmony from the official ISModKit (pinned revision)...'
            Invoke-WebRequest 'https://raw.githubusercontent.com/DionicSoftware/ISModKit/4700f3545e201e11fe34e77b3b511daec035795c/ExampleMods/CryoChambers/0Harmony.dll' -OutFile $HarmonyPath
        }
        if ((Get-FileHash -LiteralPath $HarmonyPath).Hash -ne $expectedHash) { throw 'Harmony download checksum did not match. Use -HarmonyPath to supply it manually.' }
    }
}
if (!$HarmonyPath -or !(Test-Path -LiteralPath $HarmonyPath)) { throw 'Use -HarmonyPath to select the ISModKit 0Harmony.dll.' }
$HarmonyPath = (Resolve-Path -LiteralPath $HarmonyPath).Path
$pwshPath = (Get-Process -Id $PID).Path
& $DotNetPath build (Join-Path $PSScriptRoot 'InfraSpaceQoL.csproj') -c Release "-p:InfraSpaceManagedDir=$ManagedDir" "-p:HarmonyPath=$HarmonyPath"
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
& $pwshPath -NoProfile -File (Join-Path $PSScriptRoot 'tests/Regression.ps1') -ManagedDir $ManagedDir
if ($LASTEXITCODE -ne 0) { throw 'Regression checks failed.' }
& $pwshPath -NoProfile -File (Join-Path $PSScriptRoot 'tests/SpeedRegression.ps1') -ManagedDir $ManagedDir
if ($LASTEXITCODE -ne 0) { throw 'Speed regression checks failed.' }
& $pwshPath -NoProfile -File (Join-Path $PSScriptRoot 'installer/build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
& $pwshPath -NoProfile -File (Join-Path $PSScriptRoot 'tests/InstallerRegression.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Installer regression checks failed.' }
Write-Output "Ready to install: $(Join-Path $PSScriptRoot 'installer/bin/Release/InfraSpaceQoLInstaller.exe')"
