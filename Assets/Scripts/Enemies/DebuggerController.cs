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

    [SerializeField]
    private LivePatchController livePatchController;

    [SerializeField]
    private LivePatchUI adaptivePatchHintUI;

    [SerializeField]
    private Camera combatCamera;

    [SerializeField]
    private InfiniteParallaxBackground infiniteParallaxBackground;

    [SerializeField]
    private DebuggerCombatCameraFollow combatCameraFollow;

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

    [Header("Combat Retreat Spacing")]
    [SerializeField, Min(0f)]
    private float retreatCooldown = 2f;

    [SerializeField, Min(0f)]
    private float preferredCombatDistance = 3f;

    [SerializeField, Min(0f)]
    private float minimumRetreatDistance = 0.15f;

    [SerializeField, Range(0f, 0.49f)]
    private float safeViewportMinX = 0.12f;

    [SerializeField, Range(0.51f, 1f)]
    private float safeViewportMaxX = 0.88f;

    [SerializeField, Range(0f, 0.5f)]
    private float emergencyViewportMargin = 0.2f;

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

    [Header("Adaptive Patch Guidance")]
    [SerializeField, Min(0f)]
    private float adaptiveHintGraceDuration = 1.25f;

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
    private float nextRetreatAllowedAt;
    private bool retreatVisualActive;
    private bool retreatParallaxActive;
    private bool backDashAdaptationEnabled;
    private bool predictiveStrikeReady;
    private bool adaptationAnalysisPending;
    private bool adaptiveGuardReady;
    private bool fakeGuardPunishReady;
    private PlayerPatternProfile activeAdaptationProfile;
    private PlayerPatternProfile pendingAdaptationProfile;
    private float nextPredictiveStrikeTime;
    private float predictiveDangerMinX;
    private float predictiveDangerMaxX;
    private float predictiveDangerCenterX;
    private float predictiveDangerCenterY;
    private float predictiveDangerWidth;
    private bool createdPredictiveTelegraphAtRuntime;
    private Material predictiveTelegraphMaterial;
    private bool firstPredictiveHitHandled;
    private bool livePatchPromptActive;
    private bool firstForwardCounterAvoided;
    private float adaptiveHintGraceUntil;
    private bool adaptiveHintGraceExitLogged;
    private bool livePatchEventsSubscribed;
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
        homeX = transform.position.x;

        if (combatCamera == null)
        {
            combatCamera = Camera.main;
        }

        if (infiniteParallaxBackground == null)
        {
            infiniteParallaxBackground = UnityEngine.Object
                .FindFirstObjectByType<InfiniteParallaxBackground>();
        }

        if (combatCameraFollow == null)
        {
            combatCameraFollow = UnityEngine.Object
                .FindFirstObjectByType<DebuggerCombatCameraFollow>();
        }

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

        if (livePatchController == null && target != null)
        {
            livePatchController = target.GetComponent<
                LivePatchController>();
        }

        if (adaptivePatchHintUI == null)
        {
            adaptivePatchHintUI = UnityEngine.Object
                .FindFirstObjectByType<LivePatchUI>();
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

        UpdateAdaptationProfile();

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

        if (IsAdaptiveHintGraceActive())
        {
            StopHorizontalMovement();

            if (combatState != null)
            {
                combatState.ResetState();
            }

            return;
        }

        if (adaptationAnalysisPending)
        {
            StartAction(
                GetAnalysisActionName(
                    pendingAdaptationProfile
                ),
                AdaptationAnalysisRoutine(
                    pendingAdaptationProfile
                )
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
            if (adaptiveGuardReady)
            {
                adaptiveGuardReady = false;
                LogActionSelection(
                    distance,
                    "ADAPTIVE GUARD"
                );
                StartAction(
                    "ADAPTIVE_GUARD",
                    GuardRoutine()
                );
                return;
            }

            closeActionCount++;

            bool shouldGuard =
                closeActionCount >=
                meleeCountBeforeGuard;

            if (shouldGuard)
            {
                closeActionCount = 0;
            }

            bool shouldFakeGuard =
                shouldGuard &&
                fakeGuardPunishReady;

            LogActionSelection(
                distance,
                shouldFakeGuard
                    ? "FAKE GUARD PUNISH"
                    : shouldGuard
                    ? "GUARD THEN FORCED PROJECTILE"
                    : "MELEE"
            );

            StartAction(
                shouldFakeGuard
                    ? "FAKE_GUARD_PUNISH"
                    : shouldGuard
                    ? "GUARD_FORCED_PROJECTILE"
                    : "MELEE",
                shouldFakeGuard
                    ? FakeGuardPunishRoutine()
                    : shouldGuard
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
        PlayAttackVisual();

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

        PlayAttackVisual();

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
                ? "DEBUGGER: MELEE HIT"
                : "DEBUGGER: MELEE MISSED"
        );
    }

    // Presentation-only reuse of the established melee swing pose. Damage
    // remains owned exclusively by PerformMeleeAttack's overlap processing.
    private void PlayAttackVisual()
    {
        poseController?.PlayAttack();
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

        activeProjectile.SetVisualStyle(ProjectileVisualStyle.Debugger);
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

    private IEnumerator AdaptationAnalysisRoutine(
        PlayerPatternProfile profile)
    {
        adaptationAnalysisPending = false;

        switch (profile)
        {
            case PlayerPatternProfile.BackDashDependency:
                yield return BackDashAnalysisRoutine();
                yield break;

            case PlayerPatternProfile.SlashDependency:
                yield return SlashAnalysisRoutine();
                yield break;

            case PlayerPatternProfile.ForwardDashDependency:
                yield return ForwardDashAnalysisRoutine();
                yield break;

            default:
                FinishAction();
                yield break;
        }
    }

    private IEnumerator BackDashAnalysisRoutine()
    {
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
        ActivateAdaptationProfile(
            PlayerPatternProfile.BackDashDependency
        );

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

    private IEnumerator SlashAnalysisRoutine()
    {
        PrepareForAdaptationAnalysis();

        AppendAnalysisMessage(
            "PATTERN DETECTED: REPEATED MELEE"
        );

        yield return new WaitForSeconds(analysisLineDelay);

        AppendAnalysisMessage(
            "PROFILE: SLASH_DEPENDENCY"
        );

        yield return new WaitForSeconds(analysisLineDelay);

        AppendAnalysisMessage(
            "COUNTERMEASURE: ADAPTIVE_GUARD"
        );

        yield return new WaitForSeconds(analysisLineDelay);

        ActivateAdaptationProfile(
            PlayerPatternProfile.SlashDependency
        );
        adaptiveGuardReady = true;

        yield return new WaitForSeconds(analysisEndDelay);

        FinishAction();
    }

    private IEnumerator ForwardDashAnalysisRoutine()
    {
        PrepareForAdaptationAnalysis();

        AppendAnalysisMessage(
            "PATTERN DETECTED: GUARD RESPONSE"
        );

        yield return new WaitForSeconds(analysisLineDelay);

        AppendAnalysisMessage(
            "PROFILE: FORWARD_DASH_DEPENDENCY"
        );

        yield return new WaitForSeconds(analysisLineDelay);

        AppendAnalysisMessage(
            "COUNTERMEASURE: FAKE_GUARD_PUNISH"
        );

        yield return new WaitForSeconds(analysisLineDelay);

        ActivateAdaptationProfile(
            PlayerPatternProfile.ForwardDashDependency
        );
        fakeGuardPunishReady = true;

        yield return new WaitForSeconds(analysisEndDelay);

        FinishAction();
    }

    private IEnumerator FakeGuardPunishRoutine()
    {
        InvalidateProjectileAttackSignal();
        combatState.SetGuarding(true);
        SetColor(guardColor);
        HideAllTelegraphs();
        ShowGuardIndicator();

        Debug.Log("[DBG_FAKE_GUARD] bait_started");

        float guardEndTime = Time.time + guardDuration;
        bool dashForwardDetected = false;

        while (Time.time < guardEndTime)
        {
            if (patternTracker != null &&
                patternTracker.TryGetRecordedActionForActiveWindow(
                    CombatObservationContext.EnemyGuarding,
                    out HeroActionType action) &&
                action == HeroActionType.DashForward)
            {
                dashForwardDetected = true;
                break;
            }

            yield return null;
        }

        HideGuardIndicator();
        combatState.SetGuarding(false);
        RestoreNormalColor();

        if (dashForwardDetected)
        {
            Debug.Log("[DBG_FAKE_GUARD] dash_forward_detected");
            Debug.Log("[DBG_FAKE_GUARD] punish_committed");
            yield return MeleeAttackRoutine();
            yield break;
        }

        Debug.Log("[DBG_FAKE_GUARD] bait_expired");
        yield return RangedAttackRoutine(forced: true);
    }

    private void PrepareForAdaptationAnalysis()
    {
        if (combatState != null)
        {
            combatState.ResetState();
        }

        StopHorizontalMovement();
        HideAllTelegraphs();
        HideGuardIndicator();
        RestoreNormalColor();
    }

    private void ActivateAdaptationProfile(
        PlayerPatternProfile profile)
    {
        activeAdaptationProfile = profile;
        pendingAdaptationProfile = PlayerPatternProfile.None;

        Debug.Log(
            "[DBG_ADAPT] profile=" +
            activeAdaptationProfile
        );
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

            Vector2 hitPosition = hit.ClosestPoint(
                new Vector2(
                    predictiveDangerCenterX,
                    predictiveDangerCenterY
                )
            );

            health.TakeDamage(predictiveDamage);
            HitVfxManager.ReportConfirmedStrongHit(
                health,
                hitPosition
            );
            SpawnHitEffect(hitPosition);

            result = "HIT";
            break;
        }

        if (result == "HIT")
        {
            HandleFirstPredictiveHit();
        }
        else if (result == "MISS")
        {
            HandleFirstForwardCounterAvoided();
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
            poseController?.PlayWalk();
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

                float destinationX;
                if (advanceReason == AdvanceReason.ArenaRecovery)
                {
                    destinationX = homeX;
                }
                else
                {
                    // ADVANCE was the remaining offscreen-movement path: it
                    // could repeatedly target Hero's ever-increasing X with
                    // no home-range limit. Keep the existing short arena
                    // range as a gameplay safety boundary; camera follow
                    // supplies the visual scrolling once the action reaches
                    // its dead zone.
                    destinationX = Mathf.Clamp(
                        target.position.x,
                        homeX - homeLeashDistance,
                        homeX + homeLeashDistance
                    );
                }

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
            poseController?.StopWalk();
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
        // Each action is authoritative for its own visual. ADVANCE requests
        // Walk from inside its routine; dash/guard/attack paths must not
        // inherit locomotion from a preceding advance.
        poseController?.StopWalk();
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

        if (Time.time < nextRetreatAllowedAt)
        {
            LogRetreatBlocked("COOLDOWN");
            yield break;
        }

        if (!TryGetRetreatDestination(out float destinationX,
                                      out string blockedReason))
        {
            LogRetreatBlocked(blockedReason);
            yield break;
        }

        BeginCombatRetreatVisual(destinationX);

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

            float previousX = body.position.x;
            yield return new WaitForFixedUpdate();
            if (retreatParallaxActive)
            {
                infiniteParallaxBackground?.ScrollCombatDelta(
                    body.position.x - previousX
                );
            }
            elapsed += Time.fixedDeltaTime;
        }

        StopForcedRangedBackstep();
    }

    private bool TryGetRetreatDestination(
        out float destinationX,
        out string blockedReason)
    {
        destinationX = body != null ? body.position.x : transform.position.x;
        blockedReason = string.Empty;

        float currentDistance = GetHorizontalTargetDistance();
        float requestedDistance = Mathf.Min(
            forcedRangedBackstepDistance,
            Mathf.Max(0f, preferredCombatDistance - currentDistance)
        );

        if (requestedDistance < minimumRetreatDistance)
        {
            blockedReason = "PREFERRED_DISTANCE";
            return false;
        }

        float direction = GetBackstepDirection();
        float requestedX = destinationX + direction * requestedDistance;
        float minimumX = homeX - homeLeashDistance;
        float maximumX = homeX + homeLeashDistance;

        if (TryGetSafeViewportBounds(out float safeMinimumX,
                                     out float safeMaximumX))
        {
            minimumX = Mathf.Max(minimumX, safeMinimumX);
            maximumX = Mathf.Min(maximumX, safeMaximumX);
        }

        if (minimumX > maximumX)
        {
            blockedReason = "SAFE_BOUNDS_INVALID";
            return false;
        }

        destinationX = Mathf.Clamp(requestedX, minimumX, maximumX);

        if (Mathf.Abs(destinationX - body.position.x) <
            minimumRetreatDistance)
        {
            blockedReason = "EDGE";
            return false;
        }

        return true;
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

        if (!retreatVisualActive)
        {
            return;
        }

        retreatVisualActive = false;
        if (retreatParallaxActive)
        {
            infiniteParallaxBackground?.EndCombatRetreatScroll();
            retreatParallaxActive = false;
        }
        nextRetreatAllowedAt = Time.time + retreatCooldown;

        if (verboseTelegraphLogging)
        {
            Debug.Log(
                "DEBUGGER RETREAT_END\n" +
                $"currentX={transform.position.x:F2}\n" +
                $"nextAllowedAt={nextRetreatAllowedAt:F2}"
            );
        }
    }

    private void BeginCombatRetreatVisual(float destinationX)
    {
        retreatVisualActive = true;
        retreatParallaxActive =
            combatCameraFollow == null ||
            !combatCameraFollow.IsCombatFramingActive;

        if (retreatParallaxActive)
        {
            infiniteParallaxBackground?.BeginCombatRetreatScroll();
        }

        if (verboseTelegraphLogging)
        {
            Debug.Log(
                "DEBUGGER RETREAT_START\n" +
                $"currentX={transform.position.x:F2}\n" +
                $"destinationX={destinationX:F2}\n" +
                $"distance={GetHorizontalTargetDistance():F2}"
            );
        }
    }

    private bool TryGetSafeViewportBounds(
        out float minimumX,
        out float maximumX)
    {
        minimumX = 0f;
        maximumX = 0f;

        Camera camera = combatCamera != null
            ? combatCamera
            : Camera.main;
        if (camera == null)
        {
            return false;
        }

        float depth = Mathf.Abs(
            transform.position.z - camera.transform.position.z
        );
        float leftX = camera.ViewportToWorldPoint(
            new Vector3(safeViewportMinX, 0.5f, depth)
        ).x;
        float rightX = camera.ViewportToWorldPoint(
            new Vector3(safeViewportMaxX, 0.5f, depth)
        ).x;
        float visualHalfWidth = spriteRenderer != null
            ? spriteRenderer.bounds.extents.x
            : 0f;

        minimumX = Mathf.Min(leftX, rightX) + visualHalfWidth;
        maximumX = Mathf.Max(leftX, rightX) - visualHalfWidth;
        return minimumX <= maximumX;
    }

    private bool TryGetViewportX(out float viewportX)
    {
        Camera camera = combatCamera != null
            ? combatCamera
            : Camera.main;
        if (camera == null)
        {
            viewportX = 0f;
            return false;
        }

        viewportX = camera.WorldToViewportPoint(transform.position).x;
        return true;
    }

    private void LogRetreatBlocked(string reason)
    {
        if (verboseTelegraphLogging)
        {
            Debug.Log(
                "DEBUGGER RETREAT_BLOCKED_" + reason + "\n" +
                $"currentX={transform.position.x:F2}\n" +
                $"distance={GetHorizontalTargetDistance():F2}"
            );
        }
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
        guardVisual?.PlayGuard();
    }

    private void HideGuardIndicator()
    {
        if (guardVisual != null)
        {
            // This does not extend Debugger combatState.IsGuarding. It only
            // leaves the visual child alive for the Break one-shot.
            guardVisual.StopGuard();
            return;
        }

        if (guardIndicator != null)
        {
            guardIndicator.SetActive(false);
        }
    }

    private void OnEnable()
    {
        SubscribeToLivePatchEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromLivePatchEvents();
        ClearAdaptiveGuidance();
        CancelCurrentAction("DEBUGGER DISABLED");
    }

    private void OnDestroy()
    {
        UnsubscribeFromLivePatchEvents();

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
        ClearAdaptiveGuidance();

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
        adaptationAnalysisPending = false;
        pendingAdaptationProfile =
            PlayerPatternProfile.None;
        adaptiveGuardReady = false;
        fakeGuardPunishReady = false;
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

    private void UpdateAdaptationProfile()
    {
        if (patternTracker == null)
        {
            return;
        }

        PlayerPatternProfile detectedProfile =
            patternTracker.CurrentProfile;

        if (detectedProfile ==
            PlayerPatternProfile.None)
        {
            if (activeAdaptationProfile !=
                    PlayerPatternProfile.None ||
                pendingAdaptationProfile !=
                    PlayerPatternProfile.None)
            {
                DisableCountermeasures();
                activeAdaptationProfile =
                    PlayerPatternProfile.None;
                pendingAdaptationProfile =
                    PlayerPatternProfile.None;
                adaptationAnalysisPending = false;
            }

            return;
        }

        if (detectedProfile == activeAdaptationProfile ||
            detectedProfile == pendingAdaptationProfile)
        {
            return;
        }

        PlayerPatternProfile previousProfile =
            activeAdaptationProfile;

        DisableCountermeasures();
        activeAdaptationProfile =
            PlayerPatternProfile.None;
        pendingAdaptationProfile = detectedProfile;
        adaptationAnalysisPending = true;

        Debug.Log(
            "[DBG_ADAPT] " +
            previousProfile +
            " -> " +
            detectedProfile
        );
    }

    private void DisableCountermeasures()
    {
        backDashAdaptationEnabled = false;
        predictiveStrikeReady = false;
        adaptiveGuardReady = false;
        fakeGuardPunishReady = false;
    }

    private static string GetAnalysisActionName(
        PlayerPatternProfile profile)
    {
        return profile switch
        {
            PlayerPatternProfile.BackDashDependency =>
                "BACK_DASH_ANALYSIS",
            PlayerPatternProfile.SlashDependency =>
                "SLASH_ANALYSIS",
            PlayerPatternProfile.ForwardDashDependency =>
                "FORWARD_DASH_ANALYSIS",
            _ => "ADAPTATION_ANALYSIS"
        };
    }

    private void AppendAnalysisMessage(string message)
    {
        if (runtimeConsole != null)
        {
            runtimeConsole.AppendSystemMessage(message);
        }

        Debug.Log($"DEBUGGER ANALYSIS: {message}");
    }

    private void HandleFirstPredictiveHit()
    {
        if (firstPredictiveHitHandled ||
            runtime == null ||
            !runtime.IsRunning ||
            target == null ||
            !target.gameObject.activeInHierarchy)
        {
            return;
        }

        firstPredictiveHitHandled = true;
        livePatchPromptActive = true;

        AppendAnalysisMessage(
            "COUNTER CONFIRMED: DASH.BACK INTERCEPTED\n" +
            "YOUR CURRENT RESPONSE HAS BEEN PATCHED OUT\n" +
            "LIVE PATCH REQUIRED\n" +
            "PRESS [SPACE] TO MODIFY RUNNING CODE"
        );

        bool restoredPatch = livePatchController != null &&
                             livePatchController.EnsurePatchAvailable();

        if (restoredPatch)
        {
            AppendAnalysisMessage(
                "EMERGENCY LIVE PATCH TOKEN RESTORED"
            );
        }

        if (adaptivePatchHintUI != null)
        {
            adaptivePatchHintUI.ShowAdaptivePatchHint();
        }

        adaptiveHintGraceUntil =
            Time.time + adaptiveHintGraceDuration;
        adaptiveHintGraceExitLogged = false;

        int remainingPatches = livePatchController == null
            ? -1
            : livePatchController.RemainingPatches;

        Debug.Log(
            "DEBUGGER ADAPTIVE HIT: FIRST COUNTER CONFIRMED\n" +
            "action=DASH_BACK\n" +
            $"livePatchRemaining={remainingPatches}"
        );

        Debug.Log(
            "DEBUGGER ADAPTIVE HINT GRACE: ENTER " +
            $"duration={adaptiveHintGraceDuration:F2}"
        );
    }

    private void HandleFirstForwardCounterAvoided()
    {
        if (firstForwardCounterAvoided ||
            patternTracker == null ||
            !patternTracker.TryGetRecordedActionForActiveWindow(
                CombatObservationContext.EnemyAttacking,
                out HeroActionType action) ||
            action != HeroActionType.DashForward)
        {
            return;
        }

        firstForwardCounterAvoided = true;
        livePatchPromptActive = false;
        HideAdaptivePatchHint();

        AppendAnalysisMessage(
            "PATCH VERIFIED: FORWARD DASH BYPASSED THE COUNTER\n" +
            "ADAPTIVE ATTACK NEUTRALIZED"
        );

        Debug.Log(
            "DEBUGGER COUNTER BYPASSED\n" +
            "response=DASH_FORWARD\n" +
            "result=MISS"
        );
    }

    private bool IsAdaptiveHintGraceActive()
    {
        if (adaptiveHintGraceUntil <= 0f)
            return false;

        if (Time.time < adaptiveHintGraceUntil)
            return true;

        adaptiveHintGraceUntil = 0f;

        if (!adaptiveHintGraceExitLogged)
        {
            adaptiveHintGraceExitLogged = true;
            Debug.Log("DEBUGGER ADAPTIVE HINT GRACE: EXIT");
        }

        return false;
    }

    private void HandleLivePatchModeEntered()
    {
        if (!firstPredictiveHitHandled ||
            !livePatchPromptActive)
        {
            return;
        }

        livePatchPromptActive = false;
        HideAdaptivePatchHint();
    }

    private void HandleLivePatchCompileFinished(bool succeeded)
    {
        if (!firstPredictiveHitHandled)
            return;

        if (succeeded)
        {
            livePatchPromptActive = false;
            HideAdaptivePatchHint();
            AppendAnalysisMessage(
                "LIVE PATCH APPLIED\n" +
                "NEW RESPONSE WILL EXECUTE FROM THE NEXT ATTACK WINDOW"
            );
            return;
        }

        livePatchPromptActive = true;

        if (adaptivePatchHintUI != null)
        {
            adaptivePatchHintUI.ShowAdaptivePatchHint(
                "FIX CODE AND COMPILE — LIVE PATCH ACTIVE"
            );
        }

        AppendAnalysisMessage(
            "PATCH FAILED — FIX THE CODE AND COMPILE AGAIN"
        );
    }

    private void HideAdaptivePatchHint()
    {
        if (adaptivePatchHintUI != null)
        {
            adaptivePatchHintUI.HideAdaptivePatchHint();
        }
    }

    private void ClearAdaptiveGuidance()
    {
        livePatchPromptActive = false;

        if (adaptiveHintGraceUntil > 0f &&
            !adaptiveHintGraceExitLogged)
        {
            adaptiveHintGraceExitLogged = true;
            Debug.Log("DEBUGGER ADAPTIVE HINT GRACE: EXIT");
        }

        adaptiveHintGraceUntil = 0f;
        HideAdaptivePatchHint();
    }

    private void SubscribeToLivePatchEvents()
    {
        if (livePatchEventsSubscribed ||
            livePatchController == null)
        {
            return;
        }

        livePatchController.LivePatchModeEntered +=
            HandleLivePatchModeEntered;
        livePatchController.LivePatchCompileFinished +=
            HandleLivePatchCompileFinished;
        livePatchEventsSubscribed = true;
    }

    private void UnsubscribeFromLivePatchEvents()
    {
        if (!livePatchEventsSubscribed ||
            livePatchController == null)
        {
            return;
        }

        livePatchController.LivePatchModeEntered -=
            HandleLivePatchModeEntered;
        livePatchController.LivePatchCompileFinished -=
            HandleLivePatchCompileFinished;
        livePatchEventsSubscribed = false;
    }

    private bool NeedsArenaRecovery()
    {
        // Normal retreat is constrained before it can leave the safe camera
        // region. Keep the old home-leash recovery only as a no-camera
        // fallback; with a combat camera this path is emergency-only.
        if (TryGetViewportX(out float viewportX))
        {
            return viewportX < -emergencyViewportMargin ||
                   viewportX > 1f + emergencyViewportMargin;
        }

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
