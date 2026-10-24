using System.Linq;
using RoR2;
using UnityEngine;

namespace AutoItemPickup.ItemDistributors;

internal class ClosestDistributor : ItemDistributor
{
    private CharacterMaster[] targets;

    public ClosestDistributor(AutoItemPickup plugin) : base(plugin) {}

    public override void UpdateTargets()
    {
        targets = GetValidTargets().Select(player => player.master).ToArray();
    }

    public override CharacterMaster GetTarget(Vector3 position, PickupIndex pickupIndex,
        TargetFilter extraFilter = null)
    {
        CharacterMaster closestTarget = null;
        var closestDistance = float.MaxValue;

        var itemPosition = position;

        foreach (var target in targets)
        {
            if (!target) continue;
            if (extraFilter != null && !extraFilter(target)) continue;
            var targetPos = target.hasBody ? target.GetBodyObject().transform.position : target.deathFootPosition;

            var distance = (itemPosition - targetPos).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestTarget = target;
                closestDistance = distance;
            }
        }

        return closestTarget;
    }
}
