using UnityEngine;

/// <summary>
/// Drives the visual-only Start -> Loop -> Break sequence below the existing
/// GuardIndicator transform. Guard gameplay remains owned by the controller;
/// this component never changes guard state, damage handling, or cooldowns.
/// </summary>
public sealed class GuardVisualLoop : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private SpriteSequencePlayer sequencePlayer;
    [SerializeField] private Sprite[] startFrames;
    [SerializeField] private Sprite[] loopFrames;
    [SerializeField] private Sprite[] breakFrames;
    [SerializeField, Min(0.01f)] private float framesPerSecond = 10f;

    private int transitionVersion;
    private bool isGuardVisualActive;

    public SpriteRenderer TargetRenderer => targetRenderer;
    public SpriteSequencePlayer SequencePlayer => sequencePlayer;
    public Sprite[] StartFrames => startFrames;
    public Sprite[] LoopFrames => loopFrames;
    public Sprite[] BreakFrames => breakFrames;
    public float FramesPerSecond => framesPerSecond;
    public bool IsGuardVisualActive => isGuardVisualActive;

    /// <summary>
    /// Begins the visual guard sequence at Start frame zero. Repeated calls
    /// intentionally restart Start so a newly-started gameplay guard never
    /// resumes a stale Break or Loop callback.
    /// </summary>
    public void PlayGuard()
    {
        int requestVersion = ++transitionVersion;
        isGuardVisualActive = true;
        SetRendererVisible(true);

        if (sequencePlayer == null ||
            startFrames == null ||
            startFrames.Length == 0)
        {
            PlayLoop(requestVersion);
            return;
        }

        sequencePlayer.PlayOnce(
            startFrames,
            framesPerSecond,
            () =>
            {
                if (requestVersion == transitionVersion)
                {
                    PlayLoop(requestVersion);
                }
            }
        );
    }

    /// <summary>
    /// Stops the visual guard immediately and plays Break once. The caller has
    /// already ended the actual guard state before or after this call; Break
    /// never delays gameplay state changes.
    /// </summary>
    public void StopGuard()
    {
        // Controllers also call their existing HideGuardIndicator method
        // before melee/ranged actions. Those calls must remain visually inert
        // unless a guard Start or Loop is actually in progress.
        if (!isGuardVisualActive)
        {
            return;
        }

        int requestVersion = ++transitionVersion;
        isGuardVisualActive = false;

        if (sequencePlayer == null ||
            breakFrames == null ||
            breakFrames.Length == 0)
        {
            HideImmediately();
            return;
        }

        SetRendererVisible(true);
        sequencePlayer.PlayOnce(
            breakFrames,
            framesPerSecond,
            () =>
            {
                if (requestVersion == transitionVersion)
                {
                    HideImmediately();
                }
            }
        );
    }

    /// <summary>
    /// Used only for scene initialization or when the owning character itself
    /// is disabled. It does not alter guard gameplay.
    /// </summary>
    public void HideImmediately()
    {
        transitionVersion++;
        isGuardVisualActive = false;
        sequencePlayer?.Stop();
        SetRendererVisible(false);
    }

    private void OnEnable()
    {
        // A character can be disabled for a result/state transition while a
        // Break is in progress. Re-enabling it must not reveal a stale frame.
        HideImmediately();
    }

    private void OnDisable()
    {
        transitionVersion++;
        sequencePlayer?.Stop();
    }

    private void PlayLoop(int requestVersion)
    {
        if (requestVersion != transitionVersion)
        {
            return;
        }

        if (sequencePlayer == null ||
            loopFrames == null ||
            loopFrames.Length == 0)
        {
            HideImmediately();
            return;
        }

        sequencePlayer.PlayLoop(loopFrames, framesPerSecond);
    }

    private void SetRendererVisible(bool visible)
    {
        if (targetRenderer != null)
        {
            targetRenderer.enabled = visible;
        }
    }
}
