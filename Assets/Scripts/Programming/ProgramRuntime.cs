using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HeroActionExecutor))]
public sealed class ProgramRuntime : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HeroActionExecutor executor;
    [SerializeField] private Transform enemy;
    [SerializeField] private EnemyCombatState enemyState;

    [Header("Runtime")]
    [SerializeField, Min(0.02f)]
    private float evaluationInterval = 0.1f;

    [SerializeField, Min(0.1f)]
    private float enemyNearDistance = 2.0f;

    [SerializeField]
    private bool disableManualInputOnStart = true;

    private readonly List<BattleRule> compiledRules = new();

    private float evaluationTimer;

    public bool IsRunning { get; private set; }
    public string LastCompileMessage { get; private set; } =
        "READY. Enter program and compile.";

    private void Awake()
    {
        if (executor == null)
        {
            executor = GetComponent<HeroActionExecutor>();
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

        Debug.LogError(message);

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
            }

            // 첫 번째로 조건이 참인 규칙이
            // 이번 평가 주기를 차지한다.
            return;
        }
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