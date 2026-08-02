using UnityEngine;

public sealed class EnemyCombatState : MonoBehaviour
{
    public bool IsAttacking { get; private set; }
    public bool IsGuarding { get; private set; }

    public void SetAttacking(bool value)
    {
        IsAttacking = value;

        if (value)
        {
            IsGuarding = false;
        }
    }

    public void SetGuarding(bool value)
    {
        IsGuarding = value;

        if (value)
        {
            IsAttacking = false;
        }
    }

    public void ResetState()
    {
        IsAttacking = false;
        IsGuarding = false;
    }

    private void OnDisable()
    {
        ResetState();
    }
}