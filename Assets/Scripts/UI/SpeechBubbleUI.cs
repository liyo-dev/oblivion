using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// Bocadillo de cómic flotante que sigue a un personaje en espacio de pantalla.
/// Vive en el Canvas persistente (Start.unity). Singleton.
/// </summary>
public class SpeechBubbleUI : MonoBehaviour
{
    public static SpeechBubbleUI Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }
#endif

    [Header("Referencias UI")]
    [SerializeField] CanvasGroup _rootGroup;
    [SerializeField] RectTransform _bubbleRect;
    [SerializeField] TextMeshProUGUI _label;
    [SerializeField] RectTransform _parentCanvasRect;

    [Header("Sprites")]
    [SerializeField] Sprite _defaultSprite;
    [SerializeField] Sprite _emphasisSprite;

    [Header("Animación")]
    [SerializeField] float _fadeInDuration  = 0.15f;
    [SerializeField] float _fadeOutDuration = 0.15f;
    [SerializeField] float _popInDuration   = 0.35f;
    [SerializeField] float _popOutDuration  = 0.18f;

    [Header("Tamaño")]
    [Tooltip("Ancho mínimo del bocadillo en píxeles de canvas. Aumenta si el texto se corta por los lados.")]
    [SerializeField] float _bubbleMinWidth = 380f;
    [Tooltip("Margen interno horizontal del texto (izquierda y derecha).")]
    [SerializeField] float _labelHorizontalMargin = 24f;

    [Header("Posición")]
    [SerializeField] Vector3 _worldOffset = new Vector3(0f, 2.2f, 0f);

    Image _bubbleImage;
    Camera _cam;
    Transform _target;
    bool _isShowing;
    Coroutine _autoHideRoutine;

    // Oculto temporalmente porque hay un menú (pausa, equipo, tienda...) abierto encima.
    bool _hiddenByMenu;

    // Caché de componentes de animación: evita GetComponentInChildren repetido
    readonly Dictionary<Transform, Animator> _animatorCache = new();
    readonly Dictionary<Transform, NPCSimpleAnimator> _npcAnimCache = new();
    readonly Dictionary<Transform, PlayerDialogueAnimator> _playerAnimCache = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);

        _cam = Camera.main;
        _bubbleImage = _bubbleRect.GetComponent<Image>();

        if (_parentCanvasRect == null)
            _parentCanvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();

        _rootGroup.alpha = 0f;
        _rootGroup.blocksRaycasts = false;
        _bubbleRect.localScale = Vector3.zero;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Mismo sistema que ya usan BossHealthBar/MinimapController: ocultarse mientras
        // hay un menú abierto (pausa incluida) y restaurarse al cerrar el último.
        MenuManager.MenuOpened += OnMenuOpened;
        MenuManager.MenuClosed += OnMenuClosed;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        MenuManager.MenuOpened -= OnMenuOpened;
        MenuManager.MenuClosed -= OnMenuClosed;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m) => _cam = Camera.main;

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _rootGroup?.DOKill();
        _bubbleRect?.DOKill();
    }

    void LateUpdate()
    {
        if (!_isShowing || _target == null || _cam == null || _parentCanvasRect == null) return;

        Vector3 screenPos = _cam.WorldToScreenPoint(_target.position + _worldOffset);
        if (screenPos.z < 0f) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentCanvasRect, screenPos, null, out Vector2 local))
            _bubbleRect.anchoredPosition = local;
    }

    // ── API pública ───────────────────────────────────────────────────────────

    [ContextMenu("TEST Show (Player)")]
    void TestShow()
    {
        var player = GameObject.FindWithTag("Player");
        Show(player != null ? player.transform : transform, "¡Otra vez ese sueño...!", 4f);
    }

    [ContextMenu("TEST Hide")]
    void TestHide() => Hide();

    /// <summary>
    /// Muestra el bocadillo sobre <paramref name="target"/>.
    /// </summary>
    /// <param name="animTrigger">Trigger del Animator del personaje a disparar mientras habla.</param>
    /// <param name="emphasis">Si true, usa el sprite de énfasis (ej: burbuja explosiva).</param>
    public void Show(Transform target, string text, float duration = 0f,
                     Action onComplete = null, string animTrigger = null, bool emphasis = false)
    {
        if (_autoHideRoutine != null) { StopCoroutine(_autoHideRoutine); _autoHideRoutine = null; }

        _target = target;
        _isShowing = true;
        _label.text = text;

        // Margen horizontal para que el texto no roque los bordes del bocadillo
        _label.margin = new Vector4(_labelHorizontalMargin, _label.margin.y,
                                    _labelHorizontalMargin, _label.margin.w);

        // Ancho mínimo: si el bocadillo es más pequeño que _bubbleMinWidth lo forzamos
        if (_bubbleMinWidth > 0f && _bubbleRect != null)
        {
            Vector2 sd = _bubbleRect.sizeDelta;
            if (sd.x < _bubbleMinWidth)
                _bubbleRect.sizeDelta = new Vector2(_bubbleMinWidth, sd.y);
        }

        if (_bubbleImage != null)
        {
            Sprite sprite = emphasis && _emphasisSprite != null ? _emphasisSprite : _defaultSprite;
            if (sprite != null) _bubbleImage.sprite = sprite;
        }

        // Animación del personaje: preferir el wrapper de alto nivel para respetar el state machine
        if (!string.IsNullOrEmpty(animTrigger) && target != null)
        {
            var npcAnim = GetCachedNPCAnimator(target);
            if (npcAnim != null)
            {
                npcAnim.PlaySocialGesture(animTrigger);
            }
            else
            {
                var playerAnim = GetCachedPlayerAnimator(target);
                if (playerAnim != null)
                    playerAnim.PlayGesture(animTrigger);
                else
                {
                    var anim = GetCachedAnimator(target);
                    if (anim != null) anim.Play(animTrigger);
                }
            }
        }

        // Pop-in: escala desde 0 con rebote + fade
        _rootGroup.DOKill();
        _bubbleRect.DOKill();

        _rootGroup.alpha = 0f;
        _bubbleRect.localScale = Vector3.zero;

        _rootGroup.DOFade(1f, _fadeInDuration).SetUpdate(true);
        _bubbleRect.DOScale(Vector3.one, _popInDuration)
                   .SetEase(Ease.OutBack)
                   .SetUpdate(true);

        if (duration > 0f)
            _autoHideRoutine = StartCoroutine(AutoHide(duration, onComplete));
    }

    public void Hide()
    {
        if (_autoHideRoutine != null) { StopCoroutine(_autoHideRoutine); _autoHideRoutine = null; }
        _isShowing = false;

        _rootGroup.DOKill();
        _bubbleRect.DOKill();

        _rootGroup.DOFade(0f, _fadeOutDuration).SetUpdate(true);
        _bubbleRect.DOScale(Vector3.zero, _popOutDuration)
                   .SetEase(Ease.InBack)
                   .SetUpdate(true);
    }

    // ── MenuManager (pausa / cualquier menú) ────────────────────────────────────

    /// <summary>Oculta el bocadillo mientras haya un menú (pausa incluida) abierto encima.</summary>
    void OnMenuOpened(MenuKind kind)
    {
        if (!_isShowing || _hiddenByMenu || _rootGroup == null) return;
        _hiddenByMenu = true;
        _rootGroup.DOKill();
        _rootGroup.DOFade(0f, 0.15f).SetUpdate(true);
        _rootGroup.blocksRaycasts = false;
    }

    /// <summary>Restaura el bocadillo al cerrarse el último menú abierto, si seguía en pantalla.</summary>
    void OnMenuClosed(MenuKind kind)
    {
        if (!_hiddenByMenu) return;
        if (MenuManager.AnyOpen()) return; // todavía queda otro menú abierto
        _hiddenByMenu = false;

        if (!_isShowing || _rootGroup == null) return; // se ocultó por otro motivo mientras tanto
        _rootGroup.DOKill();
        _rootGroup.DOFade(1f, 0.2f).SetUpdate(true);
        _rootGroup.blocksRaycasts = false; // este bocadillo nunca bloquea clics, ver Awake
    }

    // ── Interno ───────────────────────────────────────────────────────────────

    IEnumerator AutoHide(float duration, Action onComplete)
    {
        yield return new WaitForSecondsRealtime(duration);
        Hide();
        _autoHideRoutine = null;
        onComplete?.Invoke();
    }

    Animator GetCachedAnimator(Transform target)
    {
        if (!_animatorCache.TryGetValue(target, out Animator anim))
        {
            anim = target.GetComponentInChildren<Animator>();
            _animatorCache[target] = anim;
        }
        return anim;
    }

    NPCSimpleAnimator GetCachedNPCAnimator(Transform target)
    {
        if (!_npcAnimCache.TryGetValue(target, out NPCSimpleAnimator npcAnim))
        {
            npcAnim = target.GetComponentInChildren<NPCSimpleAnimator>();
            _npcAnimCache[target] = npcAnim;
        }
        return npcAnim;
    }

    PlayerDialogueAnimator GetCachedPlayerAnimator(Transform target)
    {
        if (!_playerAnimCache.TryGetValue(target, out PlayerDialogueAnimator playerAnim))
        {
            playerAnim = target.GetComponentInChildren<PlayerDialogueAnimator>();
            _playerAnimCache[target] = playerAnim;
        }
        return playerAnim;
    }
}
