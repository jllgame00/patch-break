using UnityEngine;

public sealed class EnemyCombatState : MonoBehaviour
{
    public bool IsAttacking { get; private set; }

    public void SetAttacking(bool value)
    {
        IsAttacking = value;
    }

    private void OnDisable()
    {
        IsAttacking = false;
    }
}