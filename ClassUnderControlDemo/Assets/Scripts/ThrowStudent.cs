using UnityEngine;

public class ThrowStudent : MonoBehaviour, IClassStudent, IAnnoyableStudent
{
    public Transform seatPoint;
    public Transform throwPoint;
    public Transform playerTarget;
    public AudioSource audioSource;
    public AudioClip prankClip;
    public GameObject paperBallPrefab;

    public float minWait = 10f;
    public float maxWait = 15f;

    public float launchSpeed = 10f;
    public float extraUpward = 1f;
    public float throwAtNormalizedTime = 1f;

    public string castingStateName = "Casting Spell";
    public string sittingStateName = "Sitting";
    public string annoyedStateName = "Annoyed";
    public string stunnedStateName = "Stunned";
    public float castCrossFade = 0.08f;
    public float annoyedCrossFade = 0.08f;
    public float stunnedCrossFade = 0.08f;
    public float fallbackCastDuration = 1f;
    public bool useAnimationEventForThrow = true;

    public ParticleSystem castingWarningParticles;

    float timer;
    float castTimer;
    float castingClipLength;
    bool isCasting;
    bool throwReleasedThisCast;
    int castingStateHash;
    bool classEnded;
    bool isStunned;
    float stunTimer;

    Animator animator;

    public string studentName = "";
    public string raiseHandStateName = "RaiseHand";
    public AudioSource voiceReplySource;
    public AudioClip presentClip;
    public float raiseHandCrossFade = 0.05f;
    static readonly int ThrowHash = Animator.StringToHash("Throw");

    bool externallyAnnoyed;

    public Transform SeatPoint => seatPoint;
    public bool CanBeAnnoyed => !classEnded && !externallyAnnoyed && !isStunned && !IsClassTimerPaused() && !isCasting;

    void Start()
    {
        animator = GetComponent<Animator>();
        castingStateHash = Animator.StringToHash(castingStateName);
        CacheCastingClipLength();
        StopCastingWarning(true);
        if (seatPoint)
        {
            transform.position = seatPoint.position;
            transform.rotation = seatPoint.rotation;
        }
        ScheduleNext();
    }

    void Update()
    {
        if (classEnded) return;
        if (UpdateStunTimer()) return;
        if (IsClassTimerPaused()) return;
        if (externallyAnnoyed) return;

        if (playerTarget)
        {
            Vector3 look = playerTarget.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 5f * Time.deltaTime);
        }

