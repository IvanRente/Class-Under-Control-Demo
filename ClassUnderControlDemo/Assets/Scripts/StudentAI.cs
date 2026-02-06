using UnityEngine;

public class StudentAI : MonoBehaviour
{
    public enum State { IdleAtSeat, TryingToLeave, ReturningToSeat }

    public State currentState = State.IdleAtSeat;

    public Transform seatPoint;
    public Transform doorPoint;
    public float walkSpeed = 1.2f;

    public float minWaitBeforeLeave = 5f;
    public float maxWaitBeforeLeave = 10f;
    float leaveTimer;

    public GameObject escapeVFX;

    public float escapePenalty = 1.0f;

    [Header("Catch Fallback")]
    public Transform playerTarget;
    public float catchDistance = 1.25f;

    [Header("Animation")]
    public string sittingStateName = "Sitting";
    public string sneakingStateName = "Sneaking";
    public string sadWalkStateName = "SadWalk";
    public float animCrossFade = 0.08f;

    Animator animator;
    static readonly int AnimState = Animator.StringToHash("State");


    void Start()
    {
        animator = GetComponent<Animator>();
        if (seatPoint)
        {
            transform.position = seatPoint.position;
            transform.rotation = seatPoint.rotation;
        }
        SetState(State.IdleAtSeat, true);
        ScheduleNextLeave();
        if (escapeVFX) escapeVFX.SetActive(false);
    }

    void Update()
    {
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

        if (!doorPoint) return;
        MoveTowards(doorPoint.position);

        float dist = Vector3.Distance(transform.position, doorPoint.position);
        if (dist < 0.5f)
        {
            GameManager.I.SubGPA(escapePenalty);
            transform.position = seatPoint.position;
            transform.rotation = seatPoint.rotation;
            SetState(State.IdleAtSeat);
            if (escapeVFX) escapeVFX.SetActive(false);
            ScheduleNextLeave();
        }
    }

    void HandleReturningToSeat()
    {
        if (!seatPoint) return;
        MoveTowards(seatPoint.position);

        float dist = Vector3.Distance(transform.position, seatPoint.position);
        if (dist < 0.2f)
        {
            transform.position = seatPoint.position;
            transform.rotation = seatPoint.rotation;
            SetState(State.IdleAtSeat);
            if (escapeVFX) escapeVFX.SetActive(false);
            ScheduleNextLeave();
        }
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
        SetState(State.ReturningToSeat);
        if (escapeVFX) escapeVFX.SetActive(false);
    }

    void MoveTowards(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position);
        dir.y = 0f;
        if (dir.magnitude > 0.01f)
        {
            dir = dir.normalized;
            transform.position += dir * walkSpeed * Time.deltaTime;
            if (dir != Vector3.zero)
                transform.forward = Vector3.Lerp(transform.forward, dir, 10f * Time.deltaTime);
        }
    }

    void ScheduleNextLeave()
    {
        leaveTimer = Random.Range(minWaitBeforeLeave, maxWaitBeforeLeave);
    }

    void OnTriggerEnter(Collider other)
    {
        if (currentState == State.TryingToLeave && other.CompareTag("Player"))
        {
            BeginReturnToSeat();
        }
    }
}
