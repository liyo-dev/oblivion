using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Servicio global de mensajes HUD no bloqueantes (estilo "toast").
/// Se auto-crea al arrancar el juego; no requiere prefab ni setup en escena.
///
/// Uso:
///   HudToastService.Instance.Show("NPC_REQUIRES_WILL");
///   HudToastService.Instance.Show("NPC_REQUIRES_WILL", duration: 2.5f);
/// </summary>
public class HudToastService : MonoBehaviour
{
    public static HudToastService Instance { get; private set; }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif

    private GameObject   _panel;
    private CanvasGroup  _panelGroup;
    private TextMeshProUGUI _text;
    private Coroutine    _activeRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;

        var root = new GameObject("[HudToastService]");
        DontDestroyOnLoad(root);

        // Canvas overlay, por encima de todo
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Panel semitransparente centrado en la parte baja
        var panelGo = new GameObject("ToastPanel");
        panelGo.transform.SetParent(root.transform, false);

        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin    = new Vector2(0.2f, 0.06f);
        panelRect.anchorMax    = new Vector2(0.8f, 0.13f);
        panelRect.offsetMin    = Vector2.zero;
        panelRect.offsetMax    = Vector2.zero;

        var bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.65f);

        var panelGroup = panelGo.AddComponent<CanvasGroup>();

        // Texto interior
        var textGo = new GameObject("ToastText");
        textGo.transform.SetParent(panelGo.transform, false);

        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 4f);
        textRect.offsetMax = new Vector2(-12f, -4f);

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.alignment  = TextAlignmentOptions.Center;
        tmp.fontSize   = 22f;
        tmp.color      = Color.white;
        tmp.fontStyle  = FontStyles.Normal;

        panelGo.SetActive(false);

        var service = root.AddComponent<HudToastService>();
        service._panel      = panelGo;
        service._panelGroup = panelGroup;
        service._text       = tmp;

        Instance = service;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ────────────────────────────────────────────────────────────────────────
    #region API pública

    /// <summary>Muestra un mensaje localizado durante <paramref name="duration"/> segundos.</summary>
    public void Show(string locKey, float duration = 3f)
    {
        string msg = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get(locKey, locKey)
            : locKey;

        ShowRaw(msg, duration);
    }

    /// <summary>Muestra un mensaje literal (sin pasar por localización).</summary>
    public void ShowRaw(string message, float duration = 3f)
    {
        if (_panel == null) return;

        if (_activeRoutine != null)
            StopCoroutine(_activeRoutine);

        _text.text = message;
        _activeRoutine = StartCoroutine(ToastRoutine(duration));
    }

    #endregion

    // ────────────────────────────────────────────────────────────────────────
    #region Internals

    // Nota: cada paso comprueba MenuManager.AnyOpen() (pausa o cualquier otro menú) y, si hay
    // uno abierto, se congela con el toast invisible en vez de seguir avanzando/desvaneciendo.
    // Así el toast reaparece igual que estaba al cerrar el menú, en vez de quedar flotando por
    // encima de él o consumir su tiempo de vida mientras está tapado.
    private IEnumerator ToastRoutine(float duration)
    {
        _panel.SetActive(true);
        _panelGroup.alpha = 0f;

        // Fade in
        float t = 0f;
        while (t < 1f)
        {
            if (MenuManager.AnyOpen())
            {
                _panelGroup.alpha = 0f;
                yield return null;
                continue;
            }
            t += Time.unscaledDeltaTime / 0.25f;
            _panelGroup.alpha = Mathf.Clamp01(t);
            yield return null;
        }

        // Esperar (oculto y congelado mientras haya un menú abierto)
        bool wasMenuOpen = false;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            bool menuOpen = MenuManager.AnyOpen();
            if (menuOpen != wasMenuOpen)
            {
                _panelGroup.alpha = menuOpen ? 0f : 1f;
                wasMenuOpen = menuOpen;
            }
            if (!menuOpen)
                elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Fade out
        t = 1f;
        while (t > 0f)
        {
            if (MenuManager.AnyOpen())
            {
                _panelGroup.alpha = 0f;
                yield return null;
                continue;
            }
            t -= Time.unscaledDeltaTime / 0.35f;
            _panelGroup.alpha = Mathf.Clamp01(t);
            yield return null;
        }

        _panel.SetActive(false);
        _activeRoutine = null;
    }

    #endregion
}
