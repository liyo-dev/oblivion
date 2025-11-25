using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

[DisallowMultipleComponent]
public class MainMenuController : MonoBehaviour
{
    static bool _forceInputDebounce;
    static float _forcedDelay = -1f;

    public static void RequestInputDebounce(float minimumDelay = 0.2f)
    {
        _forceInputDebounce = true;
        _forcedDelay = Mathf.Max(0f, minimumDelay);
    }

    [Header("Refs")]
    private SaveSystem saveSystem;

    [Tooltip("Fila / contenedor de la opción CONTINUAR (con tu Text dentro). Si no hay partida, se oculta.")]
    [SerializeField] private GameObject continueRow;

    [Tooltip("Button de la fila CONTINUAR.")]
    [SerializeField] private Button continueButton;

    [Tooltip("Button de la fila NUEVA PARTIDA.")]
    [SerializeField] private Button newGameButton;

    [Tooltip("Button de la fila AJUSTES.")]
    [SerializeField] private Button settingsButton;

    [Header("Settings")]
    [SerializeField] private SettingsMenuController settingsMenu;

    [Header("Scene when continuing")]
    [SerializeField] private string nextSceneContinue = "MainWorld";

    [Header("Scene when starting new game")]
    [SerializeField] private string nextSceneNewGame = "Prologo";

    [Header("Loading Screen")]
    [SerializeField] private string loadingOverlayScene = "LoadingScreen";

    [Header("Fade override (opcional)")]
    [SerializeField] private EasyTransition.TransitionSettings fadeOverride;
    [Min(0f)] public float fadeDelay = 0f;

    [Header("UI / Intro (opcional)")]
    [SerializeField] private CanvasGroup rootGroup;          // si está vacío, se añade en Awake
    [SerializeField] private RectTransform[] animatedItems;  // hijos que caen en intro
    [Tooltip("Retraso antes de que aparezca el rootGroup")]
    [Min(0f)] public float rootAppearDelay = 1f;
    [Min(0f)] public float introDelay = 0.05f;
    [Min(0f)] public float introStagger = 0.04f;
    [Min(0f)] public float introDuration = 0.35f;
    public float introYOffset = 40f;
    [Header("Input Debounce")]
    [Tooltip("Activar para ignorar el Submit inicial al entrar al menú.")]
    public bool enableInputDebounce = false;
    [Min(0f)] public float inputArmDelay = 0.2f;

    private Sequence _introSeq;
    private bool _isLoading = false;
    private bool _inputArmed = true;
    private Coroutine _armRoutine;
    private bool _armSnapshotValid;
    private bool _armSnapshotRaycasts;
    private bool _armSnapshotContinueInteractable;
    private bool _armSnapshotNewInteractable;

    void Awake()
    {
        if (!saveSystem)
            saveSystem = ServiceLocator.Get<SaveSystem>();

        if (!rootGroup)
            rootGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        // Autowire de botones si faltan referencias
        if (!continueButton && continueRow)
            continueButton = continueRow.GetComponent<Button>();

        if (!newGameButton)
            newGameButton = TryFindNewGameButton();

        if (!settingsButton)
            settingsButton = TryFindSettingsButton();

        if (!settingsMenu)
            settingsMenu = GetComponentInChildren<SettingsMenuController>(true);

        if (settingsMenu)
            settingsMenu.Close();

        WireButton(continueButton, OnClickContinue, "CONTINUE");
        WireButton(newGameButton, OnClickNewGame, "NEW GAME");
        WireButton(settingsButton, OnClickSettings, "SETTINGS");

        // Ensure UISelectVisual exists on main menu buttons so selection visuals (highlight/pulse) work.
        var menuButtons = GetComponentsInChildren<Button>(true);
        foreach (var b in menuButtons)
        {
            if (b == null) continue;
            if (!b.GetComponent<UISelectVisual>())
            {
                var v = b.gameObject.AddComponent<UISelectVisual>();
                v.normalColor = Color.white;
                v.highlightColor = new Color(0.95f, 0.9f, 0.7f);
                v.selectedScale = 1.08f;
                v.animDuration = 0.12f;
                v.enablePulse = true;
                v.enableShadowPunch = true;
            }
        }
    }

