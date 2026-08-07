using UnityEngine;
using UnityEngine.EventSystems;

public sealed class FloatingConsoleResizeHandle : MonoBehaviour,
    IPointerDownHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [SerializeField] private FloatingConsoleWindow window;

    public void Configure(FloatingConsoleWindow configuredWindow)
    {
        window = configuredWindow;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        window?.BringToFront();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        window?.BeginResize(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        window?.ResizeWindow(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        window?.EndResize();
    }
}
