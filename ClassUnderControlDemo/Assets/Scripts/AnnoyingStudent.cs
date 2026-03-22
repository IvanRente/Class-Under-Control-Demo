using System.Collections.Generic;
using UnityEngine;

public class AnnoyingStudent : MonoBehaviour, IClassStudent, IAnnoyableStudent
{
    public enum State { IdleAtSeat, MovingToTarget, AnnoyingTarget, ReturningToSeat, LeavingClassroom }

    public State currentState = State.IdleAtSeat;

    public Transform seatPoint;
    public Transform playerTarget;
    public bool autoFindPlayerTarget = true;
    public float walkSpeed = 1.25f;
    public float seatReachDistance = 0.2f;
    public float targetReachDistance = 0.35f;
    public float classroomExitReachDistance = 0.35f;
    public float catchDistance = 1.25f;
    public float minWaitBeforeAnnoying = 8f;
    public float maxWaitBeforeAnnoying = 14f;
    public Vector3 targetSeatLocalOffset = new Vector3(0.7f, 0f, 0f);
    public float gpaPenaltyPerSecond = 0.1f;

    public string sittingStateName = "Sitting";
    public string walkStateName = "Walk";
    public string shakingStateName = "ShakeStudent";
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

    readonly List<IAnnoyableStudent> annoyableStudents = new List<IAnnoyableStudent>();

    Animator animator;
    Rigidbody rb;
    IAnnoyableStudent targetStudent;
    MonoBehaviour targetStudentBehaviour;
    float nextActionTimer;
    float gpaTickTimer;
    bool classEnded;
    bool externallyAnnoyed;
    bool isStunned;
    float stunTimer;
    bool leavingClassroom;
    bool hiddenBetweenClasses;
    Transform classroomExitPoint;

