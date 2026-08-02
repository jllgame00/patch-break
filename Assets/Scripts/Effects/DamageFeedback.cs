using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Health))]
public sealed class DamageFeedback : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Health health;

    [SerializeField]
    private SpriteRenderer targetRenderer;

    [SerializeField]
    private CameraShake cameraShake;

    [Header("Optional Enemy State")]
    [SerializeField]
    private EnemyCombatState enemyCombatState;

    [SerializeField]
    private Color attackingColor =
        new(1f, 0.25f, 0.15f, 1f);

    [Header("Hit Flash")]
    [SerializeField]
    private Color flashColor =
        new(1f, 1f, 0.2f, 1f);

    [SerializeField, Min(0.01f)]
    private float flashDuration = 0.07f;

    [Header("Camera Shake")]
    [SerializeField, Min(0f)]
    private float shakeDuration = 0.1f;

    [SerializeField, Min(0f)]
    private float shakeStrength = 0.12f;

    private Color normalColor = Color.white;
    private Coroutine flashRoutine;

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (targetRenderer == null)
        {
            targetRenderer =
                GetComponent<SpriteRenderer>();
        }

        if (targetRenderer != null)
        {
            normalColor = targetRenderer.color;
        }
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Damaged += HandleDamaged;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= HandleDamaged;
        }

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        RestoreColor();
    }

    private void HandleDamaged(
        Health damagedHealth,
        int damage)
    {
        if (targetRenderer != null)
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine =
                StartCoroutine(FlashRoutine());
        }

        if (cameraShake != null)
        {
            cameraShake.Shake(
                shakeDuration,
                shakeStrength
            );
        }
    }

    private IEnumerator FlashRoutine()
    {
        targetRenderer.color = flashColor;

        float elapsed = 0f;

        while (elapsed < flashDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        RestoreColor();
        flashRoutine = null;
    }

    private void RestoreColor()
    {
        if (targetRenderer == null)
            return;

        bool isEnemyAttacking =
            enemyCombatState != null &&
            enemyCombatState.IsAttacking;

        targetRenderer.color =
            isEnemyAttacking
                ? attackingColor
                : normalColor;
    }
}