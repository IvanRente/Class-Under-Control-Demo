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

    void Start()
    {
        transform.position = seatPoint.position;
        transform.rotation = seatPoint.rotation;
        currentState = State.IdleAtSeat;
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

    void HandleIdle()
    {
        leaveTimer -= Time.deltaTime;
        if (leaveTimer <= 0f)
        {
            currentState = State.TryingToLeave;
            if (escapeVFX) escapeVFX.SetActive(true);
        }
    }

    void HandleTryingToLeave()
    {
        MoveTowards(doorPoint.position);

        float dist = Vector3.Distance(transform.position, doorPoint.position);
        if (dist < 0.5f)
        {
            GameManager.I.SubGPA(escapePenalty);
            transform.position = seatPoint.position;
            transform.rotation = seatPoint.rotation;
            currentState = State.IdleAtSeat;
            if (escapeVFX) escapeVFX.SetActive(false);
            ScheduleNextLeave();
        }
    }

    void HandleReturningToSeat()
    {
        MoveTowards(seatPoint.position);

        float dist = Vector3.Distance(transform.position, seatPoint.position);
        if (dist < 0.2f)
        {
            transform.position = seatPoint.position;
            transform.rotation = seatPoint.rotation;
            currentState = State.IdleAtSeat;
            if (escapeVFX) escapeVFX.SetActive(false);
            ScheduleNextLeave();
        }
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
            currentState = State.ReturningToSeat;
            if (escapeVFX) escapeVFX.SetActive(false);
        }
    }
}
