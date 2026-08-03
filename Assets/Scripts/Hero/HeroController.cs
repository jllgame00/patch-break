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
    [SerializeField, Range(0f, 0.05f)]
    private float postDashInvulnerabilityGrace;

    private Rigidbody2D body;

    private float moveInput;
    private float facingDirection = 1f;
    private float dashDirection;
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

    public float FacingDirection => facingDirection;
    public bool IsDashing => isDashing;
    public bool IsInvulnerable =>
        isDashing || Time.time < invulnerableUntil;
    public bool IsStaggered =>
        isRecoiling || Time.time < staggerUntil;
    public bool IsVerboseDashLogging =>
        actionExecutor != null &&
        actionExecutor.VerboseDashLogging;

    private HeroActionExecutor actionExecutor;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        actionExecutor = GetComponent<HeroActionExecutor>();
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
            return;

        FaceDirection(moveInput);
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

        body.linearVelocity = new Vector2(
            0f,
            body.linearVelocity.y
        );
    }

    public bool TryDash(float direction)
    {
        return StartDash(direction, ignoreCooldown: false);
    }

    public bool ForceDash(float direction)
    {
        if (isDashing)
        {
            return false;
        }

        ClearGuardRecoilAndStagger();

        return StartDash(direction, ignoreCooldown: true);
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

    private bool StartDash(float direction, bool ignoreCooldown)
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
        dashTimer = dashDuration;
        nextDashTime = Time.time + dashCooldown;
        invulnerableUntil = 0f;
        waitingForInvulnerabilityEnd = false;
        isDashing = true;
        moveInput = 0f;

        body.linearVelocity = new Vector2(
            dashDirection * dashSpeed,
            body.linearVelocity.y
        );

        LogDashStart();

        return true;
    }

    private void UpdateDash()
    {
        dashTimer -= Time.fixedDeltaTime;

        body.linearVelocity = new Vector2(
            dashDirection * dashSpeed,
            body.linearVelocity.y
        );

        if (dashTimer > 0f)
            return;

        isDashing = false;
        body.linearVelocity = new Vector2(
            0f,
            body.linearVelocity.y
        );

        invulnerableUntil =
            Time.time + postDashInvulnerabilityGrace;

        waitingForInvulnerabilityEnd =
            postDashInvulnerabilityGrace > 0f;

        LogDashMovementEnd();

        if (!waitingForInvulnerabilityEnd)
        {
            invulnerableUntil = 0f;
            LogInvulnerabilityEnd();
        }
    }

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
