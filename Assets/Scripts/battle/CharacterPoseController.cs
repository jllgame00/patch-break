using UnityEngine;

/// <summary>
/// Coordinates the existing root SpriteRenderer's travel, ready-idle, and
/// attack visuals. It does not own facing, color feedback, transforms, or
/// gameplay timing.
/// </summary>
public sealed class CharacterPoseController : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Sprite baseSprite;
    [SerializeField] private Sprite readySprite;
    [SerializeField] private Sprite phaseSprite;

    [Header("Optional Combat Sprite Sequences")]
    [SerializeField] private SpriteSequencePlayer sequencePlayer;
    [SerializeField] private Sprite[] readyIdleFrames;
    [SerializeField] private Sprite[] attackFrames;
    [SerializeField, Min(0.01f)] private float readyIdleFramesPerSecond = 7f;
    [SerializeField, Min(0.01f)] private float attackFramesPerSecond = 12f;

    private int visualRequestVersion;

    public SpriteRenderer TargetRenderer => targetRenderer;
    public Sprite BaseSprite => baseSprite;
    public Sprite ReadySprite => readySprite;
    public Sprite PhaseSprite => phaseSprite;
    public SpriteSequencePlayer SequencePlayer => sequencePlayer;
    public Sprite[] ReadyIdleFrames => readyIdleFrames;
    public Sprite[] AttackFrames => attackFrames;
    public float ReadyIdleFramesPerSecond => readyIdleFramesPerSecond;
    public float AttackFramesPerSecond => attackFramesPerSecond;

    public void SetBasePose()
    {
        visualRequestVersion++;
        SetStaticSprite(baseSprite);
    }

    public void SetReadyPose()
    {
        visualRequestVersion++;
        ApplyReadyVisual();
    }

    public void SetPhasePose()
    {
        visualRequestVersion++;
        SetStaticSprite(phaseSprite != null ? phaseSprite : baseSprite);
    }

    /// <summary>
    /// Called by existing attack gameplay at its already-established action
    /// time. Sequence frames are visual feedback only; they never schedule
    /// damage, movement, or cooldowns.
    /// </summary>
    public void PlayAttack()
    {
        int requestVersion = ++visualRequestVersion;

        if (sequencePlayer == null ||
            attackFrames == null ||
            attackFrames.Length == 0)
        {
            ApplyReadyVisual();
            return;
        }

        sequencePlayer.PlayOnce(
            attackFrames,
            attackFramesPerSecond,
            () =>
            {
                if (requestVersion == visualRequestVersion)
                {
                    ApplyReadyVisual();
                }
            }
        );
    }

    private void ApplyReadyVisual()
    {
        if (sequencePlayer != null &&
            readyIdleFrames != null &&
            readyIdleFrames.Length > 0)
        {
            sequencePlayer.PlayLoop(
                readyIdleFrames,
                readyIdleFramesPerSecond
            );
            return;
        }

        // Debugger's static Ready sprite remains intentionally absent. Its
        // combat idle sheet supplies the ready visual without inventing a
        // Debugger phase hook.
        SetStaticSprite(readySprite != null ? readySprite : baseSprite);
    }

    private void SetStaticSprite(Sprite sprite)
    {
        if (sequencePlayer != null)
        {
            sequencePlayer.SetStatic(sprite);
            return;
        }

        if (targetRenderer != null && sprite != null)
        {
            targetRenderer.sprite = sprite;
        }
    }
}
