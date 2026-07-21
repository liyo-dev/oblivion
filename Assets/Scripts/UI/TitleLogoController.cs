using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador del overlay de logo/título del juego.
/// Crea un Canvas persistente (mismo patrón que FS_ScreenFade en FeedbackService) con
/// una imagen de logo y un subtítulo opcional, y expone una corrutina de fade-in →
/// retención → fade-out.
///
/// Pensado para momentos de "revelación de título" durante la partida
/// (ver KingdomExitTransitionNode), no para la pantalla de arranque del juego.
/// </summary>
public sealed class TitleLogoController : MonoBehaviour
{
    public static TitleLogoController Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }
#endif

    Canvas _canvas;
    CanvasGroup _canvasGroup;
    Image _logoImage;
    Text _subtitleText;

    /// <summary>
    /// Devuelve la instancia activa, creándola si hace falta (igual que DefaultNarrativeSignals.EnsureInstance).
    /// </summary>
    public static TitleLogoController EnsureInstance()
    {
        if (Instance != null) return Instance;

        var existing = ServiceLocator.Get<TitleLogoController>(false);
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        var go = new GameObject("TitleLogoController");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<TitleLogoController>();
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        ServiceLocator.Register(this);
        BuildUI();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            ServiceLocator.Unregister(this);
            Instance = null;
        }
    }

    void BuildUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9997; // debajo del fade a negro (9998, FeedbackService) y del flash

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        var logoGo = new GameObject("Logo");
        logoGo.transform.SetParent(transform, false);
        _logoImage = logoGo.AddComponent<Image>();
        _logoImage.preserveAspect = true;
        _logoImage.color = Color.white;
        var logoRt = _logoImage.rectTransform;
        logoRt.anchorMin = new Vector2(0.5f, 0.35f);
        logoRt.anchorMax = new Vector2(0.5f, 0.65f);
        logoRt.sizeDelta = new Vector2(800, 300);
        logoRt.anchoredPosition = Vector2.zero;

        var subGo = new GameObject("Subtitle");
        subGo.transform.SetParent(transform, false);
        _subtitleText = subGo.AddComponent<Text>();
        _subtitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _subtitleText.alignment = TextAnchor.MiddleCenter;
        _subtitleText.fontSize = 32;
        _subtitleText.color = new Color(1f, 1f, 1f, 0.85f);
        var subRt = _subtitleText.rectTransform;
        subRt.anchorMin = new Vector2(0.5f, 0.28f);
        subRt.anchorMax = new Vector2(0.5f, 0.34f);
        subRt.sizeDelta = new Vector2(900, 60);
        subRt.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// Ejecuta fade-in → hold → fade-out del logo (y subtítulo opcional).
    /// Usa tiempo real (unscaled) para no depender de Time.timeScale durante cinemáticas.
    /// </summary>
    public IEnumerator ShowLogo(Sprite logo, string subtitle, float fadeInSeconds, float holdSeconds, float fadeOutSeconds)
    {
        if (logo != null) _logoImage.sprite = logo;
        _subtitleText.text = subtitle ?? string.Empty;
        _subtitleText.enabled = !string.IsNullOrEmpty(subtitle);

        _canvasGroup.alpha = 0f;

        yield return FadeCanvas(0f, 1f, Mathf.Max(0.01f, fadeInSeconds));

        if (holdSeconds > 0f)
            yield return new WaitForSecondsRealtime(holdSeconds);

        yield return FadeCanvas(1f, 0f, Mathf.Max(0.01f, fadeOutSeconds));
    }

    IEnumerator FadeCanvas(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        _canvasGroup.alpha = to;
    }
}
