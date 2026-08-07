using UnityEngine;
using UnityEngine.EventSystems;

public sealed class FloatingConsoleWindow : MonoBehaviour,
    IPointerDownHandler
{
    [Header("Window References")]
    [SerializeField] private RectTransform windowRect;
    [SerializeField] private RectTransform clampArea;
    [SerializeField] private Transform frontLayer;

    [Header("Sizing")]
    [SerializeField] private Vector2 minimumSize = new Vector2(420f, 280f);
    [SerializeField, Range(0.5f, 1f)]
    private float maximumCanvasFraction = 0.9f;

    [Header("Visible Grab Area")]
    [SerializeField, Min(20f)] private float visibleTitleWidth = 56f;
    [SerializeField, Min(20f)] private float visibleTitleHeight = 42f;

    private Vector2 dragStartPointer;
    private Vector2 dragStartPosition;
    private Vector2 resizeStartPointer;
    private Vector2 resizeStartSize;
    private bool dragging;
    private bool resizing;

    private void Awake()
    {
        if (windowRect == null)
        {
            windowRect = transform as RectTransform;
        }

        if (clampArea == null && windowRect != null)
        {
            clampArea = windowRect.parent as RectTransform;
        }

        if (frontLayer == null)
        {
            frontLayer = clampArea;
        }
    }

    private void Start()
    {
        Canvas.ForceUpdateCanvases();
        ClampToVisibleTitleBar();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (Application.isPlaying && !dragging && !resizing)
        {
            ClampToVisibleTitleBar();
        }
    }

    public void Configure(
        RectTransform configuredClampArea,
        Transform configuredFrontLayer
    )
    {
        windowRect = transform as RectTransform;
        clampArea = configuredClampArea;
        frontLayer = configuredFrontLayer;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        BringToFront();
    }

    public void BringToFront()
    {
        if (windowRect == null ||
            frontLayer == null ||
            windowRect.parent != frontLayer)
        {
            return;
        }

        windowRect.SetAsLastSibling();
    }

    public void BeginWindowDrag(PointerEventData eventData)
    {
        if (!TryGetLocalPointerPosition(eventData, out dragStartPointer))
        {
            return;
        }

        BringToFront();
        dragStartPosition = windowRect.anchoredPosition;
        dragging = true;
    }

    public void DragWindow(PointerEventData eventData)
    {
        if (!dragging ||
            !TryGetLocalPointerPosition(eventData, out Vector2 pointer))
        {
            return;
        }

        windowRect.anchoredPosition = dragStartPosition +
                                      (pointer - dragStartPointer);
        ClampToVisibleTitleBar();
    }

    public void EndWindowDrag()
    {
        dragging = false;
    }

    public void BeginResize(PointerEventData eventData)
    {
        if (!TryGetLocalPointerPosition(eventData, out resizeStartPointer))
        {
            return;
        }

        BringToFront();
        resizeStartSize = windowRect.rect.size;
        resizing = true;
    }

    public void ResizeWindow(PointerEventData eventData)
    {
        if (!resizing ||
            !TryGetLocalPointerPosition(eventData, out Vector2 pointer))
        {
            return;
        }

        Vector2 pointerDelta = pointer - resizeStartPointer;
        Vector2 maximumSize = GetMaximumSize();
        Vector2 requestedSize = resizeStartSize + new Vector2(
            pointerDelta.x,
            -pointerDelta.y
        );

        float width = Mathf.Clamp(
            requestedSize.x,
            minimumSize.x,
            maximumSize.x
        );
        float height = Mathf.Clamp(
            requestedSize.y,
            minimumSize.y,
            maximumSize.y
        );

        windowRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            width
        );
        windowRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            height
        );
        ClampToVisibleTitleBar();
    }

    public void EndResize()
    {
        resizing = false;
    }

    private bool TryGetLocalPointerPosition(
        PointerEventData eventData,
        out Vector2 localPointerPosition
    )
    {
        localPointerPosition = default;

        return clampArea != null &&
               RectTransformUtility.ScreenPointToLocalPointInRectangle(
                   clampArea,
                   eventData.position,
                   eventData.pressEventCamera,
                   out localPointerPosition
               );
    }

    private Vector2 GetMaximumSize()
    {
        if (clampArea == null)
        {
            return minimumSize;
        }

        Rect areaRect = clampArea.rect;
        return new Vector2(
            Mathf.Max(minimumSize.x, areaRect.width * maximumCanvasFraction),
            Mathf.Max(
                minimumSize.y,
                areaRect.height * maximumCanvasFraction
            )
        );
    }

    private void ClampToVisibleTitleBar()
    {
        if (windowRect == null || clampArea == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        Vector3[] corners = new Vector3[4];
        windowRect.GetWorldCorners(corners);

        Vector2 bottomLeft = clampArea.InverseTransformPoint(corners[0]);
        Vector2 topLeft = clampArea.InverseTransformPoint(corners[1]);
        Vector2 topRight = clampArea.InverseTransformPoint(corners[2]);
        Rect areaRect = clampArea.rect;
        Vector2 offset = Vector2.zero;

        if (topRight.x < areaRect.xMin + visibleTitleWidth)
        {
            offset.x = areaRect.xMin + visibleTitleWidth - topRight.x;
        }
        else if (topLeft.x > areaRect.xMax - visibleTitleWidth)
        {
            offset.x = areaRect.xMax - visibleTitleWidth - topLeft.x;
        }

        if (topLeft.y < areaRect.yMin + visibleTitleHeight)
        {
            offset.y = areaRect.yMin + visibleTitleHeight - topLeft.y;
        }
        else if (topLeft.y - visibleTitleHeight > areaRect.yMax)
        {
            offset.y = areaRect.yMax -
                       (topLeft.y - visibleTitleHeight);
        }

        if (offset != Vector2.zero)
        {
            windowRect.anchoredPosition += offset;
        }
    }
}
