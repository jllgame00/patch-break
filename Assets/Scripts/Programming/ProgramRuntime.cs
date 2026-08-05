using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HeroActionExecutor))]
public sealed class ProgramRuntime : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HeroActionExecutor executor;
    [SerializeField] private Transform enemy;
    [SerializeField] private EnemyCombatState enemyState;
    [SerializeField] private PlayerPatternTracker patternTracker;

    [Header("Runtime")]
    [SerializeField, Min(0.02f)]
    private float evaluationInterval = 0.1f;

    [SerializeField, Min(0.1f)]
    private float enemyNearDistance = 2.0f;

    [SerializeField]
    private bool disableManualInputOnStart = true;

    [Header("Combat Window Gate")]
    [SerializeField]
    private bool verboseCombatWindowGate;

    private readonly List<BattleRule> compiledRules = new();

    private float evaluationTimer;
    private CombatObservationContext consumedWindowContext =
        CombatObservationContext.None;
    private int consumedWindowId = -1;
    private CombatObservationContext lastLoggedWindowContext =
        CombatObservationContext.None;
    private int lastLoggedWindowId = -1;
    private CombatObservationContext suppressedWindowContext =
        CombatObservationContext.None;
    private int suppressedWindowId = -1;

    public bool IsRunning { get; private set; }
    public Transform Target => enemy;
    public string LastCompileMessage { get; private set; } =
        "READY. Enter program and compile.";

    private void Awake()
    {
        if (executor == null)
        {
            executor = GetComponent<HeroActionExecutor>();
        }

        if (patternTracker == null)
        {
            patternTracker =
                GetComponent<PlayerPatternTracker>();
        }

        if (patternTracker == null)
        {
            Debug.LogWarning(
                "ProgramRuntime: Player Pattern Tracker " +
                "is not assigned. Tracking is disabled.",
                this
            );
        }
        else
        {
            patternTracker.TrackingReset +=
                ResetCombatWindowGate;
        }
    }

    private void OnDestroy()
    {
        if (patternTracker != null)
        {
            patternTracker.TrackingReset -=
                ResetCombatWindowGate;
        }
    }

    private void Start()
    {
        if (enemyState == null && enemy != null)
        {
            enemyState =
                enemy.GetComponent<EnemyCombatState>();
        }

        if (disableManualInputOnStart)
        {
            DisableManualInput();
        }

        StopProgram();
    }

    private void Update()
    {
        if (!IsRunning)
            return;

        if (enemy == null || !enemy.gameObject.activeInHierarchy)
        {
            StopProgram();
            return;
        }

        if (patternTracker != null)
        {
            patternTracker.ObserveEnemyState(
                enemyState
            );
        }

        evaluationTimer -= Time.deltaTime;

        if (evaluationTimer > 0f)
            return;

        evaluationTimer = evaluationInterval;
        EvaluateProgram();
    }

    public bool CompileAndRun(string sourceCode)
    {
        bool preserveCurrentProgram =
            IsRunning && compiledRules.Count > 0;

        List<BattleRule> candidateRules = new();

        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return CompileFailed(
                "COMPILE ERROR\nProgram is empty.",
                preserveCurrentProgram
            );
        }

        string normalizedSource = sourceCode
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');

        string[] sourceLines = normalizedSource.Split('\n');

        for (int index = 0; index < sourceLines.Length; index++)
        {
            string source = sourceLines[index].Trim();

            if (string.IsNullOrWhiteSpace(source))
                continue;

            if (source.StartsWith("#") ||
                source.StartsWith("//"))
            {
                continue;
            }

            if (!RuleParser.TryParse(
                    source,
                    out BattleRule rule,
                    out string error))
            {
                return CompileFailed(
                    $"COMPILE ERROR LINE {index + 1}\n" +
                    $"{error}\n" +
                    $"> {source}",
                    preserveCurrentProgram
                );
            }

            candidateRules.Add(rule);
        }

        if (candidateRules.Count == 0)
        {
            return CompileFailed(
                "COMPILE ERROR\nNo executable rules found.",
                preserveCurrentProgram
            );
        }

        compiledRules.Clear();
        compiledRules.AddRange(candidateRules);

        evaluationTimer = 0f;
        IsRunning = true;

        LastCompileMessage =
            $"BUILD SUCCESS\n" +
            $"{compiledRules.Count} rule(s) compiled.\n" +
            "Executing HERO_RUNTIME.EXE";

        Debug.Log(LastCompileMessage);

        return true;
    }

    private bool CompileFailed(
        string message,
        bool preserveCurrentProgram)
    {
        LastCompileMessage = message;

        if (!preserveCurrentProgram)
        {
            StopProgram();
        }

        // Player-authored DSL failures are recoverable gameplay feedback.
        // Keep them out of Unity's Error Pause path so Live Patch remains editable.
        Debug.LogWarning(message);

        return false;
    }

    public void StopProgram()
    {
        IsRunning = false;

        if (executor != null)
        {
            executor.StopMovement();
        }
    }
    
    private void EvaluateProgram()
    {
        if (TryGetActiveCombatWindow(
                out CombatObservationContext activeContext,
                out int activeWindowId))
        {
            LogNewCombatWindow(activeContext, activeWindowId);

            if (IsCombatWindowConsumed(
                    activeContext,
                    activeWindowId))
            {
                LogSuppressedAutoAction(
                    activeContext,
                    activeWindowId
                );
                return;
            }
        }

        foreach (BattleRule rule in compiledRules)
        {
            if (!CheckCondition(rule.Condition))
            {
                continue;
            }

            bool executed = executor.TryExecute(
                rule.Action,
                enemy
            );

            if (executed)
            {
                Debug.Log($"EXECUTE: {rule.Source}");

                if (patternTracker != null)
                {
                    if (TryGetActiveCombatWindow(
                            out CombatObservationContext context,
                            out int windowId))
                    {
                        ConsumeCombatWindow(
                            context,
                            windowId,
                            rule.Action
                        );
                    }

                    patternTracker.RecordSuccessfulAction(
                        rule.Action,
                        enemyState
                    );
                }
            }

            // 첫 번째로 조건이 참인 규칙이
            // 이번 평가 주기를 차지한다.
            return;
        }
    }

    private bool TryGetActiveCombatWindow(
        out CombatObservationContext context,
        out int windowId)
    {
        context = CombatObservationContext.None;
        windowId = -1;

        return patternTracker != null &&
               patternTracker.TryGetActiveWindow(
                   out context,
                   out windowId
               );
    }

    private bool IsCombatWindowConsumed(
        CombatObservationContext context,
        int windowId)
    {
        return consumedWindowContext == context &&
               consumedWindowId == windowId;
    }

    private void ConsumeCombatWindow(
        CombatObservationContext context,
        int windowId,
        HeroActionType action)
    {
        consumedWindowContext = context;
        consumedWindowId = windowId;

        if (!verboseCombatWindowGate)
            return;

        Debug.Log(
            "PROGRAM WINDOW GATE: CONSUMED\n" +
            $"context={GetCombatContextLabel(context)}\n" +
            $"window={windowId}\n" +
            $"action={GetActionLabel(action)}"
        );
    }

    private void LogNewCombatWindow(
        CombatObservationContext context,
        int windowId)
    {
        if (!verboseCombatWindowGate ||
            (lastLoggedWindowContext == context &&
             lastLoggedWindowId == windowId))
        {
            return;
        }

        lastLoggedWindowContext = context;
        lastLoggedWindowId = windowId;

        Debug.Log(
            "PROGRAM WINDOW GATE: NEW WINDOW\n" +
            $"context={GetCombatContextLabel(context)}\n" +
            $"window={windowId}"
        );
    }

    private void LogSuppressedAutoAction(
        CombatObservationContext context,
        int windowId)
    {
        if (!verboseCombatWindowGate ||
            (suppressedWindowContext == context &&
             suppressedWindowId == windowId))
        {
            return;
        }

        suppressedWindowContext = context;
        suppressedWindowId = windowId;

        Debug.Log(
            "PROGRAM WINDOW GATE: AUTO ACTION SUPPRESSED\n" +
            $"context={GetCombatContextLabel(context)}\n" +
            $"window={windowId}"
        );
    }

    private void ResetCombatWindowGate()
    {
        consumedWindowContext =
            CombatObservationContext.None;
        consumedWindowId = -1;
        lastLoggedWindowContext =
            CombatObservationContext.None;
        lastLoggedWindowId = -1;
        suppressedWindowContext =
            CombatObservationContext.None;
        suppressedWindowId = -1;
    }

    private string GetCombatContextLabel(
        CombatObservationContext context)
    {
        return context switch
        {
            CombatObservationContext.EnemyAttacking =>
                "ATTACK",
            CombatObservationContext.EnemyGuarding =>
                "GUARD",
            _ => "NONE"
        };
    }

    private string GetActionLabel(HeroActionType action)
    {
        return action switch
        {
            HeroActionType.DashBack => "DASH_BACK",
            HeroActionType.DashForward => "DASH_FORWARD",
            HeroActionType.Slash => "SLASH",
            HeroActionType.Approach => "APPROACH",
            _ => "NONE"
        };
    }

    private bool CheckCondition(ConditionType condition)
    {
        switch (condition)
        {
            case ConditionType.EnemyNear:
                return IsEnemyNear();

            case ConditionType.EnemyFar:
                return IsEnemyFar();

            case ConditionType.EnemyAttacking:
                return enemyState != null &&
                       enemyState.IsAttacking;
            
            case ConditionType.EnemyGuarding:
                return enemyState != null &&
                       enemyState.IsGuarding;

            default:
                return false;
        }
    }

    private bool IsEnemyNear()
    {
        if (enemy == null)
            return false;

        return GetEnemyDistance() <= enemyNearDistance;
    }

    private bool IsEnemyFar()
    {
        if (enemy == null)
            return false;

        return GetEnemyDistance() > enemyNearDistance;
    }

    private float GetEnemyDistance()
    {
        return Vector2.Distance(
            transform.position,
            enemy.position
        );
    }

    private void DisableManualInput()
    {
        HeroManualInput manualInput =
            GetComponent<HeroManualInput>();

        if (manualInput != null)
        {
            manualInput.enabled = false;
        }
    }
}
