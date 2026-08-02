using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class HeroAttack : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField]
    private Transform attackPoint;

    [SerializeField, Min(0.1f)]
    private float attackRange = 1.2f;

    [SerializeField, Min(1)]
    private int damage = 25;

    [FormerlySerializedAs("cooldown")]
    [SerializeField, Min(0.01f)]
    private float attackCooldown = 0.4f;

    [SerializeField]
    private LayerMask enemyLayer;

    [Header("Effects")]
    [SerializeField]
    private GameObject hitEffectPrefab;

    private float nextAttackTime;

    public bool TryAttack()
    {
        if (attackPoint == null)
        {
            Debug.LogError(
                "HeroAttack: AttackPoint is not assigned."
            );

            return false;
        }

        if (Time.time < nextAttackTime)
        {
            return false;
        }

        nextAttackTime =
            Time.time + attackCooldown;

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                attackPoint.position,
                attackRange,
                enemyLayer
            );

        HashSet<Health> damagedTargets = new();

        foreach (Collider2D hit in hits)
        {
            Health health =
                hit.GetComponentInParent<Health>();

            if (health == null ||
                !damagedTargets.Add(health))
            {
                continue;
            }

            Vector2 hitPosition =
                hit.ClosestPoint(
                    attackPoint.position
                );

            health.TakeDamage(damage);
            SpawnHitEffect(hitPosition);
        }

        Debug.Log(
            $"Hero executed SLASH. " +
            $"Hits: {damagedTargets.Count}"
        );

        // 공격 자체는 실행됐으므로
        // 맞히지 못해도 true다.
        return true;
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

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
            return;

        Gizmos.DrawWireSphere(
            attackPoint.position,
            attackRange
        );
    }
}