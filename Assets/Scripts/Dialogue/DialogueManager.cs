using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
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

    // Estado
    private DialogueAsset current;
    private int index = -1;
    private Action onEnd;
    public bool IsOpen => current != null;

    // Typewriter estado
    Coroutine _typeRoutine;
    bool _isTyping;
    string _currentText = string.Empty;

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
        if (pauseGameWhileOpen) Time.timeScale = 0f;

        // Bloquear gameplay
        SetGameplayEnabled(false);

        Next(); // pinta primera línea
    }

    public void Advance()
    {
        if (!IsOpen) return;

        // Si estamos escribiendo y se permite saltar, completar la línea actual
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

        // Restaurar gameplay
        SetGameplayEnabled(true);
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

        // Resolver nombre del hablante con localización
        string speakerNameToShow = "";
        if (!string.IsNullOrEmpty(line.speakerNameId) && LocalizationManager.Instance != null)
        {
            speakerNameToShow = LocalizationManager.Instance.Get(line.speakerNameId, "");
        }
        
        if (nameText) nameText.text = speakerNameToShow;

        // Resolver texto del diálogo con localización
        string textToShow = "";
        if (!string.IsNullOrEmpty(line.textId) && LocalizationManager.Instance != null)
        {
            textToShow = LocalizationManager.Instance.Get(line.textId, "");
        }

        _currentText = textToShow;

        if (portraitImage)
        {
            if (line.portrait != null)
            {
                portraitImage.sprite = line.portrait;
                portraitImage.enabled = true;
            }
            // Si portrait es null, mantenemos el anterior visible
        }

        if (bodyText)
        {
            // Preparar typewriter o texto completo
            StopTypewriter();
            bodyText.text = _currentText;
            if (useTypewriter)
            {
                // Forzar actualización de mesh para obtener characterCount correcto
                bodyText.ForceMeshUpdate();
                bodyText.maxVisibleCharacters = 0;
                _typeRoutine = StartCoroutine(TypeRoutine());
            }
            else
            {
                bodyText.maxVisibleCharacters = int.MaxValue; // todo el texto
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
    }
}
