using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HeroActionExecutor))]
public sealed class ProgramRuntime : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HeroActionExecutor executor;
    [SerializeField] private Transform enemy;

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
        StopProgram();
        compiledRules.Clear();

        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return CompileFailed(
                "COMPILE ERROR\nProgram is empty."
            );
        }

        string normalizedSource = sourceCode
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');

        string[] sourceLines = normalizedSource.Split('\n');

        for (int index = 0; index < sourceLines.Length; index++)
        {
            string source = sourceLines[index].Trim();

            // 빈 줄과 주석은 무시한다.
            if (string.IsNullOrWhiteSpace(source))
                continue;

            if (source.StartsWith("#") || source.StartsWith("//"))
                continue;

            if (!RuleParser.TryParse(
                    source,
                    out BattleRule rule,
                    out string error))
            {
                return CompileFailed(
                    $"COMPILE ERROR LINE {index + 1}\n" +
                    $"{error}\n" +
                    $"> {source}"
                );
            }

            compiledRules.Add(rule);
        }

        if (compiledRules.Count == 0)
        {
            return CompileFailed(
                "COMPILE ERROR\nNo executable rules found."
            );
        }

        evaluationTimer = 0f;
        IsRunning = true;

        LastCompileMessage =
            $"BUILD SUCCESS\n" +
            $"{compiledRules.Count} rule(s) compiled.\n" +
            "Executing HERO_RUNTIME.EXE";

        Debug.Log(LastCompileMessage);

        return true;
    }

    public void StopProgram()
    {
        IsRunning = false;

        if (executor != null)
        {
            executor.StopMovement();
        }
    }

    private bool CompileFailed(string message)
    {
        IsRunning = false;
        LastCompileMessage = message;

        Debug.LogError(message);

        return false;
    }

    private void EvaluateProgram()
    {
        foreach (BattleRule rule in compiledRules)
        {
            if (!CheckCondition(rule.Condition))
                continue;

            bool executed = executor.TryExecute(
                rule.Action,
                enemy
            );

            if (!executed)
                continue;

            Debug.Log($"EXECUTE: {rule.Source}");

            // 위에서부터 검사하고 첫 번째로 실행된 규칙에서 종료.
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