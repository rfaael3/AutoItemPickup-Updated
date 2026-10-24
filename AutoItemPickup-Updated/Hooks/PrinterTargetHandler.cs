using AutoItemPickup.ItemDistributors;
using RoR2;

namespace AutoItemPickup.Hooks;

public class PrinterTargetHandler : AbstractHookHandler
{
    public override void RegisterHooks()
    {
        On.RoR2.ShopTerminalBehavior.DropPickup += On_ShopTerminalBehavior_DropPickup;
    }

    public override void UnregisterHooks()
    {
        On.RoR2.ShopTerminalBehavior.DropPickup -= On_ShopTerminalBehavior_DropPickup;
    }

    private void On_ShopTerminalBehavior_DropPickup(On.RoR2.ShopTerminalBehavior.orig_DropPickup orig,
        ShopTerminalBehavior self)
    {
        var dropletHandler = hookManager.GetHandler<CreatePickupDropletHandler>();

        try
        {
            if (ModConfig.printerOverrideTarget.Value)
            {
                var interaction = self.GetComponent<PurchaseInteraction>();

                // Only terminals that cost items (printers, cauldrons). Cost types without a definition
                // (modded or new) are ignored instead of being treated as item costs.
                var costTypeDef = interaction ? CostTypeCatalog.GetCostTypeDef(interaction.costType) : null;

                if (costTypeDef != null && costTypeDef.itemTier != ItemTier.NoTier)
                {
                    var interactor = interaction.lastActivator;
                    var body = interactor ? interactor.GetComponent<CharacterBody>() : null;
                    var target = body ? body.master : null;

                    if (target)
                        dropletHandler.DistributorOverride = new FixedTargetDistributor(Plugin, target);
                }
            }

            orig(self);
        }
        finally
        {
            dropletHandler.DistributorOverride = null;
        }
    }
}
