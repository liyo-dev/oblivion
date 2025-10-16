using UnityEngine;
using TMPro;
using System.Collections;

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

    bool _bound;                    // ya suscrito al manager
    QuestManager _qm;               // cache del manager suscrito
    Coroutine _waitCo;
    private bool _isPanelVisible = true; // Estado del panel

    void OnEnable()
    {
        // Empieza a esperar al manager si aún no existe
        _waitCo = StartCoroutine(BindWhenReady());
    }

    void OnDisable()
    {
        Unbind();
        if (_waitCo != null) { StopCoroutine(_waitCo); _waitCo = null; }
    }

    void Update()
    {
        // Si el menú de pausa está abierto, ignorar entradas de D-pad para no abrir/cerrar el panel de misiones
        if (PauseMenuController.IsOpen)
        {
            Debug.Log("QuestLogListUI: entrada D-Pad ignorada porque PauseMenuController.IsOpen == true");
            return;
        }

        // Control con D-pad arriba SOLAMENTE (no joystick izquierdo)
        bool dpadUpPressed = false;
        
        #if ENABLE_INPUT_SYSTEM
        // Nuevo Input System
        if (UnityEngine.InputSystem.Gamepad.current != null)
        {
            dpadUpPressed = UnityEngine.InputSystem.Gamepad.current.dpad.up.wasPressedThisFrame;
        }
        #endif
        
        // Sistema antiguo - prueba varias opciones comunes para D-pad Up
        if (!dpadUpPressed)
        {
            // Intenta con el botón configurado
            try { dpadUpPressed = Input.GetButtonDown("DPadUp"); } catch { }
            
            // Alternativas comunes para D-pad Up en diferentes mandos
            if (!dpadUpPressed) dpadUpPressed = Input.GetKeyDown(KeyCode.UpArrow); // Flecha arriba del teclado
            
            // Eje 7 para D-pad vertical SOLAMENTE (NO usa joystick izquierdo)
            // El eje "7th axis" es específico del D-Pad en la mayoría de los mandos
            float dpadVertical = 0f;
            try { dpadVertical = Input.GetAxis("7th axis"); } catch { }
            
            // Detectar solo cuando se presiona (transición de no presionado a presionado)
            if (!dpadUpPressed && dpadVertical > 0.5f && !_lastFrameDpadUp)
            {
                dpadUpPressed = true;
            }
        }
        
        if (dpadUpPressed)
        {
            TogglePanel();
        }
        
        // Guardar estado anterior del D-Pad (eje 7, NO el joystick)
        float currentDpad = 0f;
        try { currentDpad = Input.GetAxis("7th axis"); } catch { }
        _lastFrameDpadUp = currentDpad > 0.5f;
    }

    private bool _lastFrameDpadUp = false;

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
            _bound = true;
        }

        Rebuild();
    }

    void Unbind()
    {
        if (_bound && _qm != null)
        {
            _qm.OnQuestsChanged -= Rebuild;
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
            if (!showInactive && rq.State == QuestState.Inactive) continue;
            var go = Instantiate(itemPrefab, contentRoot);
            go.Bind(rq); // el propio item gestiona nulls internos
        }

        if (headerText) headerText.text = "Misiones";
    }

    public void TogglePanel()
    {
        // Si el menú de pausa está abierto, no permitir abrir/ocultar el panel de misiones
        if (PauseMenuController.IsOpen)
        {
            Debug.Log("QuestLogListUI.TogglePanel: ignorado porque PauseMenuController.IsOpen == true");
            return;
        }

        _isPanelVisible = !_isPanelVisible;
        
        // Solo ocultar el ScrollView, no todo el panel
        if (scrollView)
        {
            scrollView.SetActive(_isPanelVisible);
        }
        
        // Actualizar el texto de ayuda según el estado
        if (helpText)
        {
            if (_isPanelVisible)
            {
                helpText.text = "[D-Pad ▲] Ocultar";
            }
            else
            {
                helpText.text = "[D-Pad ▲] Mostrar";
            }
        }
    }

    public void ShowPanel(bool show)
    {
        // Si el menú de pausa está abierto, ignorar la petición
        if (PauseMenuController.IsOpen)
        {
            Debug.Log("QuestLogListUI.ShowPanel: ignorado porque PauseMenuController.IsOpen == true");
            return;
        }

        _isPanelVisible = show;
        
        if (scrollView)
        {
            scrollView.SetActive(_isPanelVisible);
        }
        
        if (helpText)
        {
            if (_isPanelVisible)
            {
                helpText.text = "[D-Pad ▲] Ocultar";
            }
            else
            {
                helpText.text = "[D-Pad ▲] Mostrar";
            }
        }
    }
}
