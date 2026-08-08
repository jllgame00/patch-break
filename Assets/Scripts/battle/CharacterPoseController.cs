using UnityEngine;

/// <summary>
/// Coordinates the existing root SpriteRenderer's visual-only pose states.
/// It never owns facing, color feedback, transforms, physics, or gameplay
/// timing; gameplay paths explicitly request their corresponding visual.
/// </summary>
public sealed class CharacterPoseController : MonoBehaviour
{
    private enum VisualState
    {
        Base,
        Ready,
        Walk,
        Attack,
        Death
    }

    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Sprite baseSprite;
    [SerializeField] private Sprite readySprite;
    [SerializeField] private Sprite phaseSprite;

    [Header("Optional Combat Sprite Sequences")]
    [SerializeField] private SpriteSequencePlayer sequencePlayer;
    [SerializeField] private Sprite[] readyIdleFrames;
    [SerializeField] private Sprite[] walkFrames;
    [SerializeField] private Sprite[] attackFrames;
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField, Min(0.01f)] private float readyIdleFramesPerSecond = 7f;
    [SerializeField, Min(0.01f)] private float walkFramesPerSecond = 8f;
    [SerializeField, Min(0.01f)] private float attackFramesPerSecond = 12f;
    [SerializeField, Min(0.01f)] private float deathFramesPerSecond = 10f;

    private int visualRequestVersion;
    private bool walkRequested;
    private bool isDead;
    private VisualState visualState = VisualState.Base;

    public SpriteRenderer TargetRenderer => targetRenderer;
    public Sprite BaseSprite => baseSprite;
    public Sprite ReadySprite => readySprite;
    public Sprite PhaseSprite => phaseSprite;
    public SpriteSequencePlayer SequencePlayer => sequencePlayer;
    public Sprite[] ReadyIdleFrames => readyIdleFrames;
    public Sprite[] WalkFrames => walkFrames;
    public Sprite[] AttackFrames => attackFrames;
    public Sprite[] DeathFrames => deathFrames;
    public float ReadyIdleFramesPerSecond => readyIdleFramesPerSecond;
    public float WalkFramesPerSecond => walkFramesPerSecond;
    public float AttackFramesPerSecond => attackFramesPerSecond;
    public float DeathFramesPerSecond => deathFramesPerSecond;
    public bool IsDeadVisual => isDead;

    /// <summary>
    /// Explicit scene/retry initialization only. Normal pose calls may not
    /// clear a terminal death visual state.
    /// </summary>
    public void ResetForBattle()
    {
        isDead = false;
        walkRequested = false;
        visualRequestVersion++;
        visualState = VisualState.Death;
        ApplyBaseVisual();
    }

    public void SetBasePose()
    {
        if (isDead)
        {
            return;
        }

        walkRequested = false;
        visualRequestVersion++;
        ApplyBaseVisual();
    }

    public void SetReadyPose()
    {
        if (isDead)
        {
            return;
        }

        walkRequested = false;
        visualRequestVersion++;
        ApplyReadyVisual();
    }

    public void SetPhasePose()
    {
        if (isDead)
        {
            return;
        }

        walkRequested = false;
        visualRequestVersion++;
        visualState = VisualState.Base;
        SetStaticSprite(phaseSprite != null ? phaseSprite : baseSprite);
    }

    /// <summary>
    /// Starts a loop only for an already-authorized normal movement path.
    /// Dashes, recoil, knockback, attack, guard, and death must not use it.
    /// </summary>
    public void PlayWalk()
    {
        if (isDead)
        {
            return;
        }

        walkRequested = true;
        if (visualState == VisualState.Attack)
        {
            return;
        }

        ApplyWalkVisual();
    }

    public void StopWalk()
    {
        if (isDead)
        {
            return;
        }

        walkRequested = false;
        if (visualState == VisualState.Walk)
        {
            ApplyReadyVisual();
        }
    }

    /// <summary>
    /// Called by existing attack gameplay at its already-established action
    /// time. Sequence frames never schedule damage, movement, or cooldowns.
    /// </summary>
    public void PlayAttack()
    {
        if (isDead)
        {
            return;
        }

        int requestVersion = ++visualRequestVersion;
        visualState = VisualState.Attack;

        if (sequencePlayer == null ||
            attackFrames == null ||
            attackFrames.Length == 0)
        {
            ApplyLocomotionOrReadyVisual();
            return;
        }

        sequencePlayer.PlayOnce(
            attackFrames,
            attackFramesPerSecond,
            () =>
            {
                if (!isDead && requestVersion == visualRequestVersion)
                {
                    ApplyLocomotionOrReadyVisual();
                }
            }
        );
    }

    /// <summary>
    /// Terminal visual-only state. It holds the final one-shot frame and
    /// deliberately ignores later base/ready/walk/attack requests.
    /// </summary>
    public void PlayDeath()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        walkRequested = false;
        visualRequestVersion++;
        visualState = VisualState.Death;

        if (sequencePlayer != null &&
            deathFrames != null &&
            deathFrames.Length > 0)
        {
            sequencePlayer.PlayOnce(deathFrames, deathFramesPerSecond);
            return;
        }

        if (deathFrames != null && deathFrames.Length > 0)
        {
            SetStaticSprite(deathFrames[deathFrames.Length - 1]);
        }
    }

    private void ApplyLocomotionOrReadyVisual()
    {
        if (walkRequested)
        {
            ApplyWalkVisual();
            return;
        }

        ApplyReadyVisual();
    }

    private void ApplyBaseVisual()
    {
        visualState = VisualState.Base;
        SetStaticSprite(baseSprite);
    }

    private void ApplyReadyVisual()
    {
        if (visualState == VisualState.Ready)
        {
            return;
        }

        visualState = VisualState.Ready;
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

    private void ApplyWalkVisual()
    {
        // A previous lifecycle/setup callback can stop the shared sequence
        // without changing this coordinator's requested state. Only treat an
        // existing Walk state as complete while its loop is still playing.
        if (visualState == VisualState.Walk &&
            (sequencePlayer == null || sequencePlayer.IsPlaying))
        {
            return;
        }

        visualState = VisualState.Walk;
        if (sequencePlayer != null &&
            walkFrames != null &&
            walkFrames.Length > 0)
        {
            sequencePlayer.PlayLoop(walkFrames, walkFramesPerSecond);
            return;
        }

        ApplyReadyVisual();
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
