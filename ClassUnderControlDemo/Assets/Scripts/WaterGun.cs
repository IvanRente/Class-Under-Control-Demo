using UnityEngine;

public class WaterGun : PlayerInventoryItem
{
    public WaterGun(PlayerItemSystem itemSystem, PlayerItemDefinition definition)
        : base(itemSystem, definition)
    {
    }

    public override bool TryHandlePrimaryAction(Camera sourceCamera, bool pressedThisFrame, bool heldThisFrame, bool releasedThisFrame)
    {
        if (heldThisFrame)
            SprayWater(sourceCamera, Time.deltaTime);

        return pressedThisFrame || heldThisFrame || releasedThisFrame;
    }

    void SprayWater(Camera sourceCamera, float deltaTime)
    {
        if (sourceCamera == null)
            return;

        float durabilityCost = DurabilityCostPerSecond * Mathf.Max(0f, deltaTime);
        if (!TryConsumeDurability(durabilityCost))
            return;

        Ray sprayRay = new Ray(sourceCamera.transform.position, sourceCamera.transform.forward);
        RaycastHit hit;
        if (!Physics.SphereCast(
                sprayRay,
                Mathf.Max(0f, itemSystem.WaterPistolHitRadius),
                out hit,
                Mathf.Max(0.1f, itemSystem.WaterPistolRange),
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide))
        {
            return;
        }

        FireHazard fireHazard = ResolveFireHazard(hit.collider);
        if (fireHazard != null && fireHazard.IsLit)
            fireHazard.Extinguish();
    }
}
