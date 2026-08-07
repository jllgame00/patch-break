using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class WorldHealthBarUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform trackedTarget;
    [SerializeField] private Health targetHealth;
    [SerializeField] private Vector3 worldOffset =
        new(0f, 0.85f, 0f);

    [Header("UI")]
    [SerializeField] private Image fillImage;
    [SerializeField] private Color fillColor =
        new(0f, 0.85f, 0.9f, 1f);
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Tracking")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private GameObject hideWhileActive;

    private RectTransform barRect;
    private RectTransform parentRect;
    private Health subscribedHealth;
    private bool isVisible;

    private void Awake()
    {
        CacheComponents();
        DisableRaycastTargets();
    }

    private void OnEnable()
    {
        SubscribeToHealth();
    }

    private void Start()
    {
        if (!ValidateReferences())
        {
            SetVisible(false);
            enabled = false;
            return;
        }

        RefreshHealth();
        UpdatePositionAndVisibility();
    }

    private void LateUpdate()
    {
        UpdatePositionAndVisibility();
    }

    private void OnDisable()
    {
        UnsubscribeFromHealth();
    }

    private void OnValidate()
    {
        CacheComponents();
        ApplyFillColor();
        DisableRaycastTargets();
    }

    public void Configure(
        Transform target,
        Health health,
        Vector3 offset,
        Image healthFill,
        Color healthColor,
        Camera sourceCamera,
        Canvas ownerCanvas,
        CanvasGroup visibilityGroup,
        GameObject hideWhenActive)
    {
        UnsubscribeFromHealth();

        trackedTarget = target;
        targetHealth = health;
        worldOffset = offset;
        fillImage = healthFill;
        fillColor = healthColor;
        worldCamera = sourceCamera;
        rootCanvas = ownerCanvas;
        canvasGroup = visibilityGroup;
        hideWhileActive = hideWhenActive;

        CacheComponents();
        ApplyFillColor();
        DisableRaycastTargets();

        if (Application.isPlaying && isActiveAndEnabled)
        {
            SubscribeToHealth();
            RefreshHealth();
        }
    }

    private void CacheComponents()
    {
        if (barRect == null)
        {
            barRect = transform as RectTransform;
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        parentRect = barRect != null
            ? barRect.parent as RectTransform
            : null;
    }

    private bool ValidateReferences()
    {
        CacheComponents();

        if (trackedTarget != null &&
            targetHealth != null &&
            fillImage != null &&
            worldCamera != null &&
            rootCanvas != null &&
            canvasGroup != null &&
            barRect != null &&
            parentRect != null)
        {
            return true;
        }

        Debug.LogError(
            $"{name}: World health bar references are incomplete."
        );
        return false;
    }

    private void SubscribeToHealth()
    {
        if (!Application.isPlaying ||
            targetHealth == null ||
            subscribedHealth == targetHealth)
        {
            return;
        }

        UnsubscribeFromHealth();
        subscribedHealth = targetHealth;
        subscribedHealth.HealthChanged += HandleHealthChanged;
        subscribedHealth.Died += HandleTargetDied;
    }

    private void UnsubscribeFromHealth()
    {
        if (subscribedHealth != null)
        {
            subscribedHealth.HealthChanged -= HandleHealthChanged;
            subscribedHealth.Died -= HandleTargetDied;
        }

        subscribedHealth = null;
    }

    private void HandleHealthChanged(Health health)
    {
        if (health == targetHealth)
        {
            RefreshHealth();
        }
    }

    private void HandleTargetDied(Health health)
    {
        if (health != targetHealth)
            return;

        RefreshHealth();
        SetVisible(false);
    }

    private void RefreshHealth()
    {
        if (fillImage == null || targetHealth == null)
            return;

        float ratio = targetHealth.MaxHealth > 0
            ? (float)targetHealth.CurrentHealth /
              targetHealth.MaxHealth
            : 0f;

        fillImage.fillAmount = Mathf.Clamp01(ratio);

        if (targetHealth.IsDead ||
            targetHealth.CurrentHealth <= 0)
        {
            SetVisible(false);
        }
    }

    private void UpdatePositionAndVisibility()
    {
        if (trackedTarget == null ||
            targetHealth == null ||
            worldCamera == null ||
            rootCanvas == null ||
            barRect == null ||
            parentRect == null ||
            !trackedTarget.gameObject.activeInHierarchy ||
            !targetHealth.isActiveAndEnabled ||
            targetHealth.IsDead ||
            targetHealth.CurrentHealth <= 0 ||
            (hideWhileActive != null &&
             hideWhileActive.activeInHierarchy))
        {
            SetVisible(false);
            return;
        }

        Vector3 worldPosition =
            trackedTarget.position + worldOffset;
        Vector3 screenPosition =
            worldCamera.WorldToScreenPoint(worldPosition);

        if (screenPosition.z <= 0f ||
            !worldCamera.pixelRect.Contains(screenPosition))
        {
            SetVisible(false);
            return;
        }

        Camera canvasCamera =
            rootCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay
                ? null
                : rootCanvas.worldCamera;

        RectTransform canvasRect =
            rootCanvas.transform as RectTransform;

        if (canvasRect == null ||
            !RectTransformUtility.RectangleContainsScreenPoint(
                canvasRect,
                screenPosition,
                canvasCamera) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                screenPosition,
                canvasCamera,
                out Vector2 localPosition))
        {
            SetVisible(false);
            return;
        }

        barRect.anchoredPosition = localPosition;
        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
            return;

        if (isVisible != visible ||
            !Mathf.Approximately(
                canvasGroup.alpha,
                visible ? 1f : 0f))
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            isVisible = visible;
        }

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void ApplyFillColor()
    {
        if (fillImage != null)
        {
            fillImage.color = fillColor;
        }
    }

    private void DisableRaycastTargets()
    {
        Graphic[] graphics =
            GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            graphic.raycastTarget = false;
        }

        if (Application.isPlaying && canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
