using UnityEngine;

public sealed class Health : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;

    public int CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0)
            return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);

        Debug.Log($"{name} took {damage} damage. HP: {CurrentHealth}/{maxHealth}");

        if (IsDead)
            Die();
    }

    private void Die()
    {
        Debug.Log($"{name} died.");
        gameObject.SetActive(false);
    }
}