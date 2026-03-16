using UnityEngine;

public abstract class PlayerInventoryItem
{
    protected readonly PlayerItemSystem itemSystem;
    protected readonly PlayerItemDefinition definition;

    protected PlayerInventoryItem(PlayerItemSystem itemSystem, PlayerItemDefinition definition)
    {
        this.itemSystem = itemSystem;
        this.definition = definition;
    }

    public virtual void OnEquipped()
    {
    }

    public virtual void OnUnequipped()
    {
    }

    public virtual void StopActiveUse()
    {
    }

    public virtual bool TryBlockProjectile()
    {
        return false;
    }

    public virtual bool TryHandlePrimaryAction(Camera sourceCamera, bool pressedThisFrame, bool heldThisFrame, bool releasedThisFrame)
    {
        return false;
    }

    protected bool TryConsumeDurability(float amount)
    {
        return itemSystem != null && itemSystem.TryConsumeItemDurability(definition.itemId, amount);
    }

    protected float DurabilityCostPerUse => Mathf.Max(0f, definition != null ? definition.durabilityCostPerUse : 0f);
    protected float DurabilityCostPerSecond => Mathf.Max(0f, definition != null ? definition.durabilityCostPerSecond : 0f);

    protected FireHazard ResolveFireHazard(Collider hitCollider)
    {
        return itemSystem != null ? itemSystem.ResolveFireHazard(hitCollider) : null;
    }
}
