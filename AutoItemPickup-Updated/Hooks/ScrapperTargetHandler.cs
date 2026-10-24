using System;
using System.Reflection;
using AutoItemPickup.ItemDistributors;
using EntityStates.Scrapper;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

namespace AutoItemPickup.Hooks;

/// <summary>
///     Handler for scrappers.
///     Patches EntityStates.Scrapper.ScrappingToIdle.OnEnter.
///     Uses CreatePickupDropletHandler to affect CreatePickupDroplet.
/// </summary>
public class ScrapperTargetHandler : AbstractHookHandler
{
    internal static FieldInfo Field_ScrapperBaseState_scrapperController = typeof(ScrapperBaseState).GetField(
        "scrapperController",
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    internal static FieldInfo Field_ScrapperController_interactor = typeof(ScrapperController).GetField(
        "interactor",
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    public override void RegisterHooks()
    {
        // Fail early and with a clear message if the game renamed/removed these fields
        if (Field_ScrapperBaseState_scrapperController == null || Field_ScrapperController_interactor == null)
            throw new MissingFieldException(
                "ScrapperBaseState.scrapperController or ScrapperController.interactor no longer exists");

        IL.EntityStates.Scrapper.ScrappingToIdle.OnEnter += IL_ScrappingToIdle_OnEnter;
    }

    public override void UnregisterHooks()
    {
        IL.EntityStates.Scrapper.ScrappingToIdle.OnEnter -= IL_ScrappingToIdle_OnEnter;
    }

    private void IL_ScrappingToIdle_OnEnter(ILContext il)
    {
        var cursor = new ILCursor(il);

        if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchCall<PickupDropletController>("CreatePickupDroplet")))
        {
            LogPatchFailed(il, "the call to PickupDropletController.CreatePickupDroplet");
            return;
        }

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Ldfld, Field_ScrapperBaseState_scrapperController);
        cursor.Emit(OpCodes.Ldfld, Field_ScrapperController_interactor);
        cursor.EmitDelegate<Action<Interactor>>(interactor =>
        {
            if (!ModConfig.scrapperOverrideTarget.Value || !interactor) return;

            var body = interactor.GetComponent<CharacterBody>();
            var target = body ? body.master : null;

            if (target)
                hookManager.GetHandler<CreatePickupDropletHandler>().DistributorOverride =
                    new FixedTargetDistributor(Plugin, target);
        });

        cursor.Index++;
        cursor.EmitDelegate(() => { hookManager.GetHandler<CreatePickupDropletHandler>().DistributorOverride = null; });
    }
}
