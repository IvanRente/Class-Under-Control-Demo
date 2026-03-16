using UnityEngine;

public class StudentAI : MonoBehaviour, IClassStudent, IAnnoyableStudent
{
    public enum State { IdleAtSeat, TryingToLeave, ReturningToSeat }

    public State currentState = State.IdleAtSeat;

    public Transform seatPoint;
    public Transform doorPoint;
    public float walkSpeed = 1.2f;
    public float doorReachDistance = 1.1f;
    public float seatReachDistance = 0.2f;

    public float minWaitBeforeLeave = 5f;
    public float maxWaitBeforeLeave = 10f;
    float leaveTimer;

    public GameObject escapeVFX;

    public float escapePenalty = 1.0f;

    public Transform playerTarget;
    public float catchDistance = 1.25f;
    public string sittingStateName = "Sitting";
    public string sneakingStateName = "Sneaking";
    public string sadWalkStateName = "SadWalk";
    public string annoyedStateName = "Annoyed";
    public string stunnedStateName = "Stunned";
    public string raiseHandStateName = "RaiseHand";
    public float animCrossFade = 0.08f;
    public float annoyedCrossFade = 0.08f;
    public float stunnedCrossFade = 0.08f;

    Animator animator;
    Rigidbody rb;
    CapsuleCollider capsule;
    static readonly int AnimState = Animator.StringToHash("State");
    bool classEnded;

    public AudioSource voiceReplySource;
    public AudioClip presentClip;
    public float raiseHandCrossFade = 0.05f;

    public string studentName = "Jon";

    bool externallyAnnoyed;
    bool isStunned;
    float stunTimer;

