using System;
using UnityEngine;

public sealed class Health : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int maxHealth = 100;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsDead => CurrentHealth <= 0;

    public event Action<Health> HealthChanged;
    public event Action<Health, int> Damaged;
    public event Action<Health> Died;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0)
            return;

        int previousHealth = CurrentHealth;

        CurrentHealth = Mathf.Max(
            0,
            CurrentHealth - damage
        );

        int appliedDamage =
            previousHealth - CurrentHealth;

        Debug.Log(
            $"{name} took {appliedDamage} damage. " +
            $"HP: {CurrentHealth}/{maxHealth}"
        );

        HealthChanged?.Invoke(this);
        Damaged?.Invoke(this, appliedDamage);

        if (IsDead)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"{name} died.");

        // 비활성화 전에 이벤트를 보낸다.
        Died?.Invoke(this);

        gameObject.SetActive(false);
    }
}