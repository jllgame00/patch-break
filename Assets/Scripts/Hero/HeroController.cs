using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class HeroController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.15f;

    private Rigidbody2D body;
    private float moveInput;
    private float dashTimer;
    private bool isDashing;
    private float facingDirection = 1f;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        if (!Mathf.Approximately(moveInput, 0f))
        {
            facingDirection = Mathf.Sign(moveInput);
        }

        if (Input.GetKeyDown(KeyCode.K) && !isDashing)
        {
            StartDash();
        }
    }

    private void FixedUpdate()
    {
        if (isDashing)
        {
            dashTimer -= Time.fixedDeltaTime;

            if (dashTimer <= 0f)
            {
                isDashing = false;
            }

            return;
        }

        body.linearVelocity = new Vector2(
            moveInput * moveSpeed,
            body.linearVelocity.y
        );
    }

    private void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;

        body.linearVelocity = new Vector2(
            facingDirection * dashSpeed,
            body.linearVelocity.y
        );
    }
}