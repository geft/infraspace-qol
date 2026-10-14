# InfraSpace QoL Mod

InfraSpace QoL adds a `4x` Very Fast option, improves Collection Center supply, configures Red Science consumption, and automatically releases factory workers when every output buffer is full. Direct supply and worker automation are enabled by default; the Red Science multiplier defaults to `0.5`.

Storehouses accept fixed resource categories, including concrete; there is no material selector. Direct supply routes stock to Storehouses over roads after factory and research lab requests. Labs can receive their currently requested science packs directly from Collection Centers without a Distribution Center; existing road, district, and capacity checks apply.

Storehouses receive overflow only: the supplying producer's output buffer or Collection/Distribution Center's stock must be at least 90% full before scheduling a storage delivery, with a full truck available. Only resources accepted by the Storehouse are affected. Goods already in transit do not count toward the supplier's threshold. Factory and lab deliveries remain eligible below this threshold. The overflow rule also applies when direct supply is disabled.

Spaceship repairs can receive their final items even when fewer than a full truckload remain. Direct Collection Center shipments carry the exact remaining amount; Distribution Centers retain the game's final-load behavior. Items already in transit count toward repair demand.

Producer deliveries into Collection Centers enforce the center's current collection radius at dispatch. Stale producer connections outside that radius are removed before choosing a destination. The radius uses horizontal distance between building positions, as in the game; roads can still take a longer route. This check remains enabled when direct supply is disabled. Existing trucks keep their destinations.

Collection Centers, Distribution Centers, and ordinary producers prefer the nearest eligible Storehouse by road route length. A closer Storehouse must accept the resource, allow the supplier's district, have a usable road route, and have room for a full truck including deliveries already on the way. Native supplier radius and connection rules still apply. Trucks already dispatched keep their destinations.

## Configure

Factories automatically release their workers when **every output buffer is completely full**. A water-and-carbon producer keeps workers if either output has room. When any output has room again, the factory becomes eligible for workers on the next employment pass; actual staffing depends on available workers and your current resource workforce priorities. Manual output reductions, including changes made while a factory is idle, remain in effect. The mod never changes these priorities or the factory's normal worker capacity. Storage upgrades count toward the full-buffer check. Power producers, terraforming, spaceship work, and buildings without stored outputs are excluded.

Edit `settings.json` beside the mod DLL, then restart the game:

```json
{
  "Logistics": { "DirectCollectionSupply": true },
  "Research": { "RedScienceCostMultiplier": 0.5 },
  "Workforce": { "ReleaseWorkersWhenAllOutputsFull": true }
}
```

Set direct supply to `false` to disable it. Set the Red Science multiplier from `0` (free) to `1` (vanilla). Other science costs are unchanged. Previously consumed packs cannot be refunded when enabling the mod on an existing research project.

Set `ReleaseWorkersWhenAllOutputsFull` to `false` to disable automatic worker release. It defaults to `true`, including with an older settings file that has no Workforce section. Settings changes require restarting the game.

## Install from an installer package

If someone shares a locally built installer package with you, you do **not** need this repository, Git, or developer tools:

1. Install InfraSpace.
2. Keep `InfraSpaceQoLInstaller.exe`, `InfraSpaceQoL.dll`, `0Harmony.dll`, `config.json`, and `settings.json` together in one folder.
3. Close InfraSpace, run `InfraSpaceQoLInstaller.exe`, and click **Install**.
4. Start InfraSpace and enable InfraSpace QoL in the game's Mods menu.

The installer shows the package version and preserves existing settings when updating. This repository contains source code; compiled installers and DLLs are not committed. Builds run locally, with no GitHub Actions runner to configure.

## Build your own copy

Git is optional: use GitHub's **Code → Download ZIP**, extract the source, and open the extracted folder. You can also clone the repository if you prefer.

One-time requirements on Windows:

- Install InfraSpace through Steam.
- Install [PowerShell 7](https://aka.ms/powershell-release).
- Install a [.NET SDK](https://dotnet.microsoft.com/download), version 8 or newer, and the [.NET Framework 4.7.2 developer pack](https://dotnet.microsoft.com/download/dotnet-framework/net472).

Double-click **`build.cmd`**, or run this from the source folder in PowerShell 7:

```powershell
pwsh -NoProfile -File .\build.ps1
```

The script finds InfraSpace in your Steam libraries, downloads the pinned Harmony DLL from the official [ISModKit](https://github.com/DionicSoftware/ISModKit) if needed, checks its SHA-256 checksum, builds the mod, runs regression checks, and prepares the installer. It reads game reference DLLs from your own installation. The first build requires internet access for Harmony and .NET package restore.

After the build succeeds, close InfraSpace and run:

```text
installer\bin\Release\InfraSpaceQoLInstaller.exe
```

Local development builds keep the version unchanged. The installer folder contains the five files listed above; share that complete folder to let others install without building. The game reference assemblies are not included.

For a custom install location or an offline Harmony copy, override the paths:

```powershell
pwsh -NoProfile -File .\build.ps1 `
  -ManagedDir "D:\SteamLibrary\steamapps\common\InfraSpace\InfraSpace_Data\Managed" `
  -HarmonyPath "C:\path\to\0Harmony.dll"
```

`-DotNetPath` selects a particular `dotnet.exe`. Harmony can also be found through the `ISModKitDir` environment variable or reused from an existing build. Nothing is installed into the game until you run the installer.

The mod is built against InfraSpace `1.56.460`. Game updates can change Harmony patch targets. Direct Collection Center shipments use roads and trucks; rail freight is not covered.

## Local development and versioning

For compile-only iterations, use `dotnet build` directly with your game assembly and Harmony paths:

```powershell
dotnet build .\InfraSpaceQoL.csproj -c Release `
  -p:InfraSpaceManagedDir="C:\Program Files (x86)\Steam\steamapps\common\InfraSpace\InfraSpace_Data\Managed" `
  -p:HarmonyPath="C:\path\to\0Harmony.dll"
```

Run `pwsh -NoProfile -File .\tests\Regression.ps1` after building, or use `build.ps1` to build and test together. Run `installer\build.bat` to refresh only the installer; it rejects a DLL whose version does not match `config.json`.

To prepare a new version locally:

```powershell
pwsh -NoProfile -File .\release.ps1
```

Each successful release increments the patch version (for example, `1.1.2` to `1.1.3`), builds and tests, and refreshes `installer\bin\Release`. Use `-Increment Minor`, `-Increment Major`, or `-Version major.minor.patch` when appropriate. It accepts the same dependency paths as `build.ps1`. `config.json` is the version source for the DLL, installer, and installed metadata. The source version is restored if a step fails. No ZIP is created.

Commit source code, scripts, and version metadata. Build folders, EXEs, DLLs, and ZIPs are ignored by Git. Game assemblies and downloaded dependencies remain on the local machine.
