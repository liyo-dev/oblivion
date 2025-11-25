using UnityEngine;
using TMPro;
using System.Collections;
using System.Linq;
using DG.Tweening;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class QuestLogListUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform contentRoot;      // ScrollView/Viewport/Content
    [SerializeField] private QuestLogItemUI itemPrefab;  // Prefab de item misión
    [SerializeField] private TextMeshProUGUI headerText; // "Misiones" (opcional)
    [SerializeField] private bool showInactive = false;  // filtrar inactivas
    [SerializeField] private GameObject panelRoot;       // El panel completo para show/hide
    [SerializeField] private GameObject scrollView;      // Solo el ScrollView para ocultar
    [SerializeField] private TextMeshProUGUI helpText;   // Texto de ayuda para cambiar
    [SerializeField] private QuestMainMenuUI mainMenu;   // menú principal de misiones
    [SerializeField] private bool debugLogs = false;

    [Header("Animación (DOTween)")]
    [SerializeField] private RectTransform animatedRoot;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private float hideAfterSeconds = 4f;
    [SerializeField] private float hideDistance = 420f;
    [SerializeField] private float tweenDuration = 0.35f;
    [SerializeField] private Ease tweenEase = Ease.InOutSine;

    bool _bound;                    // ya suscrito al manager
    QuestManager _qm;               // cache del manager suscrito
    Coroutine _waitCo;
    private bool _isPanelVisible = false; // Estado del panel (oculto por defecto)
    private Tween _panelTween;
    private Vector2 _shownPos;
    private Vector2 _hiddenPos;
    private Coroutine _autoHideCo;
#if ENABLE_INPUT_SYSTEM
    private InputAction _quickAccessAction;
#endif

    void OnEnable()
    {
        Debug.Log("[QuestLogListUI] OnEnable: starting BindWhenReady (always logged)");
        // Empieza a esperar al manager si aún no existe
        _waitCo = StartCoroutine(BindWhenReady());

        Debug.Log($"[QuestLogListUI] Refs: contentRoot={(contentRoot!=null)}, itemPrefab={(itemPrefab!=null)}, panelRoot={(panelRoot!=null)}, scrollView={(scrollView!=null)}");

#if ENABLE_INPUT_SYSTEM
        if (_quickAccessAction == null)
        {
            _quickAccessAction = new InputAction("QuestLogQuick", InputActionType.Button);
            _quickAccessAction.AddBinding("<Gamepad>/dpad/up");
            _quickAccessAction.AddBinding("<Keyboard>/upArrow");
        }
        _quickAccessAction.performed += OnQuickAccessPerformed;
        try { _quickAccessAction.Enable(); }
        catch { }
#endif

        if (animatedRoot)
        {
            _shownPos = animatedRoot.anchoredPosition;
            _hiddenPos = _shownPos + Vector2.down * hideDistance;
            animatedRoot.anchoredPosition = _hiddenPos;
        }

        if (panelRoot) panelRoot.SetActive(false);
        if (scrollView) scrollView.SetActive(false);
        UpdateHelpText();
    }

    void OnDisable()
    {
        Unbind();
        if (_waitCo != null) { StopCoroutine(_waitCo); _waitCo = null; }
        KillTween();
        if (_autoHideCo != null) StopCoroutine(_autoHideCo);
#if ENABLE_INPUT_SYSTEM
        if (_quickAccessAction != null)
        {
            _quickAccessAction.performed -= OnQuickAccessPerformed;
            _quickAccessAction.Disable();
        }
#endif
    }

    void Update()
    {
        // Respetar GameState: no abrir/cerrar si UI global no lo permite
        if (!GameState.CanOpenInventory || (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen))
        {
            if (debugLogs) Debug.Log($"[QuestLogListUI] Update: blocked CanOpenInventory={GameState.CanOpenInventory} DialogueOpen={(DialogueManager.Instance!=null?DialogueManager.Instance.IsOpen:false)}");
            return;
        }

        bool dpadUpPressed = DetectDpadUpPressed();
        bool cancelPressed = DetectCancelPressed();

        if (cancelPressed)
        {
            bool closedAny = false;
            if (mainMenu != null && mainMenu.IsOpen)
            {
                mainMenu.HideMenu();
                closedAny = true;
            }
            if (_isPanelVisible)
            {
                ShowPanel(false);
                closedAny = true;
            }

            if (closedAny) return;
        }

        if (dpadUpPressed)
        {
            if (debugLogs) Debug.Log("[QuestLogListUI] DPadUp detected -> HandleToggleOrMenu");
            HandleToggleOrMenu();
        }
        else
        {
            // Guardar estado anterior del D-Pad (eje 7, NO el joystick)
            float currentDpad = 0f;
            try { currentDpad = Input.GetAxis("7th axis"); } catch { }
            _lastFrameDpadUp = currentDpad > 0.5f;
        }

        // Debug hotkey: for testing, permite abrir el panel forzando la apertura aunque GameState lo bloquee.
        if (Input.GetKeyDown(KeyCode.L))
        {
            if (debugLogs) Debug.Log("[QuestLogListUI] Debug hotkey L pressed -> forcing ShowPanel(true, force:true)");
            ShowPanel(true, force: true);
        }
    }

    private bool _lastFrameDpadUp = false;

