# InfraSpace QoL Mod

InfraSpace QoL adds direct truck supply from Collection Centers to factories and a configurable Red Science consumption rate. Defaults are enabled for direct supply and `0.5` for Red Science.

## Configure

Edit `settings.json` beside the mod DLL, then restart the game:

```json
{
  "Logistics": { "DirectCollectionSupply": true },
  "Research": { "RedScienceCostMultiplier": 0.5 }
}
```

Set direct supply to `false` to disable it. Set the Red Science multiplier from `0` (free) to `1` (vanilla). Other science costs are unchanged. Previously consumed packs cannot be refunded when enabling the mod on an existing research project.

## Install

Extract the release ZIP and run `InfraSpaceQoLInstaller.exe`. It preserves an existing `settings.json`; enable InfraSpace QoL in the game's Mods menu after installation.

## Build

Build with the .NET SDK, the installed game's `InfraSpace_Data\Managed` assemblies, and `0Harmony.dll` from the official [ISModKit](https://github.com/DionicSoftware/ISModKit):

```powershell
dotnet build .\InfraSpaceQoL.csproj -c Release `
  -p:InfraSpaceManagedDir="C:\Program Files (x86)\Steam\steamapps\common\InfraSpace\InfraSpace_Data\Managed" `
  -p:HarmonyPath="C:\path\to\ISModKit\ExampleMods\CryoChambers\0Harmony.dll"
```

The project was built against InfraSpace `1.56.460`. Game updates can change the Harmony patch targets. Direct Collection Center shipments use roads and trucks; rail freight is not covered.

Run the regression checks with PowerShell 7 after building:

```powershell
pwsh -NoProfile -File .\tests\Regression.ps1
```

Build the Windows installer with `installer\build.bat`.
