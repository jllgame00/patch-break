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

    [Header("Debugger Hit Profile")]
    // The shared root is scaled (0.5, 0.12). These local values produce a
    // 2.6 x 2.16 world-space danger area: it covers the solid crescent body
    // without turning the full 3.6 x 3.6 transparent sprite rectangle into
    // a hitbox. Knight retains the prefab's original 1 x 1 profile.
    [SerializeField] private Vector2 debuggerColliderSize = new Vector2(
        5.2f,
        18f
    );

    [SerializeField] private Vector2 debuggerColliderOffset = Vector2.zero;

    // Hero DashBack travels 1.8 units. The small margin lets the Debugger
    // crescent complete a real collision sweep after a post-lock DashBack,
    // while remaining well below its existing 32-unit lifetime limit.
    [SerializeField, Min(0f)]
    private float debuggerTravelPastTargetDistance = 2f;

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
    private BoxCollider2D hitCollider;
    private float horizontalDirection;
    private float lockedTargetX;
    private float travelEndX;
    private Vector2 launchPosition;
    private bool hasLockedTarget;
    private bool reachedLockedTarget;
    private bool resolved;
    private bool debuggerHitProfileActive;
    private bool verboseProjectileLogging;
    private System.Action resolvedCallback;
    private bool resolutionNotified;
    private string logPrefix = "KNIGHT PROJECTILE";
    private SpriteRenderer visualRenderer;
    private Color knightVisualColor = Color.white;
    private bool knightVisualFlipX;
    private Vector2 knightColliderSize;
    private Vector2 knightColliderOffset;
    private Bounds debuggerGeometryHeroBounds;
    private bool hasDebuggerGeometryHeroBounds;

    public SpriteSequencePlayer VisualSequence => visualSequence;
    public Sprite[] KnightBeamFrames => knightBeamFrames;
    public Sprite[] DebuggerBeamFrames => debuggerBeamFrames;
    public float BeamFramesPerSecond => beamFramesPerSecond;
    public Vector3 KnightVisualLocalScale => knightVisualLocalScale;
    public Vector3 DebuggerVisualLocalScale => debuggerVisualLocalScale;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        hitCollider = GetComponent<BoxCollider2D>();
        visualRenderer = visualSequence != null
            ? visualSequence.TargetRenderer
            : null;

        if (hitCollider != null)
        {
            knightColliderSize = hitCollider.size;
            knightColliderOffset = hitCollider.offset;
        }

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
        debuggerHitProfileActive =
            style == ProjectileVisualStyle.Debugger;

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

        ApplyColliderProfile();

        visualSequence?.PlayLoop(frames, beamFramesPerSecond);
    }

    /// <summary>
    /// Keeps the shared gameplay root and collider on their already-tested
    /// torso trajectory while moving only the Debugger visual to the sword
    /// height. Renderer bounds, rather than its pivot, are the alignment
    /// source of truth.
    /// </summary>
    public void SetDebuggerVisualWorldCenterY(float worldCenterY)
    {
        if (!debuggerHitProfileActive || visualRenderer == null ||
            visualSequence == null)
        {
            return;
        }

        Transform visualTransform = visualSequence.transform;
        float centerDeltaY =
            worldCenterY - visualRenderer.bounds.center.y;
        Vector3 desiredWorldPosition = visualTransform.position;
        desiredWorldPosition.y += centerDeltaY;

        Vector3 desiredLocalPosition = transform.InverseTransformPoint(
            desiredWorldPosition
        );
        Vector3 localPosition = visualTransform.localPosition;
        localPosition.y = desiredLocalPosition.y;
        visualTransform.localPosition = localPosition;
    }

    /// <summary>
    /// Captures the Hero's actual collider bounds once for concise Debugger
    /// projectile geometry diagnostics. It has no gameplay effect.
    /// </summary>
    public void SetDebuggerGeometryTarget(Collider2D heroCollider)
    {
        hasDebuggerGeometryHeroBounds = heroCollider != null;
        if (hasDebuggerGeometryHeroBounds)
        {
            debuggerGeometryHeroBounds = heroCollider.bounds;
        }
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
        travelEndX = lockedTargetX +
            (debuggerHitProfileActive
                ? horizontalDirection *
                  debuggerTravelPastTargetDistance
                : 0f);
        launchPosition = transform.position;
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

        if (verboseProjectileLogging)
        {
            Debug.Log(
                "[DBG_PROJECTILE_SPAWN] " +
                $"style={(debuggerHitProfileActive ? "DEBUGGER" : "KNIGHT")} " +
                $"spawn={launchPosition:F2} " +
                $"targetX={lockedTargetX:F2} " +
                $"travelEndX={travelEndX:F2} " +
                $"collider={FormatBounds(hitCollider)}"
            );

        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debuggerHitProfileActive)
        {
            Debug.Log(
                "[DBG_PROJECTILE_GEOMETRY] " +
                $"rootWorld={transform.position:F2} " +
                $"visualBoundsCenter={GetVisualBoundsCenter():F2} " +
                $"visualBoundsSize={GetVisualBoundsSize():F2} " +
                $"colliderLocalOffset={hitCollider.offset:F2} " +
                $"colliderLocalSize={hitCollider.size:F2} " +
                $"colliderWorldCenter={hitCollider.bounds.center:F2} " +
                $"colliderWorldSize={hitCollider.bounds.size:F2} " +
                $"heroBoundsCenter={GetDebuggerGeometryHeroCenter():F2} " +
                $"heroBoundsSize={GetDebuggerGeometryHeroSize():F2}"
            );
        }
#endif

        CheckInitialOverlap();
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
            travelEndX - body.position.x;

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
                    travelEndX,
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

        TryResolveContact(other, "TRIGGER");
    }

    private void TryResolveContact(
        Collider2D other,
        string source)
    {
        if (resolved || other == null || other == hitCollider)
        {
            return;
        }

        if (IsInBlockingLayer(other.gameObject.layer))
        {
            ResolveAndDestroy("BLOCKING");
            return;
        }

        if (!IsInTargetLayer(other.gameObject.layer))
        {
            return;
        }

        HeroController hero =
            other.GetComponentInParent<HeroController>();

        bool heroIsDashing =
            hero != null && hero.IsDashing;

        bool heroIsInvulnerable =
            hero != null && hero.IsInvulnerable;

        bool shouldLogContact = verboseProjectileLogging ||
            (hero != null && hero.IsVerboseDashLogging);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        shouldLogContact |= debuggerHitProfileActive;
#endif

        if (shouldLogContact)
        {
            Debug.Log(
                "[DBG_PROJECTILE_HIT] " +
                $"source={source}\n" +
                $"heroX={other.transform.position.x:F2}\n" +
                $"projectileX={transform.position.x:F2}\n" +
                $"targetX={lockedTargetX:F2}\n" +
                $"projectileBounds={FormatBounds(hitCollider)}\n" +
                $"heroBounds={FormatBounds(other)}\n" +
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

            ResolveAndDestroy("EVADED");
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

        ResolveAndDestroy("HIT");
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
                travelEndX,
                body.position.y
            );

        if (verboseProjectileLogging)
        {
            Debug.Log(
                "[DBG_PROJECTILE_END] reason=RANGE_END\n" +
                $"currentX={transform.position.x:F2}\n" +
                $"targetX={lockedTargetX:F2}\n" +
                $"travelEndX={travelEndX:F2}\n" +
                $"travel={GetTravelDistance():F2}\n" +
                "result=MISS"
            );
        }

        ResolveAndDestroy("RANGE_END");
    }

    private void ResolveAndDestroy(string reason)
    {
        if (resolved)
            return;

        resolved = true;
        hasLockedTarget = false;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        if (verboseProjectileLogging)
        {
            Debug.Log(
                "[DBG_PROJECTILE_END] " +
                $"reason={reason} " +
                $"spawnX={launchPosition.x:F2} " +
                $"endX={transform.position.x:F2} " +
                $"travel={GetTravelDistance():F2}"
            );
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
        if (!resolved && verboseProjectileLogging)
        {
            Debug.Log(
                "[DBG_PROJECTILE_END] " +
                "reason=LIFETIME_OR_EXTERNAL " +
                $"spawnX={launchPosition.x:F2} " +
                $"endX={transform.position.x:F2} " +
                $"travel={GetTravelDistance():F2}"
            );
        }

        if (body != null)
        {
            body.linearVelocity =
                Vector2.zero;
        }

        NotifyResolved();
    }

    private void ApplyColliderProfile()
    {
        if (hitCollider == null)
        {
            return;
        }

        hitCollider.size = debuggerHitProfileActive
            ? debuggerColliderSize
            : knightColliderSize;
        hitCollider.offset = debuggerHitProfileActive
            ? debuggerColliderOffset
            : knightColliderOffset;
    }

    private void CheckInitialOverlap()
    {
        if (!debuggerHitProfileActive || hitCollider == null || resolved)
        {
            return;
        }

        Physics2D.SyncTransforms();

        Collider2D[] overlaps = Physics2D.OverlapBoxAll(
            hitCollider.bounds.center,
            hitCollider.bounds.size,
            0f
        );

        foreach (Collider2D overlap in overlaps)
        {
            TryResolveContact(overlap, "INITIAL_OVERLAP");
            if (resolved)
            {
                return;
            }
        }
    }

    private float GetTravelDistance()
    {
        return Mathf.Abs(transform.position.x - launchPosition.x);
    }

    private static string FormatBounds(Collider2D collider)
    {
        if (collider == null)
        {
            return "none";
        }

        Bounds bounds = collider.bounds;
        return $"center={bounds.center:F2} size={bounds.size:F2}";
    }

    private Vector3 GetVisualBoundsCenter()
    {
        return visualRenderer != null
            ? visualRenderer.bounds.center
            : transform.position;
    }

    private Vector3 GetVisualBoundsSize()
    {
        return visualRenderer != null
            ? visualRenderer.bounds.size
            : Vector3.zero;
    }

    private Vector3 GetDebuggerGeometryHeroCenter()
    {
        return hasDebuggerGeometryHeroBounds
            ? debuggerGeometryHeroBounds.center
            : Vector3.zero;
    }

    private Vector3 GetDebuggerGeometryHeroSize()
    {
        return hasDebuggerGeometryHeroBounds
            ? debuggerGeometryHeroBounds.size
            : Vector3.zero;
    }
}
