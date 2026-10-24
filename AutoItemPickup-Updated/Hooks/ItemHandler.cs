using System;
using System.Linq;
using AutoItemPickup.Enums;
using UnityEngine.Networking;

namespace AutoItemPickup.Hooks;

public class ItemHandler : AbstractHookHandler
{
    public override void RegisterHooks()
    {
        On.RoR2.SceneExitController.Begin += On_SceneExitController_Begin;
        On.RoR2.GenericPickupController.Start += On_GenericPickupController_Start;
    }

    public override void UnregisterHooks()
    {
        On.RoR2.SceneExitController.Begin -= On_SceneExitController_Begin;
        On.RoR2.GenericPickupController.Start -= On_GenericPickupController_Start;
    }

    // The original method must ALWAYS run, even if distribution throws. Otherwise a bug in the mod
    // would break item pickups, or stop the stage from ending.
    private void On_GenericPickupController_Start(On.RoR2.GenericPickupController.orig_Start orig,
        RoR2.GenericPickupController self)
    {
        try
        {
            if (NetworkServer.active && ModConfig.distributeOnDrop.Value && self)
                if (ModConfig.ShouldDistribute(self.pickupIndex, Cause.Drop))
                    Plugin.DistributeItem(self, Cause.Drop);
        }
        catch (Exception e)
        {
            AutoItemPickup.Log.LogError($"Error while distributing dropped item:\n{e}");
        }

        orig(self);
    }

    private void On_SceneExitController_Begin(On.RoR2.SceneExitController.orig_Begin orig,
        RoR2.SceneExitController self)
    {
        try
        {
            if (NetworkServer.active && ModConfig.distributeOnTeleport.Value)
            {
                var originalPickups = RoR2.InstanceTracker.GetInstancesList<RoR2.GenericPickupController>();

                var pickups = new RoR2.GenericPickupController[originalPickups.Count];

                originalPickups.CopyTo(pickups);

                Plugin.DistributeItems(
                    pickups.Where(pickup => pickup && ModConfig.ShouldDistribute(pickup.pickupIndex, Cause.Teleport)),
                    Cause.Teleport);
            }
        }
        catch (Exception e)
        {
            AutoItemPickup.Log.LogError($"Error while distributing items on teleport:\n{e}");
        }

        orig(self);
    }
}
