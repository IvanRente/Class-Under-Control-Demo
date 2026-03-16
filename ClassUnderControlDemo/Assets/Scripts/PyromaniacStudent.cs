using UnityEngine;

public class PyromaniacStudent : MonoBehaviour, IClassStudent, IAnnoyableStudent
{
    public enum State { IdleAtSeat, MovingToFirePoint, CreatingFire, ReturningToSeat }

    public State currentState = State.IdleAtSeat;

    public Transform seatPoint;
    public Transform playerTarget;
    public bool autoFindPlayerTarget = true;
    public Transform[] firePoints = new Transform[0];
    public GameObject fireHazardPrefab;
    public GameObject lighterObject;
    public bool randomizeFirePointOrder;
    public Vector3 fireSpawnLocalOffset;
    public float walkSpeed = 1.25f;
    public float seatReachDistance = 0.2f;
    public float firePointReachDistance = 0.35f;
    public float catchDistance = 1.25f;
    public float minWaitBeforeFire = 10f;
    public float maxWaitBeforeFire = 16f;
    public float fireCreateDelay = 2f;

    public string sittingStateName = "Sitting";
    public string walkStateName = "Walk";
    public string crouchStateName = "CreateFire";
    public string annoyedStateName = "Annoyed";
    public string stunnedStateName = "Stunned";
    public string raiseHandStateName = "RaiseHand";
    public float animCrossFade = 0.08f;
    public float annoyedCrossFade = 0.08f;
    public float stunnedCrossFade = 0.08f;
    public float raiseHandCrossFade = 0.05f;

    public AudioSource voiceReplySource;
    public AudioClip presentClip;
    public string studentName = "";

    Animator animator;
    Rigidbody rb;
    int[] fireOrder = new int[0];
    int fireOrderCursor;
    int activeFirePointIndex = -1;
    float nextActionTimer;
    float fireCreateTimer;
    bool classEnded;
    bool externallyAnnoyed;
    bool hasCompletedFireRoutine;
    bool[] firePointUsed = new bool[0];
    bool isStunned;
    float stunTimer;

    public Transform SeatPoint => seatPoint;
    public bool CanBeAnnoyed => !classEnded && !externallyAnnoyed && !isStunned && !IsClassTimerPaused() && currentState == State.IdleAtSeat;

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        if (animator) animator.applyRootMotion = false;
        if (rb)
        {
            rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.constraints &= ~RigidbodyConstraints.FreezePositionY;
        }

        if (seatPoint)
            TeleportTo(seatPoint);

