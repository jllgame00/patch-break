using System.Collections;
using UnityEngine;

public sealed class StageBattleSequenceController : MonoBehaviour
{
    public enum SequenceState
    {
        HeroEntering,
        Briefing,
        EnemyEntering,
        WaitingForCompile,
        Combat,
        VictoryDelay,
        HeroExiting,
        Transitioning
    }

    [Header("Existing Battle References")]
    [SerializeField] private Transform hero;
    [SerializeField] private Transform enemy;
    [SerializeField] private MonoBehaviour enemyAiController;
    [SerializeField] private BattleBriefingController briefingController;
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private RuntimeConsoleUI runtimeConsoleUI;
    [SerializeField] private ProgramRuntime programRuntime;

    [Header("Optional Stage Travel Background Scroll")]
    [SerializeField] private InfiniteParallaxBackground infiniteParallaxBackground;

    [Header("Stage Sequence Points")]
    [SerializeField] private Transform heroEntranceStart;
    [SerializeField] private Transform heroBattlePosition;
    [SerializeField] private Transform heroExitPoint;
    [SerializeField] private Transform enemyEntranceStart;
    [SerializeField] private Transform enemyBattlePosition;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float heroEntranceSpeed = 5.5f;
    [SerializeField, Min(0.1f)] private float enemyEntranceSpeed = 2.4f;
    [SerializeField, Min(0.1f)] private float heroExitSpeed = 6f;
    [SerializeField, Min(0f)] private float victoryExitDelay = 0.45f;

    [Header("Optional Animator Hooks")]
    [SerializeField] private Animator heroAnimator;
    [SerializeField] private Animator enemyAnimator;
    [SerializeField] private string moveBoolParameter = "IsMoving";

    private HeroController heroController;
    private HeroManualInput heroManualInput;
    private CharacterPoseController heroPoseController;
    private CharacterPoseController enemyPoseController;
    private SpriteRenderer heroRenderer;
    private Rigidbody2D heroBody;
    private Rigidbody2D enemyBody;
    private Coroutine activeSequence;
    private bool victoryExitRequested;
    private bool heroSimulationWasEnabled;
    private bool enemySimulationWasEnabled;
    private bool sequencePhysicsSuspended;

    public SequenceState State { get; private set; } =
        SequenceState.HeroEntering;

    private void Awake()
    {
        if (!HasRequiredReferences())
        {
            Debug.LogError(
                "StageBattleSequenceController: Required stage sequence " +
                "reference is missing.",
                this
            );
            enabled = false;
            return;
        }

        heroController = hero.GetComponent<HeroController>();
        heroManualInput = hero.GetComponent<HeroManualInput>();
        heroPoseController = hero.GetComponent<CharacterPoseController>();
        enemyPoseController = enemy.GetComponent<CharacterPoseController>();
        heroRenderer = hero.GetComponent<SpriteRenderer>();
        heroBody = hero.GetComponent<Rigidbody2D>();
        enemyBody = enemy.GetComponent<Rigidbody2D>();

        briefingController.BriefingFinished += HandleBriefingFinished;
        battleManager.VictoryConfirmed += HandleVictoryConfirmed;
    }

    private void Start()
    {
        PrepareEntranceState();
        activeSequence = StartCoroutine(RunHeroEntrance());
    }

    private void Update()
    {
        if (State != SequenceState.WaitingForCompile ||
            programRuntime == null ||
            !programRuntime.IsRunning)
        {
            return;
        }

        SetEnemyAiActive(true);
        State = SequenceState.Combat;
    }

    private void OnDestroy()
    {
        if (briefingController != null)
        {
            briefingController.BriefingFinished -= HandleBriefingFinished;
        }

        if (battleManager != null)
        {
            battleManager.VictoryConfirmed -= HandleVictoryConfirmed;
        }
    }

    private bool HasRequiredReferences()
    {
        return hero != null &&
               enemy != null &&
               enemyAiController != null &&
               briefingController != null &&
               battleManager != null &&
               runtimeConsoleUI != null &&
               programRuntime != null &&
               heroEntranceStart != null &&
               heroBattlePosition != null &&
               heroExitPoint != null &&
               enemyEntranceStart != null &&
               enemyBattlePosition != null;
    }

    private void PrepareEntranceState()
    {
        SetStageTravelPoses();
        runtimeConsoleUI.SetEditorInputLocked(true);
        SuspendSequencePhysics();
        SetHeroControlActive(false);
        SetEnemyAiActive(false);

        PlaceActor(hero, heroEntranceStart);
        PlaceActor(enemy, enemyEntranceStart);
        FaceHero(1f);
        FaceEnemy(-1f);
    }

