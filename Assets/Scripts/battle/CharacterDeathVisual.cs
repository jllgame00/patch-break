using UnityEngine;

/// <summary>
/// Keeps death presentation separate from an actor's gameplay root. Health
/// still disables that root immediately after all Died listeners run; this
/// prebuilt scene-root visual detaches during that event, plays once, and
/// holds its final frame.
/// </summary>
public sealed class CharacterDeathVisual : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private CharacterPoseController poseController;
    [SerializeField] private SpriteRenderer sourceRenderer;
    [SerializeField] private Transform deathVisualRoot;
    [SerializeField] private SpriteRenderer deathRenderer;
    [SerializeField] private SpriteSequencePlayer sequencePlayer;
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField, Min(0.01f)] private float framesPerSecond = 10f;

    private bool subscribed;
    private bool deathPlayed;

    public Health Health => health;
    public CharacterPoseController PoseController => poseController;
    public SpriteRenderer SourceRenderer => sourceRenderer;
    public Transform DeathVisualRoot => deathVisualRoot;
    public SpriteRenderer DeathRenderer => deathRenderer;
    public SpriteSequencePlayer SequencePlayer => sequencePlayer;
    public Sprite[] DeathFrames => deathFrames;
    public float FramesPerSecond => framesPerSecond;

    public void Configure(
        Health configuredHealth,
        CharacterPoseController configuredPoseController,
        SpriteRenderer configuredSourceRenderer,
        Transform configuredDeathVisualRoot,
        SpriteRenderer configuredDeathRenderer,
        SpriteSequencePlayer configuredSequencePlayer,
        Sprite[] configuredDeathFrames,
        float configuredFramesPerSecond)
    {
        health = configuredHealth;
        poseController = configuredPoseController;
        sourceRenderer = configuredSourceRenderer;
        deathVisualRoot = configuredDeathVisualRoot;
        deathRenderer = configuredDeathRenderer;
        sequencePlayer = configuredSequencePlayer;
        deathFrames = configuredDeathFrames ?? System.Array.Empty<Sprite>();
        framesPerSecond = Mathf.Max(0.01f, configuredFramesPerSecond);
    }

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (sourceRenderer == null)
        {
            sourceRenderer = GetComponent<SpriteRenderer>();
        }

        if (poseController == null)
        {
            poseController = GetComponent<CharacterPoseController>();
        }

        HideVisual();
    }

    private void OnEnable()
    {
        if (!subscribed && health != null)
        {
            health.Died += HandleDied;
            subscribed = true;
        }
    }

    private void OnDisable()
    {
        if (subscribed && health != null)
        {
            health.Died -= HandleDied;
            subscribed = false;
        }
    }

    /// <summary>
    /// Explicit retry/setup reset only. It is intentionally not called by
    /// normal pose changes, so terminal death visuals cannot be overwritten.
    /// </summary>
    public void ResetForBattle()
    {
        deathPlayed = false;
        HideVisual();
        poseController?.ResetForBattle();
    }

    private void HandleDied(Health diedHealth)
    {
        if (deathPlayed || diedHealth != health ||
            deathVisualRoot == null || deathRenderer == null)
        {
            return;
        }

        deathPlayed = true;
        // Terminal-lock the gameplay root's coordinator before Health disables
        // that root. The detached renderer below is what remains visible.
        poseController?.PlayDeath();
        deathVisualRoot.SetParent(null, true);
        deathVisualRoot.position = transform.position;
        deathVisualRoot.rotation = transform.rotation;
        // The target is a scene root, so it has no parent scale to inherit.
        // Preserve rendered world-size with lossyScale while taking the
        // established root-local X sign as the authoritative facing flip.
        Vector3 worldScale = transform.lossyScale;
        float facingSign = Mathf.Sign(transform.localScale.x);
        if (Mathf.Approximately(facingSign, 0f))
        {
            facingSign = Mathf.Sign(worldScale.x);
        }

        deathVisualRoot.localScale = new Vector3(
            Mathf.Abs(worldScale.x) * (Mathf.Approximately(facingSign, 0f)
                ? 1f
                : facingSign),
            worldScale.y,
            worldScale.z
        );

        if (sourceRenderer != null)
        {
            deathRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            deathRenderer.sortingOrder = sourceRenderer.sortingOrder;
        }

        // Death presentation is a fresh static art state, not a continuation
        // of a temporary windup/guard/damage tint on the gameplay renderer.
        deathRenderer.color = Color.white;
        deathRenderer.enabled = true;
        deathVisualRoot.gameObject.SetActive(true);

        if (sequencePlayer != null &&
            deathFrames != null &&
            deathFrames.Length > 0)
        {
            sequencePlayer.PlayOnce(
                deathFrames,
                framesPerSecond,
                HideVisual
            );
        }
        else if (deathFrames != null && deathFrames.Length > 0)
        {
            deathRenderer.sprite = deathFrames[deathFrames.Length - 1];
            HideVisual();
        }
    }

    private void HideVisual()
    {
        if (sequencePlayer != null)
        {
            sequencePlayer.Stop();
        }

        if (deathRenderer != null)
        {
            deathRenderer.enabled = false;
        }

        if (deathVisualRoot != null)
        {
            deathVisualRoot.gameObject.SetActive(false);
        }
    }
}
