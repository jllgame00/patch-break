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
    private float horizontalDirection;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    public void Launch(
        Vector2 direction,
        HeroController targetHero)
    {
        horizontalDirection =
            Mathf.Sign(direction.x);

        if (Mathf.Approximately(
                horizontalDirection,
                0f))
        {
            horizontalDirection = 1f;
        }

        body.linearVelocity =
            new Vector2(
                horizontalDirection * speed,
                0f
            );

        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsInTargetLayer(other.gameObject.layer))
            return;

        HeroController hero =
            other.GetComponentInParent<HeroController>();

        bool heroIsDashing =
            hero != null && hero.IsDashing;

        bool heroIsInvulnerable =
            hero != null && hero.IsInvulnerable;

        if (hero != null &&
            hero.IsVerboseDashLogging)
        {
            Debug.Log(
                "KNIGHT PROJECTILE CONTACT\n" +
                "time=" + Time.time.ToString("F3") + "\n" +
                "heroIsDashing=" + heroIsDashing + "\n" +
                "heroIsInvulnerable=" +
                heroIsInvulnerable + "\n" +
                "result=" +
                (heroIsInvulnerable ? "EVADED" : "HIT")
            );
        }

        if (heroIsInvulnerable)
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
