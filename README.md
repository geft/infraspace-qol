# InfraSpace QoL Mod

This mod contains two independent options: direct Collection Center supply for factories and a Red Science consumption multiplier. Defaults are enabled for direct supply and `0.5` for Red Science.

## Implementation

### Direct Collection Center supply

The mod records factory input requests when the game's `ConsumerProducer.RequestResources` method refreshes them. A postfix on `OrderManager.UpdateSimulationThread` handles only those queued requests, after native factory suppliers have had their normal dispatch opportunity. It checks each Collection Center storing the requested resource, district export rules, a valid `PathFinderFacadeRoads` path, and the full native truck order quantity. Collection Centers are grouped by resource once per dispatch batch. Centers are ordered by summed native route distance (`PathPart.GetPathDist`), with the next center tried if a nearer center lacks stock or cannot dispatch.

If a truck cannot enter its path, its incoming reservation is removed immediately and source inventory is refunded before trying the next center; native car destruction still uses deferred cleanup. Successful dispatch creates a normal game `Car`, reserves Collection Center inventory immediately, and registers the factory's in-flight car through the game's normal constructor path. The existing Collection Center-to-Distribution Center dispatch and Distribution Center dispatch are kept. When the direct option is enabled, Collection Center inventory dispatch is serialized with the new route, and a Distribution Center rechecks its destination's remaining input capacity and in-flight cars before sending.

Factories without a viable Collection Center shipment still receive normal factory-to-factory deliveries through the game's order manager. Direct delivery uses one full native order at a time; if no reachable center has enough for one order, it waits for stock or the normal supplier path. After a direct shipment, the game's per-consumer supplier cooldown can delay another supplier dispatch for 50 simulation frames.

Direct Collection Center shipments use the game's road pathfinder and native truck (`Car`) dispatch. Rail freight dispatch is not implemented by this feature; factories that rely on rail still need their existing rail logistics setup.

### Red Science

`TechnologyDescription` contains a shared research-point cost and a list of required science resource types. The game consumes one unit of every listed science resource per research point, so changing the shared cost would also change Blue, Green, or Yellow requirements on mixed-science technologies.

The mod patches `ResearchProductionLogic.UpdateSimulationThread` for technologies whose `requiredResources` contains the game's Red Science resource (`sciencePack4`). Red packs are consumed according to the cumulative ceiling `ceil(progress × multiplier)`. This produces exactly `ceil(original red pack count × multiplier)` for new research, while other listed packs continue to be consumed once per research point. The patch synchronizes with `ResearchManager.UpdateFrameUnity` and `SetCurrentResearch` while reading and queuing progress, so a stale progress count cannot repeat a Red Science step when multiple simulation ticks accumulate. No technology data or prerequisites are rewritten, so loading a save or reopening the research UI cannot apply the reduction repeatedly. A multiplier of `1` uses the original method; `0` also removes Red Science from the lab's resource request list.

The game stores research points in a per-technology progress dictionary, but it does not store how many units of each science resource were spent on an already-progressed technology. Existing progress is retained and future Red Science use follows the configured rate. Packs already consumed before enabling the mod cannot be refunded, so a technology in progress at first activation may not have an exact half-cost total.

## Configuration

Edit `settings.json` beside `InfraSpaceQoL.dll`, then restart InfraSpace:

```json
{
  "Logistics": {
    "DirectCollectionSupply": true
  },
  "Research": {
    "RedScienceCostMultiplier": 0.5
  }
}
```

Set `DirectCollectionSupply` to `false` to disable only the logistics patches. Set `RedScienceCostMultiplier` to `1.0` to use the vanilla research method, or to any value from `0` through `1`. Fractional cumulative costs round up using decimal arithmetic, so values such as `0.07` do not gain an extra pack from floating-point rounding. Settings are read once in the mod's `Load()` method; changes take effect after restarting the game.

`config.json` is the required InfraSpace mod metadata file. `settings.json` is read by this mod; the game does not provide a built-in settings UI for these two options.

## Build

The project targets .NET Framework 4.7.2 and references the installed game's assemblies. The official ISModKit's script-mod example documents the same target and the game's `InfraSpace_Data\Managed` reference directory: [ISModKit](https://github.com/DionicSoftware/ISModKit).

Install a .NET SDK, download the official ISModKit, then build from this directory. Supply the paths for the installed game and the kit's Harmony dependency:

```powershell
dotnet build .\InfraSpaceQoL.csproj -c Release `
  -p:InfraSpaceManagedDir="C:\Program Files (x86)\Steam\steamapps\common\InfraSpace\InfraSpace_Data\Managed" `
  -p:HarmonyPath="C:\path\to\ISModKit\ExampleMods\CryoChambers\0Harmony.dll"
```

The output folder is `bin\Release\net472`. It contains `InfraSpaceQoL.dll`, `0Harmony.dll`, `config.json`, and `settings.json`. The game assemblies are references only and are not copied into the mod.

## Install

1. Create `%USERPROFILE%\AppData\LocalLow\Dionic Software\InfraSpace\mods\InfraSpaceQoL` if it does not exist.
2. Copy the four files from `bin\Release\net472` into that folder.
3. Start InfraSpace and activate **InfraSpaceQoL** in the Mods menu.
4. Start or load a city. The mod writes one load message to `Player.log`; it does not alter game assemblies or existing save files directly.

The official modding tutorial describes the local mods directory and script-mod loading flow: [InfraSpace script mod tutorial](https://forum.dionicsoftware.com/t/infraspace-modding-tutorial-basic-overview-making-your-first-example-mod/3135).

## Compatibility and validation

The local installation inspected for this build is InfraSpace `1.56.460` on Unity `2022.3.62f2`. The mod was compiled against that installation's `Assembly-CSharp.dll`, `old.dll`, Dionic/InfraSpace assemblies, and `0Harmony.dll` from the official ISModKit CryoChambers example. Game updates may change the private Harmony patch targets. Before applying patches, the mod resolves all enabled targets and verifies the required private fields and their types. If installation fails, it removes patches owned by this mod before reporting the error.

Verified locally:

- The project builds in Release for `net472` with zero compiler warnings and zero errors.
- The installed game exposes the patched logistics and research methods described above.
- For a fresh technology, a multiplier of `0.5` yields Red Science totals of 50, 100, 150, and 250 for original costs of 100, 200, 300, and 500. Mixed-science research keeps each non-Red resource at one pack per research point.
- Direct shipments use the native `Car` path and inventory structures; no mod-specific save data is introduced.

Not verified in a running city:

- One center supplying one or several factories; multiple centers with the nearest empty; disconnected roads; and cross-district export rules.
- Concurrent requests against limited inventory; input capacity with existing Distribution Centers and in-flight deliveries; removal of a center during transit; and save/reload during transit.
- Actual train/carrier interactions and congestion behavior for direct shipments.
- In-game Red Science totals, existing save migration, and the vanilla restoration path after changing the multiplier to `1.0`.

Direct routes are road-pathfinder routes. A pipe-only resource path is excluded from direct truck supply. Rail dispatch is not covered by the new Collection Center route. The game integration cases above still require in-game validation before treating them as confirmed behavior.

## Regression checks

After building, run with PowerShell 7:

```powershell
pwsh -NoProfile -File .\tests\Regression.ps1
```

Use `-ManagedDir` to override the installed game assembly directory. The checks exercise the compiled research calculation and JSON settings for multipliers `0`, `0.07`, `0.14`, `0.5`, and `1`; native incoming-car reservation release and repeated removal; and required-field compatibility validation. These checks do not run a city or exercise Harmony rollback after a patch installation failure.
