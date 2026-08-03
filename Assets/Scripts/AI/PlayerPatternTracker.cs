using System.Collections.Generic;
using UnityEngine;

public enum CombatObservationContext
{
    None,
    EnemyAttacking,
    EnemyGuarding
}

public enum PlayerPatternProfile
{
    None,
    BackDashDependency,
    ForwardDashDependency,
    GuardAttackDependency
}

public sealed class PlayerPatternTracker : MonoBehaviour
{
    [Header("History")]
    [SerializeField, Min(3)]
    private int attackHistoryCapacity = 5;

    [SerializeField, Min(1)]
    private int guardHistoryCapacity = 4;

    [Header("Detection")]
    [SerializeField, Min(1)]
    private int backDashDetectionCount = 3;

    [Header("Diagnostics")]
    [SerializeField]
    private bool verbosePatternLogging;

    private readonly List<HeroActionType> attackReactions =
        new();

    private readonly List<HeroActionType> guardReactions =
        new();

    private bool previousAttacking;
    private bool previousGuarding;
    private int attackWindowId;
    private int guardWindowId;
    private int recordedAttackWindowId = -1;
    private int recordedGuardWindowId = -1;

    public PlayerPatternProfile CurrentProfile { get; private set; }

    public void ObserveEnemyState(EnemyCombatState targetState)
    {
        bool isAttacking =
            targetState != null &&
            targetState.IsAttacking;

        bool isGuarding =
            targetState != null &&
            targetState.IsGuarding;

        if (isAttacking && !previousAttacking)
        {
            attackWindowId++;
            LogWindowOpened(
                CombatObservationContext.EnemyAttacking,
                attackWindowId
            );
        }
        else if (!isAttacking && previousAttacking)
        {
            LogWindowClosed(
                CombatObservationContext.EnemyAttacking,
                attackWindowId
            );
        }

        if (isGuarding && !previousGuarding)
        {
            guardWindowId++;
            LogWindowOpened(
                CombatObservationContext.EnemyGuarding,
                guardWindowId
            );
        }
        else if (!isGuarding && previousGuarding)
        {
            LogWindowClosed(
                CombatObservationContext.EnemyGuarding,
                guardWindowId
            );
        }

        previousAttacking = isAttacking;
        previousGuarding = isGuarding;
    }

    public void RecordSuccessfulAction(
        HeroActionType action,
        EnemyCombatState targetState)
    {
        if (action == HeroActionType.None)
            return;

        ObserveEnemyState(targetState);

        CombatObservationContext context =
            GetCurrentContext(targetState);

        switch (context)
        {
            case CombatObservationContext.EnemyGuarding:
                RecordGuardReaction(action);
                break;

            case CombatObservationContext.EnemyAttacking:
                RecordAttackReaction(action);
                break;
        }
    }

    public void ResetTracking()
    {
        attackReactions.Clear();
        guardReactions.Clear();

        previousAttacking = false;
        previousGuarding = false;
        attackWindowId = 0;
        guardWindowId = 0;
        recordedAttackWindowId = -1;
        recordedGuardWindowId = -1;

        CurrentProfile = PlayerPatternProfile.None;
    }

    private CombatObservationContext GetCurrentContext(
        EnemyCombatState targetState)
    {
        if (targetState != null &&
            targetState.IsGuarding)
        {
            return CombatObservationContext.EnemyGuarding;
        }

        if (targetState != null &&
            targetState.IsAttacking)
        {
            return CombatObservationContext.EnemyAttacking;
        }

        return CombatObservationContext.None;
    }

    private void RecordAttackReaction(
        HeroActionType action)
    {
        if (attackWindowId <= 0)
            return;

        if (recordedAttackWindowId == attackWindowId)
        {
            LogDuplicateResponse(attackWindowId);
            return;
        }

        recordedAttackWindowId = attackWindowId;
        AddReaction(
            attackReactions,
            action,
            attackHistoryCapacity
        );

        LogRecordedReaction(
            CombatObservationContext.EnemyAttacking,
            action,
            attackWindowId
        );

        DetectBackDashDependency();
    }

    private void RecordGuardReaction(
        HeroActionType action)
    {
        if (guardWindowId <= 0)
            return;

        if (recordedGuardWindowId == guardWindowId)
        {
            LogDuplicateResponse(guardWindowId);
            return;
        }

        recordedGuardWindowId = guardWindowId;
        AddReaction(
            guardReactions,
            action,
            guardHistoryCapacity
        );

        LogRecordedReaction(
            CombatObservationContext.EnemyGuarding,
            action,
            guardWindowId
        );
    }

    private void AddReaction(
        List<HeroActionType> history,
        HeroActionType action,
        int capacity)
    {
        int safeCapacity = Mathf.Max(1, capacity);

        while (history.Count >= safeCapacity)
        {
            history.RemoveAt(0);
        }

        history.Add(action);
    }

    private void DetectBackDashDependency()
    {
        if (CurrentProfile != PlayerPatternProfile.None)
            return;

        if (attackReactions.Count <
            backDashDetectionCount)
        {
            return;
        }

        int firstRecentIndex =
            attackReactions.Count -
            backDashDetectionCount;

        for (int index = firstRecentIndex;
             index < attackReactions.Count;
             index++)
        {
            if (attackReactions[index] !=
                HeroActionType.DashBack)
            {
                return;
            }
        }

        CurrentProfile =
            PlayerPatternProfile.BackDashDependency;

        Debug.Log(
            "PATTERN DETECTED: " +
            "BACK_DASH_DEPENDENCY\n" +
            "ATTACK WINDOWS ANALYZED: " +
            $"{backDashDetectionCount}\n" +
            "DASH_BACK REACTIONS: " +
            backDashDetectionCount
        );
    }

    private void LogWindowOpened(
        CombatObservationContext context,
        int windowId)
    {
        if (!verbosePatternLogging)
            return;

        Debug.Log(
            "PATTERN TRACKER: " +
            $"{GetContextLabel(context)} " +
            $"WINDOW OPENED id={windowId}"
        );
    }

    private void LogWindowClosed(
        CombatObservationContext context,
        int windowId)
    {
        if (!verbosePatternLogging)
            return;

        Debug.Log(
            "PATTERN TRACKER: " +
            $"{GetContextLabel(context)} " +
            $"WINDOW CLOSED id={windowId}"
        );
    }

    private void LogRecordedReaction(
        CombatObservationContext context,
        HeroActionType action,
        int windowId)
    {
        if (!verbosePatternLogging)
            return;

        Debug.Log(
            "PATTERN TRACKER: RECORDED " +
            $"context={GetContextLabel(context)} " +
            $"action={GetActionLabel(action)} " +
            $"window={windowId}"
        );
    }

    private void LogDuplicateResponse(int windowId)
    {
        if (!verbosePatternLogging)
            return;

        Debug.Log(
            "PATTERN TRACKER: " +
            $"DUPLICATE RESPONSE IGNORED window={windowId}"
        );
    }

    private string GetContextLabel(
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

    private string GetActionLabel(
        HeroActionType action)
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
}
