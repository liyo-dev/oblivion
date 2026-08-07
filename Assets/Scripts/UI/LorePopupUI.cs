using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Panel singleton que muestra entradas de lore automáticamente, una tras otra,
/// según la duración configurada en cada LoreEntry del LorePopupConfig.
/// No requiere input del jugador: cada frase avanza sola.
/// </summary>
public class LorePopupUI : MonoBehaviour
{
    public static LorePopupUI Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }
#endif

    [Header("Referencias UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform popupRoot;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI bodyText;

    [Header("Animación")]
    [SerializeField] private float animInDuration = 0.35f;
    [SerializeField] private float animOutDuration = 0.25f;
    [SerializeField] private Ease easeIn = Ease.OutBack;
    [SerializeField] private Vector2 slideInFromOffset = new Vector2(0f, -40f);
    [Tooltip("Fundido entre entradas consecutivas.")]
    [SerializeField] private float crossFadeDuration = 0.2f;

    public bool IsOpen => _isOpen;

    private bool _isOpen;
    private Coroutine _sequenceCoroutine;
    private Vector2 _anchoredPositionOpen;

    // Oculto temporalmente porque hay un menú (pausa, equipo, tienda...) abierto encima.
    // La secuencia sigue "viva" (no se detiene): solo se congela (WaitRealtimeUnlessPaused)
    // y se hace invisible, para reaparecer tal cual al cerrar el menú.
    private bool _hiddenByMenu;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);

        if (popupRoot != null) popupRoot.gameObject.SetActive(false);
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    void OnEnable()
    {
        // Mismo sistema que ya usan BossHealthBar/MinimapController para ocultarse mientras
        // hay un menú abierto (incluida la pausa). Se reutiliza en vez de duplicar lógica.
        MenuManager.MenuOpened += OnMenuOpened;
        MenuManager.MenuClosed += OnMenuClosed;
    }

    void OnDisable()
    {
        MenuManager.MenuOpened -= OnMenuOpened;
        MenuManager.MenuClosed -= OnMenuClosed;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        canvasGroup?.DOKill();
        popupRoot?.DOKill();
    }

    // ── API pública ───────────────────────────────────────────────────────────

    /// <summary>
    /// Muestra el popup y recorre todas las entradas automáticamente.
    /// Llama <paramref name="onClosed"/> cuando termina la última entrada.
    /// </summary>
    public void Show(LorePopupConfig config, Action onClosed)
    {
        if (config == null || config.entries == null || config.entries.Length == 0)
        {
            onClosed?.Invoke();
            return;
        }

        if (_isOpen) StopSequence();

        _isOpen = true;
        _sequenceCoroutine = StartCoroutine(RunSequence(config, onClosed));
    }

    /// <summary>Cierra el popup inmediatamente sin esperar a que acabe la secuencia.</summary>
    public void ForceClose()
    {
        if (!_isOpen) return;
        StopSequence();
        AnimateOut(null);
    }

    // ── MenuManager (pausa / cualquier menú) ────────────────────────────────────

    /// <summary>
    /// Al abrirse cualquier menú (pausa incluida) el popup debe desaparecer de la vista y dejar
    /// de bloquear clics, para que en pantalla solo queden los elementos del menú. La secuencia
    /// sigue viva por debajo — WaitRealtimeUnlessPaused ya la congela mientras dure.
    /// </summary>
    private void OnMenuOpened(MenuKind kind)
    {
        if (!_isOpen || _hiddenByMenu || canvasGroup == null) return;
        _hiddenByMenu = true;
        canvasGroup.DOKill();
        canvasGroup.DOFade(0f, 0.15f).SetUpdate(true);
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>Restaura el popup al cerrarse el último menú abierto, si la secuencia sigue activa.</summary>
    private void OnMenuClosed(MenuKind kind)
    {
        if (!_hiddenByMenu) return;
        if (MenuManager.AnyOpen()) return; // todavía queda otro menú abierto
        _hiddenByMenu = false;

        if (!_isOpen || canvasGroup == null) return; // la secuencia ya terminó mientras estaba oculto
        canvasGroup.DOKill();
        canvasGroup.DOFade(1f, 0.2f).SetUpdate(true);
        canvasGroup.blocksRaycasts = true;
    }

    // ── Secuencia automática ──────────────────────────────────────────────────

    IEnumerator RunSequence(LorePopupConfig config, Action onClosed)
    {
        if (config.delay > 0f)
            yield return WaitRealtimeUnlessPaused(config.delay);

        // Abrir popup con primera entrada
        DisplayEntry(config.entries[0]);
        yield return StartCoroutine(AnimateInRoutine());

        for (int i = 0; i < config.entries.Length; i++)
        {
            if (i > 0)
            {
                // Cross-fade entre entradas: fundido a 0, cambio de contenido, fundido a 1
                yield return canvasGroup.DOFade(0f, crossFadeDuration).SetUpdate(true).WaitForCompletion();
                DisplayEntry(config.entries[i]);
                yield return canvasGroup.DOFade(1f, crossFadeDuration).SetUpdate(true).WaitForCompletion();
            }

            float dur = Mathf.Max(config.entries[i].duration, 0.5f);
            yield return WaitRealtimeUnlessPaused(dur);
        }

        _isOpen = false;
        _sequenceCoroutine = null;

        yield return StartCoroutine(AnimateOutRoutine());
        onClosed?.Invoke();
    }

    /// <summary>
    /// Espera en tiempo real (inmune a hit-stops/slowmo con timeScale parcial), pero
    /// se congela mientras el juego está realmente en pausa (Time.timeScale == 0f,
    /// como al abrir el menú de equipo/inventario o la tienda) o mientras haya cualquier
    /// menú abierto vía MenuManager (p.ej. pausa, que no necesariamente toca timeScale).
    /// Sin esto el lore seguía avanzando y cerrándose solo mientras el resto del juego
    /// estaba parado, o mientras estaba oculto detrás de un menú.
    /// </summary>
    IEnumerator WaitRealtimeUnlessPaused(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (Time.timeScale > 0f && !MenuManager.AnyOpen())
                elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    void StopSequence()
    {
        if (_sequenceCoroutine != null)
        {
            StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = null;
        }
        _isOpen = false;
    }

    // ── Display ───────────────────────────────────────────────────────────────

    void DisplayEntry(LoreEntry e)
    {
        if (portraitImage != null)
        {
            portraitImage.sprite = e.portrait;
            portraitImage.enabled = e.portrait != null;
        }

        if (bodyText != null)
        {
            string text = e.text;
            if (!string.IsNullOrEmpty(e.textId))
                text = LocalizationManager.Instance?.Get(e.textId, e.text) ?? e.text;
            bodyText.text = text;
        }
    }

    // ── Animación ─────────────────────────────────────────────────────────────

    IEnumerator AnimateInRoutine()
    {
        if (popupRoot == null)
        {
            Debug.LogError("[LorePopupUI] ❌ 'popupRoot' no está asignado en el Inspector. El popup no será visible.");
            yield break;
        }
        if (canvasGroup == null)
        {
            Debug.LogError("[LorePopupUI] ❌ 'canvasGroup' no está asignado en el Inspector. El popup no será visible.");
            yield break;
        }

        if (_anchoredPositionOpen == Vector2.zero)
            _anchoredPositionOpen = popupRoot.anchoredPosition;

        canvasGroup?.DOKill();
        popupRoot.DOKill();

        popupRoot.gameObject.SetActive(true);
        popupRoot.anchoredPosition = _anchoredPositionOpen + slideInFromOffset;
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        popupRoot.DOAnchorPos(_anchoredPositionOpen, animInDuration).SetEase(easeIn).SetUpdate(true);
        yield return canvasGroup?.DOFade(1f, animInDuration * 0.8f).SetUpdate(true).WaitForCompletion();
    }

    IEnumerator AnimateOutRoutine()
    {
        if (popupRoot == null) yield break;

        canvasGroup?.DOKill();
        popupRoot.DOKill();

        yield return canvasGroup?.DOFade(0f, animOutDuration).SetUpdate(true).WaitForCompletion();
        popupRoot.gameObject.SetActive(false);
    }

    void AnimateOut(Action onComplete)
    {
        canvasGroup?.DOKill();
        popupRoot?.DOKill();
        canvasGroup?.DOFade(0f, animOutDuration).SetUpdate(true)
            .OnComplete(() =>
            {
                if (popupRoot != null) popupRoot.gameObject.SetActive(false);
                onComplete?.Invoke();
            });
    }
}
