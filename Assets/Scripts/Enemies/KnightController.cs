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

    [Header("Telegraphs")]
    [SerializeField]
    private GameObject meleeTelegraph;

    [SerializeField]
    private GameObject projectileTelegraph;

    [SerializeField, Min(0.01f)]
    private float projectileTelegraphReferenceLength =
        4.05f;

    [Header("Diagnostics")]
    [SerializeField]
    private bool verboseTelegraphLogging;

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

    [SerializeField]
    private GameObject guardIndicator;

    [SerializeField, Min(0f)]
    private float guardIndicatorOffsetX = 0.75f;

    [Header("Timing")]
    [SerializeField, Min(0f)]
    private float recoveryDuration = 0.3f;

    [SerializeField, Min(0f)]
    private float actionCooldown = 0.7f;

    [Header("Forced Ranged")]
    [SerializeField, Min(0f)]
    private float forcedRangedBackstepDistance = 1.8f;

    [SerializeField, Min(0.01f)]
    private float forcedRangedBackstepDuration = 0.18f;

    [Header("Telegraph Colors")]
    [SerializeField]
    private Color attackColor =
        new(1f, 0.2f, 0.15f, 1f);

    [SerializeField]
    private Color guardColor =
        new(0.15f, 0.55f, 1f, 1f);

    private Coroutine actionRoutine;
    private Rigidbody2D body;
    private float nextActionTime;
    private float originalScaleX;
    private Color normalColor = Color.white;
    private int closeActionCount;
    private bool forceProjectileNext;
    private bool isRangedAction;
    private bool isActing;
    private bool actionSkipLogged;
    private string activeActionName;
    private float lockedProjectileTargetX;
    private bool hasLockedProjectileTarget;
    private int nextProjectileAttackSignalId;
    private int activeProjectileAttackSignalId;
    private CharacterPoseController poseController;
    private GuardVisualLoop guardVisual;

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

        body = GetComponent<Rigidbody2D>();

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

        poseController = GetComponent<CharacterPoseController>();

        guardVisual = guardIndicator != null
            ? guardIndicator.GetComponentInChildren<GuardVisualLoop>(true)
            : null;

        if (meleeTelegraph == null)
        {
            Debug.LogWarning(
                "Knight melee telegraph reference is missing.",
                this
            );
        }

        if (projectileTelegraph == null)
        {
            Debug.LogWarning(
                "Knight projectile telegraph reference is missing.",
                this
            );
        }

        if (guardIndicator == null)
        {
            Debug.LogWarning(
                "Knight guard indicator reference is missing.",
                this
            );
        }

        HideAllTelegraphs();
        HideGuardIndicator();
    }

    private void Update()
    {
        if (runtime == null || !runtime.IsRunning)
        {
            CancelCurrentAction("PROGRAM STOPPED");
            return;
        }

        if (target == null ||
            !target.gameObject.activeInHierarchy)
        {
            CancelCurrentAction("TARGET INACTIVE");
            return;
        }

        if (isActing)
        {
            if (verboseTelegraphLogging &&
                !actionSkipLogged)
            {
                Debug.Log(
                    "KNIGHT ACTION SELECT SKIPPED " +
                    "reason=ACTION_IN_PROGRESS"
                );

                actionSkipLogged = true;
            }

            UpdateProjectileTelegraph();
            return;
        }

        if (actionRoutine != null)
        {
            UpdateProjectileTelegraph();
            return;
        }

        FaceTarget();

        if (Time.time < nextActionTime)
            return;

        float distance =
            Vector2.Distance(
                transform.position,
                target.position
            );

        if (forceProjectileNext)
        {
            LogActionSelection(
                distance,
                "FORCED PROJECTILE"
            );

            StartAction(
                "FORCED_PROJECTILE",
                RangedAttackRoutine(forced: true)
            );

            return;
        }

        if (distance <= meleeTriggerDistance)
        {
            closeActionCount++;

            bool shouldGuard =
                closeActionCount % 3 == 0;

            LogActionSelection(
                distance,
                shouldGuard
                    ? "GUARD"
                    : "MELEE"
            );

            StartAction(
                shouldGuard ? "GUARD" : "MELEE",
                shouldGuard
                    ? GuardRoutine()
                    : MeleeAttackRoutine()
            );

            return;
        }

        LogActionSelection(
            distance,
            "PROJECTILE"
        );

        StartAction(
            "PROJECTILE",
            RangedAttackRoutine(forced: false)
        );
    }

    private IEnumerator MeleeAttackRoutine()
    {
        InvalidateProjectileAttackSignal();
        combatState.SetAttacking(true);
        SetColor(attackColor);
        HideGuardIndicator();
        HideAllTelegraphs();
        ShowMeleeTelegraph();

        Debug.Log(
            "KNIGHT: MELEE ATTACK WINDUP"
        );

        yield return new WaitForSeconds(
            meleeWindup
        );

        PerformMeleeAttack();
        HideAllTelegraphs();

        combatState.SetAttacking(false);
        RestoreNormalColor();

        yield return new WaitForSeconds(
            recoveryDuration
        );

        yield return WaitForActionCooldown();

        FinishAction();
    }

    private IEnumerator RangedAttackRoutine(bool forced)
    {
        isRangedAction = true;

        LogRangedEvent(
            $"ENTER forced={forced.ToString().ToUpperInvariant()}"
        );

        if (forced)
        {
            yield return ForcedRangedBackstepRoutine();
        }

        if (!TryLockProjectileTarget())
        {
            LogRangedEvent(
                "CANCELLED reason=TARGET LOCK FAILED"
            );

            FinishAction();
            yield break;
        }

        forceProjectileNext = false;

        InvalidateProjectileAttackSignal();
        combatState.SetAttacking(true);
        int attackSignalId = BeginProjectileAttackSignal();
        SetColor(attackColor);
        HideGuardIndicator();
        HideAllTelegraphs();
        ShowProjectileTelegraph();

        Debug.Log(
            "KNIGHT: PROJECTILE WINDUP"
        );

        LogRangedEvent("WINDUP START");

        yield return new WaitForSeconds(
            rangedWindup
        );

        LogRangedEvent("WINDUP COMPLETE");
        HideAllTelegraphs();

        LogRangedEvent("SPAWN PROJECTILE");
        if (!SpawnProjectile(attackSignalId))
        {
            ResolveProjectileAttackSignal(
                attackSignalId
            );
        }

        yield return new WaitForSeconds(
            recoveryDuration
        );

        yield return WaitForActionCooldown();

        FinishAction();
        LogRangedEvent("EXIT");
    }

    private IEnumerator GuardRoutine()
    {
        InvalidateProjectileAttackSignal();
        combatState.SetGuarding(true);
        SetColor(guardColor);
        HideAllTelegraphs();
        ShowGuardIndicator();

        Debug.Log("KNIGHT: GUARDING");

        yield return new WaitForSeconds(
            guardDuration
        );

        HideGuardIndicator();
        combatState.SetGuarding(false);
        RestoreNormalColor();

        forceProjectileNext = true;

        yield return new WaitForSeconds(
            recoveryDuration
        );

        yield return WaitForActionCooldown();

        FinishAction();
    }

    private void PerformMeleeAttack()
    {
        if (meleeAttackPoint == null)
            return;

        poseController?.PlayAttack();

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
            HitVfxManager.ReportConfirmedHit(
                health,
                hitPosition
            );
            SpawnHitEffect(hitPosition);
        }

        Debug.Log(
            processed.Count > 0
                ? "KNIGHT: MELEE HIT"
                : "KNIGHT: MELEE MISSED"
        );
    }

    private bool SpawnProjectile(int attackSignalId)
    {
        if (projectilePrefab == null ||
            projectileSpawnPoint == null ||
            !hasLockedProjectileTarget)
        {
            Debug.LogError(
                "KnightController: " +
                "Projectile reference missing."
            );

            return false;
        }

        float direction =
            GetLockedProjectileDirection();

        KnightProjectile projectile =
            Instantiate(
                projectilePrefab,
                projectileSpawnPoint.position,
                Quaternion.identity
            );

        projectile.SetVisualStyle(ProjectileVisualStyle.Knight);
        projectile.Launch(
            direction,
            lockedProjectileTargetX,
            verboseTelegraphLogging,
            () => ResolveProjectileAttackSignal(
                attackSignalId
            )
        );

        if (verboseTelegraphLogging)
        {
            Debug.Log(
                "KNIGHT PROJECTILE SPAWNED\n" +
                $"spawnX={projectileSpawnPoint.position.x:F2}\n" +
                $"targetX={lockedProjectileTargetX:F2}\n" +
                $"direction={direction:F0}"
            );
        }

        ClearProjectileTargetLock();

        Debug.Log(
            "KNIGHT: PROJECTILE FIRED"
        );

        return true;
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
        ReleaseActionLock();
    }

    private IEnumerator WaitForActionCooldown()
    {
        nextActionTime =
            Time.time + actionCooldown;

        yield return new WaitForSeconds(
            actionCooldown
        );
    }

    private void StartAction(
        string actionName,
        IEnumerator routine)
    {
        isActing = true;
        activeActionName = actionName;
        actionSkipLogged = false;

        if (verboseTelegraphLogging)
        {
            Debug.Log(
                "KNIGHT ACTION LOCK: " +
                $"ACQUIRED action={activeActionName}"
            );
        }

        actionRoutine = StartCoroutine(routine);
    }

    private void ReleaseActionLock()
    {
        if (verboseTelegraphLogging &&
            isActing)
        {
            Debug.Log(
                "KNIGHT ACTION LOCK: " +
                $"RELEASED action={activeActionName}"
            );
        }

        isRangedAction = false;
        actionRoutine = null;
        isActing = false;
        actionSkipLogged = false;
        activeActionName = null;
    }

    private IEnumerator ForcedRangedBackstepRoutine()
    {
        if (body == null ||
            target == null ||
            forcedRangedBackstepDistance <= 0f)
        {
            yield break;
        }

        float direction =
            GetBackstepDirection();

        float speed =
            forcedRangedBackstepDistance /
            forcedRangedBackstepDuration;

        body.linearVelocity =
            new Vector2(
                direction * speed,
                body.linearVelocity.y
            );

        yield return new WaitForSeconds(
            forcedRangedBackstepDuration
        );

        StopForcedRangedBackstep();
    }

    private float GetBackstepDirection()
    {
        float direction =
            transform.position.x -
            target.position.x;

        if (!Mathf.Approximately(direction, 0f))
            return Mathf.Sign(direction);

        return -Mathf.Sign(transform.localScale.x);
    }

    private void StopForcedRangedBackstep()
    {
        if (body == null)
            return;

        body.linearVelocity =
            new Vector2(0f, body.linearVelocity.y);
    }

    private void ShowMeleeTelegraph()
    {
        if (meleeTelegraph == null)
            return;

        float diameter = meleeRadius * 2f;

        meleeTelegraph.transform.localScale =
            new Vector3(
                diameter,
                diameter,
                1f
            );

        meleeTelegraph.SetActive(true);

        if (verboseTelegraphLogging)
        {
            Debug.Log("KNIGHT TELEGRAPH: MELEE SHOW");
        }
    }

    private void ShowProjectileTelegraph()
    {
        if (projectileTelegraph == null ||
            projectileSpawnPoint == null)
        {
            return;
        }

        projectileTelegraph.SetActive(true);
        UpdateProjectileTelegraph();

        if (verboseTelegraphLogging)
        {
            float direction =
                GetLockedProjectileDirection();

            Debug.Log(
                "KNIGHT TELEGRAPH: " +
                $"PROJECTILE SHOW direction={direction:F0}"
            );
        }
    }

    private void UpdateProjectileTelegraph()
    {
        if (projectileTelegraph == null ||
            !projectileTelegraph.activeSelf ||
            projectileSpawnPoint == null)
        {
            return;
        }

        float direction =
            GetLockedProjectileDirection();

        Vector3 position =
            projectileSpawnPoint.position;

        projectileTelegraph.transform.position =
            position;

        Transform parent =
            projectileTelegraph.transform.parent;

        float parentScaleMagnitude =
            parent == null
                ? 1f
                : Mathf.Abs(parent.lossyScale.x);

        float parentDirection =
            parent == null ||
            Mathf.Approximately(parent.lossyScale.x, 0f)
                ? 1f
                : Mathf.Sign(parent.lossyScale.x);

        float targetDistance =
            hasLockedProjectileTarget
                ? Mathf.Abs(
                    lockedProjectileTargetX -
                    projectileSpawnPoint.position.x
                )
                : 0f;

        float localScaleMagnitude =
            targetDistance /
            Mathf.Max(
                0.01f,
                projectileTelegraphReferenceLength *
                Mathf.Max(0.01f, parentScaleMagnitude)
            );

        Vector3 scale =
            projectileTelegraph.transform.localScale;

        scale.x =
            localScaleMagnitude *
            direction *
            parentDirection;

        projectileTelegraph.transform.localScale =
            scale;
    }

    private bool TryLockProjectileTarget()
    {
        if (target == null ||
            projectileSpawnPoint == null)
        {
            return false;
        }

        lockedProjectileTargetX = target.position.x;
        hasLockedProjectileTarget = true;

        if (verboseTelegraphLogging)
        {
            Debug.Log(
                "KNIGHT PROJECTILE TARGET LOCK\n" +
                $"heroX={target.position.x:F2}\n" +
                $"targetX={lockedProjectileTargetX:F2}\n" +
                $"spawnX={projectileSpawnPoint.position.x:F2}"
            );
        }

        return true;
    }

    private float GetLockedProjectileDirection()
    {
        float direction =
            !hasLockedProjectileTarget ||
            projectileSpawnPoint == null
                ? 1f
                : lockedProjectileTargetX -
                  projectileSpawnPoint.position.x;

        if (Mathf.Approximately(direction, 0f))
            return 1f;

        return Mathf.Sign(direction);
    }

    private void ClearProjectileTargetLock()
    {
        hasLockedProjectileTarget = false;
        lockedProjectileTargetX = 0f;
    }

    private int BeginProjectileAttackSignal()
    {
        nextProjectileAttackSignalId++;
        activeProjectileAttackSignalId =
            nextProjectileAttackSignalId;

        return activeProjectileAttackSignalId;
    }

    private void InvalidateProjectileAttackSignal()
    {
        activeProjectileAttackSignalId = 0;
    }

    private void ResolveProjectileAttackSignal(
        int attackSignalId)
    {
        if (attackSignalId == 0 ||
            attackSignalId !=
            activeProjectileAttackSignalId)
        {
            return;
        }

        activeProjectileAttackSignalId = 0;

        if (combatState != null)
        {
            combatState.SetAttacking(false);
        }

        RestoreNormalColor();
    }

    private void HideAllTelegraphs()
    {
        bool meleeWasActive =
            meleeTelegraph != null &&
            meleeTelegraph.activeSelf;

        bool projectileWasActive =
            projectileTelegraph != null &&
            projectileTelegraph.activeSelf;

        if (meleeTelegraph != null)
        {
            meleeTelegraph.SetActive(false);
        }

        if (projectileTelegraph != null)
        {
            projectileTelegraph.SetActive(false);
        }

        if (!verboseTelegraphLogging)
            return;

        if (meleeWasActive)
        {
            Debug.Log("KNIGHT TELEGRAPH: MELEE HIDE");
        }

        if (projectileWasActive)
        {
            Debug.Log("KNIGHT TELEGRAPH: PROJECTILE HIDE");
        }
    }

    private void LogActionSelection(
        float distance,
        string action)
    {
        if (!verboseTelegraphLogging)
            return;

        Debug.Log(
            "KNIGHT ACTION SELECT " +
            $"distance={distance:F2} " +
            $"threshold={meleeTriggerDistance:F2} " +
            $"action={action} " +
            "forceProjectileNext=" +
            (forceProjectileNext ? "TRUE" : "FALSE")
        );
    }

    private void LogRangedEvent(string message)
    {
        if (!verboseTelegraphLogging)
            return;

        Debug.Log($"KNIGHT RANGED: {message}");
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

    public Color GetCurrentStateColor()
    {
        if (combatState != null &&
            combatState.IsGuarding)
        {
            return guardColor;
        }

        if (combatState != null &&
            combatState.IsAttacking)
        {
            return attackColor;
        }

        return normalColor;
    }

    private void RestoreNormalColor()
    {
        SetColor(normalColor);
    }

    private void ShowGuardIndicator()
    {
        if (guardIndicator == null)
            return;

        Vector3 localPosition =
            guardIndicator.transform.localPosition;

        localPosition.x = guardIndicatorOffsetX;
        guardIndicator.transform.localPosition =
            localPosition;

        guardIndicator.SetActive(true);
        guardVisual?.PlayGuard();
    }

    private void HideGuardIndicator()
    {
        if (guardVisual != null)
        {
            // The gameplay guard state ends at the existing call site. Only
            // the child renderer stays visible briefly for its Break frames.
            guardVisual.StopGuard();
            return;
        }

        if (guardIndicator != null)
        {
            guardIndicator.SetActive(false);
        }
    }

    private void OnDisable()
    {
        CancelCurrentAction("KNIGHT DISABLED");
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
    
    private void CancelCurrentAction(
        string reason = "CANCELLED")
    {
        if (actionRoutine != null)
        {
            if (isRangedAction)
            {
                LogRangedEvent(
                    $"CANCELLED reason={reason}"
                );
            }

            StopCoroutine(actionRoutine);
            actionRoutine = null;
        }

        ReleaseActionLock();
        forceProjectileNext = false;
        ClearProjectileTargetLock();
        InvalidateProjectileAttackSignal();
        StopForcedRangedBackstep();

        if (combatState != null)
        {
            combatState.ResetState();
        }

        HideAllTelegraphs();
        HideGuardIndicator();
        RestoreNormalColor();
    }
}
