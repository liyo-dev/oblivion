using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Core;
using Core.InputGlyphs;
using DG.Tweening;
using Sendero.Core.Feedback;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }
    
    #region Events
    /// <summary>
    /// Evento disparado cuando se inicia un diálogo con un NPC.
    /// El Transform es el NPC con el que se está hablando (puede ser null si no hay NPC).
    /// </summary>
    public static event Action<Transform> OnDialogueStarted;
    
    /// <summary>
    /// Evento disparado cuando se cierra un diálogo.
    /// El Transform es el NPC con el que se estaba hablando (puede ser null si no había NPC).
    /// </summary>
    public static event Action<Transform> OnDialogueClosed;
    
    /// <summary>
    /// Evento disparado cuando cambia la línea de diálogo actual.
    /// Incluye la línea actual (con su emoción) y el NPC involucrado.
    /// </summary>
    public static event Action<DialogueLine, Transform> OnDialogueLineChanged;
    #endregion

    [Header("UI")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Image portraitImage;

    [Header("Modo Sueño (opcional)")]
    [Tooltip("Fondo nebulosa de colores para el panel de diálogo. Hijo del canvas de diálogo, por debajo del panel.")]
    [SerializeField] private DreamBackgroundController _dreamBackground;
    [Tooltip("Chispas flotantes para el panel de diálogo. Hijo del canvas de diálogo, por encima del panel.")]
    [SerializeField] private DreamSparkleOverlay _dreamSparkles;

    [Header("Typewriter")]
    [SerializeField] private bool useTypewriter = true;
    [Tooltip("Caracteres por segundo cuando useTypewriter está activo")] 
    [SerializeField, Min(1f)] private float charsPerSecond = 35f;
    [Tooltip("Si se pulsa Avanzar mientras escribe, completa la línea al instante")] 
    [SerializeField] private bool allowSkipCurrentLine = true;
    
    [Header("Typewriter Audio")]
    [Tooltip("Reproducir sonido con cada letra del typewriter")]
    [SerializeField] private bool useLetterSound = true;
    [Tooltip("Clave del SFX en AudioGraphProfile para cada letra (ej: 'UI_DialogueLetter')")]
    [SerializeField] private string letterSoundKey = "UI_DialogueLetter";
    [Tooltip("Cada cuántos caracteres reproducir el sonido (1 = cada letra, 2 = cada 2 letras, etc.)")]
    [SerializeField, Min(1)] private int letterSoundFrequency = 1;


    [Header("Opcional")]
    [SerializeField] private bool pauseGameWhileOpen;
    [SerializeField] private bool resolveWithLocalizationManager = true;

    [Header("Cámara de Diálogo")]
    [Tooltip("Si está activo, usa el sistema cinematográfico avanzado con múltiples planos")]
    [SerializeField] private bool useCinematicCamera = true;
    
    [Tooltip("Perfil cinematográfico para diálogos (define los planos y transiciones)")]
    [SerializeField] private DialogueCinematicProfile cinematicProfile;
    
    [Tooltip("(Legacy) Sistema de cámara simple - se usa si cinematicProfile no está asignado")]
    [SerializeField] private bool useDialogueCameraLegacy = false;

    [Header("UI Hints")]
    [SerializeField] private GameObject submitHint;

    // Estado
    private DialogueAsset _current;
    private int _index = -1;
    private Action _onEnd;

    // FIX (16/08/2026): guard de reentrada para HandleSkipRequested (ver NarrativeSkipHub más
    // abajo) — evita que una segunda invocación de RequestSkip() mientras ya estamos vaciando
    // _current con el bucle de Next() intente arrancar un segundo bucle sobre el mismo diálogo.
    private bool _skipping;

    // FIX: al leer objetos sin cámara cinemática (cartas, save points, etc.) el HUD no se
    // ocultaba porque ese hide/show solo estaba conectado a los controladores cinemáticos
    // (DialogueCinematicController / SimpleCinematicDirector / CinematicSequencerBase), y este
    // camino de diálogo "no-NPC" nunca pasa por ninguno de ellos. Se ocupa aquí, de forma
    // acotada, para no interferir con el HUD hide/show ya gestionado por esos sistemas
    // cuando sí hay un NPC real con cámara cinemática.
    private bool _hudHiddenForNonCinematicDialogue;
    public bool IsOpen => _current != null;

    // FIX (24/08/2026): el botón global "mantener para saltar" (HoldToSkipUI, vía NarrativeSkipHub)
    // se registraba en TODO diálogo que pasara por StartDialogue — conversaciones normales con un
    // NPC (Interactable.cs), diálogo de quest (NPCQuestConfig/NPCQuestActionExecutor/
    // NPCInteractiveNarrativeExecutor), líneas de pre-combate (NPCCombatLifecycleHandler) y
    // mensajes de bloqueo (RoomExitBlocker) incluidos. Raúl pidió que el botón de skip solo
    // aparezca en secuencias reales, no en cualquier diálogo con un NPC — ver el nuevo parámetro
    // isSequenceDialogue en StartDialogue(asset, onFinished, isSequenceDialogue) más abajo, que
    // condiciona el registro en NarrativeSkipHub. No hace falta recordar el valor como campo:
    // HandleSkipRequested/Close ya se comportan igual estén o no suscritos.

    // Expose a few internal state values to allow external callers
    // to react to line progress (e.g. open shop when last line finishes).
    public int CurrentIndex => _index;
    public int CurrentLineCount => _current != null && _current.lines != null ? _current.lines.Length : 0;
    public bool IsTyping => _isTyping;

    [Header("Choices (optional)")]
    [SerializeField] private CanvasGroup choicesRoot;   // contenedor de los botones
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [SerializeField] private TextMeshProUGUI messageLabel; // si quieres sobreescribir el texto principal
    [SerializeField] private bool enableDpadHorizontalFallback = true; // si Navigate no recoge el D-Pad
    [SerializeField, Min(0f)] private float dpadRepeatDelay = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    float _nextDpadTime;

    Action _onYes;
    Action _onNo;
    bool _choicesUIModePushed;

    // Typewriter estado
    Coroutine _typeRoutine;
    bool _isTyping;
    string _currentText = string.Empty;

    // NPC para cámara de diálogo
    private Transform _currentNpc;
    private NPCSimpleAnimator _activeDialogueSpeakerAnimator;
    private bool _activeDialogueSpeakerIsPlayer;
    // Caché del NPCSimpleAnimator del jugador activo (se reutiliza por línea sin GetComponent extra)
    private NPCSimpleAnimator _playerDialogueAnimator;
    // ID de personaje del NPC principal para matching en ActivateSpeakerTalkAnimation
    // Se obtiene de NPCBehaviourManagerV2.dialogueCharacterId o de Interactable.dialogueCharacterId
    private string _currentNpcDialogueCharacterId;
    
    // Protección contra input inmediato al abrir diálogo
    private float _dialogueOpenedAt = -999f;
    private const float InputGracePeriod = 0.3f;
    
    // Protección contra avance doble al completar línea
    private float _lastLineCompletedAt = -999f;
    private const float LineCompleteCooldown = 0.2f;

    // Fallbacks para el player speaker (cuando no usa NPCSimpleAnimator)
    private static readonly string[] PlayerSpeakStateCandidates =
    {
        "InteractWithPeople_NoWeapon",
        "UpperBody.InteractWithPeople_NoWeapon",
        "Base Layer.InteractWithPeople_NoWeapon",
        "Greeting01_NoWeapon",
        "Greeting01",
        "UpperBody.Greeting01_NoWeapon",
        "Base Layer.Greeting01_NoWeapon",
        "UpperBody.UpperIdle",
        "UpperIdle"
    };

    private static readonly string[] PlayerLocomotionStateCandidates =
    {
        "Locomotion",
        "Free Locomotion",
        "UpperBody.UpperIdle",
        "UpperIdle"
    };

    private static readonly string[] PlayerTalkBoolCandidates =
    {
        "IsTalking"
    };

    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        OnDialogueStarted = null;
        OnDialogueClosed = null;
        OnDialogueLineChanged = null;
    }
    #endif
    
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Validar y forzar configuración correcta del typewriter
        if (!useTypewriter)
        {
            Debug.LogWarning("[DialogueManager] ⚠️ useTypewriter está DESACTIVADO en el Inspector. Forzando activación.");
            useTypewriter = true;
        }
        if (charsPerSecond <= 0f || charsPerSecond > 100f)
        {
            Debug.LogWarning($"[DialogueManager] ⚠️ charsPerSecond tiene valor incorrecto ({charsPerSecond}). Ajustando a 35.");
            charsPerSecond = 35f;
        }
        if (letterSoundFrequency < 1)
        {
            Debug.LogWarning($"[DialogueManager] ⚠️ letterSoundFrequency tiene valor incorrecto ({letterSoundFrequency}). Ajustando a 1.");
            letterSoundFrequency = 1;
        }
        
        // Debug.Log($"[DialogueManager] ✅ Configuración validada - Typewriter: {useTypewriter}, Velocidad: {charsPerSecond} chars/s, Audio: {useLetterSound}, Frecuencia: {letterSoundFrequency}");

        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        // Asegurar choices ocultos por defecto
        if (choicesRoot != null)
        {
            choicesRoot.alpha = 0f;
            choicesRoot.blocksRaycasts = false;
            choicesRoot.interactable = false;
        }
        // Auto localizar el hint si no está asignado (busca por nombre común)
        if (submitHint == null)
        {
            var t = transform.Find("Canvas/Panel/boton A");
            if (t != null) submitHint = t.gameObject;
        }
        if (submitHint != null) submitHint.SetActive(false);
    }

    void OnEnable()
    {
        GamepadInputReader.EnsureInputEventsSubscribed();
        GamepadInputReader.OnInput += HandleGamepadInput;
    }

    void OnDisable()
    {
        GamepadInputReader.OnInput -= HandleGamepadInput;
        // Defensivo: si el objeto se desactiva/destruye a mitad de un diálogo (cierre de sesión,
        // teardown de escena) evita dejar el handler colgado suscrito a un componente inerte.
        NarrativeSkipHub.UnregisterSkipHandler(HandleSkipRequested);
    }

    /// Handler registrado en NarrativeSkipHub mientras hay un diálogo abierto (ver StartDialogue/
    /// Close más arriba). Contrato de NarrativeSkipHub: saltar debe dejar el mundo "como si la
    /// secuencia se hubiera visto completa", no solo abortarla a mitad.
    ///
    /// Cómo lo consigue: en vez de reconstruir a mano el estado final (animaciones, cámara, NPCs
    /// ocultos/visibles, flags...) reutiliza el propio Next() en un bucle síncrono hasta que el
    /// diálogo se cierra solo. Cada línea restante dispara EXACTAMENTE los mismos efectos que si
    /// el jugador la hubiera visto avanzando a mano uno por uno (ActivateSpeakerTalkAnimation,
    /// OnDialogueLineChanged, DialogueCinematicController.OnDialogueLineAdvanced con sus cambios
    /// de plano/visibilidad de NPCs...), solo que sin esperar su input entre una y otra. Cuando
    /// _index supera _current.lines.Length, Next() llama a Close() de verdad — el mismo Close()
    /// del cierre normal: invoca _onEnd (crítico para que el nodo del grafo narrativo que esté
    /// esperando no se quede colgado, ver FIX C1 más arriba), dispara OnDialogueClosed, restaura
    /// cámara/HUD/party/bystanders vía DialogueCinematicController.EndCinematic(), y limpia las
    /// animaciones del último speaker con ClearActiveSpeakerAnimations(). Nada de esto hay que
    /// reimplementarlo aquí porque el bucle simplemente llega a ese mismo camino por sí solo.
    ///
    /// Next()/Close() son 100% síncronos (el typewriter que arranca cada Next() es fire-and-forget
    /// vía StartCoroutine y se para solo al entrar en la siguiente iteración o en Close(), nunca
    /// bloquea este bucle), así que todas las líneas restantes se resuelven dentro del mismo frame
    /// en el que se completó el hold — no hay parpadeo visible porque Unity no renderiza estados
    /// intermedios entre llamadas a script dentro del mismo frame.
    private void HandleSkipRequested()
    {
        if (!IsOpen || _skipping) return;

        // Si hay un choice Sí/No mostrándose (no debería solaparse con un diálogo abierto en el
        // flujo normal, pero es una decisión del jugador — el skip no puede elegir por él), no
        // tocamos nada.
        if (choicesRoot != null && choicesRoot.interactable) return;

        _skipping = true;
        try
        {
            while (_current != null)
            {
                Next();
            }
        }
        finally
        {
            _skipping = false;
        }
    }

    private void HandleGamepadInput(GamepadInputReader.InputEvent input)
    {
        if (input.Phase != InputActionPhase.Performed) return;

        // Cancel funciona siempre que haya opciones activas (sin período de gracia)
        if (input.Type == GamepadInputReader.InputEventType.Cancel)
        {
            if (choicesRoot != null && choicesRoot.interactable && _onNo != null)
                OnNoClicked();
            return;
        }

        // PROTECCIÓN: Ignorar inputs durante el período de gracia después de abrir el diálogo
        if (Time.unscaledTime - _dialogueOpenedAt < InputGracePeriod)
        {
            if (verboseLogging) Debug.Log($"[DialogueManager] ⏸️ Input ignorado durante período de gracia inicial ({Time.unscaledTime - _dialogueOpenedAt:F3}s desde apertura)");
            return;
        }
        
        // PROTECCIÓN: Ignorar inputs durante el cooldown después de completar una línea
        if (Time.unscaledTime - _lastLineCompletedAt < LineCompleteCooldown)
        {
            if (verboseLogging) Debug.Log($"[DialogueManager] ⏸️ Input ignorado durante cooldown de línea completada ({Time.unscaledTime - _lastLineCompletedAt:F3}s desde completado)");
            return;
        }

        // Manejar Submit/Avanzar diálogo
        if (input.Type == GamepadInputReader.InputEventType.Submit && IsOpen)
        {
            Advance();
            return;
        }

        // Navegación horizontal para opciones (Sí/No)
        if (!enableDpadHorizontalFallback) return;
        if (choicesRoot == null || !choicesRoot.interactable) return;

        bool left = false;
        bool right = false;

        if (input.Type == GamepadInputReader.InputEventType.DpadLeft)
            left = true;
        else if (input.Type == GamepadInputReader.InputEventType.DpadRight)
            right = true;
        else if (input.Type == GamepadInputReader.InputEventType.Navigate)
        {
            left = input.Value.x < -0.6f;
            right = input.Value.x > 0.6f;
        }

        if (!(left || right)) return;
        if (Time.unscaledTime < _nextDpadTime) return;

        var es = EventSystem.current;
        if (es != null)
        {
            var cur = es.currentSelectedGameObject;
            if (cur == null || !cur.activeInHierarchy)
            {
                // Si no hay selección, seleccionar el botón izquierdo (Yes)
                if (yesButton) es.SetSelectedGameObject(yesButton.gameObject);
            }
            else if (yesButton != null && noButton != null)
            {
                // CORREGIDO: Si estamos en Yes y presionamos derecha -> vamos a No
                if (cur == yesButton.gameObject && right)
                {
                    es.SetSelectedGameObject(noButton.gameObject);
                    if (verboseLogging) Debug.Log("[DialogueManager] Navegación: Yes -> No");
                }
                // CORREGIDO: Si estamos en No y presionamos izquierda -> vamos a Yes
                else if (cur == noButton.gameObject && left)
                {
                    es.SetSelectedGameObject(yesButton.gameObject);
                    if (verboseLogging) Debug.Log("[DialogueManager] Navegación: No -> Yes");
                }
            }
        }

        _nextDpadTime = Time.unscaledTime + dpadRepeatDelay;
    }

    /// <param name="isSequenceDialogue">
    /// FIX (24/08/2026): true SOLO cuando este diálogo forma parte de una secuencia/cinemática real
    /// (hoy en día, únicamente SimpleCinematicDirector lo pasa así) — es lo único que habilita el
    /// registro en NarrativeSkipHub y por tanto que HoldToSkipUI pueda aparecer. Las secuencias que
    /// derivan de CinematicSequencerBase (Prólogo, Taberna, etc.) no pasan por aquí en absoluto:
    /// ya muestran el botón por su cuenta vía CinematicSequencerBase.OnAnySequenceActiveChanged
    /// (ver GlobalCinematicSkipController), así que no necesitan este flag. Por defecto false, para
    /// que una conversación normal con un NPC (Interactable, diálogo de quest, líneas de
    /// pre-combate, mensajes de RoomExitBlocker...) NUNCA ofrezca la opción de saltar con mantener
    /// pulsado — solo se puede avanzar línea a línea, como cualquier diálogo normal del juego.
    /// </param>
    public void StartDialogue(DialogueAsset asset, Action onFinished = null, bool isSequenceDialogue = false)
    {
        if (asset == null || asset.lines == null || asset.lines.Length == 0) return;

        // FIX C1 (auditoría 2026-08-07): StartDialogue pisaba _current/_onEnd sin avisar al
        // llamador anterior. PlayDialogueNode del grafo narrativo espera el callback con
        // `while (!completed) yield return null`; si otro sistema abría diálogo antes de que
        // terminara (típico: post-action de quest + siguiente nodo del grafo al completarla),
        // el que llegaba segundo se quedaba con el diálogo y el primer callback no se invocaba
        // nunca — la rama del grafo quedaba bloqueada para siempre. El diálogo nuevo sigue
        // ganando (mismo comportamiento de antes); la diferencia es que ya no deja a nadie
        // esperando un callback que no iba a llegar.
        if (IsOpen && _onEnd != null)
        {
            var previousOnEnd = _onEnd;
            _onEnd = null;
            previousOnEnd.Invoke();
        }

        // Suscripción al botón global de "mantener para saltar" (ver NarrativeSkipHub y
        // HandleSkipRequested más abajo). Desregistrar antes de registrar es defensivo: si
        // StartDialogue se llama de nuevo mientras ya había un diálogo abierto (rama FIX C1 justo
        // arriba, que NO pasa por Close()), ya estaríamos suscritos desde la apertura anterior —
        // sin este desregistro previo quedaríamos suscritos dos veces al mismo handler.
        // FIX (24/08/2026): solo nos suscribimos si isSequenceDialogue es true — ver comentario del
        // parámetro arriba. Si el diálogo anterior sí era de secuencia y este nuevo no lo es (o
        // viceversa), el Unregister incondicional de abajo limpia cualquier suscripción previa
        // antes de decidir si hace falta una nueva.
        NarrativeSkipHub.UnregisterSkipHandler(HandleSkipRequested);
        if (isSequenceDialogue)
            NarrativeSkipHub.RegisterSkipHandler(HandleSkipRequested);

        ClearActiveSpeakerAnimations();
        _current = asset;
        _onEnd = onFinished;
        _index = -1;

        // Cachear el NPCSimpleAnimator del jugador para animaciones corporales durante el diálogo
        if (PlayerService.TryGetPlayer(out var pGo, allowSceneLookup: true) && pGo != null)
            _playerDialogueAnimator = pGo.GetComponent<NPCSimpleAnimator>();

        // Marcar el momento en que se abre el diálogo para ignorar inputs inmediatos
        _dialogueOpenedAt = Time.unscaledTime;
        if (verboseLogging) Debug.Log($"[DialogueManager] 🕐 Diálogo abierto en t={_dialogueOpenedAt:F3} - período de gracia activo");

        // Mostrar UI
        if (group != null)
        {
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            if (verboseLogging) Debug.Log($"[DialogueManager] ✅ UI activada - alpha={group.alpha}, blocksRaycasts={group.blocksRaycasts}, Canvas activo={group.gameObject.activeInHierarchy}");
        }
        else
        {
            Debug.LogError("[DialogueManager] ❌ CanvasGroup es NULL - el diálogo no se mostrará");
        }

        _dreamBackground?.StartDream();
        _dreamSparkles?.StartSparkles();

        // NO mostrar el submitHint al inicio - se mostrará cuando termine de escribir la primera línea
        if (submitHint != null)
            submitHint.SetActive(false);
            
        if (pauseGameWhileOpen) Time.timeScale = 0f;


        // NUEVO: Activar modo DialogueActive en PlayerActionManager
        ActivateDialogueMode(true);

        // Activar sistema cinematográfico SOLO si es un NPC real (no objetos como cartas, save points, etc.)
        bool isActualNPC = IsActualNPC(_currentNpc);
        
        if (useCinematicCamera && isActualNPC && DialogueCinematicController.Instance != null)
        {
            GameObject playerObj = PlayerService.Player;
            if (playerObj != null)
            {
                if (verboseLogging) Debug.Log($"[DialogueManager] 🎬 Activando sistema cinematográfico para NPC: {_currentNpc.name}");
                DialogueCinematicController.Instance.StartCinematic(playerObj.transform, _currentNpc, cinematicProfile, _current?.isGroupConversation ?? false);
            }
            else
            {
                Debug.LogWarning("[DialogueManager] No se encontró el jugador para el sistema cinematográfico");
            }
        }
        else if (useDialogueCameraLegacy && isActualNPC && DialogueCameraController.Instance != null)
        {
            // Fallback al sistema antiguo si no está el nuevo
            if (verboseLogging) Debug.Log($"[DialogueManager] 🎥 Activando cámara de diálogo legacy para NPC: {_currentNpc.name}");
            DialogueCameraController.Instance.StartDialogueCamera(_currentNpc);
        }
        else
        {
            if (_currentNpc != null && !isActualNPC)
            {
                if (verboseLogging) Debug.Log($"[DialogueManager] 💬 Diálogo con objeto interactivo '{_currentNpc.name}' (no NPC) - cámaras cinematográficas desactivadas");

                // FIX: sin cámara cinemática ningún sistema oculta el HUD por su cuenta.
                // Ocultarlo aquí (cartas, save points, etc.) y recordar que fuimos nosotros
                // quienes lo ocultamos, para restaurarlo en Close().
                _hudHiddenForNonCinematicDialogue = true;
                Sendero.UI.PlayerHUDV2.Instance?.HideHUD();
            }
            else
            {
                Debug.LogWarning($"[DialogueManager] ⚠️ Sistema cinematográfico NO activado - useCinematic={useCinematicCamera}, NPC={_currentNpc?.name ?? "NULL"}, esNPC={isActualNPC}, Controller Instance={DialogueCinematicController.Instance != null}");
            }
        }

        Next(); // pinta primera línea
    }

    /// <summary>
    /// Inicia un diálogo con un NPC específico (para usar la cámara de diálogo)
    /// </summary>
    /// <param name="isSequenceDialogue">Ver documentación en StartDialogue(asset, onFinished, isSequenceDialogue) — se reenvía tal cual.</param>
    public void StartDialogue(DialogueAsset asset, Transform npc, Action onFinished = null, bool isSequenceDialogue = false)
    {
        _currentNpc = npc;
        if (npc != null)
        {
            var npcMgr = npc.GetComponent<Game.NPC.NPCBehaviourManagerV2>();
            _currentNpcDialogueCharacterId = npcMgr != null
                ? npcMgr.DialogueCharacterId
                : npc.GetComponent<Interactable>()?.DialogueCharacterId;
        }
        else
            _currentNpcDialogueCharacterId = null;

        // FIX (20 ago 2026): igual que en DialogueCinematicController.StartCinematic (mismo
        // incidente, ver comentario allí) — si 'npc' resulta ser el propio personaje activo
        // oculto (ActiveCharacterSwapper.HiddenNpc, p.ej. un NPCInteractiveNarrativeExecutor
        // colgado de _ESTELA disparando su propia línea mientras el jugador la controla), su
        // transform real está oculto y congelado en la posición de otro cambio de personaje.
        // Solo afecta al TRANSFORM usado para posicionar al equipo aquí abajo — _currentNpc y
        // _currentNpcDialogueCharacterId (arriba) se quedan como estaban, siguen identificando
        // al personaje correcto para nombre/portrait y para quien escuche OnDialogueStarted.
        Transform npcForPositioning = npc;
        var activeSwapper = ActiveCharacterSwapper.Instance;
        if (activeSwapper != null && activeSwapper.HiddenNpc != null && npc == activeSwapper.HiddenNpc.transform
            && PlayerService.TryGetPlayer(out var activeCharacterGo) && activeCharacterGo != null)
        {
            npcForPositioning = activeCharacterGo.transform;
        }

        // Posicionar party members ANTES de iniciar la cámara cinematográfica
        // para que estén en su sitio cuando la cámara capture las posiciones iniciales.
        // EXCEPCIÓN: en diálogos GRUPALES con cámara cinematográfica, el posicionamiento lo hace
        // DialogueCinematicController.StartCinematic (PositionMembersForGroupDialogue), porque
        // necesita conocer primero el lado de la cámara para formar el semicírculo en el lado
        // opuesto y que ningún compañero tape el plano de espaldas.
        // (misma condición que activa StartCinematic en StartDialogue(asset): cámara activada,
        // NPC real y controller vivo — si no se cumple, se mantiene el posicionamiento genérico)
        bool groupStagingHandledByCinematic = asset != null && asset.isGroupConversation
            && useCinematicCamera && DialogueCinematicController.Instance != null
            && npcForPositioning != null && IsActualNPC(npcForPositioning);
        if (!groupStagingHandledByCinematic && Game.NPC.PlayerParty.HasInstance)
        {
            Game.NPC.PlayerParty.Instance.PositionMembersForDialogue(npcForPositioning);
        }
        
        // Iniciar el diálogo (activa cámara cinematográfica y muestra primera línea)
        StartDialogue(asset, onFinished, isSequenceDialogue);
        
        // Emitir evento - los NPCs que lo necesiten se suscriben
        OnDialogueStarted?.Invoke(_currentNpc);
        if (verboseLogging) Debug.Log($"[DialogueManager] 📢 OnDialogueStarted emitido para '{_currentNpc?.name ?? "NULL"}'");
    }

    /// <summary>
    /// Inicia un diálogo de batalla pre-combate (el jugador mira al NPC y entra en stance de batalla)
    /// </summary>
    /// <param name="applyBattlePrep">
    /// ✅ NUEVO (15 ago 2026): si es false, se salta PreparePlayerForBattleDialogue (giro del
    /// jugador, camera shake, slowmo, flash rojo). Pensado para encadenar VARIAS líneas de
    /// batalla seguidas (p.ej. equipos de NPCs donde cada uno dice su propia frase de entrada en
    /// orden): la primera llamada de la secuencia usa true (efectos de impacto normales), y las
    /// siguientes usan false para no repetir shake/slowmo/flash en cada línea.
    /// </param>
    public void StartBattleDialogue(DialogueAsset asset, Transform npc, Action onFinished = null, bool applyBattlePrep = true)
    {
        _currentNpc = npc;

        // Preparar al jugador para el diálogo de batalla
        if (applyBattlePrep && PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null && npc != null)
        {
            PreparePlayerForBattleDialogue(playerGo, npc);
        }

        StartDialogue(asset, onFinished);
    }

    /// <summary>
    /// Prepara al jugador para un diálogo previo a batalla: lo gira hacia el NPC y activa stance de batalla
    /// </summary>
    /// <param name="player">El GameObject del jugador</param>
    /// <param name="npc">El Transform del NPC</param>
    /// <param name="applySlowmo">Si es true, aplica slowmo (solo para pre-batalla, no para derrota)</param>
    private void PreparePlayerForBattleDialogue(GameObject player, Transform npc, bool applySlowmo = true)
    {
        if (verboseLogging) Debug.Log($"[DialogueManager] ⚔️ Preparando jugador para diálogo de batalla con '{npc.name}'");
        
        // 1. Girar al jugador hacia el NPC
        Vector3 directionToNpc = npc.position - player.transform.position;
        directionToNpc.y = 0f; // Mantener rotación en el plano horizontal
        
        if (directionToNpc.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToNpc);
            player.transform.rotation = targetRotation;
            if (verboseLogging) Debug.Log($"[DialogueManager] 👁️ Jugador girado hacia NPC '{npc.name}'");
        }
        
        // 2. Activar animación de Idle de batalla
        var playerAnimator = player.GetComponent<Animator>();
        if (playerAnimator != null)
        {
            // Reproducir animación de Idle de batalla (está en capa UpperBody, índice 1)
            // Usar Play para activar inmediatamente el estado, no CrossFade
            playerAnimator.Play("Idle_Battle_NoWeapon", 1);
            if (verboseLogging) Debug.Log($"[DialogueManager] 🥋 Animación 'Idle_Battle_NoWeapon' activada");
        }
        else
        {
            Debug.LogWarning($"[DialogueManager] ⚠️ No se encontró Animator en el jugador");
        }
        
        // 3. EFECTOS CINEMATOGRÁFICOS DE CÁMARA
        // Camera shake para impacto inicial
        FeedbackService.CameraShake(0.4f, 0.3f);
        if (verboseLogging) Debug.Log($"[DialogueManager] 📹 Camera shake aplicado");
        
        // Slowmo breve SOLO si es diálogo de pre-batalla (no de derrota)
        if (applySlowmo)
        {
            FeedbackService.HitStop(0.5f, 0.3f);
            if (verboseLogging) Debug.Log($"[DialogueManager] ⏱️ Slowmo breve aplicado para dramatismo (pre-batalla)");
        }
        else if (verboseLogging)
        {
            Debug.Log($"[DialogueManager] ⏭️ Slowmo omitido (diálogo de derrota)");
        }
        
        // Screen flash rojo sutil para tensión
        FeedbackService.ScreenFlash(new Color(1f, 0f, 0f, 0.1f), 0.2f);
        if (verboseLogging) Debug.Log($"[DialogueManager] 🔴 Flash rojo sutil aplicado");
    }

    public void Advance()
    {
        if (!IsOpen) return;

        // Si estamos escribiendo y se permite saltar, completa la línea actual
        if (useTypewriter && _isTyping && allowSkipCurrentLine)
        {
            CompleteCurrentLineInstant();
            return;
        }

        Next();
    }

    public void Close()
    {
        StopTypewriter();
        HideChoices();

        if (!IsOpen)
        {
            // ShowWithChoices puede haber activado group.blocksRaycasts sin pasar por StartDialogue
            // (no establece _current), así que IsOpen es false pero la UI puede estar visible.
            // Limpiar siempre para no bloquear raycasts en la siguiente escena.
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            ActivateDialogueMode(false);
            if (submitHint != null) submitHint.SetActive(false);
            if (_hudHiddenForNonCinematicDialogue)
            {
                _hudHiddenForNonCinematicDialogue = false;
                Sendero.UI.PlayerHUDV2.Instance?.ShowHUD();
            }
            // Defensivo: este camino es el de "solo había un choice, nunca StartDialogue" (ver
            // comentario de más arriba), así que normalmente no había nada registrado — pero
            // desregistrar un handler no presente es un no-op seguro en C#.
            NarrativeSkipHub.UnregisterSkipHandler(HandleSkipRequested);
            return;
        }

        _current = null;
        _onEnd?.Invoke();
        _onEnd = null;

        // El diálogo ya está cerrado de verdad (el guard de arriba solo deja llegar aquí con
        // IsOpen == true antes de este punto) — desuscribirse del botón global de skip. Es seguro
        // llamarlo también desde dentro del propio bucle de HandleSkipRequested (Next() → Close()
        // → aquí): -= sobre un delegado de evento no muta la lista que ya está en curso de
        // invocación en NarrativeSkipHub.RequestSkip(), así que no rompe el bucle que nos trajo.
        NarrativeSkipHub.UnregisterSkipHandler(HandleSkipRequested);

        // Ocultar UI
        if (pauseGameWhileOpen) Time.timeScale = 1f;
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        _dreamBackground?.StopDream();
        _dreamSparkles?.StopSparkles();

        // Desactivar sistema cinematográfico
        if (useCinematicCamera && DialogueCinematicController.Instance != null)
        {
            DialogueCinematicController.Instance.EndCinematic();
        }
        else if (useDialogueCameraLegacy && DialogueCameraController.Instance != null)
        {
            // Fallback al sistema antiguo
            DialogueCameraController.Instance.EndDialogueCamera();
        }

        // Restaurar el HUD si lo ocultamos nosotros mismos (diálogo sin cámara cinemática)
        if (_hudHiddenForNonCinematicDialogue)
        {
            _hudHiddenForNonCinematicDialogue = false;
            Sendero.UI.PlayerHUDV2.Instance?.ShowHUD();
        }

        // ✅ Liberar posicionamiento de party members
        if (Game.NPC.PlayerParty.HasInstance)
        {
            Game.NPC.PlayerParty.Instance.ReleaseDialoguePositioning();
        }

        // ✅ Finalizar visuales del speaker activo (sea player, NPC o party member)
        ClearActiveSpeakerAnimations();
        _playerDialogueAnimator = null;
        _currentNpcDialogueCharacterId = null;

        // ✅ Emitir evento - los NPCs que lo necesiten se suscriben
        // NPCSimpleAnimator maneja su propia rotación y animaciones
        OnDialogueClosed?.Invoke(_currentNpc);
        if (verboseLogging) Debug.Log($"[DialogueManager] 📢 OnDialogueClosed emitido para '{_currentNpc?.name ?? "NULL"}'");

        _currentNpc = null;

        // NUEVO: Desactivar modo DialogueActive en PlayerActionManager
        ActivateDialogueMode(false);

        if (submitHint != null)
            submitHint.SetActive(false);
        
        // ✅ IMPORTANTE: Ignorar el botón de salto (A/Submit) después de cerrar el diálogo
        // para evitar que el mismo botón que cerró el diálogo se procese como salto
        GamepadInputReader.IgnoreJumpButton(0.3f);
        if (verboseLogging) Debug.Log($"[DialogueManager] 🚫 Ignorando botón de salto por 0.3s después de cerrar diálogo");

        // Seguridad extra: si por algún motivo quedó SavePrompt activo, liberarlo
        if (GameState.Is(GamePhase.SavePrompt)) GameState.Pop(GamePhase.SavePrompt);
    }

    public void FinalizeChoiceNoFollowUp()
    {
        if (pauseGameWhileOpen) Time.timeScale = 1f;

        // NUEVO: Desactivar modo DialogueActive
        ActivateDialogueMode(false);

        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        _dreamBackground?.StopDream();
        _dreamSparkles?.StopSparkles();

        if (submitHint != null)
            submitHint.SetActive(false);

        // Seguridad extra: garantizar que SavePrompt no quede enganchado
        if (GameState.Is(GamePhase.SavePrompt)) GameState.Pop(GamePhase.SavePrompt);
    }

    void HideChoices(bool restoreGameplay = true)
    {
        if (_choicesUIModePushed)
        {
            Core.PlayerInputManager.Instance?.PopUIMode();
            GamepadInputReader.PopUiNavigationScope();
            _choicesUIModePushed = false;
        }

        if (choicesRoot != null)
        {
            choicesRoot.alpha = 0f;
            choicesRoot.blocksRaycasts = false;
            choicesRoot.interactable = false;
        }
        if (yesButton != null) yesButton.onClick.RemoveListener(OnYesClicked);
        if (noButton  != null) noButton.onClick.RemoveListener(OnNoClicked);
        _onYes = null; _onNo = null;
        if (submitHint != null)
            submitHint.SetActive(false);
        
        // NUEVO: Desactivar modo DialogueActive si no hay diálogo activo
        if (restoreGameplay && !IsOpen)
        {
            ActivateDialogueMode(false);
        }
    }

    void OnYesClicked()
    {
        var cb = _onYes;
        HideChoices(restoreGameplay: false);
        cb?.Invoke();
    }
    void OnNoClicked()
    {
        var cb = _onNo;
        HideChoices(restoreGameplay: false);
        cb?.Invoke();
    }

    public void ShowWithChoices(string message, string yesText, string noText, Action onYes, Action onNo)
    {
        // Asegurar que el panel principal esté visible/interactable
        if (group != null)
        {
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }
        // Resetear contenido anterior
        StopTypewriter();
        if (nameText) nameText.text = string.Empty;
        if (bodyText)
        {
            bodyText.text = string.Empty;
            bodyText.maxVisibleCharacters = int.MaxValue;
        }
        if (portraitImage) portraitImage.enabled = false;
        
        // NUEVO: Activar modo DialogueActive
        ActivateDialogueMode(true);
        
        if (pauseGameWhileOpen) Time.timeScale = 0f;
        if (submitHint != null)
            submitHint.SetActive(false);
        
        if (messageLabel != null && !string.IsNullOrEmpty(message))
            messageLabel.text = message;
        else if (bodyText != null && !string.IsNullOrEmpty(message))
            bodyText.text = message;

        _onYes = onYes; _onNo = onNo;

        if (yesButton != null)
        {
            var lbl = yesButton.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) lbl.text = yesText;
            yesButton.onClick.RemoveListener(OnYesClicked);
            yesButton.onClick.AddListener(OnYesClicked);
            if (yesButton.GetComponent<ChoiceButtonFx>() == null)
                yesButton.gameObject.AddComponent<ChoiceButtonFx>();
        }
        if (noButton != null)
        {
            var lbl = noButton.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) lbl.text = noText;
            noButton.onClick.RemoveListener(OnNoClicked);
            noButton.onClick.AddListener(OnNoClicked);
            if (noButton.GetComponent<ChoiceButtonFx>() == null)
                noButton.gameObject.AddComponent<ChoiceButtonFx>();
        }

        // CORREGIDO: Navegación explícita izquierda/derecha entre ambos botones
        if (yesButton != null && noButton != null)
        {
            var navYes = new Navigation { mode = Navigation.Mode.Explicit };
            navYes.selectOnLeft = null; // No hay nada a la izquierda de Yes
            navYes.selectOnRight = noButton; // Derecha va a No
            yesButton.navigation = navYes;

            var navNo = new Navigation { mode = Navigation.Mode.Explicit };
            navNo.selectOnLeft = yesButton; // Izquierda va a Yes
            navNo.selectOnRight = null; // No hay nada a la derecha de No
            noButton.navigation = navNo;
        }

        // Mostrar visualmente las opciones pero con un pequeño debounce antes de habilitar interacción
        if (choicesRoot != null)
        {
            choicesRoot.alpha = 1f;
            // Evitar confirmar inmediatamente por el mismo botón de interacción (Submit)
            choicesRoot.blocksRaycasts = false;
            choicesRoot.interactable = false;
        }

        var es = EventSystem.current;
        if (es != null && yesButton != null)
        {
            es.sendNavigationEvents = true;
            es.SetSelectedGameObject(yesButton.gameObject);
            yesButton.Select();
            StartCoroutine(SelectNextFrame(yesButton.gameObject));
        }
        else
        {
            Debug.LogWarning($"[DialogueManager] EventSystem or yesButton missing. es={(es!=null)} yes={(yesButton!=null)}");
        }

        // Activar action map UI para que Navigate (joystick/dpad) y Cancel (B) funcionen
        if (!_choicesUIModePushed)
        {
            Core.PlayerInputManager.Instance?.PushUIMode();
            GamepadInputReader.PushUiNavigationScope();
            _choicesUIModePushed = true;
        }

        // Habilitar interacción tras un breve retardo para que no se dispare el Submit previo
        StartCoroutine(ArmChoicesAfterDelay(0.15f));
    }

    System.Collections.IEnumerator SelectNextFrame(GameObject go)
    {
        yield return null;
        var es = EventSystem.current;
        if (es != null && go != null)
            es.SetSelectedGameObject(go);
    }

    System.Collections.IEnumerator ArmChoicesAfterDelay(float delay)
    {
        // Esperar tiempo no escalado por si el juego está pausado durante diálogos
        float t = 0f;
        while (t < delay)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (choicesRoot != null)
        {
            choicesRoot.blocksRaycasts = true;
            choicesRoot.interactable = true;
        }
    }

    private void Next()
    {
        _index++;
        if (_current == null || _current.lines == null || _index >= _current.lines.Length)
        {
            Close();
            return;
        }
        
        var line = _current.lines[_index];
        
        // ✅ Activar animación de hablar para el speaker actual
        ActivateSpeakerTalkAnimation(line);
        
        // ✅ Emitir evento de cambio de línea (para sistema de emociones y otros)
        OnDialogueLineChanged?.Invoke(line, _currentNpc);
        
        // Notificar al sistema cinematográfico del cambio de línea
        if (useCinematicCamera && DialogueCinematicController.Instance != null)
        {
            DialogueCinematicController.Instance.OnDialogueLineAdvanced(
                _index, 
                _current.lines.Length,
                line  // ✅ Pasar la línea completa para detectar cambios de speaker
            );
        }

        // --- NOMBRE DEL HABLANTE (localización con fallback al ID) ---
        string speakerNameToShow = string.Empty;
        if (!string.IsNullOrEmpty(line.speakerNameId))
        {
            if (resolveWithLocalizationManager && LocalizationManager.Instance != null)
                speakerNameToShow = LocalizationManager.Instance.Get(line.speakerNameId, line.speakerNameId);
            else
                speakerNameToShow = line.speakerNameId;
        }
        if (nameText) nameText.text = speakerNameToShow ?? string.Empty;

        // --- TEXTO DEL DIÁLOGO (si hay textId se localiza; si no, usa line.text) ---
        string textToShow = line.text ?? string.Empty;
        if (!string.IsNullOrEmpty(line.textId))
        {
            if (resolveWithLocalizationManager && LocalizationManager.Instance != null)
                textToShow = LocalizationManager.Instance.Get(line.textId, line.text ?? string.Empty);
            else if (!string.IsNullOrEmpty(line.text))
                textToShow = line.text;
            else
                textToShow = string.Empty;
        }
        _currentText = ProtectSpriteTagsFromWordWrap(PinSpriteTagsToExplicitAsset(ResolveDeviceConditionalText(textToShow)));

        // --- PORTRAIT (se mantiene si viene null) ---
        if (portraitImage && line.portrait != null)
        {
            portraitImage.sprite = line.portrait;
            portraitImage.enabled = true;
        }

        // --- PINTADO + TYPEWRITER ---
        if (bodyText)
        {
            StopTypewriter();
            bodyText.text = _currentText;
            if (useTypewriter)
            {
                if (verboseLogging) Debug.Log($"[DialogueManager] TYPEWRITER ACTIVADO - Texto: '{_currentText}' ({_currentText.Length} chars) - Velocidad: {charsPerSecond} chars/s");
                if (TryForceMeshUpdate())
                {
                    bodyText.maxVisibleCharacters = 0;
                    _typeRoutine = StartCoroutine(TypeRoutine());
                }
                else
                {
                    // ForceMeshUpdate falló (ver TryForceMeshUpdate) - renunciamos al typewriter
                    // para esta línea y mostramos el texto completo en vez de dejar el diálogo roto.
                    bodyText.maxVisibleCharacters = int.MaxValue;
                }
            }
            else
            {
                if (verboseLogging) Debug.Log($"[DialogueManager] TYPEWRITER DESACTIVADO - Mostrando texto completo instantáneamente");
                bodyText.maxVisibleCharacters = int.MaxValue;
                // Forzamos el rebuild aquí (dentro del try/catch de TryForceMeshUpdate) en vez de
                // dejarlo para el próximo pase automático de Canvas.SendWillRenderCanvases, que no
                // pasa por nuestro código y por tanto no captura el NRE de SaveSpriteVertexInfo si
                // salta (ver TDD.md § 13 U1 - "límite conocido").
                TryForceMeshUpdate();
            }
        }
    }


    private System.Collections.IEnumerator TypeRoutine()
    {
        _isTyping = true;
        
        // Ocultar el icono de Submit mientras se escribe
        HideSubmitHint();
        
        // Asegurar mesh info
        int total;
        if (TryForceMeshUpdate())
        {
            total = bodyText.textInfo.characterCount;
        }
        else
        {
            // ForceMeshUpdate falló a mitad de la corrutina (ver TryForceMeshUpdate) - abortamos
            // el typewriter y mostramos el texto completo en vez de quedarnos a medio escribir.
            bodyText.maxVisibleCharacters = int.MaxValue;
            _isTyping = false;
            _typeRoutine = null;
            ShowSubmitHintWithAnimation();
            yield break;
        }
        int shown = 0;
        int charactersSinceLastSound = 0;
        if (charsPerSecond <= 0f) charsPerSecond = 35f;
        
        float timePerChar = 1f / charsPerSecond; // Tiempo que debe pasar para mostrar 1 carácter
        float timeAccumulated = 0f; // Acumulador de tiempo
        
        if (verboseLogging) Debug.Log($"[DialogueManager TypeRoutine] ✅ Iniciando typewriter - Total: {total} chars, Velocidad: {charsPerSecond} chars/s ({timePerChar:F4}s por char)");

        while (shown < total)
        {
            // Acumular tiempo desde el último frame
            timeAccumulated += Time.unscaledDeltaTime;
            
            // Mostrar caracteres según el tiempo acumulado
            while (timeAccumulated >= timePerChar && shown < total)
            {
                shown++;
                timeAccumulated -= timePerChar;
                charactersSinceLastSound++;
                
                // Reproducir sonido cada X caracteres
                if (useLetterSound && charactersSinceLastSound >= letterSoundFrequency)
                {
                    PlayLetterSound();
                    charactersSinceLastSound = 0;
                }
            }
            
            bodyText.maxVisibleCharacters = shown;
            // Forzamos el rebuild aquí, dentro del try/catch de TryForceMeshUpdate, en vez de
            // dejar que lo dispare el próximo Canvas.SendWillRenderCanvases automático (fuera de
            // nuestro código, no captura el NRE de SaveSpriteVertexInfo - ver TDD.md § 13 U1).
            if (!TryForceMeshUpdate())
            {
                // Abortamos el typewriter a mitad de línea y mostramos el texto completo en vez
                // de arriesgarnos a que el próximo frame dispare la excepción sin capturar.
                bodyText.maxVisibleCharacters = int.MaxValue;
                _isTyping = false;
                _typeRoutine = null;
                ShowSubmitHintWithAnimation();
                yield break;
            }

            yield return null;
        }

        Debug.Log($"[DialogueManager TypeRoutine] ✅ Completado - {shown}/{total} caracteres mostrados");

        bodyText.maxVisibleCharacters = total;
        TryForceMeshUpdate();
        _isTyping = false;
        _typeRoutine = null;

        // Mostrar el icono de Submit con animación cuando el texto está completo
        ShowSubmitHintWithAnimation();
    }

    private void CompleteCurrentLineInstant()
    {
        if (!bodyText) return;
        StopTypewriter();
        bodyText.maxVisibleCharacters = TryForceMeshUpdate() ? bodyText.textInfo.characterCount : int.MaxValue;
        
        // Marcar el momento en que se completó la línea para ignorar inputs inmediatos
        _lastLineCompletedAt = Time.unscaledTime;
        if (verboseLogging) Debug.Log($"[DialogueManager] Línea completada instantáneamente en t={_lastLineCompletedAt:F3}");
        
        // Mostrar el icono de Submit con animación después de completar instantáneamente
        ShowSubmitHintWithAnimation();
    }

    private void StopTypewriter()
    {
        if (_typeRoutine != null)
        {
            StopCoroutine(_typeRoutine);
            _typeRoutine = null;
        }
        _isTyping = false;
    }

    // Bug conocido de TextMeshPro (com.unity.ugui, TMP_Text.SaveSpriteVertexInfo): puede lanzar
    // NullReferenceException al generar el mesh de un texto con un tag <sprite> resuelto por la
    // cadena de fallbackSpriteAssets (m_currentSpriteAsset/spriteSheet quedan null en un repaso
    // interno de GenerateTextMesh). Sin fix oficial de Unity — solo mitigaciones. Documentado en
    // TDD.md § 13 (bug U1).
    //
    // 2026-08-12 — CORRECCIÓN: la capa 0 (PinSpriteTagsToExplicitAsset, que reescribía
    // <sprite name="X"> a <sprite="X" name="X">) se ha DESACTIVADO porque, revisando el código
    // fuente real de TMP (Library/PackageCache/com.unity.ugui@.../Runtime/TMP/TMP_Text.cs, case
    // MarkupTag.SPRITE, y TMP_SpriteAsset.cs), resulta que el <sprite="AssetName" ...> explícito y
    // el <sprite name="X"> a secas NO usan la misma resolución:
    //   - <sprite name="X">  → TMP_SpriteAsset.SearchForSpriteByHashCode(), que SÍ recorre
    //     spriteAsset.fallbackSpriteAssets recursivamente buscando el CARÁCTER "X". Es la vía que
    //     funciona con DialogueIcons.asset tal y como está montado (start, algas, boots,
    //     interactable_* viven en sub-assets fallback individuales).
    //   - <sprite="AssetName" name="X"> → busca un ASSET (no un carácter) llamado "AssetName" en
    //     MaterialReferenceManager (un registro global de assets ya "vistos"); si no está ahí,
    //     intenta Resources.Load(TMP_Settings.defaultSpriteAssetPath + "AssetName") ("Sprite
    //     Assets/AssetName"). NUNCA mira fallbackSpriteAssets del asset asignado al text object. Los
    //     sub-assets de icono (Assets/Art/UI/DialogueIcons/start.asset, algas.asset, etc.) no viven
    //     en ninguna carpeta Resources ni están pre-registrados en MaterialReferenceManager, así que
    //     esa búsqueda SIEMPRE falla → el tag entero se considera inválido y TMP lo imprime tal cual
    //     como texto plano (exactamente el síntoma reportado: "<sprite="start" name="start">"
    //     visible en pantalla en vez del icono).
    // O sea: la capa 0 no solo no evitaba el crash (no hay evidencia de que lo evitara — nunca
    // llegaba a resolver el sprite por esa vía), sino que rompía SIEMPRE el renderizado normal de
    // cualquier icono en fallback. Se deja PinSpriteTagsToExplicitAsset como no-op (por si algo más
    // la llama) y el texto vuelve a pasar por la resolución por CARÁCTER de TMP, que es la que de
    // verdad funciona con el montaje actual de DialogueIcons.asset. Las capas 1 y 2 seguían de todos
    // modos: <nobr> sigue evitando el corte de línea a mitad de tag, y TryForceMeshUpdate sigue
    // capturando el NullReferenceException si llegase a saltar (no lo cambia esta corrección).
    //
    // Arreglo definitivo si el NRE de SaveSpriteVertexInfo volviera a aparecer en consola: mover el
    // glyph del icono afectado a la tabla PROPIA de DialogueIcons.asset (como ya está "interactable_A")
    // en vez de dejarlo solo como sub-asset fallback — así ni siquiera hace falta recorrer la cadena
    // de fallbacks para encontrarlo. Requiere el Editor de Unity, no se puede hacer editando el
    // .asset a mano seguro.
    //
    // 2026-08-12 — CIERRE DEL "LÍMITE CONOCIDO": hasta ahora TryForceMeshUpdate() solo protegía las
    // llamadas explícitas a ForceMeshUpdate() (inicio de línea / CompleteCurrentLineInstant). Cada
    // vez que TypeRoutine() tocaba bodyText.maxVisibleCharacters frame a frame (o ShowLine() cuando
    // useTypewriter=false), el rebuild del mesh quedaba para el próximo pase automático de
    // Canvas.SendWillRenderCanvases → TextMeshProUGUI.OnPreRenderCanvas, que NO pasa por nuestro
    // código y por tanto no captura el NRE si salta ahí (justo el stack trace real reportado:
    // Canvas:SendWillRenderCanvases() → ... → TMP_Text.SaveSpriteVertexInfo, sin ningún frame de
    // DialogueManager en medio). Ahora cada asignación a maxVisibleCharacters (en TypeRoutine y en
    // la rama sin typewriter de ShowLine) va seguida de un TryForceMeshUpdate() explícito, así que
    // si el bug se dispara, ocurre dentro de nuestro try/catch (LogWarning + fallback a texto
    // completo) en vez de como excepción no capturada en el pase de render.
    private static readonly System.Text.RegularExpressions.Regex _spriteTagRegex =
        new System.Text.RegularExpressions.Regex("<sprite[^>]*>", System.Text.RegularExpressions.RegexOptions.Compiled);

    // Solo captura tags "cortos" tipo <sprite name="X">, que es la única forma que usa este
    // proyecto (ver DialogueIconHelper.cs, obsoleto, y las líneas de Assets/Resources/Localization).
    // Si el tag ya trae un valor explícito (<sprite="Y" name="X">) o usa índice (<sprite=0>) no
    // matchea y se deja tal cual.
    private static readonly System.Text.RegularExpressions.Regex _spriteNameOnlyRegex =
        new System.Text.RegularExpressions.Regex(
            "<sprite\\s+name\\s*=\\s*\"([^\"]+)\"",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    // No-op desde 2026-08-12 (ver comentario grande arriba): reescribir a <sprite="X" name="X">
    // rompía la resolución del sprite en vez de arreglarla. Se deja el método (en vez de borrar la
    // llamada en ShowLine) para que sea fácil de revertir/experimentar sin tocar más sitios.
    private static string PinSpriteTagsToExplicitAsset(string text)
    {
        return text;
    }

    // 2026-08-12 — Segmentos de contenido de diálogo que solo tienen sentido para MANDO, envueltos
    // en el JSON de localización como "<gpadonly>...</gpadonly>". Caso que lo motiva: líneas tipo
    // "puedes usar <sprite name=\"interactable_dpad_up\"> <gpadonly>ARRIBA</gpadonly>." — la
    // palabra "ARRIBA" describe la dirección del D-Pad físico en un mando, pero en Teclado&Ratón el
    // icono ya es la tecla concreta (p.ej. "J", ver InputGlyphNames.DpadUp/InputGlyphLabels), así
    // que repetir "ARRIBA" al lado no aporta nada — "J" no es "arriba" para nadie que no conozca ese
    // binding de memoria. En mando el tag se resuelve dejando el texto tal cual (sin las etiquetas);
    // en teclado se elimina el segmento entero (etiquetas + contenido).
    //
    // LÍMITE CONOCIDO: el texto de la línea se resuelve una única vez, en el momento en que Next()
    // la muestra (ver _currentText más arriba) — a diferencia de los sprites, que sí se refrescan en
    // caliente vía InputGlyphService.FamilyChanged. Si la persona cambia de mando a teclado (o
    // viceversa) A MITAD de una línea que ya se está mostrando, la palabra "ARRIBA" no
    // aparece/desaparece retroactivamente hasta la siguiente línea. Caso límite aceptado: cambiar de
    // familia de dispositivo a mitad de una frase ya visible es raro y el peor caso es una palabra
    // de más/de menos, no un icono equivocado.
    private static readonly System.Text.RegularExpressions.Regex _gamepadOnlyTagRegex =
        new System.Text.RegularExpressions.Regex(
            "<gpadonly>(.*?)</gpadonly>",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.Singleline);

    // 2026-08-12 — Complemento de <gpadonly> para el caso inverso: segmentos de contenido que solo
    // tienen sentido en TECLADO&RATÓN, envueltos como "<kbonly>...</kbonly>". Caso que lo motiva:
    // PROTECCIÓN (ver PlayerShieldController) pide mantener LT+RT a la vez en mando, pero en
    // PlayerControls.inputactions ambas acciones comparten binding de teclado (<Keyboard>/leftCtrl,
    // ver comentario en InputGlyphLabels.GetLabel) — con una sola frase para los dos dispositivos la
    // línea salía "presionas CTRL y CTRL a la vez", que no tiene sentido para nadie con teclado (una
    // sola tecla no se pulsa "a la vez" consigo misma). Con <kbonly>/<gpadonly> el JSON puede dar dos
    // redacciones distintas para la misma línea: en teclado se resuelve dejando el texto de
    // <kbonly> tal cual (sin las etiquetas) y se elimina el segmento <gpadonly> entero; en mando es
    // al revés. Mismo límite conocido que <gpadonly> (ver comentario de arriba): no se refresca en
    // caliente si cambias de dispositivo a mitad de una línea ya visible.
    private static readonly System.Text.RegularExpressions.Regex _keyboardOnlyTagRegex =
        new System.Text.RegularExpressions.Regex(
            "<kbonly>(.*?)</kbonly>",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.Singleline);

    private static string ResolveDeviceConditionalText(string text)
    {
        if (string.IsNullOrEmpty(text) || (!text.Contains("<gpadonly>") && !text.Contains("<kbonly>")))
            return text;

        bool isKeyboard = InputGlyphService.CurrentFamily == InputGlyphDeviceFamily.KeyboardMouse;
        text = _gamepadOnlyTagRegex.Replace(text, m => isKeyboard ? string.Empty : m.Groups[1].Value);
        text = _keyboardOnlyTagRegex.Replace(text, m => isKeyboard ? m.Groups[1].Value : string.Empty);

        // Limpieza de espacios sueltos que deja la eliminación del segmento no aplicable al
        // dispositivo actual, p.ej. "usar <sprite name=\"interactable_dpad_up\"> <gpadonly>ARRIBA</gpadonly>."
        // pasa a "usar <sprite name=\"interactable_dpad_up\">  ." (doble espacio + espacio antes del
        // punto) si no se normaliza.
        text = System.Text.RegularExpressions.Regex.Replace(text, " {2,}", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, " +([.,!?])", "$1");
        return text.Trim();
    }

    private static string ProtectSpriteTagsFromWordWrap(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("<sprite"))
            return text;
        return _spriteTagRegex.Replace(text, m => "<nobr>" + m.Value + "</nobr>");
    }

    private bool TryForceMeshUpdate()
    {
        try
        {
            bodyText.ForceMeshUpdate();
            return true;
        }
        catch (NullReferenceException ex)
        {
            // LogWarning (no LogError) a propósito: este catch ya deja el diálogo en un estado
            // usable (ver arriba), así que no es un error que deba parar el Play Mode con "Error
            // Pause" activado en la consola durante el playtesting. Sigue siendo visible en la
            // consola para detectar si la capa 0 (PinSpriteTagsToExplicitAsset) deja algún caso
            // sin cubrir.
            Debug.LogWarning($"[DialogueManager] ForceMeshUpdate() falló (bug conocido de TMP con <sprite> + fallback sprite assets, ver TDD.md § 13 U1). Texto: '{_currentText}'. Excepción: {ex}");
            return false;
        }
    }
    
    private void HideSubmitHint()
    {
        if (submitHint != null)
        {
            submitHint.SetActive(false);
        }
    }
    
    private void ShowSubmitHintWithAnimation()
    {
        if (submitHint == null) return;
        
        submitHint.SetActive(true);
        
        // Reiniciar escala y aplicar animación de pulso
        submitHint.transform.localScale = Vector3.one;
        submitHint.transform.DOKill(); // Matar animaciones previas
        
        // Animación: escala a 1.15 y vuelve a 1.0 en loop
        submitHint.transform.DOScale(1.15f, 0.5f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true); // usar unscaled time
    }
    
    private void PlayLetterSound()
    {
        if (string.IsNullOrEmpty(letterSoundKey)) return;
        if (AudioService.Instance == null) return;
        
        AudioService.Instance.PlaySFX(letterSoundKey, volume: 0.5f);
    }

    /// <summary>
    /// Activa/desactiva el modo Cinematic en PlayerActionManager para bloquear completamente al jugador durante diálogos
    /// </summary>
    private void ActivateDialogueMode(bool activate)
    {
        // Buscar el jugador usando PlayerService
        if (!PlayerService.TryGetPlayer(out var player, allowSceneLookup: true) || player == null)
        {
            Debug.LogWarning("[DialogueManager] No se encontró el jugador para activar modo diálogo");
            return;
        }

        // Obtener el PlayerActionManager
        var actionManager = player.GetComponent<PlayerActionManager>();
        if (actionManager == null)
        {
            Debug.LogWarning("[DialogueManager] El jugador no tiene PlayerActionManager, no se puede bloquear el movimiento");
            return;
        }

        // Activar/desactivar modo Cinematic (bloquea todo el jugador)
        if (activate)
        {
            actionManager.PushMode(ActionMode.Cinematic);
            if (verboseLogging) Debug.Log("[DialogueManager] Modo Cinematic ACTIVADO - Jugador bloqueado para diálogo");
        }
        else
        {
            actionManager.PopMode(ActionMode.Cinematic);
            if (verboseLogging) Debug.Log("[DialogueManager] Modo Cinematic DESACTIVADO - Jugador desbloqueado tras diálogo");
        }
    }

    /// <summary>
    /// Verifica si el Transform proporcionado es realmente un NPC (no un objeto interactivo como carta, save point, etc.)
    /// NPCSimpleAnimator es el único componente que comparten TODOS los NPCs.
    /// </summary>
    private bool IsActualNPC(Transform npcTransform)
    {
        if (npcTransform == null) return false;
        
        // Verificar si tiene NPCSimpleAnimator (componente común a TODOS los NPCs)
        // NPCSimpleAnimator está en el namespace global
        var npcAnimator = npcTransform.GetComponent<NPCSimpleAnimator>();
        return npcAnimator != null;
    }
    
    /// <summary>
    /// Activa la animación de Talk para el speaker actual (player, NPC o party member)
    /// </summary>
    private void ActivateSpeakerTalkAnimation(DialogueLine line)
    {
        // Determinar quién está hablando
        string speakerId = !string.IsNullOrEmpty(line.speakerNameId) ? line.speakerNameId :
                          line.isPlayerSpeaking ? "Player" : "MainNPC";
        bool speakerIsPlayer = line.isPlayerSpeaking || speakerId == "Player";

        // FIX "el personaje activo se vuelve invisible al hablar" (mismo bug ya corregido para la
        // cámara/mirada grupal en DialogueCinematicController.FindSpeakerTransform, ver su propio
        // comentario): las líneas de diálogo están escritas asumiendo un player fijo, así que un
        // personaje como Liam o Estela siempre trae isPlayerSpeaking=false, incluso cuando en esa
        // partida concreta lo está controlando el jugador. Si speakerId identifica al party member
        // que ActiveCharacterSwapper tiene oculto ahora mismo (el jugador YA representa físicamente
        // a ese personaje), el hablante real y visible es el player, no ese NPCPartyMember con los
        // renderers apagados — sin esto, la animación de hablar se dispara sobre un cuerpo invisible
        // y el que se ve en pantalla se queda en Idle.
        // 'promotedFromHiddenDecoy' distingue POR QUÉ speakerIsPlayer es true: true real (línea de
        // Will, o speakerId=="Player") vs. promovida aquí abajo porque el speaker es el party member
        // que ActiveCharacterSwapper tiene oculto (Liam/Estela siendo controlados). Sin esta
        // distinción, el guard de "línea de Will" de más abajo no podía diferenciar ambos casos y
        // secuestraba también las líneas propias de Liam/Estela — INC-147, ver commit de hoy.
        bool promotedFromHiddenDecoy = false;
        if (!speakerIsPlayer && ActiveCharacterSwapper.Instance != null)
        {
            var hiddenNpc = ActiveCharacterSwapper.Instance.HiddenNpc;
            if (hiddenNpc != null && hiddenNpc.NPCManager != null)
            {
                var hiddenMgr = hiddenNpc.NPCManager;
                bool isHiddenSpeaker = (!string.IsNullOrEmpty(hiddenMgr.DialogueCharacterId) && hiddenMgr.DialogueCharacterId == speakerId)
                    || hiddenMgr.PersistenceId == speakerId
                    || hiddenNpc.gameObject.name == speakerId;
                if (isHiddenSpeaker)
                {
                    speakerIsPlayer = true;
                    promotedFromHiddenDecoy = true;
                }
            }
        }

        // FIX (1 sep 2026, INC-136/INC-138 seguimiento) "línea de Will anima al personaje físico
        // controlado en vez de a Will": las líneas marcadas isPlayerSpeaking (o speakerId=="Player")
        // están escritas asumiendo que Will ES el player — igual que el fix de más arriba asume que
        // un NPC con el DialogueCharacterId del personaje activo oculto es en realidad el player.
        // Pero cuando el player físico está controlando a Liam o Estela, Will deja de ser ese cuerpo:
        // pasa a ser un NPCPartyMember visible aparte (ActiveCharacterSwapper.WillNpcInstance, no
        // nulo exactamente en ese caso — ver SwitchCharacter() paso 6, "instanciar al alejarse de
        // Will, destruir al volver"). Sin este guard, SetPlayerTalkingAnimation() animaba el cuerpo
        // físico controlado (p.ej. Liam) para una línea que en pantalla se atribuye a Will.
        //
        // FIX (1 sep 2026, INC-147) "'!promotedFromHiddenDecoy' añadido": este guard usaba solo
        // 'speakerIsPlayer', así que también disparaba para la propia línea de Liam/Estela cuando
        // el fix de arriba la promociona a speakerIsPlayer=true (ella siendo el hidden decoy). Eso
        // secuestraba la animación de Estela/Liam y la redirigía a Will, dejando al personaje
        // realmente hablando (visible en pantalla) en Idle. Repro: hablar con el Rey controlando a
        // Estela — su línea "Ya verás cuando te pille" animaba a Will en vez de a ella.
        if (speakerIsPlayer && !promotedFromHiddenDecoy && ActiveCharacterSwapper.Instance != null && ActiveCharacterSwapper.Instance.WillNpcInstance != null)
        {
            var willAnimator = ActiveCharacterSwapper.Instance.WillNpcInstance.GetComponent<NPCSimpleAnimator>();
            if (willAnimator != null)
            {
                if (!_activeDialogueSpeakerIsPlayer && _activeDialogueSpeakerAnimator == willAnimator)
                {
                    willAnimator.SetTalking(true);
                    willAnimator.PlayBodyEmotion(line.emotion);
                    return;
                }

                ClearActiveSpeakerAnimations();
                willAnimator.BeginInteraction();
                willAnimator.SetTalking(true);
                willAnimator.PlayBodyEmotion(line.emotion);
                _activeDialogueSpeakerAnimator = willAnimator;
                if (verboseLogging) Debug.Log("[DialogueManager] 🗣️ Speaker 'Will (NPC)' activado -- el controller físico es otro personaje");
                return;
            }
        }

        if (speakerIsPlayer)
        {
            if (_activeDialogueSpeakerIsPlayer && _activeDialogueSpeakerAnimator == null)
            {
                SetPlayerTalkingAnimation(true);
                _playerDialogueAnimator?.PlayBodyEmotion(line.emotion);
                return;
            }

            ClearActiveSpeakerAnimations();
            SetPlayerTalkingAnimation(true);
            ActivatePlayerInteractionAnimation(true);
            _playerDialogueAnimator?.PlayBodyEmotion(line.emotion);
            _activeDialogueSpeakerIsPlayer = true;
            if (verboseLogging) Debug.Log("[DialogueManager] 🗣️ Player speaker activado");
            return;
        }

        NPCSimpleAnimator speakerAnimator = null;
        string speakerDebugName = speakerId;

        // Caso 1: NPC principal (matching por nombre de GO, por dialogueCharacterId, o por "MainNPC" genérico)
        if (_currentNpc != null)
        {
            bool isMainNpc = speakerId == "MainNPC"
                || _currentNpc.name == speakerId
                || (!string.IsNullOrEmpty(_currentNpcDialogueCharacterId) && _currentNpcDialogueCharacterId == speakerId);
            if (isMainNpc)
            {
                speakerAnimator = _currentNpc.GetComponent<NPCSimpleAnimator>();
                speakerDebugName = _currentNpc.name;
            }
        }

        // Caso 2: Party members
        if (speakerAnimator == null && Game.NPC.PlayerParty.HasInstance)
        {
            var party = Game.NPC.PlayerParty.Instance;
            foreach (var member in party.Members)
            {
                if (member.NPCManager == null) continue;

                var mgr = member.NPCManager;
                if ((!string.IsNullOrEmpty(mgr.DialogueCharacterId) && mgr.DialogueCharacterId == speakerId) ||
                    mgr.PersistenceId == speakerId ||
                    member.gameObject.name == speakerId)
                {
                    speakerAnimator = member.GetComponent<NPCSimpleAnimator>();
                    speakerDebugName = member.DisplayName;
                    break;
                }
            }
        }

        // Caso 3: Buscar en escena por nombre
        if (speakerAnimator == null)
        {
            var foundNPC = GameObject.Find(speakerId);
            if (foundNPC != null)
            {
                speakerAnimator = foundNPC.GetComponent<NPCSimpleAnimator>();
                speakerDebugName = foundNPC.name;
            }
        }

        if (speakerAnimator == null)
        {
            ClearActiveSpeakerAnimations();
            if (verboseLogging) Debug.LogWarning($"[DialogueManager] ⚠️ No se encontró animator para speaker '{speakerId}'");
            return;
        }

        // Si sigue hablando el mismo speaker, no re-disparar BeginInteraction pero sí actualizar la emoción
        if (!_activeDialogueSpeakerIsPlayer && _activeDialogueSpeakerAnimator == speakerAnimator)
        {
            speakerAnimator.SetTalking(true);
            speakerAnimator.PlayBodyEmotion(line.emotion);
            return;
        }

        ClearActiveSpeakerAnimations();
        speakerAnimator.BeginInteraction();
        speakerAnimator.SetTalking(true);
        speakerAnimator.PlayBodyEmotion(line.emotion);
        _activeDialogueSpeakerAnimator = speakerAnimator;
        if (verboseLogging) Debug.Log($"[DialogueManager] 🗣️ Speaker '{speakerDebugName}' activado");
    }

    private void ClearActiveSpeakerAnimations()
    {
        if (_activeDialogueSpeakerIsPlayer)
        {
            SetPlayerTalkingAnimation(false);
            ActivatePlayerInteractionAnimation(false);
            _activeDialogueSpeakerIsPlayer = false;
        }

        if (_activeDialogueSpeakerAnimator != null)
        {
            _activeDialogueSpeakerAnimator.SetTalking(false);
            _activeDialogueSpeakerAnimator.EndInteraction();
            _activeDialogueSpeakerAnimator = null;
        }
    }

    private bool SetPlayerTalkingAnimation(bool isTalking)
    {
        if (!PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) || playerGo == null)
            return false;

        var npcAnimator = playerGo.GetComponent<NPCSimpleAnimator>();
        if (npcAnimator != null)
        {
            npcAnimator.SetTalking(isTalking);
            return true;
        }

        var animator = playerGo.GetComponent<Animator>() ?? playerGo.GetComponentInChildren<Animator>(true);
        if (animator == null)
            return false;

        foreach (var paramName in PlayerTalkBoolCandidates)
        {
            if (TrySetAnimatorBool(animator, paramName, isTalking))
                return true;
        }

        return false;
    }

    private bool TrySetAnimatorBool(Animator animator, string parameterName, bool value)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
            return false;

        foreach (var param in animator.parameters)
        {
            if (param.type == AnimatorControllerParameterType.Bool && param.name == parameterName)
            {
                animator.SetBool(parameterName, value);
                return true;
            }
        }

        return false;
    }

    private bool TryPlayStateOnAnyLayer(Animator animator, string stateName, float crossFadeDuration = 0.12f)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return false;

        int stateHash = Animator.StringToHash(stateName);
        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            if (!animator.HasState(layer, stateHash))
                continue;

            animator.CrossFade(stateHash, crossFadeDuration, layer, 0f);
            return true;
        }

        return false;
    }

    private bool TryPlayAnyState(Animator animator, string[] stateCandidates, out string playedState)
    {
        playedState = null;
        if (animator == null || stateCandidates == null)
            return false;

        foreach (var state in stateCandidates)
        {
            if (!TryPlayStateOnAnyLayer(animator, state))
                continue;

            playedState = state;
            return true;
        }

        return false;
    }
    
    /// <summary>
    /// Activa/desactiva la animación del player speaker durante diálogos
    /// </summary>
    private void ActivatePlayerInteractionAnimation(bool activate)
    {
        if (!PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) || playerGo == null)
        {
            Debug.LogWarning("[DialogueManager] ⚠️ ActivatePlayerInteractionAnimation: No se encontró el player");
            return;
        }

        // Si el player usa NPCSimpleAnimator, usar la animación de interacción de diálogo.
        var npcSimpleAnimator = playerGo.GetComponent<NPCSimpleAnimator>();
        if (npcSimpleAnimator != null)
        {
            if (activate)
            {
                npcSimpleAnimator.BeginInteraction();
                if (verboseLogging) Debug.Log($"[DialogueManager] 🎭 Player '{playerGo.name}' animación InteractWithPeople ACTIVADA (NPCSimpleAnimator)");
            }
            else
            {
                npcSimpleAnimator.EndInteraction();
                if (verboseLogging) Debug.Log($"[DialogueManager] 🎭 Player '{playerGo.name}' animación InteractWithPeople DESACTIVADA (NPCSimpleAnimator)");
            }
            return;
        }

        // Fallback para player real (Invector / Animator directo)
        var animator = playerGo.GetComponent<Animator>() ?? playerGo.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            Debug.LogWarning($"[DialogueManager] ⚠️ ActivatePlayerInteractionAnimation: Player '{playerGo.name}' no tiene Animator");
            return;
        }

        if (activate)
        {
            bool played = TryPlayAnyState(animator, PlayerSpeakStateCandidates, out var playedState);
            if (verboseLogging)
            {
                if (played)
                    Debug.Log($"[DialogueManager] 🎭 Player '{playerGo.name}' animación diálogo ACTIVADA via Animator state '{playedState}'");
                else
                    Debug.LogWarning($"[DialogueManager] ⚠️ Player '{playerGo.name}' no tiene un state de interacción compatible para diálogo");
            }
        }
        else
        {
            SetPlayerTalkingAnimation(false);

            bool played = TryPlayAnyState(animator, PlayerLocomotionStateCandidates, out var playedState);
            if (verboseLogging && played)
            {
                Debug.Log($"[DialogueManager] 🎭 Player '{playerGo.name}' animación diálogo DESACTIVADA, retorno a '{playedState}'");
            }
        }
    }
}
