using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

#if TMP_PRESENT
using TMPro;
#endif

[DisallowMultipleComponent]
public class MenuTextHighlight : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    [Header("Target visual (auto si vacío)")]
    public Graphic targetGraphic;

    [Header("Dueño de la selección (auto: Button padre)")]
    public GameObject selectionOwner;

    [Header("Colores")]
    public Color normalColor = new Color(1,1,1,0.85f);
    public Color starColor   = new Color(0.55f, 0.95f, 1f, 1f); // cian “vórtice”

    [Header("Animación")]
    [Min(0f)] public float pulseDuration = 0.45f;
    [Min(0f)] public float twinkleScale  = 1.06f;
    [Min(0f)] public float twinkleTime   = 0.18f;

    Sequence _seq;
    Transform _t;
    bool _isHighlighted;
    EventSystem _es;

    void Awake()
    {
        _t = transform;
        _es = EventSystem.current;

        // Auto-asigna graphic (TMP o Text)
        if (!targetGraphic)
        {
#if TMP_PRESENT
            var tmp = GetComponent<TMP_Text>();
            if (tmp) targetGraphic = tmp as Graphic;
#endif
            if (!targetGraphic) targetGraphic = GetComponent<Text>();
            if (!targetGraphic) targetGraphic = GetComponent<Graphic>();
        }

        // Auto-asigna dueño de selección: Button/Selectable padre
        if (!selectionOwner)
        {
            var sel = GetComponentInParent<Selectable>();
            if (sel) selectionOwner = sel.gameObject;
            else selectionOwner = gameObject; // fallback
        }

        if (targetGraphic) targetGraphic.color = normalColor;
    }

    void OnDisable()
    {
        _seq?.Kill(); _seq = null;
        _t.DOKill();
        _t.localScale = Vector3.one;
        if (targetGraphic) targetGraphic.color = normalColor;
        _isHighlighted = false;
    }

    void Update()
    {
        // Soporta gamepad/teclado: ¿está seleccionado el dueño?
        if (!_es || !selectionOwner) return;

        bool selected = _es.currentSelectedGameObject == selectionOwner;
        if (selected != _isHighlighted)
        {
            _isHighlighted = selected;
            if (selected) HighlightOn();
            else HighlightOff();
        }
    }

    // Mouse
    public void OnPointerEnter(PointerEventData e) { _isHighlighted = true;  HighlightOn(); }
    public void OnPointerExit (PointerEventData e) { _isHighlighted = false; HighlightOff(); }

    void HighlightOn()
    {
        if (!targetGraphic) return;
        _seq?.Kill(); _t.DOKill();

        _seq = DOTween.Sequence();
        _seq.Join(targetGraphic.DOColor(starColor, pulseDuration).SetEase(Ease.OutQuad));
        _seq.Join(_t.DOScale(twinkleScale, twinkleTime).SetEase(Ease.OutCubic))
            .Append(_t.DOScale(1f, twinkleTime).SetEase(Ease.OutCubic));

        // “Respirar” mientras esté seleccionado
        _t.DOScale(1.02f, 0.9f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
    }

    void HighlightOff()
    {
        if (!targetGraphic) return;
        _seq?.Kill(); _t.DOKill();
        targetGraphic.DOColor(normalColor, 0.2f).SetEase(Ease.OutQuad);
        _t.localScale = Vector3.one;
    }
}
