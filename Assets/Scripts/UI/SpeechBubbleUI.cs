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
    [SerializeField] float _bubbleMinWidth = 420f;
    [Tooltip("Ancho máximo del bocadillo en píxeles de canvas antes de partir el texto en varias líneas. BUGFIX (Agosto 2026): antes el bocadillo solo tenía un ancho MÍNIMO y nunca crecía con el texto real, así que cualquier línea más larga que ese mínimo se salía por los lados en todas las secuencias (ver captura del bug). Ahora el ancho se calcula a partir del texto real en Show(), entre este máximo y _bubbleMinWidth. El ALTO no se toca aquí — lo calcula solo el ContentSizeFitter/VerticalLayoutGroup del hijo \"Bubble\" en el prefab (ver Show()).\nAJUSTE (Agosto 2026, 2ª pasada): min/max bajados de 560/900 a 420/620. Con los valores viejos, cualquier frase de longitud media cabía en una sola línea muy ancha y el bocadillo salía como un óvalo aplastado y alargado; y las frases cortas se estiraban igual hasta el mínimo de 560, con el mismo efecto. Con el ancho máximo más bajo, el texto envuelve antes en 2-3 líneas más cortas y el bocadillo crece en vertical en su lugar — se ve más redondo y las líneas quedan más parejas entre sí (ayuda también a que el bloque de texto centrado se perciba realmente centrado, en vez de una única línea larga pegada a los bordes).")]
    [SerializeField] float _bubbleMaxWidth = 620f;
    [Tooltip("Margen interno horizontal del texto (izquierda y derecha).")]
    [SerializeField] float _labelHorizontalMargin = 44f;

    [Header("Posición")]
    [SerializeField] Vector3 _worldOffset = new Vector3(0f, 2.2f, 0f);

    Image _bubbleImage;
    Camera _cam;
    Transform _target;
    bool _isShowing;
    Coroutine _autoHideRoutine;
    // Callback pendiente del Show() en curso — solo relevante mientras _autoHideRoutine != null.
    // Guardado aparte (además del parámetro local que ya recibe la propia corrutina AutoHide) para
    // que SkipCurrent() pueda invocarlo desde fuera sin depender de la corrutina.
    Action _pendingOnComplete;

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
    /// <param name="speakerName">
    /// Nombre del personaje que habla (ej: "Will", "Estela"). AÑADIDO (Agosto 2026) para
    /// desambiguar quién habla con varios NPCs juntos (taberna), probado como texto dentro del
    /// propio bocadillo. REVERTIDO (Agosto 2026, mismo mes): un bocadillo de cómic no lleva
    /// nombres escritos — la desambiguación correcta es que el PICO señale a quien habla (ver
    /// fix del pico más abajo). Se mantiene el parámetro sin usarlo en el texto (todas las
    /// llamadas ya lo pasan) por si en el futuro sirve para otra cosa, pero ya no se antepone al
    /// texto — no tocar esto de nuevo sin que el usuario lo pida explícitamente.
    /// </param>
    public void Show(Transform target, string text, float duration = 0f,
                     Action onComplete = null, string animTrigger = null, bool emphasis = false,
                     string speakerName = null)
    {
        if (_autoHideRoutine != null) { StopCoroutine(_autoHideRoutine); _autoHideRoutine = null; }

        _target = target;
        _isShowing = true;
        _label.text = text;

        // Centrado forzado siempre (horizontal Y vertical): no depender solo del valor por
        // defecto del prefab, que podría cambiar sin querer en una variante o en una edición
        // futura del prefab. El bocadillo de cómic siempre debe leerse centrado.
        _label.alignment = TextAlignmentOptions.Center;

        // Margen horizontal para que el texto no roque los bordes del bocadillo
        _label.margin = new Vector4(_labelHorizontalMargin, _label.margin.y,
                                    _labelHorizontalMargin, _label.margin.w);

        // BUGFIX (Agosto 2026): el bocadillo solo tenía un ancho MÍNIMO fijo (_bubbleMinWidth) y
        // nunca se adaptaba al texto real, así que cualquier línea más larga que ese mínimo se
        // salía por los lados — pasaba en todas las secuencias, no era un caso puntual. Ahora se
        // mide el ancho que ocuparía el texto en una sola línea (GetPreferredValues) y el
        // bocadillo crece hasta _bubbleMaxWidth para acomodarlo antes de partir el texto en
        // varias líneas (word wrap).
        //
        // OJO — el ALTO no se toca aquí a mano. El GameObject "Bubble" (padre de _label, ver
        // prefab SpeechBubbleUI/Bubble) ya tiene un VerticalLayoutGroup (padding real: 18 arriba,
        // 58 abajo para dejar sitio al pico del bocadillo) + ContentSizeFitter con
        // m_VerticalFit=PreferredSize — es decir, el alto SIEMPRE lo calcula ese sistema a partir
        // del preferredHeight del texto ya envuelto, no nosotros. El primer intento de este
        // arreglo calculaba también el alto a mano con un margen inventado que no coincidía con
        // ese padding real (18/58, asimétrico por el pico) — el resultado quedaba más bajo que el
        // texto de verdad y la primera línea se salía por ARRIBA del bocadillo (bug reportado tras
        // el primer pase). Ahora solo tocamos el ancho y forzamos un rebuild de layout inmediato
        // para que el ContentSizeFitter recalcule el alto ANTES del pop-in — si no, se ve un frame
        // con el alto del texto anterior mientras el ancho ya ha cambiado.
        if (_bubbleRect != null && _label != null)
        {
            _label.textWrappingMode = TextWrappingModes.Normal;

            Vector2 singleLineSize = _label.GetPreferredValues(text, 0f, 0f);
            float desiredWidth = singleLineSize.x + _labelHorizontalMargin * 2f;
            float bubbleWidth = Mathf.Clamp(desiredWidth, _bubbleMinWidth, _bubbleMaxWidth);

            Vector2 sd = _bubbleRect.sizeDelta;
            sd.x = bubbleWidth;
            _bubbleRect.sizeDelta = sd;

            LayoutRebuilder.ForceRebuildLayoutImmediate(_bubbleRect);
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

        _pendingOnComplete = onComplete;
        if (duration > 0f)
            _autoHideRoutine = StartCoroutine(AutoHide(duration, onComplete));
    }

    /// Fuerza el cierre inmediato del bocadillo con auto-hide en curso, como si su duración ya
    /// hubiera terminado: para el temporizador, lo oculta y dispara el mismo onComplete que se le
    /// pasó a Show() — así quien esperaba ese callback (típicamente ShowSpeechBubbleNode, para
    /// avanzar el grafo narrativo) lo recibe igual, solo que antes. No-op seguro si no hay ningún
    /// bocadillo con auto-hide en curso (duration <= 0, o ya se ocultó solo). Pensado para el botón
    /// global de "saltar" — ver ShowSpeechBubbleNode.RegisterSkipHandler.
    public void SkipCurrent()
    {
        if (_autoHideRoutine == null) return;
        StopCoroutine(_autoHideRoutine);
        _autoHideRoutine = null;
        var callback = _pendingOnComplete;
        _pendingOnComplete = null;
        Hide();
        callback?.Invoke();
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
        _pendingOnComplete = null;
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
