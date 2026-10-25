using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Permissions;
using AutoItemPickup.Configuration;
using AutoItemPickup.Enums;
using AutoItemPickup.Hooks;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour.HookGen;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace AutoItemPickup;

[BepInPlugin("com.rfaael.autoitempickup.updated", "AutoItemPickup Updated", "2.4.2")]
// Soft dependency: makes BepInEx load Risk Of Options first when it is installed, but the mod works without it
[BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.kuberoot.commandqueue", BepInDependency.DependencyFlags.SoftDependency)]
public class AutoItemPickup : BaseUnityPlugin
{
    private static readonly MethodInfo GenericPickupController_AttemptGrant =
        typeof(GenericPickupController).GetMethod("AttemptGrant",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private static readonly FieldInfo GenericPickupController_consumed =
        typeof(GenericPickupController).GetField("consumed",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private bool distributorNeedsUpdate;

    private HookManager hookManager;

    /// <summary>Logger reachable from handlers and distributors (BaseUnityPlugin.Logger is not).</summary>
    internal static ManualLogSource Log { get; private set; }

    public ItemDistributor Distributor { get; private set; }

    public Config ModConfig { get; private set; }

    public void OnEnable()
    {
        Log = Logger;

        // Debug code for local multiplayer testing
        // On.RoR2.Networking.NetworkManagerSystemSteam.OnClientConnect += (s, u, t) => { };

        if (GenericPickupController_AttemptGrant == null)
            Log.LogError("GenericPickupController.AttemptGrant not found; items cannot be granted to living players.");
        if (GenericPickupController_consumed == null)
            Log.LogWarning("GenericPickupController.consumed not found; double grants to dead players are possible.");

        ModConfig = new Config(this, Logger);

        TryRegisterRiskOfOptions();

        hookManager = new HookManager(this);
        hookManager.RegisterHooks();

        // Each one independent: if one no longer exists in the game, the rest of the mod still loads.
        TryRegister("PlayerCharacterMasterController.OnBodyDeath",
            () => On.RoR2.PlayerCharacterMasterController.OnBodyDeath += OnPlayerBodyDeath);
        TryRegister("PlayerCharacterMasterController.OnBodyStart",
            () => On.RoR2.PlayerCharacterMasterController.OnBodyStart += OnPlayerBodyStart);
        TryRegister("PickupDropletController.CreatePickup (IL)",
            () => IL.RoR2.PickupDropletController.CreatePickup += ModifyCreatePickup);
        TryRegister("PlayerCharacterMasterController.Awake",
            () => On.RoR2.PlayerCharacterMasterController.Awake += OnPlayerAwake);

        PlayerCharacterMasterController.onPlayerAdded += UpdateTargetsWrapper;
        PlayerCharacterMasterController.onPlayerRemoved += UpdateTargetsWrapper;

        ModConfig.distributeToDeadPlayers.SettingChanged += OnDeadPlayersSettingChanged;
        ModConfig.distributionMode.SettingChanged += OnDistributionModeChanged;
    }

    public void OnDisable()
    {
        hookManager?.UnregisterHooks();

        // Cleanup any leftover hooks
        HookEndpointManager.RemoveAllOwnedBy(
            HookEndpointManager.GetOwner((Action)OnDisable));

        PlayerCharacterMasterController.onPlayerAdded -= UpdateTargetsWrapper;
        PlayerCharacterMasterController.onPlayerRemoved -= UpdateTargetsWrapper;

        if (ModConfig != null)
        {
            ModConfig.distributeToDeadPlayers.SettingChanged -= OnDeadPlayersSettingChanged;
            ModConfig.distributionMode.SettingChanged -= OnDistributionModeChanged;
        }
    }

    private void TryRegisterRiskOfOptions()
    {
#if RISKOFOPTIONS
        if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(RiskOfOptionsCompat.Guid)) return;

        try
        {
            // Kept in a separate class so this plugin loads even when Risk Of Options is not installed
            RiskOfOptionsCompat.Register(ModConfig);
        }
        catch (Exception e)
        {
            Log.LogWarning($"Could not add the in-game Risk Of Options menu; the .cfg file still works.\n{e}");
        }
#endif
    }

    private void TryRegister(string name, Action register)
    {
        try
        {
            register();
        }
        catch (Exception e)
        {
            Log.LogError($"Failed to hook {name}; the mod keeps running without it. " +
                         $"The game was probably updated.\n{e}");
        }
    }

    private void OnDeadPlayersSettingChanged(object sender, EventArgs e)
    {
        UpdateTargets();
    }

    private void OnDistributionModeChanged(object sender, EventArgs e)
    {
        Distributor = ItemDistributor.GetItemDistributor(ModConfig.distributionMode.Value, this);
    }

    private void OnPlayerBodyDeath(On.RoR2.PlayerCharacterMasterController.orig_OnBodyDeath orig,
        PlayerCharacterMasterController self)
    {
        orig(self);
        UpdateTargets();
    }

    private void OnPlayerBodyStart(On.RoR2.PlayerCharacterMasterController.orig_OnBodyStart orig,
        PlayerCharacterMasterController self)
    {
        orig(self);
        UpdateTargets();
    }

    private void UpdateTargetsWrapper(PlayerCharacterMasterController player)
    {
        UpdateTargets();
    }

    private void OnPlayerAwake(On.RoR2.PlayerCharacterMasterController.orig_Awake orig,
        PlayerCharacterMasterController self)
    {
        orig(self);

        if (!NetworkServer.active) return;
        var master = self.master;
        if (master) master.onBodyStart += obj => UpdateTargets();
    }

    private void ModifyCreatePickup(ILContext il)
    {
        var cursor = new ILCursor(il);

        if (!cursor.TryGotoNext(MoveType.After, i => i.MatchCall<GenericPickupController>("CreatePickup")))
        {
            Log.LogError($"Could not find the call to GenericPickupController.CreatePickup in {il.Method.FullName}. " +
                         "Printer/scrapper/Command target overrides will not reach the dropped item.");
            return;
        }

        cursor.Emit(OpCodes.Dup);
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate<Action<GenericPickupController, PickupDropletController>>(
            (pickupController, self) =>
            {
                if (!pickupController || !self) return;

                var behaviour = self.GetComponent<OverrideDistributorBehaviour>();
                if (behaviour)
                {
                    var newBehaviour = pickupController.gameObject.AddComponent<OverrideDistributorBehaviour>();
                    newBehaviour.Distributor = behaviour.Distributor;
                }
            });
    }

    [Server]
    public static void GrantItem(GenericPickupController item, CharacterMaster master)
    {
        if (!item || !master) return;

        if (master.hasBody)
        {
            if (GenericPickupController_AttemptGrant == null) return;

            GenericPickupController_AttemptGrant.Invoke(item, new object[] { master.GetBody() });
        }
        else
        {
            // The game no longer supports granting items to dead players; do it manually instead
            if (GenericPickupController_consumed?.GetValue(item) is true) return;
            if (!master.inventory) return;

            var pickupDef = PickupCatalog.GetPickupDef(item.pickupIndex);
            var itemIndex = pickupDef?.itemIndex ?? ItemIndex.None;
            if (itemIndex != ItemIndex.None)
            {
                master.inventory.GiveItem(itemIndex);

                var playerCharacterMasterController = master.playerCharacterMasterController;
                var networkUser = playerCharacterMasterController != null
                    ? playerCharacterMasterController.networkUser
                    : null;
                // Based on RoR2.GenericPickupController.HandlePickupMessage
                Chat.AddMessage(new Chat.PlayerPickupChatMessage
                {
                    subjectAsNetworkUser = networkUser,
                    baseToken = "PLAYER_PICKUP",
                    pickupToken = pickupDef?.nameToken ?? PickupCatalog.invalidPickupToken,
                    pickupColor = pickupDef?.baseColor ?? Color.black,
                    pickupQuantity = (uint)master.inventory.GetItemCount(itemIndex)
                }.ConstructChatString());

                GenericPickupController_consumed?.SetValue(item, true);
                Destroy(item.gameObject);
            }
        }
    }

    //NOTE: Currently ShouldDistribute has no check for OverrideDistributorBehaviour. Items will only be distributed if whitelisted.
    // public bool ShouldDistribute(GenericPickupController pickup, Cause cause)
    // {
    //     if (pickup == null || pickup.pickupIndex == PickupIndex.none)
    //         return false;
    //
    //     var overrideDistributorBehaviour = pickup.GetComponent<OverrideDistributorBehaviour>();
    //     if (overrideDistributorBehaviour != null && overrideDistributorBehaviour.Distributor != null &&
    //         overrideDistributorBehaviour.Distributor.IsValid())
    //         return true;
    //
    //     return ModConfig.ShouldDistribute(pickup.pickupIndex, cause);
    // }

    private void UpdateTargets()
    {
        distributorNeedsUpdate = true;
    }

    public void PreDistributeItemInternal(Cause cause)
    {
        if (Distributor == null)
            Distributor = ItemDistributor.GetItemDistributor(ModConfig.distributionMode.Value, this);

        if (distributorNeedsUpdate)
        {
            distributorNeedsUpdate = false;
            Distributor?.UpdateTargets();
        }
    }

    public void DistributeItemInternal(GenericPickupController item, Cause cause)
    {
        // Items may be destroyed (e.g. granted to someone) while a batch is being processed
        if (!item) return;

        try
        {
            var distributor = GetDistributorInternal(item.gameObject, cause);
            distributor?.DistributeItem(item);
        }
        catch (Exception e)
        {
            Logger.LogError($"Caught AutoItemPickup distributor exception:\n{e}\n{e.StackTrace}");
        }
    }

    public void DistributeItem(GenericPickupController item, Cause cause)
    {
        PreDistributeItemInternal(cause);
        DistributeItemInternal(item, cause);
    }

    public void DistributeItems(IEnumerable<GenericPickupController> items, Cause cause)
    {
        PreDistributeItemInternal(cause);
        foreach (var item in items) DistributeItemInternal(item, cause);
    }

    public ItemDistributor GetDistributorInternal(GameObject item, Cause cause)
    {
        if (item != null)
        {
            var overrideDistributorBehaviour = item.GetComponent<OverrideDistributorBehaviour>();
            if (overrideDistributorBehaviour != null)
                return overrideDistributorBehaviour.Distributor;
        }

        return Distributor;
    }

    public ItemDistributor GetDistributor(GameObject item, Cause cause)
    {
        PreDistributeItemInternal(cause);
        return GetDistributorInternal(item, cause);
    }
}
