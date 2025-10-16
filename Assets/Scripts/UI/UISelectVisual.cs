using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

// Añade animación y cambio de color cuando un Selectable (Button) es seleccionado/deseleccionado
[RequireComponent(typeof(Selectable))]
public class UISelectVisual : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Visuals")]
    public Graphic targetGraphic; // si no se asigna, se busca automáticamente
    public Color normalColor = Color.white;
    public Color highlightColor = new Color(0.9f, 0.85f, 0.6f);

    [Header("Scale")]
    public float selectedScale = 1.08f;
    public float animDuration = 0.12f;

    Tween _scaleTween;
    Tween _colorTween;
    Vector3 _baseScale;

    void Awake()
    {
        if (targetGraphic == null)
        {
            // Preferir un Graphic en el mismo GameObject o en hijos
            targetGraphic = GetComponent<Graphic>() ?? GetComponentInChildren<Graphic>(true);
            // fallback: usar Selectable.targetGraphic si existe
            if (targetGraphic == null)
            {
                var sel = GetComponent<Selectable>();
                if (sel != null) targetGraphic = sel.targetGraphic;
            }
        }
        _baseScale = transform.localScale;

        // Inicializar color a normal
        if (targetGraphic != null)
        {
            targetGraphic.color = normalColor;
        }
    }

    void OnEnable()
    {
        // Reset visual
        transform.localScale = _baseScale;
        _scaleTween?.Kill();
        _colorTween?.Kill();
        if (targetGraphic != null) targetGraphic.color = normalColor;
    }

    void OnDisable()
    {
        _scaleTween?.Kill();
        _colorTween?.Kill();
    }

    public void OnSelect(BaseEventData eventData)
    {
        PlaySelect(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        PlaySelect(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Cuando el puntero entra, también seleccionar para navegación con ratón
        var es = EventSystem.current;
        if (es != null && es.currentInputModule != null)
        {
            es.SetSelectedGameObject(gameObject);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // nada extra
    }

    void PlaySelect(bool selected)
    {
        _scaleTween?.Kill();
        _colorTween?.Kill();

        Vector3 targetScale = _baseScale * (selected ? selectedScale : 1f);
        _scaleTween = transform.DOScale(targetScale, animDuration).SetUpdate(true).SetEase(Ease.OutCubic);

        if (targetGraphic != null)
        {
            Color to = selected ? highlightColor : normalColor;
            _colorTween = targetGraphic.DOColor(to, animDuration * 0.9f).SetUpdate(true);
        }
    }
}
