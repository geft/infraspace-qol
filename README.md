# InfraSpace QoL Mod

InfraSpace QoL adds a `4x` Very Fast option, improves Collection Center supply, configures Red Science consumption, and automatically releases factory workers when every output buffer is full. Direct supply and worker automation are enabled by default; the Red Science multiplier defaults to `0.5`.

Storehouses accept fixed resource categories, including concrete; there is no material selector. Direct supply routes stock to Storehouses over roads after factory and research lab requests. Labs can receive their currently requested science packs directly from Collection Centers without a Distribution Center; existing road, district, and capacity checks apply.

Storehouses receive overflow only: the supplying producer's output buffer or Collection/Distribution Center's stock must be at least 90% full before scheduling a storage delivery, with a full truck available. Only resources accepted by the Storehouse are affected. Goods already in transit do not count toward the supplier's threshold. Factory and lab deliveries remain eligible below this threshold. The overflow rule also applies when direct supply is disabled.

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

## Install

Install InfraSpace first. You do not need developer tools to install this mod.

1. Open [Actions → Build installer](https://github.com/geft/infraspace-qol/actions/workflows/release.yml), select a successful run, and download its installer artifact. A build becomes available after a maintainer configures the runner described below and runs the workflow.
2. Extract the artifact and keep `InfraSpaceQoLInstaller.exe`, `InfraSpaceQoL.dll`, `0Harmony.dll`, `config.json`, and `settings.json` together.
3. Close InfraSpace, run `InfraSpaceQoLInstaller.exe`, and click **Install**. The installer displays the package version and preserves your existing settings.
4. Start InfraSpace and enable InfraSpace QoL in the game's Mods menu.

For a local build, run the installer from `installer\bin\Release`. Installers and compiled files are generated outputs; they are not committed to this repository.

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

For local development, `pwsh -NoProfile -File .\build.ps1` builds the mod, runs regression checks, and refreshes the local installer **without changing the version**. It accepts `-DotNetPath`, `-ManagedDir`, and `-HarmonyPath`, like the release script. Direct `dotnet build` remains available for faster compile-only iterations.

For updates, run the release script with PowerShell 7:

```powershell
pwsh -NoProfile -File .\release.ps1
```

Each successful local release increments the patch version (for example, `1.1.2` to `1.1.3`), builds the mod, runs regression checks, and refreshes `installer\bin\Release` with the installer and matching mod files. No ZIP is created. `config.json` is the version source for the mod DLL, installer, and installed metadata. The source version is restored if a step fails. Ordinary `dotnet build` runs keep the version unchanged.

Use `-Increment Minor` or `-Increment Major` for larger releases. `-DotNetPath`, `-ManagedDir`, and `-HarmonyPath` select a particular SDK or game dependency location; Harmony defaults to `ISModKitDir` or the existing build's DLL. Run `installer\build.bat` to refresh only the installer and its files without incrementing the version; it rejects a mod DLL whose version does not match `config.json`.

## GitHub Actions

The **Build installer** workflow builds and tests the installer on a Windows self-hosted runner with InfraSpace installed. GitHub-hosted machines do not have the required game assemblies. Register a runner with the additional `infraspace` label, install PowerShell 7 and the .NET Framework 4.7.2 targeting pack, and set repository variable `INFRASPACE_HARMONY_PATH` to the runner's ISModKit `0Harmony.dll`. Set `INFRASPACE_MANAGED_DIR` if the game uses a different install location. The workflow installs the .NET SDK. See [GitHub's runner setup documentation](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners).

Open **Actions → Build installer → Run workflow** on `main`. Its patch version is the source patch plus the workflow run number; reruns keep the same version. Version updates exist only in the runner checkout, and the workflow does not push commits. Local dev builds keep the source version; local `release.ps1` still increments it, or accepts `-Version major.minor.patch` explicitly.

Download the resulting installer artifact from the workflow run and keep its five files together. Users need an installed copy of InfraSpace, but no SDK, runner, or separately downloaded game DLLs. GitHub packages Actions artifact downloads as ZIPs; this project does not create a separate release ZIP. The artifact contains only the installer, mod DLL, Harmony DLL, and two JSON files. The game reference assemblies stay on the build machine. The workflow uploads an artifact; it does not publish a GitHub Release.

Build folders, EXEs, DLLs, and ZIPs are ignored by Git. Commit the source, scripts, and workflow. Builds on the self-hosted machine are manually triggered on `main`; pull requests do not run code on that machine.