        ResolvePlayerTarget();
        SetLighterVisible(false);
        EnsureFirePointStateArrays();
        SetState(State.IdleAtSeat, true);
    }

    void Update()
    {
        if (classEnded) return;
        if (UpdateStunTimer()) return;
        if (IsClassTimerPaused()) return;
        if (externallyAnnoyed) return;

        if ((currentState == State.MovingToFirePoint || currentState == State.CreatingFire) && IsPlayerCloseEnoughToCatch())
        {
            AbortFireRoutine();
            return;
        }

        switch (currentState)
        {
            case State.IdleAtSeat:
                HandleIdle();
                break;
            case State.MovingToFirePoint:
                HandleMovingToFirePoint();
                break;
            case State.CreatingFire:
                HandleCreatingFire();
                break;
            case State.ReturningToSeat:
                HandleReturningToSeat();
                break;
        }
    }

    public void OnNameCalled()
    {
        if (classEnded || isStunned) return;

        PlayAnimationState(raiseHandStateName, raiseHandCrossFade);

        if (voiceReplySource && presentClip)
            voiceReplySource.PlayOneShot(presentClip);
    }

    public void OnClassEnded()
    {
        if (classEnded) return;

        classEnded = true;
        externallyAnnoyed = false;
        isStunned = false;
        stunTimer = 0f;
        SetLighterVisible(false);
        StopPlanarMovement();

        if (seatPoint)
            TeleportTo(seatPoint);

        SetState(State.IdleAtSeat, true);
    }

    public void BeginBeingAnnoyed(AnnoyingStudent annoyer)
    {
        if (!CanBeAnnoyed) return;

        externallyAnnoyed = true;
        SetLighterVisible(false);
        StopPlanarMovement();
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
        activeFirePointIndex = -1;
        fireCreateTimer = 0f;

        SetLighterVisible(false);
        StopAndFreezeForStun();
        PlayAnimationState(stunnedStateName, stunnedCrossFade);
    }

    public void NotifyPlayerCollision()
    {
        if (classEnded) return;
        if (isStunned) return;

        if (currentState == State.MovingToFirePoint || currentState == State.CreatingFire)
            AbortFireRoutine();
    }

    void HandleIdle()
    {
        if (hasCompletedFireRoutine) return;

        nextActionTimer -= Time.deltaTime;
        if (nextActionTimer > 0f) return;

        if (!TryStartFireRoutine())
        {
            hasCompletedFireRoutine = true;
            return;
        }

        SetState(State.MovingToFirePoint);
    }

    void HandleMovingToFirePoint()
    {
        Transform firePoint = GetActiveFirePoint();
        if (firePoint == null)
        {
            if (TryPrepareNextFirePoint())
            {
                SetState(State.MovingToFirePoint, true);
                return;
            }

            hasCompletedFireRoutine = true;
            SetState(State.ReturningToSeat);
            return;
        }

        float dist = PlanarDistance(GetCurrentPosition(), firePoint.position);
        if (dist <= Mathf.Max(0.05f, firePointReachDistance))
        {
            StopPlanarMovement();
            transform.position = new Vector3(firePoint.position.x, transform.position.y, firePoint.position.z);
            FaceForward(firePoint.forward);
            fireCreateTimer = Mathf.Max(0f, fireCreateDelay);
            SetState(State.CreatingFire);
            return;
        }

        MoveTowards(firePoint.position);
    }

    void HandleCreatingFire()
    {
        Transform firePoint = GetActiveFirePoint();
        if (firePoint == null)
        {
            SetLighterVisible(false);
            if (TryPrepareNextFirePoint())
            {
                SetState(State.MovingToFirePoint);
                return;
            }

            hasCompletedFireRoutine = true;
            SetState(State.ReturningToSeat);
            return;
        }

        FaceForward(firePoint.forward);
        fireCreateTimer -= Time.deltaTime;
        if (fireCreateTimer > 0f) return;

        SpawnFireAt(firePoint);

        if (activeFirePointIndex >= 0 && activeFirePointIndex < firePointUsed.Length)
            firePointUsed[activeFirePointIndex] = true;

        if (TryPrepareNextFirePoint())
        {
            SetState(State.MovingToFirePoint);
            return;
        }

        hasCompletedFireRoutine = true;
        SetState(State.ReturningToSeat);
    }

    void HandleReturningToSeat()
    {
        if (!seatPoint) return;

        float dist = PlanarDistance(GetCurrentPosition(), seatPoint.position);
        if (dist <= GetSeatReachDistance())
        {
            StopPlanarMovement();
            TeleportTo(seatPoint);
            SetState(State.IdleAtSeat, true);
            return;
        }

        MoveTowards(seatPoint.position);
    }

    void SetState(State newState, bool force = false)
    {
        if (!force && currentState == newState) return;

        currentState = newState;
        ApplyPhysicsForState(newState);

        if (newState == State.IdleAtSeat && !hasCompletedFireRoutine)
            ScheduleNextAction();

        if (newState != State.CreatingFire)
            SetLighterVisible(false);

        string targetStateName = sittingStateName;
        if (newState == State.MovingToFirePoint || newState == State.ReturningToSeat)
            targetStateName = walkStateName;
        else if (newState == State.CreatingFire)
        {
            targetStateName = crouchStateName;
            SetLighterVisible(true);
        }

        PlayAnimationState(targetStateName, animCrossFade);
    }

    bool TryStartFireRoutine()
    {
        if (fireHazardPrefab == null || firePoints == null || firePoints.Length == 0)
            return false;

        EnsureFirePointStateArrays();
        BuildFireOrder();
        return TryPrepareNextFirePoint();
    }

    void BuildFireOrder()
    {
        EnsureFirePointStateArrays();

        if (fireOrder.Length != firePoints.Length)
            fireOrder = new int[firePoints.Length];

        for (int i = 0; i < fireOrder.Length; i++)
            fireOrder[i] = i;

        if (randomizeFirePointOrder)
        {
            for (int i = fireOrder.Length - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                int temp = fireOrder[i];
                fireOrder[i] = fireOrder[swapIndex];
                fireOrder[swapIndex] = temp;
            }
        }

        fireOrderCursor = 0;
        activeFirePointIndex = -1;
    }

    bool TryPrepareNextFirePoint()
    {
        while (fireOrderCursor < fireOrder.Length)
        {
            int candidateIndex = fireOrder[fireOrderCursor];
            fireOrderCursor++;

            if (candidateIndex < 0 || candidateIndex >= firePoints.Length)
                continue;
            if (firePoints[candidateIndex] == null)
                continue;
            if (candidateIndex < firePointUsed.Length && firePointUsed[candidateIndex])
                continue;

            activeFirePointIndex = candidateIndex;
            return true;
        }

        activeFirePointIndex = -1;
        return false;
    }

    void SpawnFireAt(Transform firePoint)
    {
        if (fireHazardPrefab == null || firePoint == null) return;

        Vector3 spawnPosition = firePoint.TransformPoint(fireSpawnLocalOffset);
        GameObject spawned = Instantiate(fireHazardPrefab, spawnPosition, firePoint.rotation);
        FireHazard fireHazard = spawned.GetComponent<FireHazard>();
        if (fireHazard == null)
            fireHazard = spawned.GetComponentInChildren<FireHazard>(true);

        if (fireHazard != null)
            fireHazard.Ignite();
    }

    void AbortFireRoutine()
    {
        hasCompletedFireRoutine = true;
        SetLighterVisible(false);
        SetState(State.ReturningToSeat);
    }

    Transform GetActiveFirePoint()
    {
        if (activeFirePointIndex < 0 || activeFirePointIndex >= firePoints.Length)
            return null;

        return firePoints[activeFirePointIndex];
    }

    bool IsPlayerCloseEnoughToCatch()
    {
        ResolvePlayerTarget();
        if (!playerTarget) return false;

        Vector3 studentPos = transform.position;
        Vector3 playerPos = playerTarget.position;
        studentPos.y = 0f;
        playerPos.y = 0f;

        return Vector3.Distance(studentPos, playerPos) <= catchDistance;
    }

    void ResolvePlayerTarget()
    {
        if (playerTarget != null || !autoFindPlayerTarget) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTarget = player.transform;
    }

    void MoveTowards(Vector3 targetPos)
    {
        Vector3 currentPos = rb ? rb.position : transform.position;
        Vector3 dir = targetPos - currentPos;
        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.0001f)
        {
            StopPlanarMovement();
            return;
        }

        dir = dir.normalized;
        if (rb && !rb.isKinematic)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.x = dir.x * walkSpeed;
            velocity.z = dir.z * walkSpeed;
            rb.linearVelocity = velocity;
        }
        else
        {
            transform.position += dir * walkSpeed * Time.deltaTime;
        }

        FaceForward(dir);
    }

    void FaceForward(Vector3 forward)
    {
        Vector3 dir = forward;
        dir.y = 0f;
        if (dir.sqrMagnitude <= 0.0001f) return;

        transform.forward = Vector3.Lerp(transform.forward, dir.normalized, 10f * Time.deltaTime);
    }

    void StopPlanarMovement()
    {
        if (rb && !rb.isKinematic)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;
            rb.linearVelocity = velocity;
        }
    }

    void ApplyPhysicsForState(State state)
    {
        if (!rb) return;

        bool stationary = state == State.IdleAtSeat || state == State.CreatingFire;
        rb.isKinematic = stationary;
        rb.useGravity = !stationary;

        if (stationary)
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

    Vector3 GetCurrentPosition()
    {
        return rb ? rb.position : transform.position;
    }

    float GetSeatReachDistance()
    {
        return seatReachDistance > 0f ? seatReachDistance : 0.2f;
    }

    float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void EnsureFirePointStateArrays()
    {
        int count = firePoints != null ? firePoints.Length : 0;
        if (firePointUsed.Length != count)
            firePointUsed = new bool[count];
    }

    void ScheduleNextAction()
    {
        float min = Mathf.Max(0.5f, minWaitBeforeFire);
        float max = Mathf.Max(min, maxWaitBeforeFire);
        nextActionTimer = Random.Range(min, max);
    }

    void SetLighterVisible(bool visible)
    {
        if (lighterObject != null)
            lighterObject.SetActive(visible);
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

    void OnTriggerEnter(Collider other)
    {
        if (classEnded) return;
        if (isStunned) return;
        if (!other.CompareTag("Player")) return;

        NotifyPlayerCollision();
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
