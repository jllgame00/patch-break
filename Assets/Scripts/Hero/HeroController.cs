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
    [SerializeField, Min(0f)] private float dashInvulnerabilityDuration = 0.65f;

    private Rigidbody2D body;

    private float moveInput;
    private float facingDirection = 1f;
    private float dashDirection;
    private float dashTimer;
    private float originalScaleX;
    private float nextDashTime;
    private float dashInvulnerableUntil;

    private bool isDashing;

    public float FacingDirection => facingDirection;
    public bool IsDashing => isDashing;
    public bool IsDashInvulnerable => Time.time < dashInvulnerableUntil;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        originalScaleX = Mathf.Abs(transform.localScale.x);
    }

    private void FixedUpdate()
    {
        if (isDashing)
        {
            UpdateDash();
            return;
        }

        body.linearVelocity = new Vector2(
            moveInput * moveSpeed,
            body.linearVelocity.y
        );
    }

    public void SetMoveInput(float input)
    {
        moveInput = Mathf.Clamp(input, -1f, 1f);

        if (Mathf.Approximately(moveInput, 0f))
            return;

        facingDirection = Mathf.Sign(moveInput);
        UpdateFacingVisual();
    }
    
    public bool TryApproach(Transform target)
    {
        if (target == null || isDashing)
            return false;

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
        return StartDash(direction, ignoreCooldown: true);
    }

    private bool StartDash(float direction, bool ignoreCooldown)
    {
        if (isDashing)
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

        dashInvulnerableUntil = Mathf.Max(
            dashInvulnerableUntil,
            Time.time + dashInvulnerabilityDuration
        );
        
        isDashing = true;
        moveInput = 0f;

        body.linearVelocity = new Vector2(
            dashDirection * dashSpeed,
            body.linearVelocity.y
        );

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
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    private void UpdateFacingVisual()
    {
        Vector3 scale = transform.localScale;
        scale.x = originalScaleX * facingDirection;
        transform.localScale = scale;
    }
}