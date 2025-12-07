using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Ensures that a ScrollRect scrolls when the associated selectable receives focus.
/// </summary>
public sealed class ScrollOnSelectRelay : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    public ScrollRect scrollRect;
    public RectTransform target;
    [SerializeField, Min(0f)] private float padding = 6f;

    public void OnSelect(BaseEventData eventData)
    {
        ScrollIntoView();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Permite que el uso con ratón también alinee la lista.
        ScrollIntoView();
    }

    void ScrollIntoView()
    {
        if (scrollRect == null)
            return;

        var rectTransform = target != null ? target : transform as RectTransform;
        ScrollRectAutoScroller.ScrollTo(scrollRect, rectTransform, padding);
    }
}
