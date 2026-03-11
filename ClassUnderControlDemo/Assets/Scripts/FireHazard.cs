using UnityEngine;

public class FireHazard : MonoBehaviour
{
    public ParticleSystem fireParticles;
    public Collider interactionCollider;
    public float gpaPenaltyPerSecond = 0.1f;
    public float extinguishHoldDuration = 2f;
    public bool destroyOnExtinguish = true;
    public float destroyDelay = 0.1f;

    float gpaTickTimer;
    float extinguishHoldTimer;
    bool isLit;

    public bool IsLit => isLit;
    public float HoldProgressNormalized => extinguishHoldDuration <= 0f ? 1f : Mathf.Clamp01(extinguishHoldTimer / extinguishHoldDuration);

    void Awake()
    {
        if (fireParticles == null)
            fireParticles = GetComponentInChildren<ParticleSystem>(true);

        if (interactionCollider == null)
        {
            interactionCollider = GetComponent<Collider>();
            if (interactionCollider == null)
                interactionCollider = GetComponentInChildren<Collider>(true);
        }

        SetFireVisuals(false, true);
    }

    void Update()
    {
        if (!isLit) return;
        if (GameManager.I != null && (GameManager.I.classTimerPaused || GameManager.I.IsClassEnded)) return;

        gpaTickTimer += Time.deltaTime;
        while (gpaTickTimer >= 1f)
        {
            gpaTickTimer -= 1f;
            if (GameManager.I != null)
                GameManager.I.SubGPA(gpaPenaltyPerSecond);
        }
    }

    public void Ignite()
    {
        if (isLit) return;

        isLit = true;
        gpaTickTimer = 0f;
        extinguishHoldTimer = 0f;
        SetFireVisuals(true, false);
    }

    public void ProgressExtinguishHold(float deltaTime)
    {
        if (!isLit) return;

        extinguishHoldTimer += Mathf.Max(0f, deltaTime);
        if (extinguishHoldTimer >= extinguishHoldDuration)
            Extinguish();
    }

    public void CancelExtinguishHold()
    {
        extinguishHoldTimer = 0f;
    }

    public void Extinguish()
    {
        if (!isLit) return;

        isLit = false;
        extinguishHoldTimer = 0f;
        gpaTickTimer = 0f;
        SetFireVisuals(false, false);

        if (destroyOnExtinguish)
            Destroy(gameObject, destroyDelay);
    }

    void SetFireVisuals(bool lit, bool clear)
    {
        if (fireParticles != null)
        {
            if (lit)
            {
                fireParticles.Play(true);
            }
            else
            {
                ParticleSystemStopBehavior stopBehavior = clear
                    ? ParticleSystemStopBehavior.StopEmittingAndClear
                    : ParticleSystemStopBehavior.StopEmitting;
                fireParticles.Stop(true, stopBehavior);
            }
        }

        if (interactionCollider != null)
            interactionCollider.enabled = lit;
    }
}
