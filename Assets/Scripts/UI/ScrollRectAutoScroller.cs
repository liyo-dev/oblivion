using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public static class ScrollRectAutoScroller
{
    // Duración del scroll suave
    const float Duration = 0.20f;
    const Ease EaseType = Ease.OutCubic;

    static Tweener _activeTween;

    public static void ScrollTo(ScrollRect scrollRect, RectTransform target, float padding)
    {
        if (scrollRect == null || target == null)
            return;

        var content  = scrollRect.content;
        if (content == null)
            return;

        var viewport = scrollRect.viewport != null
            ? scrollRect.viewport
            : scrollRect.transform as RectTransform;

        if (viewport == null)
            return;

        float contentHeight  = content.rect.height;
        float viewportHeight = viewport.rect.height;

        if (contentHeight <= viewportHeight + 0.01f)
            return;

        Vector3[] viewportCorners = new Vector3[4];
        Vector3[] targetCorners   = new Vector3[4];

        viewport.GetWorldCorners(viewportCorners);
        target.GetWorldCorners(targetCorners);

        float viewportTop    = viewportCorners[1].y;
        float viewportBottom = viewportCorners[0].y;

        float targetTop      = targetCorners[1].y;
        float targetBottom   = targetCorners[0].y;

        float delta = 0f;

        if (targetTop + padding > viewportTop)
            delta = (targetTop + padding) - viewportTop;
        else if (targetBottom - padding < viewportBottom)
            delta = (targetBottom - padding) - viewportBottom;
        else
            return; // ya está a la vista

        float normalizedDelta = delta / (contentHeight - viewportHeight);
        float newPos = scrollRect.verticalNormalizedPosition + normalizedDelta;
        newPos = Mathf.Clamp01(newPos);

        // Cancelamos la animación previa para evitar rebotes
        _activeTween?.Kill();

        _activeTween = DOTween.To(
            () => scrollRect.verticalNormalizedPosition,
            v => scrollRect.verticalNormalizedPosition = v,
            newPos,
            Duration
        )
        .SetEase(EaseType)
        .SetUpdate(true); // para que funcione en pausas/menus
    }
}
