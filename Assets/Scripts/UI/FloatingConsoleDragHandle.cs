using UnityEngine;
using UnityEngine.EventSystems;

public sealed class FloatingConsoleDragHandle : MonoBehaviour,
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
        window?.BeginWindowDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        window?.DragWindow(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        window?.EndWindowDrag();
    }
}
