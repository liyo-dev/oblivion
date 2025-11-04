using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if DOTWEEN_ENABLED || true
using DG.Tweening;
#endif

/// Simple FX de feedback al navegar entre botones (escala/alpha)
[RequireComponent(typeof(Selectable))]
public class ChoiceButtonFx : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [Header("Scale FX")]
    public bool useScaleFx = true;
    public float selectedScale = 1.08f;
    public float tweenDuration = 0.15f;

    [Header("Graphic FX (opcional)")]
    public Graphic targetGraphic;
    public Color selectedColor = Color.white;
    public Color normalColor = new Color(1f,1f,1f,0.85f);

    Vector3 _baseScale;

    void Awake()
    {
        _baseScale = transform.localScale;
        if (targetGraphic == null) targetGraphic = GetComponentInChildren<Graphic>();
    }

    public void OnSelect(BaseEventData eventData)
    {
#if DOTWEEN_ENABLED || true
        if (useScaleFx)
            transform.DOScale(_baseScale * selectedScale, tweenDuration).SetUpdate(true);
#endif
        if (targetGraphic) targetGraphic.color = selectedColor;
    }

    public void OnDeselect(BaseEventData eventData)
    {
#if DOTWEEN_ENABLED || true
        if (useScaleFx)
            transform.DOScale(_baseScale, tweenDuration).SetUpdate(true);
#endif
        if (targetGraphic) targetGraphic.color = normalColor;
    }
}

