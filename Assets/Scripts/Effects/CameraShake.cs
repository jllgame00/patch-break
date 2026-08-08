using UnityEngine;

public sealed class CameraShake : MonoBehaviour
{
    private Vector3 baseLocalPosition;
    private float remainingDuration;
    private float currentStrength;
    private Vector2 currentOffset;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
    }

    /// <summary>
    /// Updates the position that shake offsets are applied around. Systems
    /// which own camera framing must use this instead of writing the camera
    /// transform directly, otherwise CameraShake would restore the old base
    /// position on its next LateUpdate.
    /// </summary>
    public void SetBaseLocalPosition(Vector3 position)
    {
        baseLocalPosition = position;
        ApplyBaseAndShakeOffset();
    }

    public Vector3 GetBaseLocalPosition()
    {
        return baseLocalPosition;
    }

    /// <summary>
    /// Combat framing reserves this maximum horizontal presentation offset
    /// when enforcing its hard screen bounds.
    /// </summary>
    public float GetMaximumHorizontalOffset()
    {
        return Mathf.Max(
            Mathf.Abs(currentOffset.x),
            remainingDuration > 0f ? currentStrength : 0f
        );
    }

    private void LateUpdate()
    {
        if (remainingDuration <= 0f)
        {
            currentOffset = Vector2.zero;
            ApplyBaseAndShakeOffset();
            return;
        }

        remainingDuration -= Time.unscaledDeltaTime;

        currentOffset = Random.insideUnitCircle * currentStrength;
        ApplyBaseAndShakeOffset();
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
        currentOffset = Vector2.zero;
        ApplyBaseAndShakeOffset();
    }

    private void OnDisable()
    {
        StopShake();
    }

    private void ApplyBaseAndShakeOffset()
    {
        transform.localPosition =
            baseLocalPosition +
            new Vector3(currentOffset.x, currentOffset.y, 0f);
    }
}