    public Transform SeatPoint => seatPoint;
    public bool CanBeAnnoyed => !classEnded && !externallyAnnoyed && !isStunned && !IsClassTimerPaused() && currentState == State.IdleAtSeat;

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        if (animator) animator.applyRootMotion = false;
        if (rb)
        {
            rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.constraints &= ~RigidbodyConstraints.FreezePositionY;
        }
        if (seatPoint)
        {
            TeleportTo(seatPoint);
        }
        SetState(State.IdleAtSeat, true);
        if (escapeVFX) escapeVFX.SetActive(false);
    }

    void Update()
    {
        if (classEnded) return;
        if (UpdateStunTimer()) return;
        if (IsClassTimerPaused()) return;
        if (externallyAnnoyed) return;

        switch (currentState)
        {
            case State.IdleAtSeat:
                HandleIdle();
                break;
            case State.TryingToLeave:
                HandleTryingToLeave();
                break;
            case State.ReturningToSeat:
                HandleReturningToSeat();
                break;
        }
    }

    void SetState(State newState, bool force = false)
    {
        if (!force && currentState == newState) return;
        currentState = newState;
        ApplyPhysicsForState(newState);
        if (newState == State.IdleAtSeat) ScheduleNextLeave();

        if (!animator) return;

        animator.SetInteger(AnimState, (int)newState);

        string targetAnimState = sittingStateName;
        if (newState == State.TryingToLeave) targetAnimState = sneakingStateName;
        else if (newState == State.ReturningToSeat) targetAnimState = sadWalkStateName;

        if (!string.IsNullOrWhiteSpace(targetAnimState))
        {
            int hash = Animator.StringToHash(targetAnimState);
            if (animator.HasState(0, hash))
            {
                animator.CrossFadeInFixedTime(hash, animCrossFade);
            }
        }
    }

    public void OnNameCalled()
    {
        if (classEnded || isStunned) return;

        if (currentState == State.IdleAtSeat && animator)
        {
            animator.ResetTrigger("RaiseHand");
            animator.SetTrigger("RaiseHand");
        }

        if (voiceReplySource && presentClip)
            voiceReplySource.PlayOneShot(presentClip);
    }

    public void BeginBeingAnnoyed(AnnoyingStudent annoyer)
    {
        if (!CanBeAnnoyed) return;

        externallyAnnoyed = true;
        StopPlanarMovement();

        if (escapeVFX) escapeVFX.SetActive(false);
        PlayAnimationState(annoyedStateName, annoyedCrossFade);
    }

    public void StopBeingAnnoyed(AnnoyingStudent annoyer)
    {
        if (!externallyAnnoyed) return;

        externallyAnnoyed = false;

        if (classEnded || isStunned) return;
        SetState(State.IdleAtSeat, true);
    }

    public void Stun(float duration)
    {
        if (classEnded || duration <= 0f) return;

        externallyAnnoyed = false;
        isStunned = true;
        stunTimer = Mathf.Max(stunTimer, duration);

        if (escapeVFX) escapeVFX.SetActive(false);
        StopAndFreezeForStun();
        PlayAnimationState(stunnedStateName, stunnedCrossFade);
    }

    void HandleIdle()
    {
        leaveTimer -= Time.deltaTime;
        if (leaveTimer <= 0f)
        {
            SetState(State.TryingToLeave);
            if (escapeVFX) escapeVFX.SetActive(true);
        }
    }

    void HandleTryingToLeave()
    {
        if (IsPlayerCloseEnoughToCatch())
        {
            BeginReturnToSeat();
            return;
        }

        if (!doorPoint)
        {
            StopPlanarMovement();
            return;
        }

        float dist = PlanarDistance(GetCurrentPosition(), doorPoint.position);
        if (dist <= GetDoorReachDistance())
        {
            StopPlanarMovement();
            GameManager.I.SubGPA(escapePenalty);
            TeleportTo(seatPoint);
            SetState(State.IdleAtSeat);
            if (escapeVFX) escapeVFX.SetActive(false);
            return;
        }

        MoveTowards(doorPoint.position);
    }

    void HandleReturningToSeat()
    {
        if (!seatPoint) return;

        float dist = PlanarDistance(GetCurrentPosition(), seatPoint.position);
        if (dist <= GetSeatReachDistance())
        {
            StopPlanarMovement();
            TeleportTo(seatPoint);
            SetState(State.IdleAtSeat);
            if (escapeVFX) escapeVFX.SetActive(false);
            return;
        }

        MoveTowards(seatPoint.position);
    }

    bool IsPlayerCloseEnoughToCatch()
    {
        if (!playerTarget) return false;

        Vector3 studentPos = transform.position;
        Vector3 playerPos = playerTarget.position;
        studentPos.y = 0f;
        playerPos.y = 0f;

        return Vector3.Distance(studentPos, playerPos) <= catchDistance;
    }

    void BeginReturnToSeat()
    {
        if (classEnded) return;
        SetState(State.ReturningToSeat);
        if (escapeVFX) escapeVFX.SetActive(false);
    }

    void MoveTowards(Vector3 targetPos)
    {
        Vector3 currentPos = rb ? rb.position : transform.position;
        Vector3 dir = (targetPos - currentPos);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            dir = dir.normalized;
            if (rb && !rb.isKinematic)
            {
                Vector3 v = rb.linearVelocity;
                v.x = dir.x * walkSpeed;
                v.z = dir.z * walkSpeed;
                rb.linearVelocity = v;
            }
            else
            {
                transform.position += dir * walkSpeed * Time.deltaTime;
            }

            transform.forward = Vector3.Lerp(transform.forward, dir, 10f * Time.deltaTime);
        }
        else
        {
            StopPlanarMovement();
        }
    }

    void StopPlanarMovement()
    {
        if (rb && !rb.isKinematic)
        {
            Vector3 v = rb.linearVelocity;
            v.x = 0f;
            v.z = 0f;
            rb.linearVelocity = v;
        }
    }

    void ApplyPhysicsForState(State state)
    {
        if (!rb) return;

        bool seated = state == State.IdleAtSeat;
        rb.isKinematic = seated;
        rb.useGravity = !seated;

        if (seated)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    void TeleportTo(Transform target)
    {
        if (!target) return;

        transform.SetPositionAndRotation(target.position, target.rotation);

        if (rb)
        {
            rb.position = target.position;
            rb.rotation = target.rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    Vector3 GetCurrentPosition()
    {
        return rb ? rb.position : transform.position;
    }

    float GetDoorReachDistance()
    {
        float d = doorReachDistance > 0f ? doorReachDistance : 1.1f;
        if (capsule) d = Mathf.Max(d, capsule.radius + 0.35f);
        return d;
    }

    float GetSeatReachDistance()
    {
        return seatReachDistance > 0f ? seatReachDistance : 0.2f;
    }

    void ScheduleNextLeave()
    {
        float min = Mathf.Max(0.5f, minWaitBeforeLeave);
        float max = Mathf.Max(min, maxWaitBeforeLeave);
        leaveTimer = Random.Range(min, max);
    }

    void OnTriggerEnter(Collider other)
    {
        if (classEnded) return;
        if (isStunned) return;

        if (currentState == State.TryingToLeave && other.CompareTag("Player"))
        {
            BeginReturnToSeat();
        }
    }

    public void OnClassEnded()
    {
        if (classEnded) return;

        classEnded = true;
        externallyAnnoyed = false;
        isStunned = false;
        stunTimer = 0f;
        if (escapeVFX) escapeVFX.SetActive(false);

        if (seatPoint)
        {
            StopPlanarMovement();
            TeleportTo(seatPoint);
        }

        SetState(State.IdleAtSeat, true);
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

        if (!seatPoint)
        {
            SetState(State.IdleAtSeat, true);
            return;
        }

        float distanceToSeat = PlanarDistance(GetCurrentPosition(), seatPoint.position);
        if (distanceToSeat <= GetSeatReachDistance())
        {
            TeleportTo(seatPoint);
            SetState(State.IdleAtSeat, true);
        }
        else
        {
            SetState(State.ReturningToSeat, true);
        }
    }

    void StopAndFreezeForStun()
    {
        StopPlanarMovement();

        if (!rb)
            return;

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}