    void OnEnable()
    {
        _isLoading = false; // reset por si vuelves al menú
        UpdateContinueVisibility();
        PlayIntro();
        AutoSelectFirstIfNeeded();
        SelfTestButtons();
        // Armar interacción tras un breve retardo para ignorar el Submit inicial
        bool shouldDebounce = enableInputDebounce || _forceInputDebounce;
        _inputArmed = !shouldDebounce;

        if (_armRoutine != null)
        {
            StopCoroutine(_armRoutine);
            _armRoutine = null;
        }

        if (shouldDebounce)
        {
            float delay = inputArmDelay;
            if (_forceInputDebounce)
            {
                delay = Mathf.Max(delay, _forcedDelay >= 0f ? _forcedDelay : delay);
                _forceInputDebounce = false;
                _forcedDelay = -1f;
            }

            _armRoutine = StartCoroutine(ArmMenuAfterDelay(delay));
        }
    }

    void Start()
    {
        UpdateContinueVisibility();
        AutoSelectFirstIfNeeded();
    }

    void OnDisable()
    {
        _introSeq?.Kill(); _introSeq = null;
        if (_armRoutine != null)
        {
            StopCoroutine(_armRoutine);
            _armRoutine = null;
        }
        RestoreArmSnapshot();
        _inputArmed = true;
    }

    // ===== Visibilidad / Selección =========================================
    void UpdateContinueVisibility()
    {
        if (!saveSystem)
            saveSystem = ServiceLocator.Get<SaveSystem>(logIfMissing: false);

        bool hasSave = saveSystem && saveSystem.HasSave();

        if (continueRow) continueRow.SetActive(hasSave);
        if (continueButton) continueButton.interactable = hasSave;
    }

    void AutoSelectFirstIfNeeded()
    {
        var es = EventSystem.current;
        if (!es) return;

        // Solo forzar selección si NO hay selección válida ya
        if (es.currentSelectedGameObject == null || !es.currentSelectedGameObject.activeInHierarchy)
        {
            // Si hay partida y existe CONTINUAR, lo seleccionamos; si no, buscamos el primer Button activo
            if (continueRow && continueRow.activeInHierarchy && continueButton && continueButton.interactable)
            {
                es.SetSelectedGameObject(continueButton.gameObject);
            }
            else
            {
                var firstBtn = FindFirstActiveButton();
                if (firstBtn) es.SetSelectedGameObject(firstBtn.gameObject);
            }
        }
    }

    // ===== Intro (CanvasGroup en cada item) =================================
    void PlayIntro()
    {
        if (!rootGroup) return;

        rootGroup.alpha = 0f;
        _introSeq?.Kill();
        _introSeq = DOTween.Sequence().AppendInterval(introDelay);

        if (animatedItems != null && animatedItems.Length > 0)
        {
            foreach (var item in animatedItems)
            {
                if (!item) continue;

                var cg = item.GetComponent<CanvasGroup>();
                if (!cg) cg = item.gameObject.AddComponent<CanvasGroup>();

                var startPos = item.anchoredPosition;
                item.anchoredPosition = startPos + new Vector2(0f, -introYOffset);
                cg.alpha = 0f;

                _introSeq.AppendCallback(() =>
                {
                    item.DOAnchorPos(startPos, introDuration).SetEase(Ease.OutCubic);
                    cg.DOFade(1f, introDuration);
                });

                _introSeq.AppendInterval(introStagger);
            }
        }

        // Hacer que el rootGroup aparezca tras un retardo configurable
        _introSeq.Insert(rootAppearDelay, rootGroup.DOFade(1f, introDuration));
    }

