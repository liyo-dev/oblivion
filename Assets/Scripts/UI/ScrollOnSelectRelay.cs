using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ScrollOnSelectRelay : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    public ScrollRect scrollRect;
    public RectTransform target;
    [SerializeField, Min(0f)] private float padding = 6f;

    // Evento cuando este objeto pasa a estar seleccionado (teclado/mand mando)
    public void OnSelect(BaseEventData eventData)
    {
        ScrollIntoView();
    }

    // Evento cuando el ratón entra en el objeto
    public void OnPointerEnter(PointerEventData eventData)
    {
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
