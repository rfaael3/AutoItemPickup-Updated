using System.Linq;
using RoR2;
using UnityEngine;

namespace AutoItemPickup.ItemDistributors;

internal class LeastItemsDistributor : ItemDistributor
{
    private CharacterMaster[] targets;

    public LeastItemsDistributor(AutoItemPickup plugin) : base(plugin) {}

    public override void UpdateTargets()
    {
        var filteredPlayers = GetValidTargets();
        targets = new CharacterMaster[filteredPlayers.Count];

        for (var i = 0; i < filteredPlayers.Count; i++)
        {
            var player = filteredPlayers[i];

            targets[i] = player.master;
        }
    }

    public override CharacterMaster GetTarget(Vector3 position, PickupIndex pickupIndex,
        TargetFilter extraFilter = null)
    {
        CharacterMaster leastItems = null;
        var leastItemCount = int.MaxValue;

        var pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
        // Pickups that are not regular items (or whose item no longer exists) have no tier to compare
        var itemDef = pickupDef != null ? ItemCatalog.GetItemDef(pickupDef.itemIndex) : null;

        foreach (var player in targets)
        {
            if (!player) continue;
            if (extraFilter != null && !extraFilter(player)) continue;

            // Without a tier, the first valid target is as good as any other
            if (itemDef == null || !player.inventory) return player;

            var count = player.inventory.GetTotalItemCountOfTier(itemDef.tier);
            if (count < leastItemCount)
            {
                leastItems = player;
                leastItemCount = count;
            }
        }

        return leastItems;
    }
}
