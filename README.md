# InfraSpace QoL Mod

Quality-of-life improvements for InfraSpace: faster simulation speeds, better deliveries, adjustable Red Science, automatic worker release, expanding terraformer coverage, and automatic laser clearing.

## Installation

1. Install InfraSpace through Steam.
2. Keep the complete installer package together in one folder: `InfraSpaceQoLInstaller.exe`, `InfraSpaceQoL.dll`, `0Harmony.dll`, `config.json`, and `settings.json`.
3. Close InfraSpace and run `InfraSpaceQoLInstaller.exe`.
4. Choose your settings in the **Speed**, **Logistics**, **Research**, **Workforce**, **Terraforming**, and **Laser** tabs, then click **Install**.
5. Start InfraSpace and enable **InfraSpace QoL** in the game's Mods menu.

When updating, the installer loads your existing settings and adds defaults for new options. Installing saves the choices shown and backs up your previous settings as `settings.json.bak`. Closing the installer without clicking **Install** saves nothing.

## Changing settings

Close the game and rerun the installer to change any option. You can also edit `settings.json` in the installed mod folder. Restart the game after changes.

All checkboxes are enabled by default. The numeric defaults are **0.5** for Red Science cost, **1,000 MW** for Magnetic Field Generator power, and **3x** for laser speed.

## Speed

Adds **4x Very Fast** and **8x Superspeed** buttons with distinct icons. Pausing and resuming keeps your selected speed.

Disable **Show 4x and 8x speed controls** to use the normal game controls.

## Logistics

### Direct Collection Center supply

Collection Centers deliver directly to factories, research labs, construction, and upgrades over roads. Labs can receive their requested science packs without a Distribution Center. Factory, lab, and construction deliveries take priority over surplus storage.

Road connections, district permissions, and storage capacity still apply. Rail freight is not covered. Storehouses accept fixed resource categories; there is no material selector.

### Storehouse deliveries

- **Storehouses receive overflow only:** suppliers must be at least **90% full** and have a full truckload before shipping to storage. Factory and lab deliveries can still happen below this threshold.
- **Exempt adamantite from the overflow threshold:** adamantite can ship to storage whenever a full truckload is available, even below 90% capacity.
- **Prefer the nearest eligible Storehouse:** suppliers choose the closest available Storehouse by road distance, accounting for capacity and goods already on the way.

These options also apply when direct Collection Center supply is disabled. Trucks already dispatched keep their destinations.

### Collection radius and final deliveries

- **Enforce Collection Center radius at dispatch:** producers outside a center's current collection radius cannot send new deliveries to it. Distance is measured directly between buildings; the road route can be longer.
- **Allow final partial construction and repair loads:** direct deliveries carry the exact remaining materials for construction, upgrades, and spaceship repairs, even when fewer than a full truckload remain. Materials already delivered or on the way count toward the requirement.

Disabling partial loads can leave the last few items dependent on the game's normal delivery system.

## Research

### Red Science cost

**Red Science cost multiplier** defaults to **0.5**, so research labs consume half the normal Red Science packs. Set it anywhere from **0** (free) to **1** (normal cost). Other science costs stay the same. Packs already consumed are not refunded.

### Red Science output

**Double Red Science output** makes factories produce **two packs per normal one-pack batch**. Ingredients, batch time, power, and worker requirements stay the same. Output upgrades are doubled too, subject to normal storage limits.

The cost multiplier and doubled output can be configured independently.

## Workforce

**Release workers when all output buffers are full** lets idle factories free their workers for other jobs. A factory with multiple outputs keeps its workers while any output still has room.

When output space becomes available, the factory becomes eligible for workers again. Staffing depends on available workers and your workforce priorities. Manual production limits stay in effect.

Power producers, terraformers, spaceship work, and buildings without stored outputs are excluded.

## Terraforming

### Expanding terraformer coverage

**Expand saturated terraformer coverage** doubles a terraformer's effective radius when at least **95% of the playable terrain within its current range reaches 99% total terraforming progress**. Coverage expands in steps until it reaches the playable map. Each larger circle must meet the same threshold before expanding again.

A lone early-game terraformer type cannot unlock expansion. Keep enough terraformer types operating together, with their normal supplies, power, and workers. The range indicator still shows the original radius.

Expanded coverage works with existing saves. If more than 5% of a circle drops below 99% progress, coverage can shrink and unsupported terrain can decay. Terraformers continue running to maintain the terrain after it reaches 100%.

### Magnetic Field Generator

- **Reduce Magnetic Field Generator adamantite cost:** lowers the upfront placement cost from **250 to 75 adamantite**. Other materials and later construction stages stay the same. Disable it to restore the normal placement cost; previously spent materials are not refunded.
- **Magnetic Field Generator power (MW):** defaults to **1,000 MW**, reduced from **5,000 MW**. Choose a value from **1 to 5,000**; use **5,000** for normal power demand. Materials, staffing, and magnetic field coverage stay the same.

Both settings apply when loading existing saves as well as starting a new game. Older settings from the former **Monuments** category move to **Terraforming** automatically.

The game's completion message can appear before the Magnetic Field Generator is built: the other four terraforming types can already provide 100% progress. This mod currently leaves that completion rule unchanged.

## Laser

### Automatic obstacle clearing

**Automatically clear laser obstacles** clears clickable, building-blocking obstacles across the map once the spaceship cannon is repaired and at least one targeting station is built.

Clearing starts near a targeting station, then moves to the closest remaining obstacle from the last target. Manual targeting takes priority; automatic clearing resumes afterward. Removing all targeting stations stops automatic fire. Multiple stations share one cannon and do not multiply its damage or speed.

Obstacles the cannon cannot acquire within five game seconds are skipped until you reload the save.

### Laser speed

**Laser speed multiplier** defaults to **3x**, increasing damage and beam warmup speed on top of your existing speed upgrades. It applies to manual and automatic fire, including when automatic clearing is disabled.

Choose a value from **1 to 10**. Set it to **1** for normal speed.

## Compatibility

Built for **InfraSpace 1.56.460**. Game updates may require a mod update. Disabling an option does not undo completed construction, consumed science packs, or cleared obstacles.

For building the mod from source or contributing changes, see the [development guide](DEVELOPMENT.md).
