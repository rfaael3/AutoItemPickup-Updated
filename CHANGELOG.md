### 2.4.2
- Display the mod's Thunderstore icon in Risk Of Options
- Embed the icon in the plugin so it works without a separate image file; icon loading failures do not prevent settings from being registered

### 2.4.1
- Fix automatic Command interaction stopping after the first item with LookingGlass: its window blur hook expected a panel after Nick's queue selected without displaying one
- Route only automatically opened, queued essences directly through Nick's queued-choice coroutine, outside panel effects; retain the normal UI chain for manual interactions
- Keep reservations, next-frame submission and pickup accounting in Nick's own implementation
- Retry after transient interaction errors instead of disabling automatic interaction for the entire session; throttle repeated error messages
- Add regression coverage that reproduces the panel-effect exception and checks consecutive essences, the live toggle, manual fallback, range and recovery

### 2.4.0
- Add optional automatic interaction with nearby permanent Command essences when Nick's Command Queue Flavour has an available queued choice
- Add `AutoOpenQueuedCommand` (enabled by default) and `QueuedCommandRadius` (8 meters by default) to the config and Risk Of Options; settings apply during a run
- Preserve CommandQueue's reservations, repeat mode, LookingGlass selection handling and pickup-based queue consumption
- Leave empty/unavailable queues and temporary/rental essences manual; do not interrupt another active selection
- Use each player's local queue and normal network interaction; remote players need both mods locally to use this feature
- Keep CommandQueue and Risk Of Options optional

### 2.3.1
- Hooks are now registered independently: if a game update breaks one of them, only that feature is disabled and a clear error is logged, instead of the whole mod failing to load
- IL patches use `TryGotoNext` and log which method changed instead of throwing
- The distribution hooks on item drop and stage exit can no longer prevent the original game method from running if the mod throws
- Fix Command essence distribution on drop relying on a global field that could be null (removed the `OnCollisionEnter` hook)
- Fix printers/cauldrons with an unknown cost type being treated as item-cost terminals
- Fix errors when no player is alive/connected (`Sequential`, `LeastItems`), with items that have no `ItemDef`, and when giving essences to dead players
- Fix possible double grant to dead players
- Hooks added in `OnEnable` are now removed in `OnDisable`
- Project now references the DLLs from the installed game (see BUILD.md) instead of the `libs` submodule
- NOTE: not compiled or tested against the Hallowed Concepts (DLC4) game DLLs - see BUILD.md

### 2.3.0
- Update for V1.4.1 - Alloyed Collective update

### 2.2.0
- Update for V1.3.1 - Seekers of the Storm update

### 2.1.0
- Update for Devotion Update

### 2.0.2
- Fix an error that prevents the game from loading when an item without an english name is present (Thanks @lt.armyfollower)

### 2.0.1
- Fix item filters not applying to teleport distribution
- Fix command essences being distributed to dead players

### 2.0.0 - Major refactor
- Major code refactor - Should reduce unexpected behavior
- Config overhaul - Options should migrate automatically
  - OnDrop distribution is now enabled by default to reduce confusion
  - Filtering is now done on a tier whitelist, item whitelist, item blacklist basis
  - Removed separate distribution modes for drop and teleport - Let me know if you were affected by this change
- Add scrapper support - if enabled, scrapped items will go to the player scrapping
- Command essences now obey target overrides - if you find yourself scrapping/printing with command enabled
- Switch to catalogs for item tiers and interactible costs
  - Configuration now allows for all item tiers, should work for modded ones.
  - Printer support should now work for non-standard costs, for example different tier printers.
- Remove R2API dependency - Was only used to declare NetworkCompatibility

### 1.7.0
- Update for SotV patch 1.2.3

### 1.6.1
- Fix error when giving items to dead players
- Fix error with `Closest` distributor when distributing to dead players is enabled and some players are dead
- Fix off-by-one issues with selecting random players, affecting mostly random distribution, that would prevent the last player from getting items
- Add support in code for unloading the mod at runtime
- The `Sequential` distributor will now try to preserve the current target when a player joins or leaves

### 1.6.0 - Update to RoR2 Survivors of the Void
- Teleporting command essences on teleport will now space them away from the center of the teleporter

### 1.4.3 - Update to RoR2 1.0 update
- Remove code to snap command cubes to the ground, since they now have gravity

### 1.4.2 - Hotfix
- Fix error when distributing command cubes if the stage doesn't have a teleporter

### 1.4.1 - Fixes for Command
- Items chosen from command cubes go to the person selecting the item
- Command cubes that are moved when the teleporter finishes charging are now properly synchronised for clients

### 1.4.0 - Update to RoR2 Artifacts update
- Added `TeleportCommandCubes` for simple Artifact of Command compatibility

### 1.3.2 - Update to RoR2 Skills 2.0 update

### 1.3.1 - Update to RoR2 Scorched Acres update

### 1.3.0
- Update to BepInExPack 2.0.0 and R2API 2.0.6; no changes to function.

### 1.2.1
- Fixed error related to changes in language handling in latest RoR2 update (Build #3830295)

### 1.2.0
- Added item blacklisting, separate for drop and teleport.

### 1.1.4
- Fixed missing UpdateTargets condition, which caused `Closest` distributor to throw after first stage. Added additional exception handling in distributor to avoid interfering with game code.

### 1.1.3
- Fixed `Closest` distribution mode throwing exceptions and preventing teleportation when dead players are not ignored and a player is dead

### 1.1.2
- Fixed `Closest` distribution mode presumably being completely broken, without actually testing to make sure the fix isn't broken. Have fun!

### 1.1.1
- Added `OverridePrinterTarget` config option that ensures the activator receives printer result as long as they're valid

### 1.1.0
- Added on-drop distribution setting
- Made on-teleport distribution togglable
- Reworked code structure
- Fixed wrong code that could've lead to errors when ignoring dead players
- Added filtering for white, green, red and boss items
- Added separate filtering options for teleport and drop

### 1.0.2
- Added config option for ignoring lunar items

### 1.0.1 - Added website url

### 1.0.0 - Initial release
