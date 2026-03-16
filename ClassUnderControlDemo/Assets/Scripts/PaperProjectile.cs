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
            if (TryBlockAtTarget())
            {
                damaged = true;
                Destroy(gameObject);
                return;
            }

            damaged = true;
            if (GameManager.I) GameManager.I.SubGPA(damage);
            Destroy(gameObject);
        }
    }

    void OnCollisionEnter(Collision c)
    {
        if (!c.collider.CompareTag("Player"))
            return;

        if (TryBlock(c.collider))
        {
            damaged = true;
            Destroy(gameObject);
            return;
        }

        if (GameManager.I)
            GameManager.I.SubGPA(damage);

        damaged = true;
        Destroy(gameObject);
    }

    bool TryBlockAtTarget()
    {
        return target != null && TryBlock(target.GetComponentInParent<PlayerItemSystem>());
    }

    bool TryBlock(Collider hitCollider)
    {
        return hitCollider != null && TryBlock(hitCollider.GetComponentInParent<PlayerItemSystem>());
    }

    bool TryBlock(PlayerItemSystem itemSystem)
    {
        return itemSystem != null && itemSystem.TryBlockProjectile();
    }
}
