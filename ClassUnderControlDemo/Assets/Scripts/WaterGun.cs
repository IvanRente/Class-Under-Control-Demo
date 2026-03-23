using UnityEngine;

public class WaterGun : PlayerInventoryItem
{
    Transform waterJetSourceTransform;
    ParticleSystem waterJetParticles;

    public WaterGun(PlayerItemSystem itemSystem, PlayerItemDefinition definition)
        : base(itemSystem, definition)
    {
    }

    public override void OnEquipped()
    {
        waterJetSourceTransform = null;
        waterJetParticles = null;

        if (itemSystem != null && itemSystem.EquippedVisual != null)
        {
            ParticleSystem sourceParticles = itemSystem.EquippedVisual.GetComponentInChildren<ParticleSystem>(true);
            if (sourceParticles != null)
            {
                waterJetSourceTransform = sourceParticles.transform;
                waterJetParticles = Object.Instantiate(sourceParticles, sourceParticles.transform.position, sourceParticles.transform.rotation);
                waterJetParticles.transform.localScale = sourceParticles.transform.lossyScale;
                waterJetParticles.gameObject.name = sourceParticles.gameObject.name + "_Runtime";
                waterJetParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        if (waterJetParticles != null)
            waterJetParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public override void OnUnequipped()
    {
        StopWaterJet();

        if (waterJetParticles != null)
            Object.Destroy(waterJetParticles.gameObject);

        waterJetSourceTransform = null;
        waterJetParticles = null;
    }

    public override void StopActiveUse()
    {
        StopWaterJet();
    }

    public override bool TryHandlePrimaryAction(Camera sourceCamera, bool pressedThisFrame, bool heldThisFrame, bool releasedThisFrame)
    {
        if (!heldThisFrame)
        {
            StopWaterJet();
            return pressedThisFrame || heldThisFrame || releasedThisFrame;
        }

        SprayWater(sourceCamera, Time.deltaTime);

        if (releasedThisFrame)
            StopWaterJet();

        return pressedThisFrame || heldThisFrame || releasedThisFrame;
    }

    void SprayWater(Camera sourceCamera, float deltaTime)
    {
        if (sourceCamera == null)
        {
            StopWaterJet();
            return;
        }

        float durabilityCost = DurabilityCostPerSecond * Mathf.Max(0f, deltaTime);
        if (!TryConsumeDurability(durabilityCost))
        {
            StopWaterJet();
            return;
        }

        SyncWaterJetTransform();
        PlayWaterJet();

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

    void PlayWaterJet()
    {
        if (waterJetParticles != null && !waterJetParticles.isPlaying)
            waterJetParticles.Play(true);
    }

    void SyncWaterJetTransform()
    {
        if (waterJetParticles == null || waterJetSourceTransform == null)
            return;

        waterJetParticles.transform.SetPositionAndRotation(waterJetSourceTransform.position, waterJetSourceTransform.rotation);
        waterJetParticles.transform.localScale = waterJetSourceTransform.lossyScale;
    }

    void StopWaterJet()
    {
        if (waterJetParticles == null)
            return;

        waterJetParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
