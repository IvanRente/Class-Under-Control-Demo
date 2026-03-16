using UnityEngine;

public class WaterGun : PlayerInventoryItem
{
    ParticleSystem waterJetParticles;

    public WaterGun(PlayerItemSystem itemSystem, PlayerItemDefinition definition)
        : base(itemSystem, definition)
    {
    }

    public override void OnEquipped()
    {
        CacheWaterJetParticles();
        StopWaterJet();
    }

    public override void OnUnequipped()
    {
        StopWaterJet();
        waterJetParticles = null;
    }

    public override void StopActiveUse()
    {
        StopWaterJet();
    }

    public override bool TryHandlePrimaryAction(Camera sourceCamera, bool pressedThisFrame, bool heldThisFrame, bool releasedThisFrame)
    {
        if (heldThisFrame)
            SprayWater(sourceCamera, Time.deltaTime);
        else
            StopWaterJet();

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

    void CacheWaterJetParticles()
    {
        waterJetParticles = null;

        if (itemSystem == null || itemSystem.EquippedVisual == null)
            return;

        ParticleSystem[] particleSystems = itemSystem.EquippedVisual.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem candidate = particleSystems[i];
            if (candidate == null)
                continue;

            string candidateName = candidate.name.ToLowerInvariant();
            if (candidateName.Contains("water") || candidateName.Contains("jet"))
            {
                waterJetParticles = candidate;
                return;
            }
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem candidate = particleSystems[i];
            if (candidate == null)
                continue;

            waterJetParticles = candidate;
            break;
        }
    }

    void PlayWaterJet()
    {
        if (waterJetParticles == null)
            CacheWaterJetParticles();

        if (waterJetParticles != null && !waterJetParticles.isPlaying)
            waterJetParticles.Play(true);
    }

    void StopWaterJet()
    {
        if (waterJetParticles == null)
            return;

        waterJetParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