#if ENABLE_INPUT_SYSTEM
    void OnQuickAccessPerformed(InputAction.CallbackContext ctx)
    {
        // Evitar que el callback intente abrir el panel cuando el objeto ya no existe o está inactivo
        if (!isActiveAndEnabled) return;

        // Imitar el flujo de Update para mantener la lógica centralizada
        if (!GameState.CanOpenInventory || (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen))
            return;

        HandleToggleOrMenu();
    }
#endif

    void HandleToggleOrMenu()
    {
        if (_isPanelVisible)
        {
            if (mainMenu != null)
            {
                if (mainMenu.IsOpen)
                    mainMenu.HideMenu();
                else
                    mainMenu.ShowMenu();
            }
            else
            {
                TogglePanel();
            }
        }
        else
        {
            ShowPanel(true);
        }

        RestartAutoHide();
    }

    IEnumerator BindWhenReady()
    {
        // Espera a que QuestManager exista (creado por tu escena Start)
        while (QuestManager.Instance == null) yield return null;

        // Si cambió de instancia (p.ej. reload), re-suscribe limpio
        if (_qm != QuestManager.Instance)
        {
            Unbind();
            _qm = QuestManager.Instance;
            _qm.OnQuestsChanged += Rebuild;
            _qm.OnQuestStarted += OnQuestStarted;
            _qm.OnQuestVisibilityChanged += OnQuestVisibilityChanged;
            _bound = true;
        }

        Rebuild();
        Debug.Log($"[QuestLogListUI] Bound to QuestManager and rebuilt UI. Quest count={(QuestManager.Instance.GetAll()==null?0:QuestManager.Instance.GetAll().Count())} GameState.CanOpenInventory={GameState.CanOpenInventory} DialogueOpen={(DialogueManager.Instance!=null?DialogueManager.Instance.IsOpen:false)}");
    }

    void Unbind()
    {
        if (_bound && _qm != null)
        {
            _qm.OnQuestsChanged -= Rebuild;
            _qm.OnQuestStarted -= OnQuestStarted;
            _qm.OnQuestVisibilityChanged -= OnQuestVisibilityChanged;
        }
        _bound = false;
        _qm = null;
    }

    public void Rebuild()
    {
        if (!contentRoot || itemPrefab == null) return;
        if (QuestManager.Instance == null) return; // por si se descargó la escena

        // limpiar
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        // poblar
        foreach (var rq in QuestManager.Instance.GetAll())
        {
            if (QuestManager.Instance.GetVisibility(rq.Id) == QuestVisibility.Hidden) continue;
            if (!showInactive && rq.State == QuestState.Inactive) continue;
            var go = Instantiate(itemPrefab, contentRoot);
            go.Bind(rq); // el propio item gestiona nulls internos
        }

        if (headerText) headerText.text = "Misiones";
    }

    void OnQuestStarted(string questId)
    {
        // Mostrar automáticamente el panel cuando aparece una nueva misión
        ShowPanel(true, force:true);
        RestartAutoHide();
    }

    void OnQuestVisibilityChanged(string questId, QuestVisibility vis)
    {
        Rebuild();
    }

    bool DetectDpadUpPressed()
    {
        bool dpadUpPressed = false;

#if ENABLE_INPUT_SYSTEM
        var gpCur = UnityEngine.InputSystem.Gamepad.current;
        if (gpCur != null)
        {
            try { dpadUpPressed = gpCur.dpad.up.wasPressedThisFrame; }
            catch { dpadUpPressed = gpCur.dpad.up.isPressed; }
            if (!dpadUpPressed)
            {
                // fallback: any dpad up pressed
                dpadUpPressed = gpCur.dpad.up.isPressed;
            }
            Debug.Log($"[QuestLogListUI] Gamepad present: {gpCur != null}, dpadUpPressed={dpadUpPressed}");
        }
#endif

        if (!dpadUpPressed)
        {
            bool tryButton = false;
            try
            {
                tryButton = Input.GetButtonDown("DPadUp");
                if (tryButton)
                {
                    Debug.Log("[QuestLogListUI] Input.GetButtonDown('DPadUp') returned true");
                    dpadUpPressed = true;
                }
            }
            catch { }

            if (!dpadUpPressed && Input.GetKeyDown(KeyCode.UpArrow))
            {
                Debug.Log("[QuestLogListUI] Keyboard UpArrow detected (Input.GetKeyDown)");
                dpadUpPressed = true;
            }

            float dpadVertical = 0f;
            try { dpadVertical = Input.GetAxis("7th axis"); } catch { }
            if (!dpadUpPressed && dpadVertical > 0.5f && !_lastFrameDpadUp)
            {
                Debug.Log($"[QuestLogListUI] Axis '7th axis' value={dpadVertical} -> treating as DPadUp");
                dpadUpPressed = true;
            }
        }

        return dpadUpPressed;
    }

    bool DetectCancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var gp = UnityEngine.InputSystem.Gamepad.current;
        if (gp != null && (gp.buttonEast.wasPressedThisFrame || gp.startButton.wasPressedThisFrame))
            return true;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
            return true;
