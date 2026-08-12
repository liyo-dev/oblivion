using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

#if TMP_PRESENT
using TMPro;
#endif

[DisallowMultipleComponent]
public class MenuTextHighlight : MonoBehaviour
{
    [Header("Target visual (auto si vacío)")]
    public Graphic targetGraphic;

    [Header("Dueño de la selección (auto: Button padre)")]
    public GameObject selectionOwner;

    [Header("Colores")]
    public Color normalColor = new Color(1,1,1,0.85f);
    public Color starColor   = new Color(1f, 0.87f, 0.2f, 1f); // amarillo destacado

    [Header("Animación")]
    [Min(0f)] public float pulseDuration = 0.45f;
    [Min(0f)] public float twinkleScale  = 1.06f;
    [Min(0f)] public float twinkleTime   = 0.18f;

    Sequence _seq;
    Transform _t;
    bool _isHighlighted;

    void Awake()
    {
        _t = transform;

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
        // IMPORTANTE: leer EventSystem.current en cada frame (NO cachearlo en Awake). Este
        // componente se instancia en items creados/destruidos dinámicamente (QuestMainMenuUI.Rebuild)
        // y, si el EventSystem "current" cambia mientras tanto (p.ej. al cargar aditivamente una
        // escena de mundo con su propio EventSystem — ver PlayerInputManager.ConnectToEventSystemModule),
        // una referencia cacheada queda apuntando al EventSystem equivocado y el highlight deja de
        // reaccionar a la selección aunque la navegación funcione. MenuNavigator ya sigue este
        // patrón (lee EventSystem.current en vivo) por el mismo motivo.
        var es = EventSystem.current;
        if (!es || !selectionOwner) return;

        bool selected = es.currentSelectedGameObject == selectionOwner;
        if (selected != _isHighlighted)
        {
            _isHighlighted = selected;
            if (selected) HighlightOn();
            else HighlightOff();
        }
    }

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
