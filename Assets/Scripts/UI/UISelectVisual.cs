using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

[RequireComponent(typeof(Selectable))]
public class UISelectVisual : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Visuals")]
    public Graphic targetGraphic;
    public Color normalColor = Color.white;
    public Color highlightColor = new Color(1f, 0.92f, 0.16f);

    [Header("Scale")]
    public float selectedScale = 1.1f;
    public float animDuration = 0.12f;

    [Header("Extras")]
    public bool enablePulse = true;
    public float pulseScale = 1.03f;
    public float pulseSpeed = 1.2f;

    public bool enableShadowPunch = true;
    public Vector2 punchStrength = new Vector2(5f, 5f);
    public int punchVibrato = 6;
    public float punchDuration = 0.2f;

    Tween _scaleTween;
    Tween _colorTween;
    Tween _pulseTween;
    Tween _punchTween;
    Vector3 _baseScale;
    Shadow _shadow;

    void Awake()
    {
        if (!targetGraphic)
        {
            targetGraphic = GetComponent<Graphic>() ?? GetComponentInChildren<Graphic>(true);
            if (!targetGraphic)
            {
                var sel = GetComponent<Selectable>();
                if (sel) targetGraphic = sel.targetGraphic;
            }
        }
        _baseScale = transform.localScale;
        if (targetGraphic) targetGraphic.color = normalColor;
        _shadow = GetComponent<Shadow>();
    }

    void OnEnable()
    {
        KillTweens();
        transform.localScale = _baseScale;
        if (targetGraphic) targetGraphic.color = normalColor;
    }

    void OnDisable() => KillTweens();

    void KillTweens()
    {
        _scaleTween?.Kill();
        _colorTween?.Kill();
        _pulseTween?.Kill();
        _punchTween?.Kill();
    }

    public void OnSelect(BaseEventData eventData) => PlaySelect(true);
    public void OnDeselect(BaseEventData eventData) => PlaySelect(false);
    public void OnPointerEnter(PointerEventData eventData) => EventSystem.current?.SetSelectedGameObject(gameObject);
    public void OnPointerExit(PointerEventData eventData) { }

    void PlaySelect(bool selected)
    {
        KillTweens();
        var scaleTarget = _baseScale * (selected ? selectedScale : 1f);
        _scaleTween = transform.DOScale(scaleTarget, animDuration).SetEase(Ease.OutCubic).SetUpdate(true);

        if (targetGraphic)
        {
            var col = selected ? highlightColor : normalColor;
            _colorTween = targetGraphic.DOColor(col, animDuration * 0.9f).SetUpdate(true);
        }

        if (selected && enablePulse)
        {
            _pulseTween = transform
                .DOScale(scaleTarget * pulseScale, 0.5f / pulseSpeed)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        if (selected && enableShadowPunch && _shadow)
        {
            var orig = _shadow.effectDistance;
            var shadowRef = _shadow;
            // Guardado en _punchTween (y no descartado como antes) para que KillTweens()
            // en OnDisable/OnEnable pueda detenerlo si el botón se desactiva/destruye a
            // mitad de la animación: si no, DOTween sigue intentando escribir
            // shadowRef.effectDistance tras la destrucción del objeto y dispara el warning
            // "The object of type 'UnityEngine.UI.Shadow' has been destroyed...".
            _punchTween = shadowRef.DOPunchEffectDistance(punchStrength, punchDuration, punchVibrato, 0.5f)
                   .SetUpdate(true)
                   .OnComplete(() =>
                   {
                       if (shadowRef) shadowRef.effectDistance = orig;
                   });
        }
    }
}

public static class DOTweenShadowExtensions
{
    public static Tweener DOPunchEffectDistance(this Shadow shadow, Vector2 strength, float duration, int vibrato, float elasticity)
    {
        Vector2 start = shadow.effectDistance;
        return DOTween.Punch(() => shadow.effectDistance, x => shadow.effectDistance = x, strength, duration, vibrato, elasticity).SetTarget(shadow);
    }
}
