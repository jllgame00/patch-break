using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class HeroController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.15f;

    private Rigidbody2D body;

    private float moveInput;
    private float facingDirection = 1f;
    private float dashDirection;
    private float dashTimer;
    private float originalScaleX;

    private bool isDashing;

    public float FacingDirection => facingDirection;
    public bool IsDashing => isDashing;

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
        if (isDashing)
            return false;

        if (Mathf.Approximately(direction, 0f))
            direction = facingDirection;

        dashDirection = Mathf.Sign(direction);
        dashTimer = dashDuration;
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