    private IEnumerator RunHeroEntrance()
    {
        State = SequenceState.HeroEntering;

        // ProgramRuntime.Start() performs its one-time StopProgram cleanup.
        // Let that lifecycle callback complete before this scripted travel
        // becomes the owner of Hero movement and its Walk pose.
        yield return null;

        heroPoseController?.SetBasePose();
        BeginHeroBackgroundScroll();
        yield return MoveActor(
            hero,
            heroBattlePosition,
            heroEntranceSpeed,
            1f,
            true,
            false
        );

        EndHeroBackgroundScroll();
        // Travel ends in the existing Base pose until Briefing completes.
        heroPoseController?.SetBasePose();
        State = SequenceState.Briefing;
        briefingController.BeginBriefing();
        activeSequence = null;
    }

    private void HandleBriefingFinished()
    {
        if (State != SequenceState.Briefing ||
            activeSequence != null)
        {
            return;
        }

        activeSequence = StartCoroutine(RunEnemyEntrance());
    }

    private IEnumerator RunEnemyEntrance()
    {
        State = SequenceState.EnemyEntering;

        // The enemy owns the world-space entrance movement. Hero remains at
        // the established battle position, but shares the travel visual while
        // the background scrolls so the encounter reads as forward motion.
        heroPoseController?.PlayWalk();
        enemyPoseController?.SetBasePose();
        BeginEncounterBackgroundScroll();
        yield return MoveActor(
            enemy,
            enemyBattlePosition,
            enemyEntranceSpeed,
            -1f,
            false,
            true
        );
        EndEncounterBackgroundScroll();

        SetCombatReadyPoses();

        SetHeroControlActive(true);
        RestoreSequencePhysics();
        runtimeConsoleUI.SetEditorInputLocked(false);
        State = SequenceState.WaitingForCompile;
        activeSequence = null;
    }

    private void HandleVictoryConfirmed()
    {
        if (victoryExitRequested ||
            State == SequenceState.Transitioning)
        {
            return;
        }

        victoryExitRequested = true;
        heroPoseController?.SetBasePose();
        activeSequence = StartCoroutine(RunVictoryExit());
    }

    private IEnumerator RunVictoryExit()
    {
        State = SequenceState.VictoryDelay;
        LogBackgroundCoverage("AfterVictoryConfirmed");
        runtimeConsoleUI.SetEditorInputLocked(true);
        SuspendSequencePhysics();
        SetHeroControlActive(false);
        SetEnemyAiActive(false);
        LogBackgroundCoverage("AfterEnemyAiDisabled");

        if (victoryExitDelay > 0f)
        {
            yield return new WaitForSeconds(victoryExitDelay);
        }

        LogBackgroundCoverage("BeforeHeroExit");
        State = SequenceState.HeroExiting;
        heroPoseController?.SetBasePose();
        BeginHeroBackgroundScroll();
        yield return MoveActorToPosition(
            hero,
            ResolveVictoryHeroExitTarget(),
            heroExitSpeed,
            1f,
            true,
            false
        );

        EndHeroBackgroundScroll();
        LogBackgroundCoverage("AfterHeroExit");
        State = SequenceState.Transitioning;
        LogBackgroundCoverage("BeforeEndingLoad");
        battleManager.CompleteVictoryTransition();
        activeSequence = null;
    }

