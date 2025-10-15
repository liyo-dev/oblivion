using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;        
using UnityEngine.InputSystem.UI;  
#endif

public class PauseController : MonoBehaviour
{
    [Header("Escena de Menú")]
    [SerializeField] private string menuSceneName = "Menu"; // cambia si tu escena se llama distinto

    [Header("Opcional: Usa UI existente")]
    [Tooltip("Si no asignas nada, se construye la UI de pausa automáticamente.")]
    [SerializeField] private CanvasGroup pauseGroup;

    private PlayerControls _controls;
    private bool _isPaused;

    // Referencias UI creadas por código
    private Button _resumeBtn, _menuBtn;

    void Awake()
    {
        _controls = new PlayerControls();
        _controls.GamePlay.Start.performed += _ => TogglePause();

        EnsureEventSystem();

        if (pauseGroup == null)
            pauseGroup = BuildPauseUI();

        SetGroupVisible(pauseGroup, false);
    }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>()) return;

        var es = new GameObject("EventSystem", typeof(EventSystem));

        // Si tienes el New Input System activo, usa su módulo de UI; si no, StandaloneInputModule.
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
    es.AddComponent<StandaloneInputModule>();
#endif
        
    }
    void OnEnable() => _controls.Enable();
    void OnDisable() => _controls.Disable();

    void TogglePause()
    {
        if (_isPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (_isPaused) return;
        _isPaused = true;

        // Congela juego
        Time.timeScale = 0f;

        // Deshabilita acciones de gameplay si tienes más mapas
        _controls.GamePlay.Disable();

        // Muestra UI
        SetGroupVisible(pauseGroup, true);

        // Selección inicial para mando/teclado
        if (_resumeBtn != null)
            EventSystem.current?.SetSelectedGameObject(_resumeBtn.gameObject);

        // Muestra cursor (por si usas ratón)
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Resume()
    {
        if (!_isPaused) return;
        _isPaused = false;

        // Reactiva juego
        Time.timeScale = 1f;
        _controls.GamePlay.Enable();

        SetGroupVisible(pauseGroup, false);

        // Si bloqueas el cursor en gameplay, vuelve a bloquearlo aquí si te interesa:
        // Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }

    public void GoToMenu()
    {
        // Descongela antes de cargar
        Time.timeScale = 1f;
        _controls.Disable();
        SceneManager.LoadScene(menuSceneName);
    }

    // ===================== UI =====================

    CanvasGroup BuildPauseUI()
    {
        // Canvas dedicado a la pausa para asegurar orden
        var canvasGO = new GameObject("PauseCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Overlay oscuro
        var overlayGO = new GameObject("Overlay", typeof(Image));
        overlayGO.transform.SetParent(canvasGO.transform, false);
        var overlayImg = overlayGO.GetComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.45f);
        var overlayRT = overlayGO.GetComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero; overlayRT.anchorMax = Vector2.one;
        overlayRT.offsetMin = Vector2.zero; overlayRT.offsetMax = Vector2.zero;

        // CanvasGroup para mostrar/ocultar todo
        var group = canvasGO.AddComponent<CanvasGroup>();

        // Panel central
        var panelGO = new GameObject("Panel", typeof(Image));
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelImg = panelGO.GetComponent<Image>();
        panelImg.color = new Color(0.12f, 0.14f, 0.20f, 0.94f); // azul/gris elegante
        // Sombras suaves
        panelGO.AddComponent<Shadow>().effectDistance = new Vector2(0, -4);
        panelGO.AddComponent<Outline>().effectDistance = new Vector2(1.5f, -1.5f);

        var panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.sizeDelta = new Vector2(480, 340);
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;

        // Layout vertical
        var layout = panelGO.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 28, 28);
        layout.spacing = 18;
        layout.childAlignment = TextAnchor.UpperCenter;

        // Título
        var title = CreateText(panelGO.transform, "PAUSA", 30, FontStyle.Bold, new Color(1f,1f,1f,0.95f));
        var titleLayout = title.gameObject.AddComponent<LayoutElement>();
        titleLayout.minHeight = 40;

        // Botón Reanudar
        _resumeBtn = CreateButton(panelGO.transform, "Reanudar", Resume);

        // Botón Ir al Menú
        _menuBtn = CreateButton(panelGO.transform, "Ir al menú", GoToMenu);

        // Pie sutil
        var hint = CreateText(panelGO.transform, "[Start] o [Esc] para reanudar", 16, FontStyle.Italic,
                              new Color(1f,1f,1f,0.6f));
        hint.alignment = TextAnchor.MiddleCenter;

        return group;
    }

    Text CreateText(Transform parent, string text, int size, FontStyle style, Color color)
    {
        var go = new GameObject("Text", typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        return t;
    }

    Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        // Root
        var btnGO = new GameObject(label, typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        var img = btnGO.GetComponent<Image>();
        img.color = new Color(0.18f, 0.22f, 0.30f, 1f); // base
        img.raycastTarget = true;

        var shadow = btnGO.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(0, -3);

        var outline = btnGO.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.45f);
        outline.effectDistance = new Vector2(1f, -1f);

        var rt = btnGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(320, 56);

        // Texto del botón
        var txt = CreateText(btnGO.transform, label, 22, FontStyle.Bold, new Color(1f,1f,1f,0.94f));
        var txtRT = txt.GetComponent<RectTransform>();
        txtRT.anchorMin = txtRT.anchorMax = new Vector2(0.5f, 0.5f);
        txtRT.anchoredPosition = Vector2.zero;

        // Colores bonitos
        var btn = btnGO.GetComponent<Button>();
        var cb = btn.colors;
        cb.normalColor = img.color;
        cb.highlightedColor = new Color(0.22f, 0.27f, 0.36f, 1f);
        cb.pressedColor = new Color(0.10f, 0.55f, 0.80f, 1f);
        cb.selectedColor = cb.highlightedColor;
        cb.disabledColor = new Color(0.18f, 0.22f, 0.30f, 0.4f);
        cb.colorMultiplier = 1f;
        btn.colors = cb;

        btn.onClick.AddListener(onClick);

        // Layout
        var le = btnGO.AddComponent<LayoutElement>();
        le.minHeight = 56;
        le.preferredWidth = 320;

        return btn;
    }

    void SetGroupVisible(CanvasGroup group, bool visible)
    {
        if (!group) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }
}
