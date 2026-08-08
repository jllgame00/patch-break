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
        activeSequence = StartCoroutine(RunVictoryExit());
    }

    private IEnumerator RunVictoryExit()
    {
        State = SequenceState.VictoryDelay;
        runtimeConsoleUI.SetEditorInputLocked(true);
        SuspendSequencePhysics();
        SetHeroControlActive(false);
        SetEnemyAiActive(false);

        if (victoryExitDelay > 0f)
        {
            yield return new WaitForSeconds(victoryExitDelay);
        }

        State = SequenceState.HeroExiting;
        BeginHeroBackgroundScroll();
        yield return MoveActor(
            hero,
            heroExitPoint,
            heroExitSpeed,
            1f,
            true,
            false
        );

        EndHeroBackgroundScroll();
        State = SequenceState.Transitioning;
        battleManager.CompleteVictoryTransition();
        activeSequence = null;
    }

    private IEnumerator MoveActor(
        Transform actor,
        Transform destination,
        float speed,
        float facingDirection,
        bool isHero,
        bool scrollEncounterTravel)
    {
        SetMovingAnimation(isHero, true);

        if (isHero)
        {
            FaceHero(facingDirection);
        }
        else
        {
            FaceEnemy(facingDirection);
        }

        while ((actor.position - destination.position).sqrMagnitude >
               0.0001f)
        {
            FreezeActorBody(actor);
            float previousActorX = actor.position.x;
            actor.position = Vector3.MoveTowards(
                actor.position,
                destination.position,
                speed * Time.deltaTime
            );

            if (isHero)
            {
                infiniteParallaxBackground?.SyncToHeroPosition();
            }
            else if (scrollEncounterTravel)
            {
                ScrollEncounterTravel(actor.position.x - previousActorX);
            }

            yield return null;
        }

        float previousActorXAtArrival = actor.position.x;
        PlaceActor(actor, destination);

        if (isHero)
        {
            infiniteParallaxBackground?.SyncToHeroPosition();
        }
        else if (scrollEncounterTravel)
        {
            ScrollEncounterTravel(actor.position.x - previousActorXAtArrival);
        }

        SetMovingAnimation(isHero, false);
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
        actor.position = marker.position;
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
