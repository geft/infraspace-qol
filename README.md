# InfraSpace QoL Mod

InfraSpace QoL adds `4x` Very Fast and `8x` Superspeed options, improves Collection Center supply, configures Red Science consumption and output, automatically releases factory workers when every output buffer is full, expands terraformer coverage as local terrain saturates, and automates laser obstacle clearing. These automations are enabled by default; the Red Science cost multiplier defaults to `0.5`.

The speed controls use three chevrons for `4x` and four for `8x`, with distinct normal, hover, and selected appearances. Both use the game's native simulation speeds and retain the selected speed when pausing and resuming. Their icon assets are embedded in the mod DLL.

Storehouses accept fixed resource categories, including concrete; there is no material selector. Direct supply routes stock to Storehouses over roads after factory and research lab requests. Labs can receive their currently requested science packs directly from Collection Centers without a Distribution Center; existing road, district, and capacity checks apply.

Storehouses receive overflow only: the supplying producer's output buffer or Collection/Distribution Center's stock must be at least 90% full before scheduling a storage delivery, with a full truck available. Adamantite (called `adamantine` internally) is exempt from the 90% threshold and can ship whenever a full truckload is available, including from Collection and Distribution Centers. Only resources accepted by the Storehouse are affected. Goods already in transit do not count toward the supplier's threshold. Factory and lab deliveries remain eligible below this threshold. The overflow rule and adamantite exemption also apply when direct supply is disabled.

Spaceship repairs can receive their final items even when fewer than a full truckload remain. Direct Collection Center shipments carry the exact remaining amount; Distribution Centers retain the game's final-load behavior. Items already in transit count toward repair demand.

Collection Centers also supply construction and upgrade stages, including the Adamantine Drill, before surplus storage. Demand subtracts materials already accumulated by the stage, arrived stock, and incoming trucks. The final direct shipment carries only the remaining amount, including a single radiation core when needed.

Producer deliveries into Collection Centers enforce the center's current collection radius at dispatch. Stale producer connections outside that radius are removed before choosing a destination. The radius uses horizontal distance between building positions, as in the game; roads can still take a longer route. This check remains enabled when direct supply is disabled. Existing trucks keep their destinations.

Collection Centers, Distribution Centers, and ordinary producers prefer the nearest eligible Storehouse by road route length. A closer Storehouse must accept the resource, allow the supplier's district, have a usable road route, and have room for a full truck including deliveries already on the way. Native supplier radius and connection rules still apply. Trucks already dispatched keep their destinations.

## Configure

The Magnetic Field Generator upfront placement cost is **75 adamantite instead of 250**. This applies when loading existing saves as well as starting a new game. Other materials and subsequent construction stages are unchanged. Previously spent placement materials are not refunded.

Terraformers automatically double their effective radius once at least **95% of the playable terrain cells inside their current radius reach 99% total terraforming progress**. Small unfinished edges or holes no longer block expansion. Coverage grows in steps: `1x → 2x → 4x → 8x`, stopping once it covers the playable map. Each larger circle must meet the same 95% area threshold before the radius doubles again. This requires enough combined terraforming types and efficiency to reach 99%; a lone early-game terraforming type cannot trigger expansion. Buildings still need their normal inputs, power, and staffing. Expanded coverage uses the game's normal terraforming rate and contribution calculations. Building range indicators continue to show their original radius.

Expansion is reconstructed from terrain progress, including when loading an existing save. If more than 5% of a circle falls below 99% progress, coverage can shrink again, and the game can then decay unsupported terrain. Set `Terraforming.ExpandWhenLocalAreaSaturated` to `false` and restart to restore normal coverage. Expansion defaults to `true`, including with an older settings file that has no Terraforming section.

Factories automatically release their workers when **every output buffer is completely full**. A water-and-carbon producer keeps workers if either output has room. When any output has room again, the factory becomes eligible for workers on the next employment pass; actual staffing depends on available workers and your current resource workforce priorities. Manual output reductions, including changes made while a factory is idle, remain in effect. The mod never changes these priorities or the factory's normal worker capacity. Storage upgrades count toward the full-buffer check. Power producers, terraforming, spaceship work, and buildings without stored outputs are excluded.

Use the installer's feature tabs to adjust every feature, or edit `settings.json` beside the mod DLL, then restart the game. All feature toggles default to `true`; older settings files automatically receive defaults for missing options.

