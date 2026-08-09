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
    SlashDependency
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

    [SerializeField, Min(1)]
    private int slashDetectionCount = 3;

    [SerializeField, Min(1)]
    private int forwardDashDetectionCount = 3;

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
    private HeroActionType recordedAttackWindowAction =
        HeroActionType.None;
    private HeroActionType recordedGuardWindowAction =
        HeroActionType.None;

    public PlayerPatternProfile CurrentProfile { get; private set; }
    public event System.Action TrackingReset;

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
            recordedAttackWindowAction = HeroActionType.None;
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
            recordedGuardWindowAction = HeroActionType.None;
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

        if (!TryGetActiveWindow(
                out CombatObservationContext context,
                out _))
        {
            return;
        }

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
        recordedAttackWindowAction = HeroActionType.None;
        recordedGuardWindowAction = HeroActionType.None;

        CurrentProfile = PlayerPatternProfile.None;

        TrackingReset?.Invoke();
    }

    public bool TryGetActiveWindow(
        out CombatObservationContext context,
        out int windowId)
    {
        if (previousGuarding && guardWindowId > 0)
        {
            context = CombatObservationContext.EnemyGuarding;
            windowId = guardWindowId;
            return true;
        }

        if (previousAttacking && attackWindowId > 0)
        {
            context = CombatObservationContext.EnemyAttacking;
            windowId = attackWindowId;
            return true;
        }

        context = CombatObservationContext.None;
        windowId = -1;
        return false;
    }

    public bool TryGetRecordedActionForActiveWindow(
        CombatObservationContext expectedContext,
        out HeroActionType action)
    {
        action = HeroActionType.None;

        if (expectedContext ==
            CombatObservationContext.EnemyAttacking)
        {
            if (!previousAttacking ||
                attackWindowId <= 0 ||
                recordedAttackWindowId != attackWindowId)
            {
                return false;
            }

            action = recordedAttackWindowAction;
            return action != HeroActionType.None;
        }

        if (expectedContext ==
            CombatObservationContext.EnemyGuarding)
        {
            if (!previousGuarding ||
                guardWindowId <= 0 ||
                recordedGuardWindowId != guardWindowId)
            {
                return false;
            }

            action = recordedGuardWindowAction;
            return action != HeroActionType.None;
        }

        return false;
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
        recordedAttackWindowAction = action;
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

        DetectAttackDependencies(action);
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
        recordedGuardWindowAction = action;
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

        DetectForwardDashDependency(action);
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

    private void DetectAttackDependencies(
        HeroActionType action)
    {
        switch (action)
        {
            case HeroActionType.DashBack:
                TryActivateDependency(
                    PlayerPatternProfile.BackDashDependency,
                    attackReactions,
                    HeroActionType.DashBack,
                    backDashDetectionCount,
                    "ATTACK WINDOWS ANALYZED"
                );
                break;

            case HeroActionType.Slash:
                TryActivateDependency(
                    PlayerPatternProfile.SlashDependency,
                    attackReactions,
                    HeroActionType.Slash,
                    slashDetectionCount,
                    "ATTACK WINDOWS ANALYZED"
                );
                break;
        }
    }

    private void DetectForwardDashDependency(
        HeroActionType action)
    {
        if (action != HeroActionType.DashForward)
            return;

        TryActivateDependency(
            PlayerPatternProfile.ForwardDashDependency,
            guardReactions,
            HeroActionType.DashForward,
            forwardDashDetectionCount,
            "GUARD WINDOWS ANALYZED"
        );
    }

    private void TryActivateDependency(
        PlayerPatternProfile profile,
        List<HeroActionType> history,
        HeroActionType expectedAction,
        int detectionCount,
        string windowLabel)
    {
        if (CurrentProfile == profile)
            return;

        if (history.Count < detectionCount)
        {
            return;
        }

        int firstRecentIndex =
            history.Count - detectionCount;

        for (int index = firstRecentIndex;
             index < history.Count;
             index++)
        {
            if (history[index] != expectedAction)
            {
                return;
            }
        }

        CurrentProfile = profile;

        // A replacement profile must be learned from new windows.  Keep the
        // current window gate intact so its accepted action cannot count twice.
        attackReactions.Clear();
        guardReactions.Clear();

        Debug.Log(
            "PATTERN DETECTED: " +
            $"{profile}\n" +
            $"{windowLabel}: {detectionCount}\n" +
            $"{expectedAction} REACTIONS: {detectionCount}"
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
