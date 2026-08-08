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

    [Header("Guard Block")]
    [SerializeField, Min(1)]
    private int guardCounterDamage = 20;

    [SerializeField, Min(0f)]
    private float guardRecoilDistance = 1f;

    [SerializeField, Min(0.01f)]
    private float guardRecoilDuration = 0.15f;

    [SerializeField, Min(0f)]
    private float guardStaggerDuration = 0.4f;

    [Header("Effects")]
    [SerializeField]
    private GameObject hitEffectPrefab;

    private float nextAttackTime;
    private HeroController heroController;
    private Health heroHealth;
    private CharacterPoseController poseController;

    private void Awake()
    {
        heroController = GetComponent<HeroController>();
        heroHealth = GetComponent<Health>();
        poseController = GetComponent<CharacterPoseController>();
    }

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

        // Gameplay damage timing remains the source of truth. This only
        // starts the visual one-shot for an already-authorized SLASH.
        poseController?.PlayAttack();
        PersistentAudioManager.PlaySwordSwing();

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                attackPoint.position,
                attackRange,
                enemyLayer
            );

        HashSet<Health> processedTargets = new();
        int successfulHits = 0;
        bool guardCounterApplied = false;

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

                if (!guardCounterApplied)
                {
                    guardCounterApplied = true;

                    if (heroHealth != null)
                    {
                        heroHealth.TakeDamage(
                            guardCounterDamage
                        );

                        Debug.Log(
                            "HERO TOOK " +
                            $"{guardCounterDamage} " +
                            "GUARD COUNTER DAMAGE"
                        );

                        if (heroHealth.IsDead)
                        {
                            return true;
                        }
                    }

                    if (heroController != null)
                    {
                        heroController.ApplyGuardRecoil(
                            combatState.transform,
                            guardRecoilDistance,
                            guardRecoilDuration,
                            guardStaggerDuration
                        );
                    }
                }

                continue;
            }

            health.TakeDamage(damage);
            HitVfxManager.ReportConfirmedHit(
                health,
                hitPosition
            );
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
