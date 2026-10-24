using RoR2;
using UnityEngine;

namespace AutoItemPickup.ItemDistributors;

internal class FixedTargetDistributor : ItemDistributor
{
    private readonly CharacterMaster target;

    public FixedTargetDistributor(AutoItemPickup plugin, PlayerCharacterMasterController target) : this(plugin,
        target.master) {}

    public FixedTargetDistributor(AutoItemPickup plugin, CharacterMaster target) : base(plugin)
    {
        this.target = target;
    }

    public override bool IsValid()
    {
        return IsValidTarget(target);
    }

    public override void UpdateTargets()
    {
    }

    public override CharacterMaster GetTarget(Vector3 position, PickupIndex pickupIndex,
        TargetFilter extraFilter = null)
    {
        if (extraFilter != null && !extraFilter(target)) return null;
        return target;
    }
}