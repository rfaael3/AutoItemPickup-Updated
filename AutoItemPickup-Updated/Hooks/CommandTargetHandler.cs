using System;
using System.Reflection;
using AutoItemPickup.ItemDistributors;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

namespace AutoItemPickup.Hooks;

/// <summary>
///     Command: when a player picks an item from the essence, that item goes to the player who picked it.
/// </summary>
public class CommandTargetHandler : AbstractHookHandler
{
    private static readonly FieldInfo Field_PickupPickerController_networkUIPromptController =
        typeof(PickupPickerController).GetField("networkUIPromptController",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    public override void RegisterHooks()
    {
        if (Field_PickupPickerController_networkUIPromptController == null)
            throw new MissingFieldException("PickupPickerController.networkUIPromptController no longer exists");

        IL.RoR2.PickupPickerController.CreatePickup_UniquePickup += IL_PickupPickerController_CreatePickup;
    }

    public override void UnregisterHooks()
    {
        IL.RoR2.PickupPickerController.CreatePickup_UniquePickup -= IL_PickupPickerController_CreatePickup;
    }

    private void IL_PickupPickerController_CreatePickup(ILContext il)
    {
        var cursor = new ILCursor(il);

        if (!cursor.TryGotoNext(MoveType.After, i => i.MatchCallOrCallvirt<GenericPickupController>("CreatePickup")))
        {
            LogPatchFailed(il, "the call to GenericPickupController.CreatePickup");
            return;
        }

        cursor.Emit(OpCodes.Dup);
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate<Action<GenericPickupController, PickupPickerController>>((pickup, self) =>
        {
            if (!pickup || !self) return;

            var pickupDef = PickupCatalog.GetPickupDef(pickup.pickupIndex);
            if (pickupDef == null) return;
            var itemDef = ItemCatalog.GetItemDef(pickupDef.itemIndex);
            if (itemDef == null) return;

            var networkUIPromptController =
                Field_PickupPickerController_networkUIPromptController.GetValue(self) as NetworkUIPromptController;
            var participantMaster = networkUIPromptController ? networkUIPromptController.currentParticipantMaster : null;
            if (!participantMaster) return;

            var behaviour = pickup.gameObject.AddComponent<OverrideDistributorBehaviour>();
            behaviour.Distributor = new FixedTargetDistributor(Plugin, participantMaster);
        });
    }
}
