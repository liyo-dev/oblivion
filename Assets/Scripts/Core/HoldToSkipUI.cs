using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Playables;
using Core.InputGlyphs;

[DisallowMultipleComponent]
public class HoldToSkipUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private Image buttonIcon;      // tu sprite del botón (opcional) — usado como fallback si InputGlyphService no tiene sprite para la familia activa (ver RefreshIcon)
    [SerializeField] private Image progressCircle;  // Image con Type=Filled (Radial)
    [SerializeField] private CanvasGroup group;     // opcional

    [Header("Comportamiento")]
    [SerializeField, Min(0.2f)] private float holdSeconds = 1.25f;
    [SerializeField] private bool showOnlyWhileHolding = true;
    [SerializeField] private float fadeIn = 0.12f;
    [SerializeField] private float fadeOut = 0.12f;
    [SerializeField] private bool disableSelfOnSkip = true;

    [Header("Input")]
    [Tooltip("Acción a mantener. ASÍG-NALA: UI/Submit o la que quieras. (Si queda vacío, usa <Gamepad>/buttonSouth)")]
    [SerializeField] private InputActionReference holdActionRef;

    [Header("Acción al Completar")]
    [SerializeField] private SkipAction skipAction = SkipAction.UnityEventOnly;
    
    [SerializeField, Tooltip("Timeline a finalizar (si SkipAction = StopTimeline). Si está vacío, busca automáticamente.")]
    private PlayableDirector timelineToStop;

    [Header("Eventos")]
    public UnityEvent OnSkipCompleted;

    // ---- estado ----
    private InputAction holdAction; // resuelta en runtime
    private bool holding;
    private float heldTime;
    private float targetAlpha;
    private bool completed;
    private InputAction fallback;   // por si no asignas nada
    private bool _pushedUIMode;     // rastrea si nosotros activamos el modo UI

    // FIX (24/08/2026): antes buttonIcon era un sprite fijo asignado a mano en el Inspector — salía
    // siempre el icono de "A" aunque se jugara con teclado/ratón (donde el botón real es
    // Espacio/Enter, ver holdActionRef/TryConfigureInput más abajo, que usa Controls.UI.Submit) o con
    // mando de PlayStation/Switch (donde el dibujo de "A" tampoco es el botón físico correcto).
    // Se cachea aquí el sprite ya asignado a mano en el Inspector para seguir usándolo como fallback
    // si InputGlyphService no tiene sprite para la familia activa (mismo patrón de "fallbackIcon" que
    // ya usa TutorialPromptUI.Show(textTemplate, buttonName, fallbackIcon)).
    private Sprite _fallbackButtonIcon;

    public enum SkipAction
    {
        UnityEventOnly,        // Solo dispara el UnityEvent
        StopTimeline,          // Detiene el Timeline
        SkipNarrativeSequence  // Llama a CinematicSequencerBase.RequestSkipAll() (ver GlobalCinematicSkipController)
    }

    void Awake()
    {
        if (!group) group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        if (buttonIcon) _fallbackButtonIcon = buttonIcon.sprite;

        if (!progressCircle)
        {
            // intenta encontrar una Image "circular"
            foreach (var img in GetComponentsInChildren<Image>(true))
            {
                if (img == buttonIcon) continue;
                if (img.type == Image.Type.Filled) { progressCircle = img; break; }
            }
        }

        if (progressCircle)
        {
            progressCircle.type = Image.Type.Filled;
            if (progressCircle.fillMethod != Image.FillMethod.Radial360)
                progressCircle.fillMethod = Image.FillMethod.Radial360;
            progressCircle.fillOrigin = 2;
            progressCircle.fillClockwise = true;
            progressCircle.fillAmount = 0f;
        }

        targetAlpha = showOnlyWhileHolding ? 0f : 1f;
        ApplyAlphaInstant(targetAlpha);
    }

    void OnEnable()
    {
        // El mapa UI está deshabilitado por defecto (PlayerInputManager inicia en modo Gameplay).
        // Las escenas cinemáticas standalone (ej. Prologo) no llaman PushUIMode externamente,
        // así que lo hacemos aquí para que Controls.UI.Submit pueda recibir input.
        if (Core.PlayerInputManager.Instance != null)
        {
            Core.PlayerInputManager.Instance.PushUIMode();
            _pushedUIMode = true;
        }
        StartCoroutine(InitializeInputWithRetry());
        ResetHold();

        // FIX (24/08/2026): ver comentario de _fallbackButtonIcon arriba — refrescar ya con la
        // familia actual (por si cambió mientras el objeto estaba desactivado) y suscribirse para
        // que, si el jugador cambia de mando/teclado con el icono ya visible, se actualice solo.
        InputGlyphService.FamilyChanged += HandleFamilyChanged;
        RefreshIcon();
    }

    private void HandleFamilyChanged(InputGlyphDeviceFamily _) => RefreshIcon();

    /// <summary>
    /// Resuelve el sprite correcto para el botón que de verdad mantiene HoldToSkipUI: es literalmente
    /// UI/Submit (ver TryConfigureInput más abajo, Prioridad 1), el mismo botón que usa
    /// TutorialPromptUI para "Pulsa {BOTON} para despertar" — por eso reutiliza el mismo nombre de
    /// glifo, InputGlyphNames.Confirm (Espacio/Enter en teclado; en mando es físicamente el mismo
    /// botón South que Interactuar, así que InputGlyphService ya lo resuelve solo a A/Cross/B).
    /// </summary>
    private void RefreshIcon()
    {
        if (!buttonIcon) return;
        var dynamicIcon = InputGlyphService.GetSprite(InputGlyphNames.Confirm);
        buttonIcon.sprite = dynamicIcon != null ? dynamicIcon : _fallbackButtonIcon;
    }

    private System.Collections.IEnumerator InitializeInputWithRetry()
    {
        int attempts = 0;
        const int maxAttempts = 10;
        
        while (attempts < maxAttempts)
        {
            bool success = TryConfigureInput();
            
            if (success)
            {
                Debug.Log($"[HoldToSkipUI] ✅ Input configurado exitosamente (intento {attempts + 1}/{maxAttempts})");
                yield break;
            }
            
            attempts++;
            
            if (attempts < maxAttempts)
            {
                Debug.Log($"[HoldToSkipUI] ⏳ Input no disponible, reintentando... ({attempts}/{maxAttempts})");
                yield return new WaitForSecondsRealtime(0.2f);
            }
        }
        
        Debug.LogError($"[HoldToSkipUI] ❌ No se pudo configurar input tras {maxAttempts} intentos");
    }

    private bool TryConfigureInput()
    {
        // CRÍTICO: Durante cinemáticas, el sistema está en modo UI (Gameplay deshabilitado)
        // Por lo tanto, SIEMPRE debemos usar UI/Submit en lugar de GamePlay/Interact
        
        // Prioridad 1: Usar UI/Submit desde PlayerInputManager (RECOMENDADO para cinemáticas)
        if (Core.PlayerInputManager.Instance != null && Core.PlayerInputManager.Instance.Controls != null)
        {
            var submitAction = Core.PlayerInputManager.Instance.Controls.UI.Submit;
            if (submitAction != null)
            {
                holdAction = submitAction;
                // Habilitar individualmente para garantizar que la acción responde
                // aunque el mapa UI esté en transición (la habilitación de mapa es inmediata
                // desde PushUIMode pero el Disable de GamePlay es diferido).
                holdAction.Enable();
                holdAction.started  += OnHoldStarted;
                holdAction.canceled += OnHoldCanceled;

                Debug.Log($"[HoldToSkipUI] ✅ Usando UI/Submit desde PlayerInputManager - Enabled: {holdAction.enabled}");
                return true;
            }
        }
        
        // Prioridad 2: Usar InputActionReference asignado
        if (holdActionRef != null && holdActionRef.action != null)
        {
            string actionName = holdActionRef.action.name;
            Debug.Log($"[HoldToSkipUI] Intentando usar InputActionReference: {actionName}");
            
            holdAction = holdActionRef.action;
            if (!holdAction.enabled) holdAction.Enable();
            holdAction.started  += OnHoldStarted;
            holdAction.canceled += OnHoldCanceled;
            
            Debug.Log($"[HoldToSkipUI] ✅ Usando InputActionReference: {actionName}");
            return true;
        }
        
        // Prioridad 3: Fallback manual con teclado y gamepad
        if (fallback == null)
        {
            fallback = new InputAction("HoldToSkipFallback", InputActionType.Button);
            fallback.AddBinding("<Gamepad>/buttonSouth");  // A en Xbox, X en PlayStation
            fallback.AddBinding("<Keyboard>/space");        // Espacio en teclado
            fallback.AddBinding("<Keyboard>/enter");        // Enter en teclado
            fallback.Enable();
            
            holdAction = fallback;
            holdAction.started  += OnHoldStarted;
            holdAction.canceled += OnHoldCanceled;
            
            Debug.LogWarning("[HoldToSkipUI] ⚠️ Usando fallback multi-input (Gamepad/Teclado)");
            return true;
        }
        
        return false;
    }

    void OnDisable()
    {
        StopAllCoroutines();

        InputGlyphService.FamilyChanged -= HandleFamilyChanged;

        if (holdAction != null)
        {
            holdAction.started  -= OnHoldStarted;
            holdAction.canceled -= OnHoldCanceled;
        }

        if (fallback != null)
        {
            fallback.Disable();
            fallback.Dispose();
            fallback = null;
        }

        if (_pushedUIMode && Core.PlayerInputManager.Instance != null)
        {
            Core.PlayerInputManager.Instance.PopUIMode();
            _pushedUIMode = false;
        }

        holdAction = null;
        ResetHold();
    }

    void Update()
    {
        // Fade UI
        if (group)
        {
            float speed = (targetAlpha > group.alpha) ? (1f / Mathf.Max(0.0001f, fadeIn))
                                                      : (1f / Mathf.Max(0.0001f, fadeOut));
            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.unscaledDeltaTime * speed);
            group.blocksRaycasts = group.alpha > 0.001f;
            group.interactable   = group.blocksRaycasts;
        }

        // Progreso
        if (holding && !completed)
        {
            heldTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(heldTime / holdSeconds);
            if (progressCircle) progressCircle.fillAmount = t;

            // Log cada 0.25 segundos aprox
            if (Mathf.FloorToInt(heldTime * 4f) != Mathf.FloorToInt((heldTime - Time.unscaledDeltaTime) * 4f))
            {
                Debug.Log($"[HoldToSkipUI] 📊 Progreso: {t:P0} ({heldTime:F2}s / {holdSeconds:F2}s)");
            }

            if (t >= 1f)
            {
                completed = true;
                Debug.Log("[HoldToSkipUI] ✅ COMPLETADO - Ejecutando skip action");
                ExecuteSkipAction();
                if (disableSelfOnSkip) gameObject.SetActive(false);
            }
        }
    }

    private void ExecuteSkipAction()
    {
        if (skipAction == SkipAction.StopTimeline)
        {
            StopTimeline();
        }
        else if (skipAction == SkipAction.SkipNarrativeSequence)
        {
            // Salta TODAS las CinematicSequencerBase activas ahora mismo (sistema viejo,
            // Assets/Scripts/Cinematics/ — normalmente no habrá ninguna, no-op seguro).
            CinematicSequencerBase.RequestSkipAll();

            // FIX (16/08/2026): las secuencias reales de hoy las llevan DialogueCinematicController
            // y DialogueManager, que no son CinematicSequencerBase. NarrativeSkipHub es el punto de
            // enganche genérico que cualquiera de los dos puede implementar (ver su documentación
            // sobre el contrato de "reproducir el estado final" al saltar). No-op seguro si nadie
            // se ha suscrito todavía.
            NarrativeSkipHub.RequestSkip();
        }

        // Siempre disparar el UnityEvent
        OnSkipCompleted?.Invoke();
    }

    private void StopTimeline()
    {
        if (timelineToStop != null)
        {
            timelineToStop.Stop();
            Debug.Log($"[HoldToSkipUI] Timeline detenido: {timelineToStop.name}");
        }
        else
        {
            // Intentar encontrar el PlayableDirector en la escena
            var director = FindAnyObjectByType<PlayableDirector>();
            if (director != null)
            {
                director.Stop();
                Debug.Log($"[HoldToSkipUI] Timeline encontrado y detenido: {director.name}");
            }
            else
            {
                Debug.LogWarning("[HoldToSkipUI] No se encontró ningún PlayableDirector para detener.");
            }
        }
    }

    private void OnHoldStarted(InputAction.CallbackContext _)
    {
        holding = true;
        heldTime = 0f;
        completed = false;
        if (progressCircle) progressCircle.fillAmount = 0f;
        if (showOnlyWhileHolding) targetAlpha = 1f;
        
        Debug.Log("[HoldToSkipUI] 🎮 Input STARTED - Botón presionado");
    }

    private void OnHoldCanceled(InputAction.CallbackContext _)
    {
        holding = false;
        if (!completed)
        {
            heldTime = 0f;
            if (progressCircle) progressCircle.fillAmount = 0f;
        }
        if (showOnlyWhileHolding) targetAlpha = 0f;
        
        Debug.Log($"[HoldToSkipUI] 🎮 Input CANCELED - Botón soltado (completed={completed})");
    }

    private void ResetHold()
    {
        holding = false;
        completed = false;
        heldTime = 0f;
        if (progressCircle) progressCircle.fillAmount = 0f;
        targetAlpha = showOnlyWhileHolding ? 0f : 1f;
        ApplyAlphaInstant(targetAlpha);
    }

    private void ApplyAlphaInstant(float a)
    {
        if (!group) return;
        group.alpha = a;
        group.blocksRaycasts = a > 0.001f;
        group.interactable   = group.blocksRaycasts;
    }

    // API
    public void SetHoldSeconds(float seconds) => holdSeconds = Mathf.Max(0.2f, seconds);
    public void SetIcon(Sprite s) { if (buttonIcon) buttonIcon.sprite = s; }
    public void SetSkipAction(SkipAction action) => skipAction = action;
    public void SetTimelineToStop(PlayableDirector director) => timelineToStop = director;
}