    // ===== Acciones de menú =================================================
    public void OnClickContinue()
    {
        if (_isLoading || !_inputArmed)
        {
            if (!_inputArmed)
                Debug.Log("[MainMenu] Ignorando Continue mientras el menú arma la entrada.");
            return;
        }
        _isLoading = true;

        if (!saveSystem)
            saveSystem = ServiceLocator.Get<SaveSystem>(logIfMissing: false);

        bool hasSave = saveSystem != null && saveSystem.HasSave();
        bool forcePreset = GameBootService.IsPresetOverrideActive;

        if (forcePreset)
        {
            // MODO TESTING: El preset de testeo tiene prioridad absoluta - ignora saves completamente
            Debug.Log("[MainMenu] CONTINUE en modo preset/test → Save IGNORADO, se usará bootPreset configurado");
        }
        else if (hasSave)
        {
            Debug.Log("[MainMenu] CONTINUE → Cargando save...");
            if (GameBootService.IsAvailable && GameBootService.Profile != null)
            {
                bool loaded = GameBootService.Profile.LoadProfile(saveSystem);
                if (!loaded)
                    Debug.LogWarning("[MainMenu] No se pudo cargar el save antes de ir a la escena del mundo.");
                else
                    Debug.Log("[MainMenu] Save cargado correctamente");
            }
            else
            {
                Debug.LogWarning("[MainMenu] GameBootService no esta listo; se continuara con la sincronizacion en el mundo.");
            }
        }
        else
        {
            Debug.LogWarning("[MainMenu] CONTINUE pulsado sin save disponible → usando preset por defecto");
        }

        LoadContinueScene();
    }

    public void OnClickNewGame()
    {
        if (_isLoading || !_inputArmed)
        {
            if (!_inputArmed)
                Debug.Log("[MainMenu] Ignorando New Game mientras el menú arma la entrada.");
            return;
        }
        _isLoading = true;

        if (!saveSystem)
            saveSystem = ServiceLocator.Get<SaveSystem>(logIfMissing: false);

        bool forcePreset = GameBootService.IsPresetOverrideActive;

        if (forcePreset)
        {
            // MODO TESTING: El preset de testeo controla TODO - no hace falta resetear ni borrar saves
            Debug.Log("[MainMenu] NEW GAME en modo preset/test → Save NO se borra, bootPreset tiene control absoluto");
        }
        else
        {
            Debug.Log("[MainMenu] NEW GAME → Reseteando perfil y borrando save");
            
            if (GameBootService.IsAvailable)
            {
                GameBootService.NewGameReset();
            }
            else
            {
                Debug.LogWarning("[MainMenu] GameBootService no esta listo; se borra el save directamente.");
                if (saveSystem != null)
                {
                    bool ok = saveSystem.Delete();
                    if (!ok) Debug.LogWarning("[MainMenu] SaveSystem.Delete() devolvio false (algun fichero no se pudo borrar).");
                }
            }
        }

        LoadNewGameScene();
    }

    public void OnClickSettings()
    {
        if (!settingsMenu)
            settingsMenu = GetComponentInChildren<SettingsMenuController>(true);

        // Fallback: search the whole scene (including inactive) if not found as child
        if (!settingsMenu)
        {
            settingsMenu = UnityEngine.Object.FindObjectOfType<SettingsMenuController>(includeInactive: true);
            Debug.Log($"[MainMenu] OnClickSettings fallback FindObjectOfType -> {(settingsMenu != null ? settingsMenu.name : "<null>")}");
        }

        if (settingsMenu != null)
        {
            var es = EventSystem.current;
            var previous = es ? es.currentSelectedGameObject : null;
            settingsMenu.Show(null, () =>
            {
                if (es != null && previous != null)
                    es.SetSelectedGameObject(previous);
            });
        }
        else
        {
            Debug.LogWarning("[MainMenu] No se encontró SettingsMenuController en la jerarquía ni en la escena.");
        }
    }

    public void OnClickExit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ===== Carga de escenas diferenciada ===================================
    void LoadContinueScene()
    {
        if (string.IsNullOrEmpty(nextSceneContinue))
        {
            Debug.LogError("[MainMenu] nextSceneContinue no está configurado.");
            _isLoading = false;
            return;
        }

        LoadTargetScene(nextSceneContinue);
    }

    void LoadNewGameScene()
    {
        if (string.IsNullOrEmpty(nextSceneNewGame))
        {
            Debug.LogError("[MainMenu] nextSceneNewGame no está configurado.");
            _isLoading = false;
            return;
        }

        LoadTargetScene(nextSceneNewGame);
    }

