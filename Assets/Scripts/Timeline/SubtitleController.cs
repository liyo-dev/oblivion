using UnityEngine;
using TMPro;
using DG.Tweening;

public class SubtitleController : MonoBehaviour
{
    [Header("Refs")]
    public TextMeshProUGUI subtitleTMP;
    public CanvasGroup canvasGroup;     // en el Panel (faja)
    
    [Header("Timings")]
    [Tooltip("Duración del fade in/out")]
    public float fadeDuration = 0.35f;
    [Tooltip("Alpha objetivo al mostrar")]
    public float targetAlpha = 1f;

    [Header("Layout")]
    [Range(0f, 1f)] public float backgroundAlpha = 0.40f;

    Tween _currentTween;

    void Awake()
    {
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup) canvasGroup.alpha = 0f; // iniciar oculto
        // Asegura que la faja tenga el alpha deseado (solo afecta Image, no el CanvasGroup)
        var img = GetComponent<UnityEngine.UI.Image>();
        if (img != null)
        {
            var c = img.color;
            c.a = backgroundAlpha;
            img.color = c;
        }
    }

    /// <summary>
    /// Muestra la línea con fade in. Si holdSeconds > 0, se auto-oculta al terminar.
    /// Usar desde Timeline Signal: ShowLineTimed("texto", 6.0f) o ShowLine("texto") y luego Hide() con otra señal.
    /// </summary>
    public void ShowLine(string text)
    {
        if (!subtitleTMP || !canvasGroup) return;

        subtitleTMP.text = text;
        
        Debug.Log(subtitleTMP.text);

        // Cancela tweens previos para evitar solapados
        _currentTween?.Kill();

        // Fade in
        canvasGroup.alpha = Mathf.Clamp01(canvasGroup.alpha); // mantiene si está ya visible
        _currentTween = canvasGroup.DOFade(targetAlpha, fadeDuration)
                                   .SetUpdate(true); // true => ignora TimeScale si pausas
    }

    /// <summary>
    /// Igual que ShowLine pero con auto-hide tras 'holdSeconds'.
    /// </summary>
    public void ShowLineTimed(string text, float holdSeconds)
    {
        ShowLine(text);
        // Encadena auto-ocultado
        _currentTween = DOVirtual.DelayedCall(holdSeconds, () =>
        {
            Hide();
        }).SetUpdate(true);
    }

    public void Hide()
    {
        if (!canvasGroup) return;

        _currentTween?.Kill();
        _currentTween = canvasGroup.DOFade(0f, fadeDuration)
                                   .SetUpdate(true);
    }

    // Utilidad por si cambias de escena/Timeline
    void OnDisable()
    {
        _currentTween?.Kill();
        if (canvasGroup) canvasGroup.alpha = 0f;
    }
}