        if (isCasting)
        {
            if (HasReachedThrowMoment())
            {
                ReleaseThrow();
                return;
            }

            castTimer -= Time.deltaTime;
            if (castTimer <= 0f)
            {
                ReleaseThrow();
            }
            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            BeginCast();
        }
    }

    void ScheduleNext()
    {
        timer = Random.Range(minWait, maxWait);
    }

    void BeginCast()
    {
        if (classEnded) return;

        isCasting = true;
        throwReleasedThisCast = false;
        castingStateHash = Animator.StringToHash(castingStateName);
        castTimer = castingClipLength > 0f ? castingClipLength : fallbackCastDuration;
        PlayCastingWarning();

        if (audioSource && prankClip) audioSource.PlayOneShot(prankClip);

        if (!animator) return;

        if (!string.IsNullOrWhiteSpace(castingStateName))
        {
            int castStateHash = Animator.StringToHash(castingStateName);
            if (animator.HasState(0, castStateHash))
            {
                animator.CrossFadeInFixedTime(castStateHash, castCrossFade);
                return;
            }
        }

        animator.SetTrigger(ThrowHash);
    }

    void CacheCastingClipLength()
    {
        castingClipLength = fallbackCastDuration;

        if (!animator || animator.runtimeAnimatorController == null) return;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (!clip) continue;
            if (clip.name == castingStateName || clip.name.Contains(castingStateName))
            {
                castingClipLength = clip.length;
                return;
            }
        }
    }

    public void AnimationEvent_ReleaseThrow()
    {
        if (classEnded) return;
        if (!useAnimationEventForThrow) return;
        if (!HasReachedThrowMoment()) return;
        ReleaseThrow();
    }

    void ReleaseThrow()
    {
        if (!isCasting || throwReleasedThisCast) return;
        throwReleasedThisCast = true;
        StopCastingWarning(false);

        ThrowPaper();
        isCasting = false;
        ScheduleNext();
    }

    bool HasReachedThrowMoment()
    {
        if (!animator) return false;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.shortNameHash == castingStateHash || stateInfo.IsName(castingStateName))
        {
            return stateInfo.normalizedTime >= throwAtNormalizedTime;
        }

        return false;
    }

    void PlayCastingWarning()
    {
        if (!castingWarningParticles) return;
        castingWarningParticles.Play(true);
    }

    void StopCastingWarning(bool clear)
    {
        if (!castingWarningParticles) return;

        var stopBehavior = clear
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;
        castingWarningParticles.Stop(true, stopBehavior);
    }

    void ThrowPaper()
    {
        if (!paperBallPrefab || !throwPoint || !playerTarget) return;

        GameObject go = Instantiate(paperBallPrefab, throwPoint.position, Quaternion.identity);

        Vector3 toTarget = (playerTarget.position - throwPoint.position).normalized;
        Vector3 velocity = (toTarget + Vector3.up * extraUpward).normalized * launchSpeed;

        var rb = go.GetComponent<Rigidbody>();
        if (rb) rb.linearVelocity = velocity;
        var proj = go.GetComponent<PaperProjectile>();
        if (proj) proj.target = playerTarget;
    }

    public void OnClassEnded()
    {
        if (classEnded) return;

        classEnded = true;
        externallyAnnoyed = false;
        isStunned = false;
        stunTimer = 0f;
        isCasting = false;
        throwReleasedThisCast = true;
        timer = 0f;
        castTimer = 0f;
        StopCastingWarning(true);

        if (seatPoint)
        {
            transform.SetPositionAndRotation(seatPoint.position, seatPoint.rotation);
        }

        if (animator && !string.IsNullOrWhiteSpace(sittingStateName))
        {
            int sitHash = Animator.StringToHash(sittingStateName);
            if (animator.HasState(0, sitHash))
            {
                animator.CrossFadeInFixedTime(sitHash, castCrossFade);
            }
        }
    }

    public void OnNameCalled()
    {
        if (classEnded || isStunned) return;

        if (animator && !string.IsNullOrWhiteSpace(raiseHandStateName))
        {
            int hash = Animator.StringToHash(raiseHandStateName);
            if (animator.HasState(0, hash))
                animator.CrossFadeInFixedTime(hash, raiseHandCrossFade);
        }

        if (voiceReplySource && presentClip)
            voiceReplySource.PlayOneShot(presentClip);
    }

    public void BeginBeingAnnoyed(AnnoyingStudent annoyer)
    {
        if (!CanBeAnnoyed) return;

        externallyAnnoyed = true;
        isCasting = false;
        throwReleasedThisCast = true;
        castTimer = 0f;
        StopCastingWarning(true);
        PlayAnimationState(annoyedStateName, annoyedCrossFade);
    }

    public void StopBeingAnnoyed(AnnoyingStudent annoyer)
    {
        if (!externallyAnnoyed) return;

        externallyAnnoyed = false;
        if (classEnded || isStunned) return;

        ScheduleNext();
        PlayAnimationState(sittingStateName, castCrossFade);
    }

    public void Stun(float duration)
    {
        if (classEnded || duration <= 0f) return;

        externallyAnnoyed = false;
        isStunned = true;
        stunTimer = Mathf.Max(stunTimer, duration);
        isCasting = false;
        throwReleasedThisCast = true;
        castTimer = 0f;
        timer = 0f;

        StopCastingWarning(true);
        PlayAnimationState(stunnedStateName, stunnedCrossFade);
    }

    bool IsClassTimerPaused()
    {
        return GameManager.I != null && GameManager.I.classTimerPaused;
    }

    void PlayAnimationState(string stateName, float crossFade)
    {
        if (!animator || string.IsNullOrWhiteSpace(stateName)) return;

        int hash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, hash)) return;

        animator.CrossFadeInFixedTime(hash, crossFade);
    }

    bool UpdateStunTimer()
    {
        if (!isStunned)
            return false;

        stunTimer -= Time.deltaTime;
        if (stunTimer <= 0f)
            EndStun();

        return true;
    }

    void EndStun()
    {
        isStunned = false;
        stunTimer = 0f;

        if (classEnded)
            return;

        if (seatPoint)
            transform.SetPositionAndRotation(seatPoint.position, seatPoint.rotation);

        ScheduleNext();
        PlayAnimationState(sittingStateName, castCrossFade);
    }
}
