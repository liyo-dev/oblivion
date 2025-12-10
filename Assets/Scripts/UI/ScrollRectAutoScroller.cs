using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helper utilities to keep a ScrollRect's content aligned with the currently selected widget.
/// </summary>
public static class ScrollRectAutoScroller
{
    /// <summary>
    /// Ensures that the given <paramref name="target"/> is visible inside the <paramref name="scrollRect"/>.
    /// The method adjusts the content's anchoredPosition when the selection moves near the limits.
    /// </summary>
    /// <param name="scrollRect">ScrollRect that contains the target.</param>
    /// <param name="target">UI element to keep inside the viewport.</param>
    /// <param name="padding">Extra padding in pixels applied to the viewport bounds.</param>
    public static void ScrollTo(ScrollRect scrollRect, RectTransform target, float padding = 6f)
    {
        if (scrollRect == null || target == null)
            return;

        var content = scrollRect.content;
        if (content == null)
            return;

        var viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
        if (viewport == null)
            return;

        if (!target.IsChildOf(content))
            return;

        Canvas.ForceUpdateCanvases();

        var itemBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(content, target);
        var viewBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(content, viewport);

        float contentHeight = content.rect.height;
        float viewportHeight = Mathf.Abs(viewBounds.size.y);
        if (contentHeight <= viewportHeight + 0.01f)
            return;

        float viewportCenter = (viewBounds.max.y + viewBounds.min.y) * 0.5f;
        float targetCenter = (itemBounds.max.y + itemBounds.min.y) * 0.5f;
        float offset = 0f;

        // Mantén el elemento seleccionado dentro de una "zona segura" centrada en el viewport.
        // Esto evita que la lista se quede fija en la parte inferior y que los elementos siguientes
        // queden ocultos: al sobrepasar la mitad del viewport, el scroll acompaña al foco.
        if (targetCenter > viewportCenter + padding)
        {
            offset = targetCenter - (viewportCenter + padding);
        }
        else if (targetCenter < viewportCenter - padding)
        {
            offset = targetCenter - (viewportCenter - padding);
        }

        // Asegura que el elemento siga siendo visible incluso si es más grande que la zona segura.
        if (Mathf.Approximately(offset, 0f))
        {
            float upperLimit = viewBounds.max.y - padding;
            float lowerLimit = viewBounds.min.y + padding;

            if (itemBounds.max.y > upperLimit)
                offset = itemBounds.max.y - upperLimit;
            else if (itemBounds.min.y < lowerLimit)
                offset = itemBounds.min.y - lowerLimit;

            if (Mathf.Approximately(offset, 0f))
                return;
        }

        var anchored = content.anchoredPosition;
        anchored.y = Mathf.Clamp(anchored.y + offset, 0f, Mathf.Max(0f, contentHeight - viewportHeight));
        content.anchoredPosition = anchored;
    }
}
