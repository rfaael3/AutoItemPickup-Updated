using RoR2;
using UnityEngine;

namespace AutoItemPickup.ItemDistributors;

internal class SequentialDistributor : ItemDistributor
{
    private int index;
    private CharacterMaster[] playerDistribution;

    public SequentialDistributor(AutoItemPickup plugin) : base(plugin) {}

    public override void UpdateTargets()
    {
        // The old array can be empty (everyone left) or index can be stale; never index blindly
        CharacterMaster oldTarget = null;
        if (playerDistribution != null && index >= 0 && index < playerDistribution.Length)
            oldTarget = playerDistribution[index];

        var filteredPlayers = GetValidTargets();
        playerDistribution = new CharacterMaster[filteredPlayers.Count];

        index = -1;

        for (var i = 0; i < filteredPlayers.Count; i++)
        {
            var player = filteredPlayers[i];

            playerDistribution[i] = player.master;
            if (oldTarget && player.master == oldTarget)
                index = i;
        }

        if (index == -1)
            index = playerDistribution.Length > 0 ? Random.Range(0, playerDistribution.Length) : 0;
    }

    public override CharacterMaster GetTarget(Vector3 position, PickupIndex pickupIndex,
        TargetFilter extraFilter = null)
    {
        if (playerDistribution == null || playerDistribution.Length == 0)
            return null;

        if (index < 0 || index >= playerDistribution.Length)
            index = 0;

        CharacterMaster target = null;

        for (var indexOffset = 0; indexOffset < playerDistribution.Length; indexOffset++)
        {
            var potentialTarget = playerDistribution[(index + indexOffset) % playerDistribution.Length];

            // A master can be destroyed between target updates
            if (!potentialTarget) continue;
            if (extraFilter != null && !extraFilter(potentialTarget)) continue;

            target = potentialTarget;
            index = (index + indexOffset + 1) % playerDistribution.Length;
            break;
        }

        return target;
    }
}
