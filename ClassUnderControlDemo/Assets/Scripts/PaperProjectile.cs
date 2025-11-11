using UnityEngine;

public class PaperProjectile : MonoBehaviour
{
    public Transform target;
    public float lifeTime = 3f;
    public float hitRadius = 0.6f;
    public float damage = 1f;

    bool damaged = false;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void FixedUpdate()
    {
        if (!target) return;

        if (!damaged && Vector3.Distance(transform.position, target.position) <= hitRadius)
        {
            damaged = true;
            if (GameManager.I) GameManager.I.SubGPA(damage);
            Destroy(gameObject);
        }
    }

    void OnCollisionEnter(Collision c)
    {
        if (c.collider.CompareTag("Player")) { GameManager.I.SubGPA(damage); Destroy(gameObject); }
    }
}
