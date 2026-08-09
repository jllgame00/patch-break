using UnityEngine;

public enum ProjectileVisualStyle
{
    Knight,
    Debugger
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class KnightProjectile : MonoBehaviour
{
    // The prefab is shared by Knight and Debugger. These are applied only to
    // the SpriteRenderer child, after the caller selects its visual style.
    // The Debugger value is authored by the Editor setup from the imported
    // frame bounds; neither value affects the root's gameplay components.
    [SerializeField] private Vector3 knightVisualLocalScale = new Vector3(
        2f,
        8.333334f,
        1f
    );

    [SerializeField] private Vector3 debuggerVisualLocalScale = new Vector3(
        -2.4f,
        10f,
        1f
    );

    [Header("Movement")]
    [SerializeField, Min(0.1f)]
    private float speed = 8f;

    [SerializeField, Min(0.1f)]
    private float lifetime = 4f;

    [Header("Damage")]
    [SerializeField, Min(1)]
    private int damage = 20;

    [SerializeField]
    private LayerMask targetLayer;

    [SerializeField]
    private LayerMask blockingLayer;

    [Header("Effects")]
    [SerializeField]
    private GameObject hitEffectPrefab;

    [Header("Visual Animation")]
    [SerializeField] private SpriteSequencePlayer visualSequence;
    [SerializeField] private Sprite[] knightBeamFrames;
    [SerializeField] private Sprite[] debuggerBeamFrames;
    [SerializeField, Min(0.01f)] private float beamFramesPerSecond = 12f;

    private Rigidbody2D body;
    private float horizontalDirection;
    private float lockedTargetX;
    private bool hasLockedTarget;
    private bool reachedLockedTarget;
    private bool resolved;
    private bool verboseProjectileLogging;
    private System.Action resolvedCallback;
    private bool resolutionNotified;
    private string logPrefix = "KNIGHT PROJECTILE";
    private SpriteRenderer visualRenderer;
    private Color knightVisualColor = Color.white;
    private bool knightVisualFlipX;

    public SpriteSequencePlayer VisualSequence => visualSequence;
    public Sprite[] KnightBeamFrames => knightBeamFrames;
    public Sprite[] DebuggerBeamFrames => debuggerBeamFrames;
    public float BeamFramesPerSecond => beamFramesPerSecond;
    public Vector3 KnightVisualLocalScale => knightVisualLocalScale;
    public Vector3 DebuggerVisualLocalScale => debuggerVisualLocalScale;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        visualRenderer = visualSequence != null
            ? visualSequence.TargetRenderer
            : null;

        if (visualRenderer != null)
        {
            // Preserve the shared prefab's existing Knight presentation.
            knightVisualColor = visualRenderer.color;
            knightVisualFlipX = visualRenderer.flipX;
        }
    }

    /// <summary>
    /// Called immediately after the existing projectile Instantiate call.
    /// It affects only renderer frames; Launch still owns all movement,
    /// collision, lifetime, and damage behavior.
    /// </summary>
    public void SetVisualStyle(ProjectileVisualStyle style)
    {
        Sprite[] frames = style == ProjectileVisualStyle.Debugger
            ? debuggerBeamFrames
            : knightBeamFrames;

        // Keep the shared Knight presentation unchanged. The Debugger scale
        // is calibrated from its imported sheet by the Editor setup tool.
        // visualSequence is attached exclusively to ProjectileVisual.
        if (visualSequence != null)
        {
            visualSequence.transform.localScale =
                style == ProjectileVisualStyle.Debugger
                    ? debuggerVisualLocalScale
                    : knightVisualLocalScale;
        }

        if (visualRenderer != null)
        {
            if (style == ProjectileVisualStyle.Debugger)
            {
                // The direction correction is the negative child X scale
                // above. Do not add a second SpriteRenderer flip here.
                visualRenderer.flipX = knightVisualFlipX;
                visualRenderer.color = Color.white;
            }
            else
            {
                visualRenderer.flipX = knightVisualFlipX;
                visualRenderer.color = knightVisualColor;
            }
        }

        visualSequence?.PlayLoop(frames, beamFramesPerSecond);
    }

