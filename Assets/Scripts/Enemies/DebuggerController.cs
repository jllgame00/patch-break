using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyCombatState))]
public sealed class DebuggerController : MonoBehaviour
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

    [SerializeField]
    private PlayerPatternTracker patternTracker;

    [SerializeField]
    private RuntimeConsoleUI runtimeConsole;

    [Header("Telegraphs")]
    [SerializeField]
    private GameObject meleeTelegraph;

    [SerializeField]
    private GameObject projectileTelegraph;

    [SerializeField]
    private LineRenderer predictiveSweepTelegraph;

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
    private int meleeDamage = 20;

    [SerializeField, Min(0.01f)]
    private float meleeWindup = 0.4f;

    [Header("Ranged Attack")]
    [SerializeField, Min(0.01f)]
    private float rangedWindup = 0.25f;

    [Header("Guard")]
    [SerializeField, Min(0.1f)]
    private float guardDuration = 0.9f;

    [SerializeField, Min(1)]
    private int meleeCountBeforeGuard = 2;

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

    [Header("Advance")]
    [SerializeField, Min(0.01f)]
    private float advanceSpeed = 3.5f;

    [SerializeField, Min(0f)]
    private float advanceStopDistance = 2.4f;

    [SerializeField, Min(0.01f)]
    private float advanceMaxDuration = 1f;

    [SerializeField, Min(0f)]
    private float advanceRecovery = 0.1f;

    [SerializeField]
    private bool advanceAfterProjectile = true;

    [Header("Arena Leash")]
    [SerializeField, Min(0f)]
    private float homeLeashDistance = 5.5f;

    [Header("Back Dash Analysis")]
    [SerializeField, Min(0f)]
    private float analysisLineDelay = 0.35f;

    [SerializeField, Min(0f)]
    private float analysisEndDelay = 0.25f;

    [Header("Predictive Retreat Sweep")]
    [SerializeField, Min(0.01f)]
    private float predictedDashDistance = 1.8f;

    [SerializeField, Min(0f)]
    private float predictiveDangerPadding = 0.35f;

    [SerializeField, Min(0.01f)]
    private float predictiveDangerHeight = 2f;

    [SerializeField, Min(1)]
    private int predictiveDamage = 25;

    [SerializeField, Min(0.01f)]
    private float predictiveWindup = 0.38f;

    [SerializeField, Min(0f)]
    private float predictiveRecovery = 0.25f;

    [SerializeField, Min(0f)]
    private float predictiveCooldown = 1.4f;

    [SerializeField]
    private Color predictiveTelegraphColor =
        new(0.8f, 0.15f, 1f, 0.45f);

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
    private bool isRangedAction;
    private bool isActing;
    private bool actionSkipLogged;
    private string activeActionName;
    private float lockedProjectileTargetX;
    private bool hasLockedProjectileTarget;
    private int nextProjectileAttackSignalId;
    private int activeProjectileAttackSignalId;
    private KnightProjectile activeProjectile;
    private bool shouldAdvanceNext;
    private float homeX;
    private bool backDashAnalysisPending;
    private bool backDashAdaptationEnabled;
    private bool backDashAnalysisPlayed;
    private bool predictiveStrikeReady;
    private float nextPredictiveStrikeTime;
    private float predictiveDangerMinX;
    private float predictiveDangerMaxX;
    private float predictiveDangerCenterX;
    private float predictiveDangerCenterY;
    private float predictiveDangerWidth;
    private bool createdPredictiveTelegraphAtRuntime;
    private Material predictiveTelegraphMaterial;

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
        homeX = transform.position.x;

        if (targetHero == null &&
            target != null)
        {
            targetHero =
                target.GetComponent<
                    HeroController>();
        }

        if (patternTracker == null &&
            target != null)
        {
            patternTracker =
                target.GetComponent<PlayerPatternTracker>();
        }

        if (runtimeConsole == null)
        {
            runtimeConsole = UnityEngine.Object
                .FindFirstObjectByType<RuntimeConsoleUI>();
        }

        originalScaleX =
            Mathf.Abs(transform.localScale.x);

        if (spriteRenderer != null)
        {
            normalColor =
                spriteRenderer.color;
        }

        if (meleeTelegraph == null)
        {
            Debug.LogWarning(
                "Debugger melee telegraph reference is missing.",
                this
            );
        }

        if (projectileTelegraph == null)
        {
            Debug.LogWarning(
                "Debugger projectile telegraph reference is missing.",
                this
            );
        }

        if (guardIndicator == null)
        {
            Debug.LogWarning(
                "Debugger guard indicator reference is missing.",
                this
            );
        }

        EnsurePredictiveSweepTelegraph();
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

        UpdateBackDashAdaptation();

        if (isActing)
        {
            if (verboseTelegraphLogging &&
                !actionSkipLogged)
            {
                Debug.Log(
                    "DEBUGGER ACTION SELECT SKIPPED " +
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

        float distance =
            Vector2.Distance(
                transform.position,
                target.position
            );

        if (backDashAnalysisPending)
        {
            StartAction(
                "BACK_DASH_ANALYSIS",
                BackDashAnalysisRoutine()
            );

            return;
        }

        if (NeedsArenaRecovery())
        {
            shouldAdvanceNext = false;
            LogActionSelection(distance, "ARENA RECOVERY");
            StartAction(
                "ARENA_RECOVERY",
                AdvanceTowardHeroRoutine(
                    AdvanceReason.ArenaRecovery
                )
            );

            return;
        }

        if (Time.time < nextActionTime)
            return;

        if (backDashAdaptationEnabled &&
            Time.time >= nextPredictiveStrikeTime)
        {
            predictiveStrikeReady = true;
        }

        if (predictiveStrikeReady)
        {
            predictiveStrikeReady = false;
            LogActionSelection(
                distance,
                "PREDICTIVE RETREAT SWEEP"
            );
            StartAction(
                "PREDICTIVE_RETREAT_SWEEP",
                PredictiveRetreatSweepRoutine()
            );

            return;
        }

        if (shouldAdvanceNext)
        {
            shouldAdvanceNext = false;

            if (GetHorizontalTargetDistance() >
                advanceStopDistance)
            {
                LogActionSelection(distance, "ADVANCE");
                StartAction(
                    "ADVANCE",
                    AdvanceTowardHeroRoutine(
                        AdvanceReason.PostProjectile
                    )
                );

                return;
            }
        }

        if (distance <= meleeTriggerDistance)
        {
            closeActionCount++;

            bool shouldGuard =
                closeActionCount >=
                meleeCountBeforeGuard;

            if (shouldGuard)
            {
                closeActionCount = 0;
            }

            LogActionSelection(
                distance,
                shouldGuard
                    ? "GUARD THEN FORCED PROJECTILE"
                    : "MELEE"
            );

            StartAction(
                shouldGuard
                    ? "GUARD_FORCED_PROJECTILE"
                    : "MELEE",
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
            "DEBUGGER: MELEE ATTACK WINDUP"
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

        InvalidateProjectileAttackSignal();
        combatState.SetAttacking(true);
        int attackSignalId = BeginProjectileAttackSignal();
        SetColor(attackColor);
        HideGuardIndicator();
        HideAllTelegraphs();
        ShowProjectileTelegraph();

        Debug.Log(
            "DEBUGGER: PROJECTILE WINDUP"
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
                attackSignalId,
                scheduleAdvance: false
            );
        }

        yield return new WaitUntil(
            () => attackSignalId !=
                  activeProjectileAttackSignalId
        );

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

        Debug.Log("DEBUGGER: GUARDING");

        yield return new WaitForSeconds(
            guardDuration
        );

        HideGuardIndicator();
        combatState.SetGuarding(false);
        RestoreNormalColor();

        yield return RangedAttackRoutine(forced: true);
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
                ? "DEBUGGER: MELEE HIT"
                : "DEBUGGER: MELEE MISSED"
        );
    }

    private bool SpawnProjectile(int attackSignalId)
    {
        if (projectilePrefab == null ||
            projectileSpawnPoint == null ||
            !hasLockedProjectileTarget)
        {
            Debug.LogError(
                "DebuggerController: " +
                "Projectile reference missing."
            );

            return false;
        }

        float direction =
            GetLockedProjectileDirection();

        activeProjectile =
            Instantiate(
                projectilePrefab,
                projectileSpawnPoint.position,
                Quaternion.identity
            );

        activeProjectile.Launch(
            direction,
            lockedProjectileTargetX,
            verboseTelegraphLogging,
            () => ResolveProjectileAttackSignal(
                attackSignalId
            ),
            "DEBUGGER PROJECTILE"
        );

        if (verboseTelegraphLogging)
        {
            Debug.Log(
                "DEBUGGER PROJECTILE SPAWNED\n" +
                $"spawnX={projectileSpawnPoint.position.x:F2}\n" +
                $"targetX={lockedProjectileTargetX:F2}\n" +
                $"direction={direction:F0}"
            );
        }

        ClearProjectileTargetLock();

        Debug.Log(
            "DEBUGGER: PROJECTILE FIRED"
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

    private IEnumerator BackDashAnalysisRoutine()
    {
        backDashAnalysisPending = false;

        if (combatState != null)
        {
            combatState.ResetState();
        }

        StopHorizontalMovement();
        HideAllTelegraphs();
        HideGuardIndicator();
        RestoreNormalColor();

        Debug.Log("DEBUGGER ANALYSIS: ENTER");

        AppendAnalysisMessage(
            "ANALYZING COMBAT RESPONSE..."
        );

        yield return new WaitForSeconds(analysisLineDelay);

        AppendAnalysisMessage(
            "REPEATED RETREAT DETECTED"
        );

        yield return new WaitForSeconds(analysisLineDelay);

        AppendAnalysisMessage(
            "COUNTER PATCH INSTALLED"
        );

        yield return new WaitForSeconds(analysisLineDelay);

        backDashAdaptationEnabled = true;
        predictiveStrikeReady = true;
        nextPredictiveStrikeTime = Time.time;

        AppendAnalysisMessage(
            "RETREAT VECTOR PREDICTION: ACTIVE"
        );

        Debug.Log(
            "DEBUGGER ADAPTATION ENABLED\n" +
            "profile=BACK_DASH_DEPENDENCY\n" +
            "counter=PREDICTIVE_RETREAT_SWEEP"
        );

        yield return new WaitForSeconds(analysisEndDelay);

        FinishAction();
    }

    private IEnumerator PredictiveRetreatSweepRoutine()
    {
        if (!TryCommitPredictiveSweep())
        {
            FinishAction();
            yield break;
        }

        InvalidateProjectileAttackSignal();
        combatState.SetAttacking(true);
        SetColor(attackColor);
        HideGuardIndicator();
        HideAllTelegraphs();
        ShowPredictiveSweepTelegraph();

        yield return new WaitForSeconds(predictiveWindup);

        PerformPredictiveRetreatSweep();
        HidePredictiveSweepTelegraph();

        combatState.SetAttacking(false);
        RestoreNormalColor();

        nextPredictiveStrikeTime =
            Time.time + predictiveCooldown;

        yield return new WaitForSeconds(predictiveRecovery);
        yield return WaitForActionCooldown();

        FinishAction();
    }

    private bool TryCommitPredictiveSweep()
    {
        if (target == null)
            return false;

        float heroStartX = target.position.x;
        float awayDirection =
            heroStartX - transform.position.x;

        if (Mathf.Abs(awayDirection) < 0.001f)
        {
            float facingDirection =
                Mathf.Sign(transform.localScale.x);

            awayDirection =
                Mathf.Approximately(facingDirection, 0f)
                    ? 1f
                    : -facingDirection;
        }
        else
        {
            awayDirection = Mathf.Sign(awayDirection);
        }

        float predictedBackDashX =
            heroStartX +
            awayDirection * predictedDashDistance;

        predictiveDangerMinX = Mathf.Min(
            heroStartX,
            predictedBackDashX
        ) - predictiveDangerPadding;

        predictiveDangerMaxX = Mathf.Max(
            heroStartX,
            predictedBackDashX
        ) + predictiveDangerPadding;

        predictiveDangerCenterX =
            (predictiveDangerMinX +
             predictiveDangerMaxX) * 0.5f;

        predictiveDangerCenterY = target.position.y;
        predictiveDangerWidth = Mathf.Max(
            0.01f,
            predictiveDangerMaxX - predictiveDangerMinX
        );

        Debug.Log(
            "DEBUGGER PREDICTIVE SWEEP: TARGET LOCK\n" +
            $"heroStartX={heroStartX:F2}\n" +
            $"predictedBackDashX={predictedBackDashX:F2}\n" +
            $"dangerMinX={predictiveDangerMinX:F2}\n" +
            $"dangerMaxX={predictiveDangerMaxX:F2}"
        );

        return true;
    }

    private void PerformPredictiveRetreatSweep()
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            new Vector2(
                predictiveDangerCenterX,
                predictiveDangerCenterY
            ),
            new Vector2(
                predictiveDangerWidth,
                predictiveDangerHeight
            ),
            0f,
            targetLayer
        );

        string result = "MISS";
        HeroController hitHero = targetHero;

        foreach (Collider2D hit in hits)
        {
            Health health =
                hit.GetComponentInParent<Health>();

            if (health == null)
                continue;

            hitHero = hit.GetComponentInParent<
                HeroController>();

            if (hitHero != null &&
                hitHero.IsInvulnerable)
            {
                result = "EVADED";
                break;
            }

            health.TakeDamage(predictiveDamage);
            SpawnHitEffect(
                hit.ClosestPoint(
                    new Vector2(
                        predictiveDangerCenterX,
                        predictiveDangerCenterY
                    )
                )
            );

            result = "HIT";
            break;
        }

        bool heroIsDashing =
            hitHero != null && hitHero.IsDashing;

        bool heroIsInvulnerable =
            hitHero != null && hitHero.IsInvulnerable;

        Debug.Log(
            "DEBUGGER PREDICTIVE SWEEP: IMPACT\n" +
            $"heroX={GetTargetX():F2}\n" +
            $"dangerMinX={predictiveDangerMinX:F2}\n" +
            $"dangerMaxX={predictiveDangerMaxX:F2}\n" +
            $"heroIsDashing={heroIsDashing}\n" +
            $"heroIsInvulnerable={heroIsInvulnerable}\n" +
            $"result={result}"
        );
    }

    private IEnumerator AdvanceTowardHeroRoutine(
        AdvanceReason advanceReason)
    {
        if (combatState != null)
        {
            combatState.ResetState();
        }

        HideAllTelegraphs();
        HideGuardIndicator();
        RestoreNormalColor();
        LogAdvanceEnter(advanceReason);

        string exitReason = "TIMEOUT";
        float elapsed = 0f;

        if (body == null)
        {
            exitReason = "RIGIDBODY_MISSING";
        }
        else
        {
            while (elapsed < advanceMaxDuration)
            {
                if (runtime == null || !runtime.IsRunning)
                {
                    exitReason = "PROGRAM_STOPPED";
                    break;
                }

                if (target == null ||
                    !target.gameObject.activeInHierarchy)
                {
                    exitReason = "HERO_DEAD";
                    break;
                }

                float destinationX =
                    advanceReason ==
                    AdvanceReason.ArenaRecovery
                        ? homeX
                        : target.position.x;

                float stopDistance =
                    advanceReason ==
                    AdvanceReason.ArenaRecovery
                        ? 0.01f
                        : advanceStopDistance;

                float deltaX =
                    destinationX - body.position.x;

                float remainingDistance =
                    Mathf.Abs(deltaX);

                if (remainingDistance <= stopDistance)
                {
                    exitReason = "DISTANCE_REACHED";
                    break;
                }

                float direction = Mathf.Sign(deltaX);
                FaceDirection(direction);

                float movementDistance =
                    remainingDistance - stopDistance;

                float fixedDeltaTime =
                    Mathf.Max(0.001f, Time.fixedDeltaTime);

                float speed = Mathf.Min(
                    advanceSpeed,
                    movementDistance / fixedDeltaTime
                );

                body.linearVelocity =
                    new Vector2(
                        direction * speed,
                        body.linearVelocity.y
                    );

                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            StopHorizontalMovement();
        }

        LogAdvanceExit(exitReason);

        yield return new WaitForSeconds(advanceRecovery);
        yield return WaitForActionCooldown();

        FinishAction();
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
                "DEBUGGER ACTION LOCK: " +
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
                "DEBUGGER ACTION LOCK: " +
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

        float requestedX =
            body.position.x +
            direction * forcedRangedBackstepDistance;

        float destinationX = Mathf.Clamp(
            requestedX,
            homeX - homeLeashDistance,
            homeX + homeLeashDistance
        );

        if (verboseTelegraphLogging &&
            !Mathf.Approximately(requestedX, destinationX))
        {
            Debug.Log(
                "DEBUGGER BACKSTEP CLAMPED\n" +
                $"requestedX={requestedX:F2}\n" +
                $"clampedX={destinationX:F2}\n" +
                $"homeX={homeX:F2}"
            );
        }

        float elapsed = 0f;

        while (elapsed < forcedRangedBackstepDuration)
        {
            float deltaX = destinationX - body.position.x;

            if (Mathf.Abs(deltaX) <= 0.01f)
            {
                break;
            }

            float fixedDeltaTime =
                Mathf.Max(0.001f, Time.fixedDeltaTime);

            float speed = Mathf.Min(
                forcedRangedBackstepDistance /
                forcedRangedBackstepDuration,
                Mathf.Abs(deltaX) / fixedDeltaTime
            );

            body.linearVelocity =
                new Vector2(
                    Mathf.Sign(deltaX) * speed,
                    body.linearVelocity.y
                );

            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

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
        StopHorizontalMovement();
    }

    private void StopHorizontalMovement()
    {
        if (body == null)
            return;

        body.linearVelocity =
            new Vector2(0f, body.linearVelocity.y);
    }

    private void EnsurePredictiveSweepTelegraph()
    {
        if (predictiveSweepTelegraph != null)
            return;

        GameObject telegraphObject = new(
            "PredictiveSweepTelegraph"
        );

        telegraphObject.transform.SetParent(
            transform,
            false
        );

        predictiveSweepTelegraph =
            telegraphObject.AddComponent<LineRenderer>();

        predictiveSweepTelegraph.useWorldSpace = true;
        predictiveSweepTelegraph.positionCount = 2;
        predictiveSweepTelegraph.numCapVertices = 0;
        predictiveSweepTelegraph.numCornerVertices = 0;
        predictiveSweepTelegraph.sortingOrder = 1;

        Shader shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            predictiveTelegraphMaterial = new Material(shader);
            predictiveSweepTelegraph.material =
                predictiveTelegraphMaterial;
        }
        else
        {
            Debug.LogWarning(
                "Debugger predictive sweep telegraph shader " +
                "is missing.",
                this
            );
        }

        createdPredictiveTelegraphAtRuntime = true;
        HidePredictiveSweepTelegraph();
    }

    private void ShowPredictiveSweepTelegraph()
    {
        EnsurePredictiveSweepTelegraph();

        if (predictiveSweepTelegraph == null)
            return;

        predictiveSweepTelegraph.startWidth =
            predictiveDangerHeight;
        predictiveSweepTelegraph.endWidth =
            predictiveDangerHeight;
        predictiveSweepTelegraph.startColor =
            predictiveTelegraphColor;
        predictiveSweepTelegraph.endColor =
            predictiveTelegraphColor;

        predictiveSweepTelegraph.SetPosition(
            0,
            new Vector3(
                predictiveDangerMinX,
                predictiveDangerCenterY,
                0f
            )
        );

        predictiveSweepTelegraph.SetPosition(
            1,
            new Vector3(
                predictiveDangerMaxX,
                predictiveDangerCenterY,
                0f
            )
        );

        predictiveSweepTelegraph.gameObject.SetActive(true);
    }

    private void HidePredictiveSweepTelegraph()
    {
        if (predictiveSweepTelegraph != null)
        {
            predictiveSweepTelegraph.gameObject.SetActive(false);
        }
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
            Debug.Log("DEBUGGER TELEGRAPH: MELEE SHOW");
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
                "DEBUGGER TELEGRAPH: " +
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
                "DEBUGGER PROJECTILE TARGET LOCK\n" +
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
        int attackSignalId,
        bool scheduleAdvance = true)
    {
        if (attackSignalId == 0 ||
            attackSignalId !=
            activeProjectileAttackSignalId)
        {
            return;
        }

        activeProjectileAttackSignalId = 0;
        activeProjectile = null;

        if (combatState != null)
        {
            combatState.SetAttacking(false);
        }

        RestoreNormalColor();

        if (scheduleAdvance && advanceAfterProjectile)
        {
            shouldAdvanceNext = true;
        }
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

        HidePredictiveSweepTelegraph();

        if (!verboseTelegraphLogging)
            return;

        if (meleeWasActive)
        {
            Debug.Log("DEBUGGER TELEGRAPH: MELEE HIDE");
        }

        if (projectileWasActive)
        {
            Debug.Log("DEBUGGER TELEGRAPH: PROJECTILE HIDE");
        }
    }

    private void LogActionSelection(
        float distance,
        string action)
    {
        if (!verboseTelegraphLogging)
            return;

        Debug.Log(
            "DEBUGGER ACTION SELECT " +
            $"distance={distance:F2} " +
            $"threshold={meleeTriggerDistance:F2} " +
            $"action={action}"
        );
    }

    private void LogRangedEvent(string message)
    {
        if (!verboseTelegraphLogging)
            return;

        Debug.Log($"DEBUGGER RANGED: {message}");
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

        FaceDirection(Mathf.Sign(direction));
    }

    private void FaceDirection(float direction)
    {
        if (Mathf.Approximately(direction, 0f))
            return;

        Vector3 scale = transform.localScale;

        scale.x = originalScaleX *
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
    }

    private void HideGuardIndicator()
    {
        if (guardIndicator != null)
        {
            guardIndicator.SetActive(false);
        }
    }

    private void OnDisable()
    {
        CancelCurrentAction("DEBUGGER DISABLED");
    }

    private void OnDestroy()
    {
        if (createdPredictiveTelegraphAtRuntime &&
            predictiveTelegraphMaterial != null)
        {
            Destroy(predictiveTelegraphMaterial);
        }
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
        if (IsAdvanceAction())
        {
            LogAdvanceExit(GetAdvanceCancelReason(reason));
        }

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
        ClearProjectileTargetLock();
        InvalidateProjectileAttackSignal();
        shouldAdvanceNext = false;
        backDashAnalysisPending = false;
        predictiveStrikeReady = false;

        if (activeProjectile != null)
        {
            Destroy(activeProjectile.gameObject);
            activeProjectile = null;
        }

        StopForcedRangedBackstep();

        if (combatState != null)
        {
            combatState.ResetState();
        }

        HideAllTelegraphs();
        HideGuardIndicator();
        RestoreNormalColor();
    }

    private void UpdateBackDashAdaptation()
    {
        if (backDashAnalysisPlayed ||
            patternTracker == null ||
            patternTracker.CurrentProfile !=
            PlayerPatternProfile.BackDashDependency)
        {
            return;
        }

        backDashAnalysisPlayed = true;
        backDashAnalysisPending = true;

        Debug.Log(
            "DEBUGGER ADAPTATION: PROFILE RECEIVED\n" +
            "profile=BACK_DASH_DEPENDENCY"
        );
    }

    private void AppendAnalysisMessage(string message)
    {
        if (runtimeConsole != null)
        {
            runtimeConsole.AppendSystemMessage(message);
        }

        Debug.Log($"DEBUGGER ANALYSIS: {message}");
    }

    private bool NeedsArenaRecovery()
    {
        return Mathf.Abs(transform.position.x - homeX) >
               homeLeashDistance;
    }

    private float GetHorizontalTargetDistance()
    {
        if (target == null)
            return 0f;

        return Mathf.Abs(
            target.position.x - transform.position.x
        );
    }

    private bool IsAdvanceAction()
    {
        return activeActionName == "ADVANCE" ||
               activeActionName == "ARENA_RECOVERY";
    }

    private void LogAdvanceEnter(
        AdvanceReason advanceReason)
    {
        if (!verboseTelegraphLogging)
            return;

        Debug.Log(
            "DEBUGGER ADVANCE: ENTER\n" +
            $"currentX={transform.position.x:F2}\n" +
            $"heroX={GetTargetX():F2}\n" +
            $"homeX={homeX:F2}\n" +
            $"reason={GetAdvanceReasonLabel(advanceReason)}"
        );
    }

    private void LogAdvanceExit(string reason)
    {
        if (!verboseTelegraphLogging)
            return;

        Debug.Log(
            "DEBUGGER ADVANCE: EXIT\n" +
            $"currentX={transform.position.x:F2}\n" +
            $"heroX={GetTargetX():F2}\n" +
            $"distance={GetHorizontalTargetDistance():F2}\n" +
            $"reason={reason}"
        );
    }

    private float GetTargetX()
    {
        return target == null
            ? 0f
            : target.position.x;
    }

    private string GetAdvanceCancelReason(string reason)
    {
        return reason switch
        {
            "PROGRAM STOPPED" => "PROGRAM_STOPPED",
            "DEBUGGER DISABLED" => "DEBUGGER_DISABLED",
            "TARGET INACTIVE" => "HERO_DEAD",
            _ => reason
        };
    }

    private string GetAdvanceReasonLabel(
        AdvanceReason advanceReason)
    {
        return advanceReason ==
               AdvanceReason.ArenaRecovery
            ? "ARENA_RECOVERY"
            : "POST_PROJECTILE";
    }

    private enum AdvanceReason
    {
        PostProjectile,
        ArenaRecovery
    }
}
