using UnityEngine;

[RequireComponent(typeof(HeroController))]
[RequireComponent(typeof(HeroAttack))]
public sealed class HeroActionExecutor : MonoBehaviour
{
    [Header("Diagnostics")]
    [SerializeField]
    private bool verboseDashLogging;

    private HeroController controller;
    private HeroAttack attack;

    public bool VerboseDashLogging => verboseDashLogging;

    private void Awake()
    {
        controller = GetComponent<HeroController>();
        attack = GetComponent<HeroAttack>();
    }

    public bool TryExecute(
        HeroActionType action,
        Transform target = null)
    {
        if (controller == null ||
            controller.IsStaggered)
        {
            return false;
        }

        switch (action)
        {
            case HeroActionType.Approach:
                bool approachStarted =
                    controller.TryApproach(target);

                if (approachStarted &&
                    verboseDashLogging)
                {
                    Debug.Log(
                        "HERO ACTION: APPROACH START\n" +
                        $"time={Time.time:F3}\n" +
                        $"isDashing={controller.IsDashing}\n" +
                        "isInvulnerable=" +
                        controller.IsInvulnerable
                    );
                }

                return approachStarted;

            case HeroActionType.Slash:
                controller.StopMoving();
                return attack.TryAttack();

            case HeroActionType.DashForward:
                return TryDashRelativeToTarget(
                    HeroActionType.DashForward,
                    target,
                    force: false
                );

            case HeroActionType.DashBack:
                return TryDashRelativeToTarget(
                    HeroActionType.DashBack,
                    target,
                    force: false
                );

            case HeroActionType.None:
            default:
                return false;
        }
    }

    public void StopMovement()
    {
        controller.StopAllMovement();
    }
    
    public bool ForceExecute(
        HeroActionType action,
        Transform target = null)
    {
        switch (action)
        {
            case HeroActionType.DashBack:
                controller.StopMoving();

                return TryDashRelativeToTarget(
                    HeroActionType.DashBack,
                    target,
                    force: true
                );

            case HeroActionType.DashForward:
                controller.StopMoving();

                return TryDashRelativeToTarget(
                    HeroActionType.DashForward,
                    target,
                    force: true
                );

            default:
                return false;
        }
    }

    private bool TryDashRelativeToTarget(
        HeroActionType action,
        Transform target,
        bool force)
    {
        float directionToTarget =
            GetDirectionToTarget(target);

        controller.FaceDirection(directionToTarget);

        float dashDirection =
            action == HeroActionType.DashBack
                ? -directionToTarget
                : directionToTarget;

        bool isDashBack = action == HeroActionType.DashBack;
        bool executed = force
            ? (isDashBack
                ? controller.ForceDashBack(dashDirection)
                : controller.ForceDash(dashDirection))
            : (isDashBack
                ? controller.TryDashBack(dashDirection)
                : controller.TryDash(dashDirection));

        if (executed && verboseDashLogging)
        {
            float targetX =
                target == null
                    ? float.NaN
                    : target.position.x;

            Debug.Log(
                "HERO DASH: " +
                $"action={action} " +
                $"heroX={transform.position.x:F2} " +
                $"targetX={targetX:F2} " +
                $"directionToTarget={directionToTarget:F0} " +
                $"dashDirection={dashDirection:F0} " +
                $"facingDirection={controller.FacingDirection:F0}"
            );
        }

        return executed;
    }

    private float GetDirectionToTarget(Transform target)
    {
        if (target == null)
            return controller.FacingDirection;

        float deltaX =
            target.position.x -
            transform.position.x;

        if (Mathf.Abs(deltaX) < 0.001f)
            return controller.FacingDirection;

        return Mathf.Sign(deltaX);
    }
}
