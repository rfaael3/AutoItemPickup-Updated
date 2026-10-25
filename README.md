# AutoItemPickup Updated

An updated fork of [AutoItemPickup by KubeRoot](https://thunderstore.io/package/KubeRoot/AutoItemPickup/) ([source](https://gitlab.com/KubeRoot/ror2-autoitempickup)). All credit for the original mod goes to **KubeRoot**; please consider [supporting them on Ko-fi](https://ko-fi.com/kuberoot). This fork is released under the same MIT license.

Automatically grants items on the ground to players when the teleport is initiated or an item spawns.

> **Do not install this together with the original AutoItemPickup.** Disable the original first. This fork uses its own config file, so your old settings will not carry over.

## What's different in this fork

- **Risk Of Options support (optional)**: change the settings from the in-game Mod Options menu, including the tier/item whitelist and blacklist (as text fields). Without Risk Of Options, everything works through the config file.
- **Optional CommandQueue auto-interaction**: nearby permanent Command essences open automatically when [Nick's Command Queue Flavour](https://thunderstore.io/c/riskofrain2/p/Nickgg11Team/Nicks_Command_Queue_Flavour/) has an available queued choice. Toggle `AutoOpenQueuedCommand` and adjust `QueuedCommandRadius` in Risk Of Options, or use the config file.
- **More robust against game updates**: each hook is registered separately. If a game update breaks one of them, only that feature is disabled and a clear error is logged, instead of the whole mod failing to load.
- A hook failure can no longer stop the original game code from running (item drops, stage exit).
- Fixes for errors with no players alive/connected, items without an item definition, dead players, and Command essences.

See the changelog for details.

**Status: lightly tested.** Tested on a recent game version in the author's setup. Please report problems with your `LogOutput.log` lines that start with `[AutoItemPickup Updated]`.

## Features

- Distributing on item drop and/or teleport
- Filtering by item tier, only distributes white, green and red items by default
- Items can be whitelisted/blacklisted individually, using a comma-separated config entry (English name or internal item name). For example:
  - `ItemBlacklist = Scrap\, White, Bustling Fungus, Syringe, Infusion, BleedOnHit, Backup Magazine`
- Can grant items to dead players
- **Host-side item distribution**: only the host needs the mod for distribution. Each player who wants automatic Command interaction needs both this mod and Nick's Command Queue Flavour installed locally.
- By default items from printers/cauldrons/similar go to the activator, as long as the activator is still a valid target
  - Also includes an option for scrappers
- 4 distribution modes:
  - `Sequential` - one item to each player in turn
  - `Random` - random player
  - `Closest` - the nearest player
  - `LeastItems` - the player with the fewest items of that tier
- Command artifact support
  - On drop, the Command essence is teleported to a player
  - On teleport, Command essences go near the teleporter once it charges
  - With Nick's Command Queue Flavour installed, nearby permanent essences with an available queued choice are opened automatically by default
  - Empty queues, unavailable queued items, temporary/rental essences, and other selection interactables remain manual
  - Nick's mod handles the choice, reservations, repeat mode and queue consumption on pickup

## Automatic Command interaction

In **Settings > Mod Options > AutoItemPickup Updated > Command** (Risk Of Options), use:

| Setting | Default | Description |
| --- | --- | --- |
| `AutoOpenQueuedCommand` | `true` | Automatically interact with nearby Command essences when your local queue has an available permanent choice. Changes apply during a run. |
| `QueuedCommandRadius` | `8` | Maximum interaction distance in meters (1–30), also limited by the game's prompt cutoff distance. |

Without Risk Of Options, edit these settings under `[Command]` in `BepInEx/config/com.rfaael.autoitempickup.updated.cfg`.

This feature is independent of the existing item distribution and essence teleport filters. It only opens nearby essences; the existing teleport settings determine whether distant essences are moved to a player. It uses the normal interaction and Nick's normal item selection flow. It does not change how the resulting item is granted or distributed. Only the first local user is supported in split-screen, matching Nick's queue accounting.

Automatically queued selections create no selection panel. Version 2.4.1 routes these selections through Nick's own coroutine without running panel-only effects, avoiding the LookingGlass window blur exception that could stop automatic interaction in 2.4.0. Manual interactions retain their existing UI behavior.

Nick's mod is optional. Without it, or with a queue mod that lacks its reservation-aware API, automatic interaction stays inactive. Do not install multiple CommandQueue forks together.

## Installation

Install with a mod manager (r2modman or Thunderstore Mod Manager). Optionally install [Risk Of Options](https://thunderstore.io/c/riskofrain2/p/Rune580/Risk_Of_Options/) for the in-game menu.

Config file: `BepInEx/config/com.rfaael.autoitempickup.updated.cfg`. Review it before use.

## Credits

- **KubeRoot**: original author of AutoItemPickup.
