using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class HitEffect : MonoBehaviour
{
    [SerializeField, Min(0.01f)]
    private float duration = 0.12f;

    [SerializeField]
    private Vector3 startScale =
        new(0.15f, 0.15f, 1f);

    [SerializeField]
    private Vector3 endScale =
        new(0.75f, 0.75f, 1f);

    [SerializeField]
    private float rotationSpeed = 360f;

    private SpriteRenderer targetRenderer;
    private Color initialColor;
    private float elapsed;

    private void Awake()
    {
        targetRenderer = GetComponent<SpriteRenderer>();
        initialColor = targetRenderer.color;

        transform.localScale = startScale;
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;

        float progress = Mathf.Clamp01(
            elapsed / duration
        );

        transform.localScale = Vector3.Lerp(
            startScale,
            endScale,
            progress
        );

        transform.Rotate(
            0f,
            0f,
            rotationSpeed *
            Time.unscaledDeltaTime
        );

        Color color = initialColor;
        color.a = 1f - progress;

        targetRenderer.color = color;

        if (elapsed >= duration)
        {
            Destroy(gameObject);
        }
    }
}