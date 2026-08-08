using UnityEngine;

/// <summary>
/// One reusable world-space hit VFX slot. It owns only its temporary visual
/// lifetime; it never changes gameplay transforms, damage, or renderer color
/// outside of this VFX renderer.
/// </summary>
public sealed class HitVfxSlot : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private SpriteSequencePlayer sequencePlayer;

    private float hideAtTime;
    private bool isPlaying;

    public SpriteRenderer TargetRenderer => targetRenderer;
    public SpriteSequencePlayer SequencePlayer => sequencePlayer;
    public bool IsPlaying => isPlaying;

    public void Configure(
        SpriteRenderer renderer,
        SpriteSequencePlayer sequence)
    {
        targetRenderer = renderer;
        sequencePlayer = sequence;
    }

    public void Play(
        Sprite[] frames,
        float framesPerSecond,
        Vector3 worldPosition)
    {
        if (targetRenderer == null || sequencePlayer == null ||
            frames == null || frames.Length == 0)
        {
            return;
        }

        transform.position = worldPosition;
        targetRenderer.enabled = true;
        targetRenderer.color = Color.white;
        sequencePlayer.PlayOnce(frames, framesPerSecond);

        float duration = frames.Length /
            Mathf.Max(0.01f, framesPerSecond);
        hideAtTime = Time.time + duration;
        isPlaying = true;
    }

    public void HideImmediately()
    {
        if (sequencePlayer != null)
        {
            sequencePlayer.Stop();
        }

        if (targetRenderer != null)
        {
            targetRenderer.enabled = false;
        }

        isPlaying = false;
        hideAtTime = 0f;
    }

    private void OnDisable()
    {
        HideImmediately();
    }

    private void Update()
    {
        if (isPlaying && Time.time >= hideAtTime)
        {
            HideImmediately();
        }
    }
}
