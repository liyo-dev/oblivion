using UnityEngine;
using TMPro;
using System.Collections;
using System.Linq;
#if UNITY_2021_1_OR_NEWER
using UnityEngine.UI;
#else
using UnityEngine.UI;
#endif
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
    [SerializeField] private bool debugLogs = false;

    [SerializeField] private CanvasGroup panelGroup;

    bool _bound;                    // ya suscrito al manager
    QuestManager _qm;               // cache del manager suscrito
    Coroutine _waitCo;
    private bool _isPanelVisible = false; // Estado del panel (oculto por defecto)
    private bool _panelRootIsSelf;
#if ENABLE_INPUT_SYSTEM
    private InputAction _quickAccessAction;
#endif

    void Awake()
    {
        if (!panelRoot)
            panelRoot = gameObject;
        _panelRootIsSelf = panelRoot == gameObject;

        if (!scrollView)
        {
            var sr = GetComponentInChildren<ScrollRect>(true);
            if (sr != null)
                scrollView = sr.gameObject;
        }

        if (!contentRoot)
        {
            var sr = GetComponentInChildren<ScrollRect>(true);
            if (sr != null && sr.content != null)
                contentRoot = sr.content;
        }

        if (!panelGroup)
            panelGroup = GetComponent<CanvasGroup>();
    }

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

        ShowPanel(false);
    }

    void OnDisable()
    {
        Unbind();
        if (_waitCo != null) { StopCoroutine(_waitCo); _waitCo = null; }
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
        bool dpadUpPressed = DetectDpadUpPressed();

        if (dpadUpPressed)
        {
            if (debugLogs) Debug.Log("[QuestLogListUI] DPadUp detected -> showing panel sin restricciones");
            ShowPanel(true);
        }
        else
        {
            // Guardar estado anterior del D-Pad (eje 7, NO el joystick)
            float currentDpad = 0f;
            try { currentDpad = Input.GetAxis("7th axis"); }
            catch { }
            _lastFrameDpadUp = currentDpad > 0.5f;
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            if (debugLogs) Debug.Log("[QuestLogListUI] Debug hotkey L pressed -> forcing ShowPanel(true)");
            ShowPanel(true);
        }
    }

    private bool _lastFrameDpadUp = false;

#if ENABLE_INPUT_SYSTEM
    void OnQuickAccessPerformed(InputAction.CallbackContext ctx)
    {
        if (!isActiveAndEnabled) return;
        ShowPanel(true);
    }
#endif

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
        ShowPanel(true);
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

    public void TogglePanel()
    {
        ShowPanel(!_isPanelVisible);
    }

    public void ShowPanel(bool show)
    {
        _isPanelVisible = show;
        if (panelRoot && !_panelRootIsSelf) panelRoot.SetActive(_isPanelVisible);
        if (scrollView) scrollView.SetActive(_isPanelVisible);
        if (panelGroup)
        {
            panelGroup.alpha = _isPanelVisible ? 1f : 0f;
            panelGroup.blocksRaycasts = _isPanelVisible;
            panelGroup.interactable = _isPanelVisible;
        }
        UpdateHelpText();
    }

    void UpdateHelpText()
    {
        if (!helpText) return;
        helpText.text = _isPanelVisible ? "[D-Pad ▲] Ocultar" : "[D-Pad ▲] Mostrar";
    }

}
