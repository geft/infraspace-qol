#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Patch', 'Minor', 'Major')]
    [string]$Increment = 'Patch',
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,
    [string]$DotNetPath = 'dotnet',
    [string]$ManagedDir = 'C:\Program Files (x86)\Steam\steamapps\common\InfraSpace\InfraSpace_Data\Managed',
    [string]$HarmonyPath
)

$ErrorActionPreference = 'Stop'
$configPath = Join-Path $PSScriptRoot 'config.json'
$originalConfig = [IO.File]::ReadAllBytes($configPath)
$configText = [IO.File]::ReadAllText($configPath)
$config = $configText | ConvertFrom-Json
if ($config.version -notmatch '^\d+\.\d+\.\d+$') { throw 'config.json must have a major.minor.patch version.' }
$current = [version]$config.version
$nextVersion = switch ($Increment) {
    'Major' { '{0}.0.0' -f ($current.Major + 1) }
    'Minor' { '{0}.{1}.0' -f $current.Major, ($current.Minor + 1) }
    'Patch' { '{0}.{1}.{2}' -f $current.Major, $current.Minor, ($current.Build + 1) }
}
if ($Version) { $nextVersion = $Version }

$versionPattern = [regex]'("version"\s*:\s*")[^"]+(")'
$newConfig = $versionPattern.Replace($configText, { param($match) $match.Groups[1].Value + $nextVersion + $match.Groups[2].Value }, 1)
try {
    [IO.File]::WriteAllText($configPath, $newConfig, [Text.UTF8Encoding]::new($false))
    Write-Output "Building release $($config.version) -> $nextVersion"
    & (Join-Path $PSScriptRoot 'build.ps1') -DotNetPath $DotNetPath -ManagedDir $ManagedDir -HarmonyPath $HarmonyPath
}
catch {
    [IO.File]::WriteAllBytes($configPath, $originalConfig)
    throw
}
Write-Output "Release $nextVersion ready: $(Join-Path $PSScriptRoot 'installer/bin/Release/InfraSpaceQoLInstaller.exe')"
