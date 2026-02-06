using UnityEngine;

public class ThrowStudent : MonoBehaviour
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

    [Header("Animation Timing")]
    public string castingStateName = "Casting Spell";
    public float castCrossFade = 0.08f;
    public float fallbackCastDuration = 1f;
    public bool useAnimationEventForThrow = true;

    float timer;
    float castTimer;
    float castingClipLength;
    bool isCasting;
    bool throwReleasedThisCast;

    Animator animator;
    static readonly int ThrowHash = Animator.StringToHash("Throw");

    void Start()
    {
        animator = GetComponent<Animator>();
        CacheCastingClipLength();
        if (seatPoint)
        {
            transform.position = seatPoint.position;
            transform.rotation = seatPoint.rotation;
        }
        ScheduleNext();
    }

    void Update()
    {
        if (playerTarget)
        {
            Vector3 look = playerTarget.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 5f * Time.deltaTime);
        }

        if (isCasting)
        {
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
        isCasting = true;
        throwReleasedThisCast = false;
        castTimer = castingClipLength > 0f ? castingClipLength : fallbackCastDuration;

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
        if (!useAnimationEventForThrow) return;
        ReleaseThrow();
    }

    void ReleaseThrow()
    {
        if (!isCasting || throwReleasedThisCast) return;
        throwReleasedThisCast = true;

        ThrowPaper();
        isCasting = false;
        ScheduleNext();
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
}
