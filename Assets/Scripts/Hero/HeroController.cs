using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class HeroController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 0.75f;

    [Header("Dash Back")]
    // DashForward keeps the established speed. DashBack receives its own
    // travel distance so it remains a meaningful spatial dodge in every
    // encounter without changing forward-engage behavior.
    [SerializeField, Min(0.1f)]
    private float dashBackDistance = 2.3f;

    [SerializeField, Range(0f, 0.05f)]
    private float postDashInvulnerabilityGrace;

    private Rigidbody2D body;
    private Collider2D heroCollider;

    private float moveInput;
    private float facingDirection = 1f;
    private float dashDirection;
    private float activeDashSpeed;
    private float dashTimer;
    private float originalScaleX;
    private float nextDashTime;
    private float invulnerableUntil;
    private float recoilDirection;
    private float recoilSpeed;
    private float recoilTimer;
    private float staggerUntil;

    private bool isDashing;
    private bool isRecoiling;
    private bool waitingForInvulnerabilityEnd;
    private bool activeDashIsBack;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private float dashBackStartX;
    private float dashBackProjectileStartX;
    private Bounds dashBackProjectileStartBounds;
    private Collider2D dashBackProjectileCollider;
    private bool pendingDashBackDiagnostics;
#endif

    public float FacingDirection => facingDirection;
    public float DashBackDistance => dashBackDistance;
    public bool IsDashing => isDashing;
    public bool IsInvulnerable =>
        isDashing || Time.time < invulnerableUntil;
    public bool IsStaggered =>
        isRecoiling || Time.time < staggerUntil;
    public bool IsVerboseDashLogging =>
        actionExecutor != null &&
        actionExecutor.VerboseDashLogging;

    private HeroActionExecutor actionExecutor;
    private CharacterPoseController poseController;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        heroCollider = GetComponent<Collider2D>();
        actionExecutor = GetComponent<HeroActionExecutor>();
        poseController = GetComponent<CharacterPoseController>();
        originalScaleX = Mathf.Abs(transform.localScale.x);
    }

    private void Update()
    {
        if (!waitingForInvulnerabilityEnd ||
            Time.time < invulnerableUntil)
        {
            return;
        }

        waitingForInvulnerabilityEnd = false;
        invulnerableUntil = 0f;
        LogInvulnerabilityEnd();
    }

    private void FixedUpdate()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // MovePosition applies during physics simulation. Defer this one-shot
        // report until the following fixed callback so it observes the final
        // fractional dash position and the resulting collider bounds.
        if (pendingDashBackDiagnostics)
        {
            pendingDashBackDiagnostics = false;
            LogDashBackDiagnostics();
        }
