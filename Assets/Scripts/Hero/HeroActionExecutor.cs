using UnityEngine;

[RequireComponent(typeof(HeroController))]
[RequireComponent(typeof(HeroAttack))]
public sealed class HeroActionExecutor : MonoBehaviour
{
    private HeroController controller;
    private HeroAttack attack;

    private void Awake()
    {
        controller = GetComponent<HeroController>();
        attack = GetComponent<HeroAttack>();
    }

    public bool TryExecute(
        HeroActionType action,
        Transform target = null)
    {
        switch (action)
        {
            case HeroActionType.Approach:
                return controller.TryApproach(target);

            case HeroActionType.Slash:
                controller.StopMoving();
                return attack.TryAttack();

            case HeroActionType.DashForward:
                return controller.TryDash(
                    controller.FacingDirection
                );

            case HeroActionType.DashBack:
                return controller.TryDash(
                    -controller.FacingDirection
                );

            case HeroActionType.None:
            default:
                return false;
        }
    }

    public void StopMovement()
    {
        controller.StopMoving();
    }
}