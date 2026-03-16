using UnityEngine;

public class AirHorn : PlayerInventoryItem
{
    public AirHorn(PlayerItemSystem itemSystem, PlayerItemDefinition definition)
        : base(itemSystem, definition)
    {
    }

    public override bool TryHandlePrimaryAction(Camera sourceCamera, bool pressedThisFrame, bool heldThisFrame, bool releasedThisFrame)
    {
        if (!pressedThisFrame)
            return false;

        UseHorn();
        return true;
    }

    void UseHorn()
    {
        if (!TryConsumeDurability(DurabilityCostPerUse))
            return;

        if (itemSystem.AirHornClip != null)
            AudioSource.PlayClipAtPoint(itemSystem.AirHornClip, itemSystem.transform.position);

        if (GameManager.I != null)
            GameManager.I.StunAllStudents(itemSystem.AirHornStunDuration);
    }
}
