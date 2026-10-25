# Testing version 2.4.2

## Build verification

Version 2.4.2 adds the package icon to Risk Of Options. The release build passed; assembly inspection verified that the embedded PNG matches the package icon byte for byte and that the icon registration call is compiled. Visual verification of the icon in-game is still required. CommandQueue behavior is unchanged from the user-confirmed 2.4.1 fix.

Compiled successfully against the locally installed Risk of Rain 2 game assemblies and generated MMHOOK_RoR2.dll, both with and without Risk Of Options support. The release package includes the Risk Of Options menu. Existing obsolete-API warnings remain in the original distribution code.

The installed Nick's Command Queue Flavour 2.1.1 assembly exposes the reservation-aware `CommandQueue.QueueManager.PeekAllUnreserved()` API and the `SubmitQueuedChoiceNextFrame` coroutine used by this integration. Both signatures are validated before enabling automatic interaction.

## Regression verification

The 2.4.0 gameplay log showed a `NullReferenceException` in `LookingGlass.CommandWindowBlur.NoWindowBlur.OnDisplayBegin` after Nick's queue selected without creating a panel. The exception reached the interaction scanner, which then unsubscribed itself for the rest of the session.

Run `dotnet run --project tests/CommandQueueRegression -c Release` with .NET SDK 10. This harness compiles the production handler against game/queue doubles. It reproduces the old panel-effect exception, then verifies automatic interaction bypasses panel effects, multiple essences continue selecting, toggling off/on works, manual UI is retained, empty/unavailable/temporary choices stay manual, radius applies, transient errors recover, other active prompts are respected and hooks are removed on unload. All 14 checks passed.

The harness does not emulate Unity, actual MonoMod detour ordering, Nick's full coroutine internals or network timing. Those still require gameplay testing.

**In-game verification is still required.** Compilation and API inspection do not verify gameplay, multiplayer timing or the rendered options menu.

## In-game checklist before publishing

1. Replace the old AutoItemPickup.dll with the new one. Do not leave two versions installed. Keep Nick's Command Queue Flavour 2.1.1 and optionally Risk Of Options enabled.
2. Start a run with Artifact of Command. In Mod Options > AutoItemPickup Updated > Command, confirm `AutoOpenQueuedCommand` and `QueuedCommandRadius` are present and described in English.
3. Queue two different white items. Spawn or obtain several white essences within range. Confirm each opens without pressing Interact, the correct items are selected, and the queue decreases only on actual pickup.
4. Leave the queue empty, or queue an item unavailable in the essence. Confirm the essence stays available for manual interaction without a menu opening automatically.
5. Enable repeat mode. Confirm the sequence continues in the right order as essences are opened and items are received.
6. Turn `AutoOpenQueuedCommand` off during the run. Confirm automatic interaction stops and manual interaction still works. Turn it back on and confirm it resumes.
7. Change `QueuedCommandRadius` during the run. Check essences inside and outside the new radius. The game's own prompt cutoff can impose a smaller radius.
8. Check green/red and enabled lunar/boss/void/Hallowed/Hex queues. The automatic interaction setting is independent of the original essence teleport tier filters.
9. Confirm temporary/rental essences, scrappers, Void potentials and other picker types remain manual. Open another selection and confirm automatic interaction does not replace it.
10. Repeat with LookingGlass enabled and its window blur removal enabled. Open at least ten chests consecutively; confirm every queued essence opens, the items match the queue and the log has no window blur exceptions from automatic interactions.
11. Check solo, host and a remote client. Each player who wants automatic interaction needs both mods locally; the host cannot read a remote player's queue. Check that an essence already in use by another player is not opened again.
12. Start once without Nick's mod, and once without Risk Of Options. Confirm the base mod still loads. Look for errors in BepInEx/LogOutput.log.

This update only automates the interaction with Command essences. Resulting item grants and distribution continue through the existing game/mod flow. If items remain on the ground with your settings, check the existing `Items/DistributeOnDrop` and item filters.
