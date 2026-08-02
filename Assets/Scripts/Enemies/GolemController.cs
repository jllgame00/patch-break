using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyCombatState))]
public sealed class GolemController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private EnemyCombatState combatState;
    [SerializeField] private SpriteRenderer attackRangeTelegraph;
    [SerializeField] private GameObject hitEffectPrefab;

    [Header("Attack")]
    [SerializeField, Min(0.1f)]
    private float attackTriggerDistance = 2.4f;

    [SerializeField, Min(0.1f)]
    private float attackRadius = 1.2f;

    [SerializeField, Min(1)]
    private int attackDamage = 35;

    [SerializeField, Min(0.1f)]
    private float windupDuration = 0.65f;

    [SerializeField, Min(0f)]
    private float recoveryDuration = 0.35f;

    [SerializeField, Min(0f)]
    private float attackCooldown = 0.8f;

    [SerializeField]
    private LayerMask targetLayer;

    [Header("Telegraph")]
    [SerializeField]
    private Color windupColor = new(1f, 0.25f, 0.15f, 1f);

    private Coroutine attackRoutine;
    private float nextAttackTime;
    private float originalScaleX;
    private Color normalColor = Color.white;

    private void Awake()
    {
        if (combatState == null)
        {
            combatState = GetComponent<EnemyCombatState>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        originalScaleX = Mathf.Abs(transform.localScale.x);

        if (spriteRenderer != null)
        {
            normalColor = spriteRenderer.color;
        }
        
        SetAttackRangeTelegraph(false);
    }

    private void Update()
    {
        if (target == null ||
            !target.gameObject.activeInHierarchy)
        {
            return;
        }

        if (attackRoutine != null)
        {
            return;
        }

        FaceTarget();

        if (Time.time < nextAttackTime)
        {
            return;
        }

        float distance = Vector2.Distance(
            transform.position,
            target.position
        );

        if (distance <= attackTriggerDistance)
        {
            attackRoutine = StartCoroutine(AttackRoutine());
        }
    }

    private IEnumerator AttackRoutine()
    {
        combatState.SetAttacking(true);

        SetTelegraphVisual(true);
        SetAttackRangeTelegraph(true);

        Debug.Log("GOLEM: ATTACK WINDUP");

        yield return new WaitForSeconds(
            windupDuration
        );

        PerformAttack();

        combatState.SetAttacking(false);

        SetTelegraphVisual(false);
        SetAttackRangeTelegraph(false);

        yield return new WaitForSeconds(
            recoveryDuration
        );

        nextAttackTime =
            Time.time + attackCooldown;

        attackRoutine = null;
    }

    private void PerformAttack()
    {
        if (attackPoint == null)
        {
            Debug.LogError(
                "GolemController: AttackPoint is not assigned."
            );

            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            attackPoint.position,
            attackRadius,
            targetLayer
        );

        HashSet<Health> damagedTargets = new();
        int successfulHits = 0;

        foreach (Collider2D hit in hits)
        {
            Health health = hit.GetComponentInParent<Health>();

            if (health == null ||
                !damagedTargets.Add(health))
            {
                continue;
            }

            HeroController hero =
                hit.GetComponentInParent<
                    HeroController>();

            if (hero != null &&
                hero.IsInvulnerable)
            {
                Debug.Log("GOLEM: ATTACK EVADED");
                continue;
            }

            Vector2 hitPosition =
                hit.ClosestPoint(
                    attackPoint.position
                );

            health.TakeDamage(attackDamage);
            SpawnHitEffect(hitPosition);
            successfulHits++;
        }

        Debug.Log(
            successfulHits > 0
                ? "GOLEM: ATTACK HIT"
                : "GOLEM: ATTACK MISSED"
        );
    }

    private void FaceTarget()
    {
        float horizontalDirection =
            target.position.x - transform.position.x;

        if (Mathf.Approximately(horizontalDirection, 0f))
        {
            return;
        }

        Vector3 scale = transform.localScale;

        scale.x =
            originalScaleX * Mathf.Sign(horizontalDirection);

        transform.localScale = scale;
    }

    private void SetTelegraphVisual(bool active)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color =
                active ? windupColor : normalColor;
        }
    }

    private void OnDisable()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (combatState != null)
        {
            combatState.SetAttacking(false);
        }

        SetTelegraphVisual(false);
        SetAttackRangeTelegraph(false);
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
        {
            return;
        }

        Gizmos.DrawWireSphere(
            attackPoint.position,
            attackRadius
        );
    }
    
    private void SetAttackRangeTelegraph(
        bool active)
    {
        if (attackRangeTelegraph == null)
            return;

        attackRangeTelegraph.enabled = active;

        float diameter = attackRadius * 2f;

        attackRangeTelegraph.transform.localScale =
            new Vector3(
                diameter,
                diameter,
                1f
            );
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
}