    public void Launch(
        float direction,
        float targetX,
        bool verboseLogging,
        System.Action onResolved,
        string projectileLogPrefix = "KNIGHT PROJECTILE")
    {
        horizontalDirection = Mathf.Sign(direction);

        if (Mathf.Approximately(
                horizontalDirection,
                0f))
        {
            horizontalDirection = 1f;
        }

        lockedTargetX = targetX;
        hasLockedTarget = true;
        reachedLockedTarget = false;
        resolved = false;
        verboseProjectileLogging = verboseLogging;
        resolvedCallback = onResolved;
        resolutionNotified = false;
        logPrefix = string.IsNullOrWhiteSpace(
            projectileLogPrefix)
            ? "KNIGHT PROJECTILE"
            : projectileLogPrefix;

        body.linearVelocity =
            new Vector2(
                horizontalDirection * speed,
                0f
            );

        PersistentAudioManager.PlayProjectile();

        Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        if (body == null ||
            !hasLockedTarget ||
            resolved)
        {
            return;
        }

        if (reachedLockedTarget)
        {
            ReachLockedTarget();
            return;
        }

        float remainingDistance =
            lockedTargetX - body.position.x;

        if (HasReachedOrPassedTarget(remainingDistance))
        {
            ReachLockedTarget();
            return;
        }

        float movementThisStep =
            speed * Time.fixedDeltaTime;

        if (Mathf.Abs(remainingDistance) <=
            movementThisStep)
        {
            body.MovePosition(
                new Vector2(
                    lockedTargetX,
                    body.position.y
                )
            );

            body.linearVelocity = Vector2.zero;
            reachedLockedTarget = true;
            return;
        }

        body.linearVelocity =
            new Vector2(
                horizontalDirection * speed,
                0f
            );
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (resolved)
            return;

        if (IsInBlockingLayer(other.gameObject.layer))
        {
            ResolveAndDestroy();
            return;
        }

        if (!IsInTargetLayer(other.gameObject.layer))
            return;

        HeroController hero =
            other.GetComponentInParent<HeroController>();

        bool heroIsDashing =
            hero != null && hero.IsDashing;

        bool heroIsInvulnerable =
            hero != null && hero.IsInvulnerable;

        if (verboseProjectileLogging ||
            (hero != null &&
             hero.IsVerboseDashLogging))
        {
            Debug.Log(
                $"{logPrefix} CONTACT\n" +
                $"heroX={other.transform.position.x:F2}\n" +
                $"projectileX={transform.position.x:F2}\n" +
                $"targetX={lockedTargetX:F2}\n" +
                "heroIsDashing=" + heroIsDashing + "\n" +
                "heroIsInvulnerable=" +
                heroIsInvulnerable + "\n" +
                "result=" +
                (heroIsInvulnerable ? "EVADED" : "HIT")
            );
        }

        if (heroIsInvulnerable)
        {
            Debug.Log(
                $"{logPrefix} EVADED"
            );

            ResolveAndDestroy();
            return;
        }

        Health health =
            other.GetComponentInParent<Health>();

        if (health == null || health.IsDead)
            return;

        Vector2 hitPosition =
            other.ClosestPoint(
                transform.position
            );

        health.TakeDamage(damage);
        HitVfxManager.ReportConfirmedHit(
            health,
            hitPosition
        );
        SpawnHitEffect(hitPosition);

        Debug.Log($"{logPrefix} HIT");

        ResolveAndDestroy();
    }

    private bool IsInTargetLayer(int layer)
    {
        return
            (targetLayer.value & (1 << layer)) != 0;
    }

    private bool IsInBlockingLayer(int layer)
    {
        return
            (blockingLayer.value & (1 << layer)) != 0;
    }

    private bool HasReachedOrPassedTarget(
        float remainingDistance)
    {
        return horizontalDirection > 0f
            ? remainingDistance <= 0f
            : remainingDistance >= 0f;
    }

    private void ReachLockedTarget()
    {
        if (resolved)
            return;

        body.position =
            new Vector2(
                lockedTargetX,
                body.position.y
            );

        if (verboseProjectileLogging)
        {
            Debug.Log(
                $"{logPrefix} REACHED LOCKED TARGET\n" +
                $"currentX={transform.position.x:F2}\n" +
                $"targetX={lockedTargetX:F2}\n" +
                "result=MISS"
            );
        }

        ResolveAndDestroy();
    }

    private void ResolveAndDestroy()
    {
        if (resolved)
            return;

        resolved = true;
        hasLockedTarget = false;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        NotifyResolved();
        Destroy(gameObject);
    }

    private void NotifyResolved()
    {
        if (resolutionNotified)
            return;

        resolutionNotified = true;
        resolvedCallback?.Invoke();
        resolvedCallback = null;
    }

    private void SpawnHitEffect(Vector3 position)
    {
        if (hitEffectPrefab == null)
            return;

        Instantiate(
            hitEffectPrefab,
            position,
            Quaternion.identity
        );
    }

    private void OnDisable()
    {
        if (body != null)
        {
            body.linearVelocity =
                Vector2.zero;
        }

        NotifyResolved();
    }
}
