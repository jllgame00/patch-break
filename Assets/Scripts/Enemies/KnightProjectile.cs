using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class KnightProjectile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0.1f)]
    private float speed = 8f;

    [SerializeField, Min(0.1f)]
    private float lifetime = 4f;

    [Header("Damage")]
    [SerializeField, Min(1)]
    private int damage = 20;

    [SerializeField]
    private LayerMask targetLayer;

    [Header("Effects")]
    [SerializeField]
    private GameObject hitEffectPrefab;

    private Rigidbody2D body;
    private bool preDodged;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    public void Launch(
        Vector2 direction,
        HeroController targetHero)
    {
        direction.Normalize();

        preDodged =
            targetHero != null &&
            targetHero.IsDashInvulnerable;

        // 공격 예고에 반응해 이미 대시했다면
        // 검기가 살짝 위로 빗나간다.
        if (preDodged)
        {
            direction = new Vector2(
                direction.x,
                direction.y + 0.45f
            ).normalized;
        }

        body.linearVelocity =
            direction * speed;

        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsInTargetLayer(other.gameObject.layer))
            return;

        HeroController hero =
            other.GetComponentInParent<HeroController>();

        if (preDodged ||
            (hero != null &&
             hero.IsDashInvulnerable))
        {
            Debug.Log(
                "KNIGHT PROJECTILE EVADED"
            );

            Destroy(gameObject);
            return;
        }

        Health health =
            other.GetComponentInParent<Health>();

        if (health == null || health.IsDead)
            return;

        Vector2 hitPosition =
            other.ClosestPoint(
                transform.position
            );

        health.TakeDamage(damage);
        SpawnHitEffect(hitPosition);

        Debug.Log("KNIGHT PROJECTILE HIT");

        Destroy(gameObject);
    }

    private bool IsInTargetLayer(int layer)
    {
        return
            (targetLayer.value & (1 << layer)) != 0;
    }

    private void SpawnHitEffect(Vector3 position)
    {
        if (hitEffectPrefab == null)
            return;

        Instantiate(
            hitEffectPrefab,
            position,
            Quaternion.identity
        );
    }

    private void OnDisable()
    {
        if (body != null)
        {
            body.linearVelocity =
                Vector2.zero;
        }
    }
}