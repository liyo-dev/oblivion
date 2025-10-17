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
    public Color highlightColor = new Color(0.95f, 0.9f, 0.7f);

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
            _shadow.DOPunchEffectDistance(punchStrength, punchDuration, punchVibrato, 0.5f)
                   .SetUpdate(true)
                   .OnComplete(() => _shadow.effectDistance = orig);
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