    void LoadTargetScene(string sceneName)
    {
        bool useOverlay = !string.IsNullOrEmpty(loadingOverlayScene);

        if (useOverlay)
        {
            if (fadeOverride != null)
                SceneTransitionLoader.LoadWithOverlay(sceneName, loadingOverlayScene, fadeOverride, fadeDelay);
            else
                SceneTransitionLoader.LoadWithOverlay(sceneName, loadingOverlayScene);
        }
        else
        {
            if (fadeOverride != null)
                SceneTransitionLoader.Load(sceneName, fadeOverride, fadeDelay);
            else
                SceneTransitionLoader.Load(sceneName);
        }
    }

    // ===== Utilidades =======================================================
    void WireButton(Button btn, UnityEngine.Events.UnityAction action, string label)
    {
        if (!btn)
        {
            Debug.LogWarning($"[MainMenu] Botón {label} no asignado/encontrado.");
            return;
        }

        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);

        if (!btn.interactable)
            Debug.LogWarning($"[MainMenu] Botón {label} no está interactable.");

        if (!btn.gameObject.activeInHierarchy)
            Debug.LogWarning($"[MainMenu] Botón {label} está inactivo en jerarquía.");
    }

    Button TryFindNewGameButton()
    {
        // Busca en hijos un botón cuyo nombre sugiera "New Game"
        var all = GetComponentsInChildren<Button>(true);
        foreach (var b in all)
        {
            var n = b.gameObject.name.ToLowerInvariant();
            if (n.Contains("new") || n.Contains("nueva") || n.Contains("newgame") || n.Contains("nueva_partida"))
                return b;
        }
        return null;
    }

    Button TryFindSettingsButton()
    {
        var all = GetComponentsInChildren<Button>(true);
        foreach (var b in all)
        {
            var n = b.gameObject.name.ToLowerInvariant();
            if (n.Contains("setting") || n.Contains("ajuste") || n.Contains("config"))
                return b;
        }
        return null;
    }

    Button FindFirstActiveButton()
    {
        var all = GetComponentsInChildren<Button>(true);
        foreach (var b in all)
        {
            if (b.gameObject.activeInHierarchy && b.interactable)
                return b;
        }
        return null;
    }

    void SelfTestButtons()
    {
        // Pequeño chequeo de ambiente para raycasts/interactables.
        if (newGameButton)
        {
            bool active = newGameButton.gameObject.activeInHierarchy;
            bool interact = newGameButton.interactable;
            bool raycastOK = true;

            foreach (var cg in newGameButton.GetComponentsInParent<CanvasGroup>(true))
            {
                if (!cg.blocksRaycasts) { raycastOK = false; break; }
            }

            Debug.Log($"[MainMenu][SelfTest] NewGame active={active}, interactable={interact}, blocksRaycasts={raycastOK}");
        }
    }

    void CaptureArmSnapshot()
    {
        if (rootGroup != null)
        {
            _armSnapshotRaycasts = rootGroup.blocksRaycasts;
            rootGroup.blocksRaycasts = false;
        }

        if (continueButton)
        {
            _armSnapshotContinueInteractable = continueButton.interactable;
            continueButton.interactable = false;
        }

        if (newGameButton)
        {
            _armSnapshotNewInteractable = newGameButton.interactable;
            newGameButton.interactable = false;
        }

        _armSnapshotValid = true;
    }

    void RestoreArmSnapshot()
    {
        if (!_armSnapshotValid) return;

        if (rootGroup != null)
            rootGroup.blocksRaycasts = _armSnapshotRaycasts;

        if (continueButton)
            continueButton.interactable = _armSnapshotContinueInteractable;

        if (newGameButton)
            newGameButton.interactable = _armSnapshotNewInteractable;

        _armSnapshotValid = false;
    }

    System.Collections.IEnumerator ArmMenuAfterDelay(float delay)
    {
        CaptureArmSnapshot();

        try
        {
            float t = 0f;
            while (t < delay)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        finally
        {
            RestoreArmSnapshot();
            _inputArmed = true;
            _armRoutine = null;
        }
    }
}
