using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HeroActionExecutor))]
public sealed class ProgramRuntime : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HeroActionExecutor executor;
    [SerializeField] private Transform enemy;

    [Header("Program")]
    [SerializeField] private List<string> sourceLines = new()
    {
        "if enemy.near => slash"
    };

    [Header("Runtime")]
    [SerializeField, Min(0.02f)]
    private float evaluationInterval = 0.1f;

    [SerializeField, Min(0.1f)]
    private float enemyNearDistance = 2.2f;

    [SerializeField]
    private bool disableManualInputOnStart = true;

    private readonly List<BattleRule> compiledRules = new();

    private float evaluationTimer;
    private bool compileSucceeded;

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

        compileSucceeded = CompileProgram();

        if (compileSucceeded)
        {
            Debug.Log(
                $"BUILD SUCCESS: {compiledRules.Count} rule(s) compiled."
            );
        }
    }

    private void Update()
    {
        if (!compileSucceeded)
            return;

        if (enemy == null || !enemy.gameObject.activeInHierarchy)
        {
            executor.StopMovement();
            return;
        }

        evaluationTimer -= Time.deltaTime;

        if (evaluationTimer > 0f)
            return;

        evaluationTimer = evaluationInterval;

        EvaluateProgram();
    }

    private bool CompileProgram()
    {
        compiledRules.Clear();

        if (sourceLines == null || sourceLines.Count == 0)
        {
            Debug.LogError("COMPILE ERROR: Program has no rules.");
            return false;
        }

        for (int index = 0; index < sourceLines.Count; index++)
        {
            string source = sourceLines[index];

            if (string.IsNullOrWhiteSpace(source))
                continue;

            if (!RuleParser.TryParse(
                    source,
                    out BattleRule rule,
                    out string error))
            {
                Debug.LogError(
                    $"COMPILE ERROR LINE {index + 1}: {error}\n" +
                    $"SOURCE: {source}"
                );

                return false;
            }

            compiledRules.Add(rule);

            Debug.Log(
                $"LINE {index + 1} VALID: {source}"
            );
        }

        return compiledRules.Count > 0;
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

            Debug.Log(
                $"EXECUTE: {rule.Source}"
            );

            // 위에서 아래로 읽고,
            // 실행 가능한 첫 번째 규칙만 실행한다.
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

        float distance = Vector2.Distance(
            transform.position,
            enemy.position
        );

        return distance <= enemyNearDistance;
    }
    
    private bool IsEnemyFar()
    {
        if (enemy == null)
            return false;

        float distance = Vector2.Distance(
            transform.position,
            enemy.position
        );

        return distance > enemyNearDistance;
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