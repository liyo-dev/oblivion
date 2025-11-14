using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

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

    [Header("Input (solo mando)")]
    [Tooltip("Acción para AVANZAR. Usa UI/Submit (Gamepad South = A).")]
    [SerializeField] private InputActionReference advanceAction;

    [Header("Bloqueo de Inputs")]
    [Tooltip("Referencias a InputActionReference que se deshabilitan mientras el diálogo esté abierto (p.ej. movimiento, ataque, etc.).")]
    [SerializeField] private InputActionReference[] inputActionsToDisable;

    [Header("Opcional")]
    [SerializeField] private bool pauseGameWhileOpen;
    [SerializeField] private bool resolveWithLocalizationManager = true;

    [Header("Cámara de Diálogo")]
    [Tooltip("Si está activo, la cámara se posicionará para enfocar la conversación con NPCs")]
    [SerializeField] private bool useDialogueCamera = true;

    [Header("UI Hints")]
    [SerializeField] private GameObject submitHint;

    // Estado
    private DialogueAsset current;
    private int index = -1;
    private Action onEnd;
    public bool IsOpen => current != null;

    [Header("Choices (optional)")]
    [SerializeField] private CanvasGroup choicesRoot;   // contenedor de los botones
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [SerializeField] private TextMeshProUGUI messageLabel; // si quieres sobreescribir el texto principal
    [SerializeField] private bool enableDpadHorizontalFallback = true; // si Navigate no recoge el D-Pad
    [SerializeField, Min(0f)] private float dpadRepeatDelay = 0.2f;

    float _dpadCooldown;

    System.Action _onYes;
    System.Action _onNo;

    // Typewriter estado
    Coroutine _typeRoutine;
    bool _isTyping;
    string _currentText = string.Empty;

    // NPC para cámara de diálogo
    private Transform currentNPC = null;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

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
        if (advanceAction?.action != null)
        {
            if (!advanceAction.action.enabled) advanceAction.action.Enable();
            advanceAction.action.performed += OnAdvance;
        }
    }

    void OnDisable()
    {
        if (advanceAction?.action != null)
            advanceAction.action.performed -= OnAdvance;
    }

    void Update()
    {
        // Fallback explícito para D-Pad izquierda/derecha cuando las opciones están activas
        if (!enableDpadHorizontalFallback) return;
        if (choicesRoot == null || !choicesRoot.interactable) return;

        if (_dpadCooldown > 0f) _dpadCooldown -= Time.unscaledDeltaTime;

        bool left = false, right = false;
#if ENABLE_INPUT_SYSTEM
        var gp = UnityEngine.InputSystem.Gamepad.current;
        if (gp != null)
        {
            left  |= gp.dpad.left.wasPressedThisFrame;
            right |= gp.dpad.right.wasPressedThisFrame;
        }
#endif
        // Teclado
        left  |= Input.GetKeyDown(KeyCode.LeftArrow);
        right |= Input.GetKeyDown(KeyCode.RightArrow);

        if ((left || right) && _dpadCooldown <= 0f)
        {
            var es = EventSystem.current;
            if (es != null)
            {
                var cur = es.currentSelectedGameObject;
                if (cur == null || !cur.activeInHierarchy)
                {
                    if (yesButton) es.SetSelectedGameObject(yesButton.gameObject);
                }
                else if (yesButton != null && noButton != null)
                {
                    if (cur == yesButton.gameObject && right)
                        es.SetSelectedGameObject(noButton.gameObject);
                    else if (cur == noButton.gameObject && left)
                        es.SetSelectedGameObject(yesButton.gameObject);
                }
            }
            _dpadCooldown = dpadRepeatDelay;
        }
    }

    public void StartDialogue(DialogueAsset asset, Action onFinished = null)
    {
        if (asset == null || asset.lines == null || asset.lines.Length == 0) return;

        current = asset;
        onEnd = onFinished;
        index = -1;

        // Mostrar UI
        if (group != null)
        {
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }
        if (submitHint != null)
            submitHint.SetActive(true);
        if (pauseGameWhileOpen) Time.timeScale = 0f;

        // Bloquear gameplay
        SetGameplayEnabled(false);

        // NUEVO: Activar modo DialogueActive en PlayerActionManager
        ActivateDialogueMode(true);

        // Activar cámara de diálogo si hay un NPC asignado
        if (useDialogueCamera && currentNPC != null && DialogueCameraController.Instance != null)
        {
            DialogueCameraController.Instance.StartDialogueCamera(currentNPC);
        }

        Next(); // pinta primera línea
    }

    /// <summary>
    /// Inicia un diálogo con un NPC específico (para usar la cámara de diálogo)
    /// </summary>
    public void StartDialogue(DialogueAsset asset, Transform npc, Action onFinished = null)
    {
        currentNPC = npc;
        StartDialogue(asset, onFinished);
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

        current = null;
        onEnd?.Invoke();
        onEnd = null;

        // Ocultar UI
        if (pauseGameWhileOpen) Time.timeScale = 1f;
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        // Desactivar cámara de diálogo
        if (useDialogueCamera && DialogueCameraController.Instance != null)
        {
            DialogueCameraController.Instance.EndDialogueCamera();
        }

        currentNPC = null;

        // NUEVO: Desactivar modo DialogueActive en PlayerActionManager
        ActivateDialogueMode(false);

        // Restaurar gameplay
        SetGameplayEnabled(true);
        if (submitHint != null)
            submitHint.SetActive(false);

        // Seguridad extra: si por algún motivo quedó SavePrompt activo, liberarlo
        if (GameState.Is(GamePhase.SavePrompt)) GameState.Pop(GamePhase.SavePrompt);
    }

    public void FinalizeChoiceNoFollowUp()
    {
        if (pauseGameWhileOpen) Time.timeScale = 1f;
        
        // NUEVO: Desactivar modo DialogueActive
        ActivateDialogueMode(false);
        
        SetGameplayEnabled(true);
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
            SetGameplayEnabled(true);
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

    public void ShowWithChoices(string message, string yesText, string noText, System.Action onYes, System.Action onNo)
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
        
        // bloquear gameplay igual que StartDialogue
        SetGameplayEnabled(false);
        
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

        // Navegación explícita izquierda/derecha entre ambos botones (UI/Navigate)
        if (yesButton != null && noButton != null)
        {
            var navYes = new Navigation { mode = Navigation.Mode.Explicit };
            navYes.selectOnLeft = noButton; navYes.selectOnRight = noButton;
            yesButton.navigation = navYes;

            var navNo = new Navigation { mode = Navigation.Mode.Explicit };
            navNo.selectOnLeft = yesButton; navNo.selectOnRight = yesButton;
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

    private void OnAdvance(InputAction.CallbackContext _)
    {
        if (IsOpen) Advance();
    }

    private void Next()
    {
        index++;
        if (current == null || current.lines == null || index >= current.lines.Length)
        {
            Close();
            return;
        }

        var line = current.lines[index];

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
                bodyText.ForceMeshUpdate();
                bodyText.maxVisibleCharacters = 0;
                _typeRoutine = StartCoroutine(TypeRoutine());
            }
            else
            {
                bodyText.maxVisibleCharacters = int.MaxValue;
            }
        }
    }

    private System.Collections.IEnumerator TypeRoutine()
    {
        _isTyping = true;
        // Asegurar mesh info
        bodyText.ForceMeshUpdate();
        int total = bodyText.textInfo.characterCount;
        int shown = 0;
        if (charsPerSecond <= 0f) charsPerSecond = 35f;

        while (shown < total)
        {
            // avanzar con tiempo no escalado para funcionar si Time.timeScale=0
            shown += Mathf.Max(1, Mathf.FloorToInt(charsPerSecond * Time.unscaledDeltaTime));
            bodyText.maxVisibleCharacters = Mathf.Clamp(shown, 0, total);
            yield return null;
        }

        bodyText.maxVisibleCharacters = total;
        _isTyping = false;
        _typeRoutine = null;
    }

    private void CompleteCurrentLineInstant()
    {
        if (!bodyText) return;
        StopTypewriter();
        bodyText.ForceMeshUpdate();
        bodyText.maxVisibleCharacters = bodyText.textInfo.characterCount;
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

    private void SetGameplayEnabled(bool enable)
    {
        if (inputActionsToDisable != null)
        {
            foreach (var actionRef in inputActionsToDisable)
            {
                if (actionRef?.action != null)
                {
                    if (enable)
                        actionRef.action.Enable();
                    else
                        actionRef.action.Disable();
                }
            }
        }
        if (enable && pauseGameWhileOpen)
            Time.timeScale = 1f;
    }

    /// <summary>
    /// Activa/desactiva el modo Cinematic en PlayerActionManager para bloquear completamente al jugador durante diálogos
    /// </summary>
    private void ActivateDialogueMode(bool activate)
    {
        // Buscar el jugador
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[DialogueManager] No se encontró el jugador con tag 'Player' para activar modo diálogo");
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
            Debug.Log("[DialogueManager] Modo Cinematic ACTIVADO - Jugador bloqueado para diálogo");
        }
        else
        {
            actionManager.PopMode(ActionMode.Cinematic);
            Debug.Log("[DialogueManager] Modo Cinematic DESACTIVADO - Jugador desbloqueado tras diálogo");
        }
    }
}
