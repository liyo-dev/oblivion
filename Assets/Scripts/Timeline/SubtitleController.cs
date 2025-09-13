using UnityEngine;
using TMPro;
using DG.Tweening;

public class SubtitleController : MonoBehaviour
{
    public TextMeshProUGUI subtitleTMP;
    public CanvasGroup canvasGroup;
    public float fadeDuration = 0.35f;
    public float targetAlpha = 1f;
    [Range(0f, 1f)] public float backgroundAlpha = 0.40f;

    Tween _currentTween;

    void Awake()
    {
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup) canvasGroup.alpha = 0f;
        var img = GetComponent<UnityEngine.UI.Image>();
        if (img != null)
        {
            var c = img.color;
            c.a = backgroundAlpha;
            img.color = c;
        }
    }

    public void ShowLine(string text)
    {
        if (!subtitleTMP || !canvasGroup) return;
        subtitleTMP.text = text;
        _currentTween?.Kill();
        _currentTween = canvasGroup.DOFade(targetAlpha, fadeDuration).SetUpdate(true);
    }

    public void ShowLineById(string id)
    {
        var text = LocalizationManager.Instance?.Get(id, id) ?? id;
        ShowLine(text);
    }

    public void ShowLineTimed(string text, float holdSeconds)
    {
        ShowLine(text);
        _currentTween = DOVirtual.DelayedCall(holdSeconds, Hide).SetUpdate(true);
    }

    public void Hide()
    {
        if (!canvasGroup) return;
        _currentTween?.Kill();
        _currentTween = canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true);
    }

    void OnDisable()
    {
        _currentTween?.Kill();
        if (canvasGroup) canvasGroup.alpha = 0f;
    }
}