#endif

        return Input.GetKeyDown(KeyCode.Escape)
            || Input.GetKeyDown(KeyCode.Backspace)
            || Input.GetKeyDown(KeyCode.JoystickButton1)
            || Input.GetKeyDown(KeyCode.JoystickButton7);
    }

    public void TogglePanel()
    {
        if (!GameState.CanOpenInventory) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return;

        _isPanelVisible = !_isPanelVisible;

        if (_isPanelVisible)
            AnimateShow();
        else
            AnimateHide();

        UpdateHelpText();
    }

    public void ShowPanel(bool show, bool force = false)
    {
        if (show && !force)
        {
            if (!GameState.CanOpenInventory) return;
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return;
        }

        _isPanelVisible = show;

        if (_isPanelVisible)
            AnimateShow();
        else
            AnimateHide();

        UpdateHelpText();
    }

    void AnimateShow()
    {
        if (panelRoot) panelRoot.SetActive(true);
        if (scrollView) scrollView.SetActive(true);
        KillTween();
        if (panelGroup)
        {
            panelGroup.alpha = 0f;
            panelGroup.blocksRaycasts = true;
            panelGroup.interactable = true;
            _panelTween = panelGroup.DOFade(1f, tweenDuration).SetEase(tweenEase).SetUpdate(true);
        }
        if (animatedRoot)
        {
            animatedRoot.anchoredPosition = _hiddenPos;
            _panelTween = animatedRoot.DOAnchorPos(_shownPos, tweenDuration).SetEase(tweenEase).SetUpdate(true);
        }
        RestartAutoHide();
    }

    void AnimateHide()
    {
        KillTween();
        if (panelGroup)
        {
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
            _panelTween = panelGroup.DOFade(0f, tweenDuration).SetEase(tweenEase).SetUpdate(true)
                .OnComplete(() => { if (panelRoot) panelRoot.SetActive(false); });
        }
        if (animatedRoot)
        {
            _panelTween = animatedRoot.DOAnchorPos(_hiddenPos, tweenDuration).SetEase(tweenEase).SetUpdate(true)
                .OnComplete(() => { if (scrollView) scrollView.SetActive(false); });
        }
        if (_autoHideCo != null) { StopCoroutine(_autoHideCo); _autoHideCo = null; }
        if (mainMenu != null && mainMenu.IsOpen)
            mainMenu.HideMenu();
    }

    void RestartAutoHide()
    {
        if (!gameObject.activeInHierarchy) return;
        if (_autoHideCo != null) StopCoroutine(_autoHideCo);
        _autoHideCo = StartCoroutine(AutoHideAfterDelay());
    }

    IEnumerator AutoHideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(hideAfterSeconds);
        _isPanelVisible = false;
        AnimateHide();
        UpdateHelpText();
    }

    [ContextMenu("Force Show Panel (Editor)")]
    private void ContextMenuForceShow()
    {
        Debug.Log("[QuestLogListUI] ContextMenuForceShow invoked");
        ShowPanel(true, force: true);
    }

    void UpdateHelpText()
    {
        if (!helpText) return;
        helpText.text = _isPanelVisible ? "[D-Pad ▲] Ocultar" : "[D-Pad ▲] Mostrar";
    }

    void KillTween()
    {
        if (_panelTween != null && _panelTween.IsActive()) _panelTween.Kill();
        _panelTween = null;
    }
}
