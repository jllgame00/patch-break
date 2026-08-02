using UnityEngine;

public sealed class CameraShake : MonoBehaviour
{
    private Vector3 baseLocalPosition;
    private float remainingDuration;
    private float currentStrength;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
    }

    private void LateUpdate()
    {
        if (remainingDuration <= 0f)
        {
            transform.localPosition = baseLocalPosition;
            return;
        }

        remainingDuration -= Time.unscaledDeltaTime;

        Vector2 offset =
            Random.insideUnitCircle * currentStrength;

        transform.localPosition =
            baseLocalPosition +
            new Vector3(offset.x, offset.y, 0f);
    }

    public void Shake(float duration, float strength)
    {
        remainingDuration = Mathf.Max(
            remainingDuration,
            duration
        );

        currentStrength = Mathf.Max(
            currentStrength,
            strength
        );
    }

    public void StopShake()
    {
        remainingDuration = 0f;
        currentStrength = 0f;
        transform.localPosition = baseLocalPosition;
    }

    private void OnDisable()
    {
        StopShake();
    }
}