    private Vector3 ResolveVictoryHeroExitTarget()
    {
        Vector3 currentHeroPosition = hero.position;
        Vector3 markerPosition = heroExitPoint.position;
        float serializedExitX = markerPosition.x;
        float resolvedExitX = serializedExitX;
        float cameraX = 0f;
        float cameraRightX = float.NegativeInfinity;
        float spriteHalfWidth = heroRenderer != null
            ? heroRenderer.bounds.extents.x
            : 0f;
        const float OffscreenMargin = 0.25f;

        Camera camera = Camera.main;
        if (camera != null &&
            camera.enabled &&
            camera.gameObject.activeInHierarchy)
        {
            cameraX = camera.transform.position.x;
            float depth = Mathf.Abs(
                currentHeroPosition.z - camera.transform.position.z
            );
            cameraRightX = camera.ViewportToWorldPoint(
                new Vector3(1f, 0.5f, depth)
            ).x;
            float cameraOffscreenExitX =
                cameraRightX + spriteHalfWidth + OffscreenMargin;
            resolvedExitX = Mathf.Max(
                serializedExitX,
                cameraOffscreenExitX
            );
        }

        if (resolvedExitX <= currentHeroPosition.x)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(
                "HERO_EXIT_TARGET_BEHIND " +
                $"HeroX={currentHeroPosition.x:F3} " +
                $"SerializedExitX={serializedExitX:F3} " +
                $"ResolvedExitX={resolvedExitX:F3} " +
                $"CameraX={cameraX:F3}",
                this
            );
#endif
            resolvedExitX = currentHeroPosition.x +
                            Mathf.Max(
                                0.1f,
                                spriteHalfWidth * 2f + OffscreenMargin
                            );
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            "HERO_EXIT_TARGET " +
            $"HeroX={currentHeroPosition.x:F3} " +
            $"SerializedExitX={serializedExitX:F3} " +
            $"ResolvedExitX={resolvedExitX:F3} " +
            $"CameraX={cameraX:F3} " +
            $"CameraRightX={cameraRightX:F3} " +
            $"SpriteHalfWidth={spriteHalfWidth:F3}",
            this
        );
#endif

