public class Shield : PlayerInventoryItem
{
    public Shield(PlayerItemSystem itemSystem, PlayerItemDefinition definition)
        : base(itemSystem, definition)
    {
    }

    public override bool TryBlockProjectile()
    {
        return TryConsumeDurability(DurabilityCostPerUse);
    }
}
