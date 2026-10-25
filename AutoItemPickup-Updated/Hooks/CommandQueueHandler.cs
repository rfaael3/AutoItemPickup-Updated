using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx;
using RoR2;
using RoR2.Artifacts;
using UnityEngine;

namespace AutoItemPickup.Hooks;

/// <summary>
/// Opens nearby Command cubes for the local player's queue. Nick's CommandQueue still owns
/// selection, reservations, repeat mode and pickup accounting; we only perform the interaction.
/// No queue data is sent to the host or read on behalf of remote players.
/// </summary>
public class CommandQueueHandler : AbstractHookHandler
{
    private Func<IEnumerable<(ItemTier, PickupIndex)>> peekAllUnreserved;
    private float nextScan;
    private BaseUnityPlugin queuePluginInstance;
    private PickupPickerController pendingInteraction;
    private float pendingUntil;
    private Func<PickupPickerController, ItemTier, PickupIndex, NetworkUIPromptController,
        LocalUser, CameraRigController, IEnumerator> submitQueuedChoice;
    private bool displayHookRegistered;
    private float nextErrorLog;

    public override void RegisterHooks()
    {
        if (!Chainloader.PluginInfos.TryGetValue("com.kuberoot.commandqueue", out var queuePlugin))
            return;

        // Optional integration: do not link CommandQueue.dll into this plugin. Older forks that
        // lack reservation-aware peeking must keep their normal manual interaction behavior.
        var queueType = queuePlugin.Instance.GetType().Assembly.GetType("CommandQueue.QueueManager");
        var peek = queueType?.GetMethod("PeekAllUnreserved", BindingFlags.Public | BindingFlags.Static,
            null, Type.EmptyTypes, null);
        if (peek == null || peek.ReturnType != typeof(IEnumerable<(ItemTier, PickupIndex)>))
        {
            AutoItemPickup.Log.LogWarning("CommandQueue does not expose PeekAllUnreserved; automatic Command interaction is disabled.");
            return;
        }

        // Reuse Nick's own reservation and direct-submit coroutine rather than duplicating its
        // queue bookkeeping. Validate the optional API before enabling any automatic interaction.
        var submit = queuePlugin.Instance.GetType().GetMethod("SubmitQueuedChoiceNextFrame",
            BindingFlags.NonPublic | BindingFlags.Static, null,
            new[] { typeof(PickupPickerController), typeof(ItemTier), typeof(PickupIndex),
                typeof(NetworkUIPromptController), typeof(LocalUser), typeof(CameraRigController) }, null);
        if (submit == null || submit.ReturnType != typeof(IEnumerator))
        {
            AutoItemPickup.Log.LogWarning("CommandQueue does not expose the supported queued-choice coroutine; automatic Command interaction is disabled.");
            return;
        }
        submitQueuedChoice = (Func<PickupPickerController, ItemTier, PickupIndex, NetworkUIPromptController,
            LocalUser, CameraRigController, IEnumerator>)Delegate.CreateDelegate(
                typeof(Func<PickupPickerController, ItemTier, PickupIndex, NetworkUIPromptController,
                    LocalUser, CameraRigController, IEnumerator>), submit);

        peekAllUnreserved = (Func<IEnumerable<(ItemTier, PickupIndex)>>)Delegate.CreateDelegate(
            typeof(Func<IEnumerable<(ItemTier, PickupIndex)>>), peek);
        queuePluginInstance = queuePlugin.Instance;
        // Register after startup UI hooks (notably LookingGlass). Automated selections create no
        // panel, so panel effects must not wrap Nick's UI-less selection path.
        Run.onRunStartGlobal += OnRunStart;
        if (Run.instance) OnRunStart(Run.instance);
        RoR2Application.onUpdate += Update;
        AutoItemPickup.Log.LogInfo("CommandQueue integration ready: nearby queued Command essences can be opened automatically for the local player.");
    }

    public override void UnregisterHooks()
    {
        RoR2Application.onUpdate -= Update;
        Run.onRunStartGlobal -= OnRunStart;
        if (displayHookRegistered)
            On.RoR2.PickupPickerController.OnDisplayBegin -= OnDisplayBegin;
        displayHookRegistered = false;
        peekAllUnreserved = null;
        submitQueuedChoice = null;
        queuePluginInstance = null;
        pendingInteraction = null;
    }

    private void OnRunStart(Run run)
    {
        // Reattach at each run so this guard is outside UI hooks installed during startup.
        if (displayHookRegistered)
            On.RoR2.PickupPickerController.OnDisplayBegin -= OnDisplayBegin;
        On.RoR2.PickupPickerController.OnDisplayBegin += OnDisplayBegin;
        displayHookRegistered = true;
        pendingInteraction = null;
        nextScan = 0f;
    }