```json
{
  "Speed": { "ExtraSpeedControls": true },
  "Logistics": {
    "DirectCollectionSupply": true,
    "OverflowOnlyStorage": true,
    "AdamantiteStorageExemption": true,
    "PreferNearestStorehouse": true,
    "EnforceCollectionRadius": true,
    "AllowPartialFinalLoads": true
  },
  "Research": { "RedScienceCostMultiplier": 0.5, "DoubleRedScienceOutput": true },
  "Workforce": { "ReleaseWorkersWhenAllOutputsFull": true },
  "Terraforming": { "ExpandWhenLocalAreaSaturated": true },
  "Laser": { "AutoClearObstacles": true, "SpeedMultiplier": 3 },
  "Monuments": { "ReduceMagneticFieldGeneratorCost": true }
}
```

Set direct supply to `false` to disable it. Set the Red Science multiplier from `0` (free) to `1` (vanilla). Other science costs are unchanged. Previously consumed packs cannot be refunded when enabling the mod on an existing research project.

Additional controls:

| Setting | Effect when disabled |
| --- | --- |
| `Speed.ExtraSpeedControls` | Omit the added 4x and 8x buttons. |
| `Logistics.OverflowOnlyStorage` | Allow full truckloads to Storehouses below the 90% supplier threshold. |
| `Logistics.AdamantiteStorageExemption` | Apply the overflow threshold to adamantite too, when overflow-only storage is enabled. |
| `Logistics.PreferNearestStorehouse` | Leave native storage selection in effect; direct supply can use any eligible Storehouse. |
| `Logistics.EnforceCollectionRadius` | Disable the extra radius check and stale connection cleanup. |
| `Logistics.AllowPartialFinalLoads` | Require full loads for direct construction, upgrade and spaceship repair deliveries; the last few items may then need native supply. |
| `Monuments.ReduceMagneticFieldGeneratorCost` | Restore the generator's upfront placement cost to 250 adamantite. |

Storage selection, overflow rules, and the radius check are independently configurable, including with direct supply disabled. Existing trucks keep their destinations. Disabling a feature takes effect after restarting the game; it does not undo completed construction, research, or cleared obstacles.

The spaceship laser automatically clears clickable, building-blocking environment obstacles across the whole map once its cannon is repaired and at least one targeting station is built. It starts with the nearest obstacle to a targeting station, then picks the nearest remaining obstacle to its last target. Manual targeting queues take priority, and clearing resumes when they finish. Multiple targeting stations share one cannon, so they do not stack damage or firing speed. Removing all targeting stations stops automatic firing. Obstacles the cannon cannot acquire within five game seconds are skipped until you reload the save. Automation defaults to `true`, including with older settings files; set `Laser.AutoClearObstacles` to `false` and restart to disable it.

The laser deals damage and warms up its beam at **3× normal speed**, multiplying its existing speed upgrades. This applies to both manual and automatic firing, even with automation disabled. `Laser.SpeedMultiplier` defaults to `3`, including with older settings files; set it to `1` for normal speed, or a value up to `10`, then restart the game. Target acquisition still uses the game's normal raycasts.

Red Science factories produce **two packs per normal one-pack batch** by default, including with an older settings file. Ingredient amounts, batch time, power, and staffing requirements stay the same. Existing output upgrades are doubled too. Normal output storage limits still apply, and other resource outputs are unchanged. Set `Research.DoubleRedScienceOutput` to `false` and restart to restore normal output. This is independent of `RedScienceCostMultiplier`, which controls packs consumed by research labs.

Set `ReleaseWorkersWhenAllOutputsFull` to `false` to disable automatic worker release. It defaults to `true`, including with an older settings file that has no Workforce section. Settings changes require restarting the game.

## Install from an installer package

If someone shares a locally built installer package with you, you do **not** need this repository, Git, or developer tools:

1. Install InfraSpace.
2. Keep `InfraSpaceQoLInstaller.exe`, `InfraSpaceQoL.dll`, `0Harmony.dll`, `config.json`, and `settings.json` together in one folder.
3. Close InfraSpace and run `InfraSpaceQoLInstaller.exe`.
4. Review the **Speed**, **Logistics**, **Research**, **Workforce**, **Terraforming**, **Laser**, and **Monuments** tabs. Scroll within a tab if needed. Change the checkboxes and numeric values, then click **Install**.
5. Start InfraSpace and enable InfraSpace QoL in the game's Mods menu.

The installer shows the package version and loads installed settings when updating; new installs use the package settings. New options use the same defaults as the mod. **Install** writes the choices shown, preserves unknown settings keys, and keeps the previous settings as `settings.json.bak`. Closing the installer without installing saves nothing. Invalid JSON or setting values block installation with an error so you can correct the file first. Rerun the installer to change settings later, with the game closed.

Recent additions include distinct 4x/8x speed icons, double Red Science factory output, automatic laser obstacle clearing, and adjustable laser speed for both manual and automatic fire. Each now has an installer control, alongside the existing logistics, workforce, terraforming, and monument options.

This repository contains source code; compiled installers and DLLs are not committed. Builds run locally, with no GitHub Actions runner to configure.

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