    public Transform SeatPoint => seatPoint;
    public bool CanBeAnnoyed => !classEnded && !leavingClassroom && !hiddenBetweenClasses && !externallyAnnoyed && !isStunned && !IsClassTimerPaused() && currentState == State.IdleAtSeat;

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
        SetState(State.IdleAtSeat, true);
    }

    void Update()
    {
        if (hiddenBetweenClasses) return;
        if (leavingClassroom)
        {
            HandleLeavingClassroom();
            return;
        }

        if (classEnded) return;
        if (UpdateStunTimer()) return;
        if (IsClassTimerPaused()) return;
        if (externallyAnnoyed) return;

        if ((currentState == State.MovingToTarget || currentState == State.AnnoyingTarget) && IsPlayerCloseEnoughToCatch())
        {
            StopAnnoyingAndReturn();
            return;
        }

        switch (currentState)
        {
            case State.IdleAtSeat:
                HandleIdle();
                break;
            case State.MovingToTarget:
                HandleMovingToTarget();
                break;
            case State.AnnoyingTarget:
                HandleAnnoyingTarget();
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
        leavingClassroom = false;
        hiddenBetweenClasses = false;
        classroomExitPoint = null;
        externallyAnnoyed = false;
        isStunned = false;
        stunTimer = 0f;
        ReleaseTarget();

        if (seatPoint)
        {
            StopPlanarMovement();
            TeleportTo(seatPoint);
        }

        SetState(State.IdleAtSeat, true);
    }

    public void LeaveClassroom(Transform exitPoint)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        classEnded = true;
        leavingClassroom = true;
        hiddenBetweenClasses = false;
        classroomExitPoint = exitPoint;
        externallyAnnoyed = false;
        isStunned = false;
        stunTimer = 0f;
        ReleaseTarget();
        SetState(State.LeavingClassroom, true);
    }

    public void PrepareForNewClass()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        classEnded = false;
        leavingClassroom = false;
        hiddenBetweenClasses = false;
        classroomExitPoint = null;
        externallyAnnoyed = false;
        isStunned = false;
        stunTimer = 0f;
        ReleaseTarget();

        if (seatPoint)
        {
            StopPlanarMovement();
            TeleportTo(seatPoint);
        }

        SetState(State.IdleAtSeat, true);
    }

    public void BeginBeingAnnoyed(AnnoyingStudent annoyer)
    {
        if (!CanBeAnnoyed) return;

        externallyAnnoyed = true;
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

        ReleaseTarget();
        StopAndFreezeForStun();
        PlayAnimationState(stunnedStateName, stunnedCrossFade);
    }

    public void NotifyPlayerCollision()
    {
        if (classEnded) return;
        if (isStunned) return;

        if (currentState == State.MovingToTarget || currentState == State.AnnoyingTarget)
            StopAnnoyingAndReturn();
    }

    void HandleIdle()
    {
        nextActionTimer -= Time.deltaTime;
        if (nextActionTimer > 0f) return;

        if (!TrySelectTarget())
        {
            ScheduleNextAction();
            return;
        }

        SetState(State.MovingToTarget);
    }

    void HandleMovingToTarget()
    {
        if (!HasValidTarget())
        {
            SetState(State.ReturningToSeat);
            return;
        }

        if (!targetStudent.CanBeAnnoyed)
        {
            ClearTargetReference();
            SetState(State.ReturningToSeat);
            return;
        }

        Vector3 targetPosition = GetTargetStandPosition();
        float dist = PlanarDistance(GetCurrentPosition(), targetPosition);
        if (dist <= Mathf.Max(0.05f, targetReachDistance))
        {
            StopPlanarMovement();
            transform.position = new Vector3(targetPosition.x, transform.position.y, targetPosition.z);
            FacePosition(targetStudent.SeatPoint.position);
            targetStudent.BeginBeingAnnoyed(this);
            gpaTickTimer = 0f;
            SetState(State.AnnoyingTarget);
            return;
        }

        MoveTowards(targetPosition);
    }

    void HandleAnnoyingTarget()
    {
        if (!HasValidTarget())
        {
            SetState(State.ReturningToSeat);
            return;
        }

        Vector3 targetPosition = GetTargetStandPosition();
        transform.position = new Vector3(targetPosition.x, transform.position.y, targetPosition.z);
        FacePosition(targetStudent.SeatPoint.position);

        gpaTickTimer += Time.deltaTime;
        while (gpaTickTimer >= 1f)
        {
            gpaTickTimer -= 1f;
            if (GameManager.I != null)
                GameManager.I.SubGPA(gpaPenaltyPerSecond);
        }
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
            return;
        }

        MoveTowards(seatPoint.position);
    }

    void HandleLeavingClassroom()
    {
        if (!classroomExitPoint)
        {
            HideForBetweenClasses();
            return;
        }

        float dist = PlanarDistance(GetCurrentPosition(), classroomExitPoint.position);
        if (dist <= Mathf.Max(0.05f, classroomExitReachDistance))
        {
            HideForBetweenClasses();
            return;
        }

        MoveTowards(classroomExitPoint.position);
    }

    void SetState(State newState, bool force = false)
    {
        if (!force && currentState == newState) return;

        currentState = newState;
        ApplyPhysicsForState(newState);

        if (newState == State.IdleAtSeat)
            ScheduleNextAction();

        string targetStateName = sittingStateName;
        if (newState == State.MovingToTarget || newState == State.ReturningToSeat || newState == State.LeavingClassroom)
            targetStateName = walkStateName;
        else if (newState == State.AnnoyingTarget)
            targetStateName = shakingStateName;

        PlayAnimationState(targetStateName, animCrossFade);
    }

    bool TrySelectTarget()
    {
        ClearTargetReference();
        ClassStudentUtility.GetObjectsImplementing(annoyableStudents);

        int candidateCount = 0;
        for (int i = 0; i < annoyableStudents.Count; i++)
        {
            IAnnoyableStudent candidate = annoyableStudents[i];
            if (candidate == null) continue;
            if (ReferenceEquals(candidate, this)) continue;

            MonoBehaviour behaviour = candidate as MonoBehaviour;
            if (behaviour == null || behaviour == this) continue;
            if (!candidate.CanBeAnnoyed) continue;

            annoyableStudents[candidateCount] = candidate;
            candidateCount++;
        }

        if (candidateCount <= 0)
            return false;

        int selectedIndex = Random.Range(0, candidateCount);
        targetStudent = annoyableStudents[selectedIndex];
        targetStudentBehaviour = targetStudent as MonoBehaviour;
        return targetStudentBehaviour != null;
    }

    void StopAnnoyingAndReturn()
    {
        ReleaseTarget();
        SetState(State.ReturningToSeat);
    }

    void ReleaseTarget()
    {
        if (HasValidTarget())
            targetStudent.StopBeingAnnoyed(this);

        ClearTargetReference();
    }

    void ClearTargetReference()
    {
        targetStudent = null;
        targetStudentBehaviour = null;
        gpaTickTimer = 0f;
    }

    bool HasValidTarget()
    {
        return targetStudent != null && targetStudentBehaviour != null && targetStudent.SeatPoint != null;
    }

    Vector3 GetTargetStandPosition()
    {
        return targetStudent.SeatPoint.TransformPoint(targetSeatLocalOffset);
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

        FaceDirection(dir);
    }

    void FacePosition(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            FaceDirection(dir.normalized);
    }

    void FaceDirection(Vector3 dir)
    {
        transform.forward = Vector3.Lerp(transform.forward, dir, 10f * Time.deltaTime);
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

        bool stationary = state == State.IdleAtSeat || state == State.AnnoyingTarget;
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

    void ScheduleNextAction()
    {
        float min = Mathf.Max(0.5f, minWaitBeforeAnnoying);
        float max = Mathf.Max(min, maxWaitBeforeAnnoying);
        nextActionTimer = Random.Range(min, max);
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

    void HideForBetweenClasses()
    {
        leavingClassroom = false;
        hiddenBetweenClasses = true;
        ReleaseTarget();
        StopPlanarMovement();

        if (rb)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        gameObject.SetActive(false);
    }
}
