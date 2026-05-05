using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Core;
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
    public bool IsOpen => _current != null;

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

    // Typewriter estado
    Coroutine _typeRoutine;
    bool _isTyping;
    string _currentText = string.Empty;

    // NPC para cámara de diálogo
    private Transform _currentNpc;
    private NPCSimpleAnimator _activeDialogueSpeakerAnimator;
    private bool _activeDialogueSpeakerIsPlayer;
    
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
    }

    private void HandleGamepadInput(GamepadInputReader.InputEvent input)
    {
        if (input.Phase != InputActionPhase.Performed) return;

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

    public void StartDialogue(DialogueAsset asset, Action onFinished = null)
    {
        if (asset == null || asset.lines == null || asset.lines.Length == 0) return;

        ClearActiveSpeakerAnimations();
        _current = asset;
        _onEnd = onFinished;
        _index = -1;

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
    public void StartDialogue(DialogueAsset asset, Transform npc, Action onFinished = null)
    {
        _currentNpc = npc;
        
        // ✅ CRÍTICO: Iniciar el diálogo PRIMERO para que IsOpen sea true
        StartDialogue(asset, onFinished);
        
        // ✅ Posicionar party members para el diálogo
        if (Game.NPC.PlayerParty.HasInstance)
        {
            Game.NPC.PlayerParty.Instance.PositionMembersForDialogue(npc);
        }
        
        // ✅ Emitir evento - los NPCs que lo necesiten se suscriben
        // NPCSimpleAnimator maneja su propia rotación y animaciones
        OnDialogueStarted?.Invoke(_currentNpc);
        if (verboseLogging) Debug.Log($"[DialogueManager] 📢 OnDialogueStarted emitido para '{_currentNpc?.name ?? "NULL"}'");
    }

    /// <summary>
    /// Inicia un diálogo de batalla pre-combate (el jugador mira al NPC y entra en stance de batalla)
    /// </summary>
    public void StartBattleDialogue(DialogueAsset asset, Transform npc, Action onFinished = null)
    {
        _currentNpc = npc;
        
        // Preparar al jugador para el diálogo de batalla
        if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null && npc != null)
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
        if (!IsOpen) return;

        StopTypewriter();

        HideChoices();

        _current = null;
        _onEnd?.Invoke();
        _onEnd = null;

        // Ocultar UI
        if (pauseGameWhileOpen) Time.timeScale = 1f;
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

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
        
        // ✅ Liberar posicionamiento de party members
        if (Game.NPC.PlayerParty.HasInstance)
        {
            Game.NPC.PlayerParty.Instance.ReleaseDialoguePositioning();
        }

        // ✅ Finalizar visuales del speaker activo (sea player, NPC o party member)
        ClearActiveSpeakerAnimations();

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
        if (submitHint != null)
            submitHint.SetActive(false);

        // Seguridad extra: garantizar que SavePrompt no quede enganchado
        if (GameState.Is(GamePhase.SavePrompt)) GameState.Pop(GamePhase.SavePrompt);
    }

    void HideChoices(bool restoreGameplay = true)
    {
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
            if (LocalizationManager.Instance != null)
                speakerNameToShow = LocalizationManager.Instance.Get(line.speakerNameId, line.speakerNameId);
            else
                speakerNameToShow = line.speakerNameId;
        }
        if (nameText) nameText.text = speakerNameToShow ?? string.Empty;

        // --- TEXTO DEL DIÁLOGO (si hay textId se localiza; si no, usa line.text) ---
        string textToShow = line.text ?? string.Empty;
        if (!string.IsNullOrEmpty(line.textId))
        {
            if (LocalizationManager.Instance != null)
                textToShow = LocalizationManager.Instance.Get(line.textId, line.text ?? string.Empty);
            else if (!string.IsNullOrEmpty(line.text))
                textToShow = line.text;
            else
                textToShow = string.Empty;
        }
        _currentText = textToShow;

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
                bodyText.ForceMeshUpdate();
                bodyText.maxVisibleCharacters = 0;
                _typeRoutine = StartCoroutine(TypeRoutine());
            }
            else
            {
                if (verboseLogging) Debug.Log($"[DialogueManager] TYPEWRITER DESACTIVADO - Mostrando texto completo instantáneamente");
                bodyText.maxVisibleCharacters = int.MaxValue;
            }
        }
    }


    private System.Collections.IEnumerator TypeRoutine()
    {
        _isTyping = true;
        
        // Ocultar el icono de Submit mientras se escribe
        HideSubmitHint();
        
        // Asegurar mesh info
        bodyText.ForceMeshUpdate();
        int total = bodyText.textInfo.characterCount;
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
            
            yield return null;
        }
        
        Debug.Log($"[DialogueManager TypeRoutine] ✅ Completado - {shown}/{total} caracteres mostrados");

        bodyText.maxVisibleCharacters = total;
        _isTyping = false;
        _typeRoutine = null;
        
        // Mostrar el icono de Submit con animación cuando el texto está completo
        ShowSubmitHintWithAnimation();
    }

    private void CompleteCurrentLineInstant()
    {
        if (!bodyText) return;
        StopTypewriter();
        bodyText.ForceMeshUpdate();
        bodyText.maxVisibleCharacters = bodyText.textInfo.characterCount;
        
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

        if (speakerIsPlayer)
        {
            if (_activeDialogueSpeakerIsPlayer && _activeDialogueSpeakerAnimator == null)
            {
                SetPlayerTalkingAnimation(true);
                return;
            }

            ClearActiveSpeakerAnimations();
            SetPlayerTalkingAnimation(true);
            ActivatePlayerInteractionAnimation(true);
            _activeDialogueSpeakerIsPlayer = true;
            if (verboseLogging) Debug.Log("[DialogueManager] 🗣️ Player speaker activado");
            return;
        }

        NPCSimpleAnimator speakerAnimator = null;
        string speakerDebugName = speakerId;

        // Caso 1: NPC principal
        if (speakerId == "MainNPC" || (_currentNpc != null && _currentNpc.name == speakerId))
        {
            if (_currentNpc != null)
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
                if (member.NPCManager?.Configuration?.interactiveNarrativeConfig == null)
                    continue;

                var config = member.NPCManager.Configuration.interactiveNarrativeConfig;
                if ((!string.IsNullOrEmpty(config.dialogueCharacterId) && config.dialogueCharacterId == speakerId) ||
                    config.persistenceId == speakerId ||
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

        // Si sigue hablando el mismo speaker, evitar re-disparar animaciones cada línea
        if (!_activeDialogueSpeakerIsPlayer && _activeDialogueSpeakerAnimator == speakerAnimator)
        {
            speakerAnimator.SetTalking(true);
            return;
        }

        ClearActiveSpeakerAnimations();
        speakerAnimator.BeginInteraction();
        speakerAnimator.SetTalking(true);
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
