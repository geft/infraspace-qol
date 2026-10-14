# InfraSpace QoL Mod

InfraSpace QoL adds a `4x` Very Fast option to the dashboard speed controls, sends Collection Center stock to factory inputs first and then to Storehouses with room, and configures Red Science consumption. Direct supply is enabled by default; the Red Science multiplier defaults to `0.5`.

Storehouses accept fixed resource categories, including concrete; there is no material selector. Direct supply routes stock to Storehouses over roads after factory requests.

Producer deliveries into Collection Centers enforce the center's current collection radius at dispatch. Stale producer connections outside that radius are removed before choosing a destination. The radius uses horizontal distance between building positions, as in the game; roads can still take a longer route. This check remains enabled when direct supply is disabled. Existing trucks keep their destinations.

Collection Centers, Distribution Centers, and ordinary producers prefer the nearest eligible Storehouse by road route length. A closer Storehouse must accept the resource, allow the supplier's district, have a usable road route, and have room for a full truck including deliveries already on the way. Native supplier radius and connection rules still apply. Trucks already dispatched keep their destinations.

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