    private void OnDisplayBegin(On.RoR2.PickupPickerController.orig_OnDisplayBegin orig,
        PickupPickerController self, NetworkUIPromptController prompt, LocalUser user, CameraRigController camera)
    {
        if (self == pendingInteraction && ModConfig.autoOpenQueuedCommand.Value &&
            queuePluginInstance && queuePluginInstance.isActiveAndEnabled &&
            user == LocalUserManager.GetFirstLocalUser())
        {
            pendingInteraction = null;
            foreach (var (tier, pickup) in peekAllUnreserved())
                foreach (var option in self.options)
                    if (option.available && option.pickup.pickupIndex == pickup &&
                        !option.pickup.isTempItem && !option.pickup.isRental)
                    {
                        self.StartCoroutine(submitQueuedChoice(self, tier, pickup, prompt, user, camera));
                        return;
                    }
        }

        // Manual interactions and queues changed while the request was in flight keep the normal
        // UI chain. Only an automatic interaction with a valid queued choice bypasses panel effects.
        orig(self, prompt, user, camera);
    }

    private void Update()
    {
        if (!Plugin || !Plugin.isActiveAndEnabled || !ModConfig.autoOpenQueuedCommand.Value || !Run.instance)
            return;
        if (Time.time < nextScan) return;
        nextScan = Time.time + 0.2f;

        try
        {
            if (!queuePluginInstance || !queuePluginInstance.isActiveAndEnabled) return;
            // A client's interaction request may still be in transit. Wait before sending another
            // one, even though its participant has not yet been synchronized from the server.
            if (pendingInteraction && Time.time < pendingUntil) return;
            // CommandQueue itself uses the first local user for pickup accounting. Do not apply
            // that shared queue to a second split-screen player.
            var localUser = LocalUserManager.GetFirstLocalUser();
            var body = localUser?.cachedBody;
            var master = localUser?.cachedMaster;
            if (!body || !master || !body.healthComponent || !body.healthComponent.alive) return;
            var interactor = body.GetComponent<Interactor>();
            if (!interactor) return;

            var cubes = InstanceTracker.GetInstancesList<PickupPickerController>().ToArray();
            // Never replace an existing selection (including a scrapper, Void potential, etc.).
            foreach (var prompt in InstanceTracker.GetInstancesList<NetworkUIPromptController>())
            {
                if (prompt && prompt.currentParticipantMaster == master) return;
            }

            var prefab = CommandArtifactManager.commandCubePrefab;
            var commandPicker = prefab ? prefab.GetComponent<PickupPickerController>() : null;
            if (!commandPicker || !commandPicker.panelPrefab) return;

            var queued = new HashSet<PickupIndex>();
            foreach (var (_, pickup) in peekAllUnreserved())
                if (pickup != PickupIndex.none) queued.Add(pickup);
            if (queued.Count == 0) return;

            PickupPickerController nearest = null;
            var nearestDistance = float.PositiveInfinity;
            var origin = body.inputBank ? body.inputBank.aimOrigin : body.corePosition;
            foreach (var cube in cubes)
            {
                if (!cube || !cube.isActiveAndEnabled || cube.panelPrefab != commandPicker.panelPrefab ||
                    cube.GetInteractability(interactor) != Interactability.Available) continue;

                // Stay inside the game's prompt cutoff so it cannot cancel the interaction before
                // CommandQueue submits its next-frame choice. This also avoids map-wide collection.
                var radius = Mathf.Min(ModConfig.queuedCommandRadius.Value, Mathf.Max(0f, cube.cutoffDistance - 0.5f));
                var distance = (cube.transform.position - origin).sqrMagnitude;
                if (distance > radius * radius || distance >= nearestDistance) continue;

                var hasChoice = false;
                foreach (var option in cube.options)
                    if (option.available && queued.Contains(option.pickup.pickupIndex) &&
                        !option.pickup.isTempItem && !option.pickup.isRental)
                    {
                        hasChoice = true;
                        break;
                    }
                if (!hasChoice) continue;

                nearest = cube;
                nearestDistance = distance;
            }

            // Use normal network interaction, then route the automatic display through Nick's
            // queued-choice coroutine without running effects that require an actual UI panel.
            if (nearest)
            {
                pendingInteraction = nearest;
                pendingUntil = Time.time + 1f;
                interactor.AttemptInteraction(nearest.gameObject);
            }
        }
        catch (Exception e)
        {
            // A transient interaction failure must not disable the feature for the whole session.
            nextScan = Time.time + 2f;
            if (Time.time >= nextErrorLog)
            {
                nextErrorLog = Time.time + 10f;
                AutoItemPickup.Log.LogError($"Automatic CommandQueue interaction failed; retrying after a short delay.\n{e}");
            }
        }
    }
}
