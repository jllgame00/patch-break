using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyCombatState))]
public sealed class KnightController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform target;

    [SerializeField]
    private HeroController targetHero;

    [SerializeField]
    private Transform meleeAttackPoint;

    [SerializeField]
    private Transform projectileSpawnPoint;

    [SerializeField]
    private SpriteRenderer spriteRenderer;

    [SerializeField]
    private EnemyCombatState combatState;

    [SerializeField]
    private KnightProjectile projectilePrefab;

    [SerializeField]
    private GameObject hitEffectPrefab;

    [SerializeField]
    private LayerMask targetLayer;
    
    [SerializeField]
    private ProgramRuntime runtime;

    [Header("Melee Attack")]
    [SerializeField, Min(0.1f)]
    private float meleeTriggerDistance = 2.2f;

    [SerializeField, Min(0.1f)]
    private float meleeRadius = 1.1f;

    [SerializeField, Min(1)]
    private int meleeDamage = 25;

    [SerializeField, Min(0.01f)]
    private float meleeWindup = 0.4f;

    [Header("Ranged Attack")]
    [SerializeField, Min(0.01f)]
    private float rangedWindup = 0.25f;

    [Header("Guard")]
    [SerializeField, Min(0.1f)]
    private float guardDuration = 0.9f;

    [Header("Timing")]
    [SerializeField, Min(0f)]
    private float recoveryDuration = 0.3f;

    [SerializeField, Min(0f)]
    private float actionCooldown = 0.7f;

    [Header("Telegraph Colors")]
    [SerializeField]
    private Color attackColor =
        new(1f, 0.2f, 0.15f, 1f);

    [SerializeField]
    private Color guardColor =
        new(0.15f, 0.55f, 1f, 1f);

    private Coroutine actionRoutine;
    private float nextActionTime;
    private float originalScaleX;
    private Color normalColor = Color.white;
    private int closeActionCount;

    private void Awake()
    {
        if (combatState == null)
        {
            combatState =
                GetComponent<EnemyCombatState>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer =
                GetComponent<SpriteRenderer>();
        }

        if (targetHero == null &&
            target != null)
        {
            targetHero =
                target.GetComponent<
                    HeroController>();
        }

        originalScaleX =
            Mathf.Abs(transform.localScale.x);

        if (spriteRenderer != null)
        {
            normalColor =
                spriteRenderer.color;
        }
    }

    private void Update()
    {
        if (runtime == null || !runtime.IsRunning)
        {
            CancelCurrentAction();
            return;
        }

        if (target == null ||
            !target.gameObject.activeInHierarchy)
        {
            CancelCurrentAction();
            return;
        }

        if (actionRoutine != null)
            return;

        FaceTarget();

        if (Time.time < nextActionTime)
            return;

        float distance =
            Vector2.Distance(
                transform.position,
                target.position
            );

        if (distance <= meleeTriggerDistance)
        {
            closeActionCount++;

            bool shouldGuard =
                closeActionCount % 3 == 0;

            actionRoutine = shouldGuard
                ? StartCoroutine(GuardRoutine())
                : StartCoroutine(
                    MeleeAttackRoutine()
                );

            return;
        }

        actionRoutine =
            StartCoroutine(
                RangedAttackRoutine()
            );
    }

    private IEnumerator MeleeAttackRoutine()
    {
        combatState.SetAttacking(true);
        SetColor(attackColor);

        Debug.Log(
            "KNIGHT: MELEE ATTACK WINDUP"
        );

        yield return new WaitForSeconds(
            meleeWindup
        );

        PerformMeleeAttack();

        combatState.SetAttacking(false);
        RestoreNormalColor();

        yield return new WaitForSeconds(
            recoveryDuration
        );

        FinishAction();
    }

    private IEnumerator RangedAttackRoutine()
    {
        combatState.SetAttacking(true);
        SetColor(attackColor);

        Debug.Log(
            "KNIGHT: PROJECTILE WINDUP"
        );

        yield return new WaitForSeconds(
            rangedWindup
        );

        SpawnProjectile();

        combatState.SetAttacking(false);
        RestoreNormalColor();

        yield return new WaitForSeconds(
            recoveryDuration
        );

        FinishAction();
    }

    private IEnumerator GuardRoutine()
    {
        combatState.SetGuarding(true);
        SetColor(guardColor);

        Debug.Log("KNIGHT: GUARDING");

        yield return new WaitForSeconds(
            guardDuration
        );

        combatState.SetGuarding(false);
        RestoreNormalColor();

        yield return new WaitForSeconds(
            recoveryDuration
        );

        FinishAction();
    }

    private void PerformMeleeAttack()
    {
        if (meleeAttackPoint == null)
            return;

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                meleeAttackPoint.position,
                meleeRadius,
                targetLayer
            );

        HashSet<Health> processed = new();

        foreach (Collider2D hit in hits)
        {
            Health health =
                hit.GetComponentInParent<
                    Health>();

            if (health == null ||
                !processed.Add(health))
            {
                continue;
            }

            Vector2 hitPosition =
                hit.ClosestPoint(
                    meleeAttackPoint.position
                );

            health.TakeDamage(meleeDamage);
            SpawnHitEffect(hitPosition);
        }

        Debug.Log(
            processed.Count > 0
                ? "KNIGHT: MELEE HIT"
                : "KNIGHT: MELEE MISSED"
        );
    }

    private void SpawnProjectile()
    {
        if (projectilePrefab == null ||
            projectileSpawnPoint == null ||
            target == null)
        {
            Debug.LogError(
                "KnightController: " +
                "Projectile reference missing."
            );

            return;
        }

        Vector2 direction =
            target.position -
            projectileSpawnPoint.position;

        KnightProjectile projectile =
            Instantiate(
                projectilePrefab,
                projectileSpawnPoint.position,
                Quaternion.identity
            );

        projectile.Launch(
            direction,
            targetHero
        );

        Debug.Log(
            "KNIGHT: PROJECTILE FIRED"
        );
    }

    private void SpawnHitEffect(
        Vector3 position)
    {
        if (hitEffectPrefab == null)
            return;

        Instantiate(
            hitEffectPrefab,
            position,
            Quaternion.identity
        );
    }

    private void FinishAction()
    {
        nextActionTime =
            Time.time + actionCooldown;

        actionRoutine = null;
    }

    private void FaceTarget()
    {
        float direction =
            target.position.x -
            transform.position.x;

        if (Mathf.Approximately(
                direction,
                0f))
        {
            return;
        }

        Vector3 scale =
            transform.localScale;

        scale.x =
            originalScaleX *
            Mathf.Sign(direction);

        transform.localScale = scale;
    }

    private void SetColor(Color color)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }

    private void RestoreNormalColor()
    {
        SetColor(normalColor);
    }

    private void OnDisable()
    {
        CancelCurrentAction();
    }

    private void OnDrawGizmosSelected()
    {
        if (meleeAttackPoint == null)
            return;

        Gizmos.DrawWireSphere(
            meleeAttackPoint.position,
            meleeRadius
        );
    }
    
    private void CancelCurrentAction()
    {
        if (actionRoutine != null)
        {
            StopCoroutine(actionRoutine);
            actionRoutine = null;
        }

        if (combatState != null)
        {
            combatState.ResetState();
        }

        RestoreNormalColor();
    }
}