#endif

        if (isRecoiling)
        {
            UpdateGuardRecoil();
            return;
        }

        if (isDashing)
        {
            UpdateDash();
            return;
        }

        if (IsStaggered)
        {
            body.linearVelocity = new Vector2(
                0f,
                body.linearVelocity.y
            );

            return;
        }

        // This is the authoritative normal-movement path. Reassert the
        // already-requested visual state here so a one-time setup stop cannot
        // leave a moving Hero on the Ready pose. Dashes, recoil, stagger, and
        // attacks return above or clear moveInput through their existing code.
        if (!Mathf.Approximately(moveInput, 0f))
        {
            poseController?.PlayWalk();
        }

        body.linearVelocity = new Vector2(
            moveInput * moveSpeed,
            body.linearVelocity.y
        );
    }

    public void SetMoveInput(float input)
    {
        if (IsStaggered)
        {
            return;
        }

        moveInput = Mathf.Clamp(input, -1f, 1f);

        if (Mathf.Approximately(moveInput, 0f))
        {
            poseController?.StopWalk();
            return;
        }

        FaceDirection(moveInput);
        poseController?.PlayWalk();
    }

    public void FaceDirection(float direction)
    {
        if (Mathf.Approximately(direction, 0f))
            return;

        facingDirection = Mathf.Sign(direction);
        UpdateFacingVisual();
    }
    
    public bool TryApproach(Transform target)
    {
        if (target == null ||
            isDashing ||
            IsStaggered)
        {
            return false;
        }

        float horizontalDistance =
            target.position.x - transform.position.x;

        if (Mathf.Abs(horizontalDistance) <= 0.05f)
        {
            StopMoving();
            return false;
        }

        SetMoveInput(Mathf.Sign(horizontalDistance));
        return true;
    }

    public void StopMoving()
    {
        moveInput = 0f;
        poseController?.StopWalk();

        body.linearVelocity = new Vector2(
            0f,
            body.linearVelocity.y
        );
    }

    public bool TryDash(float direction)
    {
        return StartDash(
            direction,
            ignoreCooldown: false,
            dashSpeed,
            isDashBack: false
        );
    }

    public bool ForceDash(float direction)
    {
        if (isDashing)
        {
            return false;
        }

        ClearGuardRecoilAndStagger();

        return StartDash(
            direction,
            ignoreCooldown: true,
            dashSpeed,
            isDashBack: false
        );
    }

    public bool TryDashBack(float direction)
    {
        return StartDash(
            direction,
            ignoreCooldown: false,
            GetDashBackSpeed(),
            isDashBack: true
        );
    }

    public bool ForceDashBack(float direction)
    {
        if (isDashing)
        {
            return false;
        }

        ClearGuardRecoilAndStagger();

        return StartDash(
            direction,
            ignoreCooldown: true,
            GetDashBackSpeed(),
            isDashBack: true
        );
    }

    public void ApplyGuardRecoil(
        Transform source,
        float recoilDistance,
        float recoilDuration,
        float staggerDuration)
    {
        CancelDashState();
        StopMoving();

        recoilDirection = GetDirectionAwayFrom(source);

        staggerUntil = Mathf.Max(
            staggerUntil,
            Time.time + Mathf.Max(0f, staggerDuration)
        );

        if (recoilDistance <= 0f ||
            recoilDuration <= 0f)
        {
            isRecoiling = false;
            recoilTimer = 0f;
            recoilSpeed = 0f;
            return;
        }

        recoilSpeed = recoilDistance / recoilDuration;
        recoilTimer = recoilDuration;
        isRecoiling = true;

        body.linearVelocity = new Vector2(
            recoilDirection * recoilSpeed,
            body.linearVelocity.y
        );
    }

    public void ClearGuardRecoilAndStagger()
    {
        isRecoiling = false;
        recoilTimer = 0f;
        recoilSpeed = 0f;
        staggerUntil = 0f;

        StopMoving();
    }

    public void StopAllMovement()
    {
        CancelDashState();
        ClearGuardRecoilAndStagger();
    }

    private bool StartDash(
        float direction,
        bool ignoreCooldown,
        float requestedDashSpeed,
        bool isDashBack)
    {
        if (isDashing || IsStaggered)
        {
            return false;
        }

        if (!ignoreCooldown && Time.time < nextDashTime)
        {
            return false;
        }

        if (Mathf.Approximately(direction, 0f))
        {
            direction = facingDirection;
        }

        dashDirection = Mathf.Sign(direction);
        activeDashSpeed = Mathf.Max(0.1f, requestedDashSpeed);
        activeDashIsBack = isDashBack;
        dashTimer = dashDuration;
        nextDashTime = Time.time + dashCooldown;
        invulnerableUntil = 0f;
        waitingForInvulnerabilityEnd = false;
        isDashing = true;
        moveInput = 0f;
        poseController?.StopWalk();

        body.linearVelocity = new Vector2(
            dashDirection * activeDashSpeed,
            body.linearVelocity.y
        );

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (activeDashIsBack)
        {
            BeginDashBackDiagnostics();
        }
#endif

        LogDashStart();

        return true;
    }

    private void UpdateDash()
    {
        // A dash can end partway through a physics tick (for example,
        // 0.15 seconds with a 0.02 second fixed step). Preserve the intended
        // distance by applying that final fractional step explicitly instead
        // of silently dropping it when velocity is cleared.
        float finalStepDuration = Mathf.Min(
            Time.fixedDeltaTime,
            Mathf.Max(0f, dashTimer)
        );
        dashTimer -= Time.fixedDeltaTime;

        body.linearVelocity = new Vector2(
            dashDirection * activeDashSpeed,
            body.linearVelocity.y
        );

        if (dashTimer > 0f)
            return;

        isDashing = false;
        body.linearVelocity = new Vector2(
            0f,
            body.linearVelocity.y
        );

        if (finalStepDuration > 0f)
        {
            body.MovePosition(
                body.position +
                Vector2.right *
                dashDirection *
                activeDashSpeed *
                finalStepDuration
            );
        }

        invulnerableUntil =
            Time.time + postDashInvulnerabilityGrace;

        waitingForInvulnerabilityEnd =
            postDashInvulnerabilityGrace > 0f;

        LogDashMovementEnd();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (activeDashIsBack)
        {
            pendingDashBackDiagnostics = true;
        }
#endif

        if (!waitingForInvulnerabilityEnd)
        {
            invulnerableUntil = 0f;
            LogInvulnerabilityEnd();
        }

        activeDashSpeed = 0f;
        activeDashIsBack = false;
    }

    private float GetDashBackSpeed()
    {
        return dashBackDistance /
            Mathf.Max(0.01f, dashDuration);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void BeginDashBackDiagnostics()
    {
        dashBackStartX = transform.position.x;
        dashBackProjectileStartX = float.NaN;
        dashBackProjectileStartBounds = default;
        dashBackProjectileCollider = null;

        KnightProjectile projectile =
            Object.FindFirstObjectByType<KnightProjectile>();
        if (projectile == null)
        {
            return;
        }

        dashBackProjectileCollider =
            projectile.GetComponent<Collider2D>();
        dashBackProjectileStartX = projectile.transform.position.x;

        if (dashBackProjectileCollider != null)
        {
            dashBackProjectileStartBounds =
                dashBackProjectileCollider.bounds;
        }
    }

    private void LogDashBackDiagnostics()
    {
        Bounds heroBounds = heroCollider != null
            ? heroCollider.bounds
            : default;
        bool projectileAlive = dashBackProjectileCollider != null;
        Bounds projectileBounds = projectileAlive
            ? dashBackProjectileCollider.bounds
            : default;
        float projectileEndX = projectileAlive
            ? dashBackProjectileCollider.transform.position.x
            : float.NaN;
        float separation = projectileAlive && heroCollider != null
            ? GetHorizontalSeparation(heroBounds, projectileBounds)
            : float.NaN;

        Debug.Log(
            "[DBG_DASH_BACK] " +
            $"startX={dashBackStartX:F2} " +
            $"endX={transform.position.x:F2} " +
            $"actualDistance={Mathf.Abs(transform.position.x - dashBackStartX):F2} " +
            $"duration={dashDuration:F2} " +
            $"projectileXAtStart={dashBackProjectileStartX:F2} " +
            $"projectileXAtEnd={projectileEndX:F2} " +
            $"projectileBoundsAtStart={FormatBounds(dashBackProjectileStartBounds)} " +
            $"heroBoundsAfter={FormatBounds(heroBounds)} " +
            $"projectileBoundsAfter={FormatBounds(projectileBounds)} " +
            $"separation={separation:F2} " +
            $"projectileAlive={projectileAlive}"
        );
    }

    private static float GetHorizontalSeparation(
        Bounds first,
        Bounds second)
    {
        if (first.min.x > second.max.x)
        {
            return first.min.x - second.max.x;
        }

        if (second.min.x > first.max.x)
        {
            return second.min.x - first.max.x;
        }

        return -Mathf.Min(
            first.max.x - second.min.x,
            second.max.x - first.min.x
        );
    }

    private static string FormatBounds(Bounds bounds)
    {
        return $"center={bounds.center:F2} size={bounds.size:F2}";
    }
#endif

    private void UpdateGuardRecoil()
    {
        recoilTimer -= Time.fixedDeltaTime;

        body.linearVelocity = new Vector2(
            recoilDirection * recoilSpeed,
            body.linearVelocity.y
        );

        if (recoilTimer > 0f)
            return;

        isRecoiling = false;
        recoilSpeed = 0f;
        body.linearVelocity = new Vector2(
            0f,
            body.linearVelocity.y
        );
    }

    private float GetDirectionAwayFrom(Transform source)
    {
        if (source != null)
        {
            float deltaX =
                transform.position.x - source.position.x;

            if (Mathf.Abs(deltaX) >= 0.001f)
            {
                return Mathf.Sign(deltaX);
            }
        }

        return -facingDirection;
    }

    private void CancelDashState()
    {
        isDashing = false;
        dashTimer = 0f;
        activeDashSpeed = 0f;
        activeDashIsBack = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        pendingDashBackDiagnostics = false;
#endif
        invulnerableUntil = 0f;
        waitingForInvulnerabilityEnd = false;
    }

    private void LogDashStart()
    {
        if (!IsVerboseDashLogging)
            return;

        Debug.Log(
            "HERO DASH: START\n" +
            $"time={Time.time:F3}\n" +
            $"heroX={transform.position.x:F2}\n" +
            $"direction={dashDirection:F0}\n" +
            $"speed={activeDashSpeed:F2}\n" +
            $"isDashing={isDashing}\n" +
            $"isInvulnerable={IsInvulnerable}"
        );
    }

    private void LogDashMovementEnd()
    {
        if (!IsVerboseDashLogging)
            return;

        Debug.Log(
            "HERO DASH: MOVEMENT END\n" +
            $"time={Time.time:F3}\n" +
            $"velocityX={body.linearVelocity.x:F2}\n" +
            $"isDashing={isDashing}\n" +
            $"isInvulnerable={IsInvulnerable}"
        );
    }

    private void LogInvulnerabilityEnd()
    {
        if (!IsVerboseDashLogging)
            return;

        Debug.Log(
            "HERO DASH: INVULNERABILITY END\n" +
            $"time={Time.time:F3}\n" +
            $"isDashing={isDashing}\n" +
            $"isInvulnerable={IsInvulnerable}"
        );
    }

    private void UpdateFacingVisual()
    {
        Vector3 scale = transform.localScale;
        scale.x = originalScaleX * facingDirection;
        transform.localScale = scale;
    }

    private void OnDisable()
    {
        StopAllMovement();
    }
}
