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
    private int damage = 15;

    [FormerlySerializedAs("cooldown")]
    [SerializeField, Min(0.01f)]
    private float attackCooldown = 0.75f;

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

        HashSet<Health> processedTargets = new();
        int successfulHits = 0;

        foreach (Collider2D hit in hits)
        {
            Health health =
                hit.GetComponentInParent<Health>();

            if (health == null ||
                !processedTargets.Add(health))
            {
                continue;
            }

            Vector2 hitPosition =
                hit.ClosestPoint(
                    attackPoint.position
                );

            EnemyCombatState combatState =
                hit.GetComponentInParent<
                    EnemyCombatState>();

            if (combatState != null &&
                combatState.IsGuarding)
            {
                SpawnHitEffect(hitPosition);

                Debug.Log(
                    "HERO SLASH BLOCKED BY GUARD"
                );

                continue;
            }

            health.TakeDamage(damage);
            SpawnHitEffect(hitPosition);

            successfulHits++;
        }

        Debug.Log(
            $"Hero executed SLASH. " +
            $"Hits: {successfulHits}"
        );

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
