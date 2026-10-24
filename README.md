# AutoItemPickup Updated

An updated fork of [AutoItemPickup by KubeRoot](https://thunderstore.io/package/KubeRoot/AutoItemPickup/) ([source](https://gitlab.com/KubeRoot/ror2-autoitempickup)). All credit for the original mod goes to **KubeRoot**; please consider [supporting them on Ko-fi](https://ko-fi.com/kuberoot). This fork is released under the same MIT license.

Automatically grants items on the ground to players when the teleport is initiated or an item spawns.

> **Do not install this together with the original AutoItemPickup.** Disable the original first. This fork uses its own config file, so your old settings will not carry over.

## What's different in this fork

- **Risk Of Options support (optional)**: change the settings from the in-game Mod Options menu, including the tier/item whitelist and blacklist (as text fields). Without Risk Of Options, everything works through the config file.
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
- **Server-only**: only the host needs the mod, clients do not
- By default items from printers/cauldrons/similar go to the activator, as long as the activator is still a valid target
  - Also includes an option for scrappers
- 4 distribution modes:
  - `Sequential` - one item to each player in turn
  - `Random` - random player
  - `Closest` - the nearest player
  - `LeastItems` - the player with the fewest items of that tier
- Command artifact support
  - On drop, the Command essence is teleported to a player (not opened automatically)
  - On teleport, Command essences go near the teleporter once it charges

## Installation

Install with a mod manager (r2modman or Thunderstore Mod Manager). Optionally install [Risk Of Options](https://thunderstore.io/c/riskofrain2/p/Rune580/Risk_Of_Options/) for the in-game menu.

Config file: `BepInEx/config/com.rfaael.autoitempickup.updated.cfg`. Review it before use.

## Credits

- **KubeRoot**: original author of AutoItemPickup.
