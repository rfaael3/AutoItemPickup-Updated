using System.Reflection;
using AutoItemPickup.Enums;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace AutoItemPickup.Hooks;

public class CommandHandler : AbstractHookHandler
{
    // PickupDropletController.createPickupInfo was removed/renamed in recent game versions, so it is
    // looked up at runtime. If it is missing, only the "redirect Command essences on drop" feature is
    // disabled; everything else (including moving essences to the teleporter) keeps working.
    private static readonly FieldInfo Field_createPickupInfo =
        typeof(PickupDropletController).GetField("createPickupInfo",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private static bool CanRedirectCommandCube =>
        Field_createPickupInfo != null &&
        Field_createPickupInfo.FieldType == typeof(GenericPickupController.CreatePickupInfo);

    public override void RegisterHooks()
    {
        if (CanRedirectCommandCube)
            On.RoR2.PickupDropletController.CreateCommandCube += On_PickupDropletController_CreateCommandCube;
        else
            AutoItemPickup.Log.LogWarning(
                "PickupDropletController.createPickupInfo no longer exists in this game version. " +
                "Command essences will not be redirected on drop until the mod is updated.");

        TeleporterInteraction.onTeleporterChargedGlobal += OnTeleporterCharged;
    }

    public override void UnregisterHooks()
    {
        On.RoR2.PickupDropletController.CreateCommandCube -= On_PickupDropletController_CreateCommandCube;

        TeleporterInteraction.onTeleporterChargedGlobal -= OnTeleporterCharged;
    }

    private void On_PickupDropletController_CreateCommandCube(On.RoR2.PickupDropletController.orig_CreateCommandCube orig, PickupDropletController self)
    {
        try
        {
            var info = (GenericPickupController.CreatePickupInfo)Field_createPickupInfo.GetValue(self);

            if (ModConfig.ShouldDistributeCommand(info.pickupIndex, Cause.Drop))
            {
                // 'self' IS the droplet; the old code relied on a separate OnCollisionEnter hook
                // to find it, which was null whenever the cube was created from anywhere else.
                Plugin.GetDistributor(self.gameObject, Cause.Drop)
                    ?.DistributeItem(ref info, target => target && target.hasBody);
                Field_createPickupInfo.SetValue(self, info);
            }
            else if (TeleporterInteraction.instance &&
                     TeleporterInteraction.instance.isCharged &&
                     ModConfig.ShouldDistributeCommand(info.pickupIndex, Cause.Teleport))
            {
                info.position = GetTeleporterCommandTargetPosition();
                Field_createPickupInfo.SetValue(self, info);
            }
        }
        catch (System.Exception e)
        {
            AutoItemPickup.Log.LogError($"Error while distributing Command essence:\n{e}");
        }

        orig(self);
    }

    private Vector3 GetTeleporterCommandTargetPosition()
    {
        Vector3 spawnposition;

        var center = TeleporterInteraction.instance.transform.position;

        var angle = Random.Range(0, Mathf.PI * 2f);
        float distance = Random.Range(4, 15);
        spawnposition = center + Mathf.Sin(angle) * distance * Vector3.forward +
                        Mathf.Cos(angle) * distance * Vector3.right + Vector3.up * 10;

        return spawnposition;
    }

    private void TeleportToTeleporter(NetworkBehaviour obj)
    {
        TeleportHelper.TeleportGameObject(obj.gameObject, GetTeleporterCommandTargetPosition());
    }

    private void OnTeleporterCharged(TeleporterInteraction obj)
    {
        if (!NetworkServer.active || !ModConfig.teleportCommandOnTeleport.Value) return;
        if (!TeleporterInteraction.instance) return;

        try
        {
            var originalPickups = InstanceTracker.GetInstancesList<PickupPickerController>();

            var pickups = new PickupPickerController[originalPickups.Count];

            originalPickups.CopyTo(pickups);

            var teleporterPosition = TeleporterInteraction.instance.transform.position;

            foreach (var pickup in pickups)
            {
                if (!pickup) continue;

                var pickupIndexNetworker = pickup.GetComponent<PickupIndexNetworker>();
                if (pickupIndexNetworker && (pickup.transform.position - teleporterPosition).sqrMagnitude > 100 &&
                    ModConfig.ShouldDistributeCommand(pickupIndexNetworker.pickupIndex, Cause.Teleport))
                    TeleportToTeleporter(pickup);
            }
        }
        catch (System.Exception e)
        {
            AutoItemPickup.Log.LogError($"Error while moving Command essences to the teleporter:\n{e}");
        }
    }
}