        // Preserve the settled gameplay baseline. Camera combat framing only
        // changes horizontal presentation, so Y/Z must not come from a
        // potentially stale fixed-world exit marker.
        return new Vector3(
            resolvedExitX,
            currentHeroPosition.y,
            currentHeroPosition.z
        );
    }

    private IEnumerator MoveActor(
        Transform actor,
        Transform destination,
        float speed,
        float facingDirection,
        bool isHero,
        bool scrollEncounterTravel)
    {
        yield return MoveActorToPosition(
            actor,
            destination.position,
            speed,
            facingDirection,
            isHero,
            scrollEncounterTravel
        );
    }

    private IEnumerator MoveActorToPosition(
        Transform actor,
        Vector3 destinationPosition,
        float speed,
        float facingDirection,
        bool isHero,
        bool scrollEncounterTravel)
    {
        SetMovingAnimation(isHero, true);
        SetStageTravelWalk(isHero, true);
        bool isVictoryHeroExit =
            isHero && State == SequenceState.HeroExiting;
        bool loggedHeroExitFirstFrame = false;
        bool loggedHeroExitMiddleFrame = false;
        float heroExitStartDistance = isVictoryHeroExit
            ? Vector3.Distance(actor.position, destinationPosition)
            : 0f;

        if (isHero)
        {
            FaceHero(facingDirection);
        }
        else
        {
            FaceEnemy(facingDirection);
        }

        while ((actor.position - destinationPosition).sqrMagnitude >
               0.0001f)
        {
            FreezeActorBody(actor);
            float previousActorX = actor.position.x;
            actor.position = Vector3.MoveTowards(
                actor.position,
                destinationPosition,
                speed * Time.deltaTime
            );

            if (isHero)
            {
                infiniteParallaxBackground?.SyncToHeroPosition();

                if (isVictoryHeroExit)
                {
                    float remainingDistance = Vector3.Distance(
                        actor.position,
                        destinationPosition
                    );
                    float progress = heroExitStartDistance <= Mathf.Epsilon
                        ? 1f
                        : 1f - remainingDistance / heroExitStartDistance;
                    if (!loggedHeroExitFirstFrame)
                    {
                        loggedHeroExitFirstFrame = true;
                        LogBackgroundCoverage(
                            "HeroExitFrame sample=first"
                        );
                    }
                    else if (!loggedHeroExitMiddleFrame &&
                             progress >= 0.5f)
                    {
                        loggedHeroExitMiddleFrame = true;
                        LogBackgroundCoverage(
                            "HeroExitFrame sample=middle"
                        );
                    }
                }
            }
            else if (scrollEncounterTravel)
            {
                ScrollEncounterTravel(actor.position.x - previousActorX);
            }

            yield return null;
        }

        float previousActorXAtArrival = actor.position.x;
        PlaceActor(actor, destinationPosition);

        if (isHero)
        {
            infiniteParallaxBackground?.SyncToHeroPosition();
            if (isVictoryHeroExit)
            {
                LogBackgroundCoverage("HeroExitFrame sample=final");
            }
        }
        else if (scrollEncounterTravel)
        {
            ScrollEncounterTravel(actor.position.x - previousActorXAtArrival);
        }

        SetMovingAnimation(isHero, false);
        SetStageTravelWalk(isHero, false);
    }

    private void BeginHeroBackgroundScroll()
    {
        if (infiniteParallaxBackground != null)
        {
            infiniteParallaxBackground.BeginHeroScroll();
        }
    }

    private void EndHeroBackgroundScroll()
    {
        if (infiniteParallaxBackground != null)
        {
            infiniteParallaxBackground.EndHeroScroll();
        }
    }

    private void LogBackgroundCoverage(string phase)
    {
        infiniteParallaxBackground?.LogCameraCoveragePhase(phase);
    }

    private void BeginEncounterBackgroundScroll()
    {
        infiniteParallaxBackground?.BeginTravelScroll();
    }

    private void EndEncounterBackgroundScroll()
    {
        infiniteParallaxBackground?.EndTravelScroll();
    }

    private void ScrollEncounterTravel(float enemyDeltaX)
    {
        infiniteParallaxBackground?.ScrollTravelDelta(
            Mathf.Abs(enemyDeltaX)
        );
    }

    private void PlaceActor(Transform actor, Transform marker)
    {
        PlaceActor(actor, marker.position);
    }

    private void PlaceActor(Transform actor, Vector3 position)
    {
        actor.position = position;
        FreezeActorBody(actor);
    }

    private void FreezeActorBody(Transform actor)
    {
        Rigidbody2D body = actor == hero
            ? heroBody
            : enemyBody;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    private void SuspendSequencePhysics()
    {
        if (sequencePhysicsSuspended)
        {
            return;
        }

        heroSimulationWasEnabled = heroBody != null && heroBody.simulated;
        enemySimulationWasEnabled = enemyBody != null && enemyBody.simulated;

        if (heroBody != null)
        {
            heroBody.simulated = false;
        }

        if (enemyBody != null)
        {
            enemyBody.simulated = false;
        }

        sequencePhysicsSuspended = true;
    }

    private void RestoreSequencePhysics()
    {
        if (!sequencePhysicsSuspended)
        {
            return;
        }

        if (heroBody != null)
        {
            heroBody.simulated = heroSimulationWasEnabled;
        }

        if (enemyBody != null)
        {
            enemyBody.simulated = enemySimulationWasEnabled;
        }

        sequencePhysicsSuspended = false;
    }

    private void SetStageTravelPoses()
    {
        heroPoseController?.SetBasePose();
        enemyPoseController?.SetBasePose();
    }

    private void SetCombatReadyPoses()
    {
        heroPoseController?.SetReadyPose();
        enemyPoseController?.SetReadyPose();
    }

    private void SetHeroControlActive(bool active)
    {
        if (heroController == null)
        {
            return;
        }

        if (!active)
        {
            heroController.StopAllMovement();
        }

        heroController.enabled = active;

        // Stage travel owns the Hero's movement and pose, so suppress manual
        // zero-input updates while it is active. ProgramRuntime owns the
        // normal scripted-combat policy and disables ManualInput at startup;
        // do not re-enable it here when stage travel ends.
        if (!active && heroManualInput != null)
        {
            heroManualInput.enabled = false;
        }
        FreezeActorBody(hero);
    }

    private void SetEnemyAiActive(bool active)
    {
        if (enemyAiController != null)
        {
            enemyAiController.enabled = active;
        }

        FreezeActorBody(enemy);
    }

    private void FaceHero(float direction)
    {
        if (heroController != null)
        {
            heroController.FaceDirection(direction);
        }
    }

    private void FaceEnemy(float direction)
    {
        Vector3 scale = enemy.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction);
        enemy.localScale = scale;
    }

    private void SetMovingAnimation(bool isHero, bool isMoving)
    {
        Animator animator = isHero ? heroAnimator : enemyAnimator;

        if (animator == null ||
            string.IsNullOrWhiteSpace(moveBoolParameter) ||
            !HasBoolParameter(animator, moveBoolParameter))
        {
            return;
        }

        animator.SetBool(moveBoolParameter, isMoving);
    }

    private void SetStageTravelWalk(bool isHero, bool isMoving)
    {
        CharacterPoseController pose = isHero
            ? heroPoseController
            : enemyPoseController;

        if (pose == null)
        {
            return;
        }

        if (isMoving)
        {
            pose.PlayWalk();
        }
        else
        {
            pose.StopWalk();
        }
    }

    private static bool HasBoolParameter(Animator animator, string parameter)
    {
        foreach (AnimatorControllerParameter animatorParameter in
                 animator.parameters)
        {
            if (animatorParameter.type ==
                    AnimatorControllerParameterType.Bool &&
                animatorParameter.name == parameter)
            {
                return true;
            }
        }

        return false;
    }
}
