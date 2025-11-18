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
    public float delayAfterSound = 1.5f;

    public float launchSpeed = 10f;
    public float extraUpward = 1f;

    float timer;

    void Start()
    {
        if (seatPoint)
        {
            transform.position = seatPoint.position;
            transform.rotation = seatPoint.rotation;
        }
        ScheduleNext();
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (playerTarget)
        {
            Vector3 look = playerTarget.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 5f * Time.deltaTime);
        }

        if (timer <= 0f)
        {
            if (audioSource && prankClip) audioSource.PlayOneShot(prankClip);

            Invoke(nameof(ThrowPaper), delayAfterSound);

            ScheduleNext();
        }
    }

    void ScheduleNext()
    {
        timer = Random.Range(minWait, maxWait);
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
