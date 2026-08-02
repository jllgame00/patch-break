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

    private float horizontalDirection;
    private float missDespawnX;
    private bool hasMissDespawnPoint;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }
    
    private void Update()
    {
        if (!preDodged ||
            !hasMissDespawnPoint)
        {
            return;
        }

        bool reachedMissPoint =
            horizontalDirection > 0f
                ? transform.position.x >=
                  missDespawnX
                : transform.position.x <=
                  missDespawnX;

        if (!reachedMissPoint)
        {
            return;
        }

        Debug.Log(
            "KNIGHT PROJECTILE EVADED"
        );

        Destroy(gameObject);
    }

    public void Launch(
        Vector2 direction,
        HeroController targetHero)
    {
        preDodged =
            targetHero != null &&
            targetHero.IsDashInvulnerable;

        horizontalDirection =
            Mathf.Sign(direction.x);

        if (Mathf.Approximately(
                horizontalDirection,
                0f))
        {
            horizontalDirection = 1f;
        }

        // 검기는 항상 수평으로 발사한다.
        body.linearVelocity =
            new Vector2(
                horizontalDirection * speed,
                0f
            );

        if (preDodged && targetHero != null)
        {
            // 발사 순간 Hero가 있던 위치를
            // 검기의 빗나감 종료 지점으로 저장한다.
            missDespawnX =
                targetHero.transform.position.x;

            hasMissDespawnPoint = true;
        }

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