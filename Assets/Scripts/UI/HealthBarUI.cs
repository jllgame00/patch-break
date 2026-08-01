using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HealthBarUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Health targetHealth;

    [Header("UI")]
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text valueText;

    [Header("Display")]
    [SerializeField] private string displayName = "TARGET";

    private void OnEnable()
    {
        if (targetHealth == null)
            return;

        targetHealth.HealthChanged += HandleHealthChanged;
        targetHealth.Died += HandleTargetDied;
    }

    private void Start()
    {
        if (!ValidateReferences())
            return;

        Refresh(targetHealth);
    }

    private void OnDisable()
    {
        if (targetHealth == null)
            return;

        targetHealth.HealthChanged -= HandleHealthChanged;
        targetHealth.Died -= HandleTargetDied;
    }

    private bool ValidateReferences()
    {
        if (targetHealth == null)
        {
            Debug.LogError(
                $"{name}: Target Health is not assigned."
            );

            enabled = false;
            return false;
        }

        if (fillImage == null)
        {
            Debug.LogError(
                $"{name}: Fill Image is not assigned."
            );

            enabled = false;
            return false;
        }

        return true;
    }

    private void HandleHealthChanged(Health health)
    {
        Refresh(health);
    }

    private void HandleTargetDied(Health health)
    {
        Refresh(health);
    }

    private void Refresh(Health health)
    {
        float healthRatio =
            health.MaxHealth > 0
                ? (float)health.CurrentHealth /
                  health.MaxHealth
                : 0f;

        fillImage.fillAmount =
            Mathf.Clamp01(healthRatio);

        if (nameText != null)
        {
            nameText.text = displayName;
        }

        if (valueText != null)
        {
            valueText.text =
                $"{health.CurrentHealth} / " +
                $"{health.MaxHealth}";
        }
    }
}