using UnityEngine;
using UnityEngine.UI;
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

    [Tooltip("Button de la fila CONTROLES.")]
    [SerializeField] private Button controlsButton;

    [Tooltip("Panel que contiene los botones principales del menú.")]
    [SerializeField] private GameObject buttonPanel;

    [Header("Settings")]
    [SerializeField] private SettingsMenuController settingsMenu;

    [Header("Controls")]
    [SerializeField] private ControlsMenuController controlsMenu;

    [Header("Scene when continuing")]
    [SerializeField] private string nextSceneContinue = "MainWorld";

    [Header("Scene when starting new game")]
    [SerializeField] private string nextSceneNewGame = "Prologo";

    [Header("Loading Screen")]
    [SerializeField] private string loadingOverlayScene = "LoadingScreen";

    [Header("Fade override (opcional)")]
    [SerializeField] private EasyTransition.TransitionSettings fadeOverride;
    [Min(0f)] public float fadeDelay;

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
    public bool enableInputDebounce;
    [Min(0f)] public float inputArmDelay = 0.2f;

    private Sequence _introSeq;
    private bool _isLoading;
    private bool _inputArmed = true;
    private Coroutine _armRoutine;
    private bool _armSnapshotValid;
    private bool _armSnapshotRaycasts;
    private bool _armSnapshotContinueInteractable;
    private bool _armSnapshotNewInteractable;
    private bool _settingsSnapshotRaycasts;
    private bool _settingsSnapshotInteractable;

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

        if (!controlsButton)
            controlsButton = TryFindControlsButton();

        if (!settingsMenu)
            settingsMenu = GetComponentInChildren<SettingsMenuController>(true);

        if (!controlsMenu)
            controlsMenu = GetComponentInChildren<ControlsMenuController>(true);

        if (settingsMenu)
            settingsMenu.Close(silent: true);

        if (controlsMenu)
            controlsMenu.Close(silent: true);

        WireButton(continueButton, OnClickContinue, "CONTINUE");
        WireButton(newGameButton, OnClickNewGame, "NEW GAME");
        WireButton(settingsButton, OnClickSettings, "SETTINGS");
        WireButton(controlsButton, OnClickControls, "CONTROLS");

        // Ensure UISelectVisual exists on main menu buttons
        var menuButtons = GetComponentsInChildren<Button>(true);
        foreach (var b in menuButtons)
        {
            if (b == null) continue;
            
            // Visual feedback
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
        // Llegar al MainMenu (por cualquier camino: recién arrancado, pausa → salir al menú,
        // créditos → menú...) es un punto de entrada seguro de sesión: nunca hay legítimamente
        // una cinemática/transición/cámara bloqueada en curso aquí. Aprovechamos para tirar
        // cualquier flag estático de sesión que haya quedado "colgado" de una sesión anterior
        // cortada a mitad de camino, antes de que el jugador pueda volver a cargar partida.
        // Ver GameBootService.ResetTransientSessionState() para el detalle.
        GameBootService.ResetTransientSessionState();

        GameState.Push(GamePhase.MainMenu);

        // Forzar modo UI de forma síncrona: cancela cualquier actualización diferida pendiente
        // que pudiera venir del gameplay anterior y deja refCount=1 para el PopUIMode de OnDisable.
        // Usamos EnsureExists() en vez de leer Instance directamente porque, según nuestra
        // arquitectura, el menú debe poder cargarse desde su propia escena (ej: Play in Editor
        // sobre MainMenu.unity) sin haber pasado antes por Start.unity, que es donde normalmente
        // vive el PlayerInputManager persistente.
        var pim = Core.PlayerInputManager.EnsureExists();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[MainMenu-Debug] OnEnable — pim={pim != null}, GameState.MainMenu={GameState.Is(GamePhase.MainMenu)}");
#endif
        pim.ForceSyncEnterUIMode();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Log estado del EventSystem
        var es = UnityEngine.EventSystems.EventSystem.current;
        var uiMod = es != null ? es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() : null;
        Debug.Log($"[MainMenu-Debug] EventSystem={es?.name ?? "NULL"}, UIInputModule={uiMod != null}, " +
                  $"UIInputModule.enabled={uiMod?.enabled}, rootGroup.blocksRaycasts={rootGroup?.blocksRaycasts}");
#endif

        _isLoading = false; // reset por si vuelves al menú
        UpdateContinueVisibility();
        PlayIntro();
        
        // Asegurar que los botones tienen UIButtonAudio
        EnsureButtonAudio();
        
        // Silenciar temporalmente los sonidos de UI para evitar el sonido de selección inicial
        UIButtonAudio.MuteAll = true;
        AutoSelectFirstIfNeeded();
        // Rehabilitar sonidos después de un frame
        StartCoroutine(EnableUISoundsNextFrame());
        
        // SelfTestButtons eliminado - solo útil en debug profundo
        
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
        Core.PlayerInputManager.Instance?.PopUIMode();
        GameState.Pop(GamePhase.MainMenu);
        
        // Asegurar que los sonidos de UI estén habilitados al salir
        UIButtonAudio.MuteAll = false;

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

        bool isTestMode = GameBootService.IsPresetOverrideActive;

        if (isTestMode)
        {
            // Modo test: el bootPreset actúa como partida cargada → solo Continuar.
            // Nueva Partida se oculta porque no tiene sentido resetear el preset de prueba.
            if (continueRow) continueRow.SetActive(true);
            if (continueButton) continueButton.interactable = true;
            if (newGameButton) newGameButton.gameObject.SetActive(false);
        }
        else
        {
            bool hasSave = saveSystem && saveSystem.HasSave();
            if (continueRow) continueRow.SetActive(hasSave);
            if (continueButton) continueButton.interactable = hasSave;
            if (newGameButton) newGameButton.gameObject.SetActive(true);
        }
    }

    void AutoSelectFirstIfNeeded()
    {
        // Seleccionar el primer botón interactivo disponible
        if (continueRow && continueRow.activeInHierarchy && continueButton && continueButton.interactable)
        {
            continueButton.Select();
        }
        else
        {
            var firstBtn = FindFirstActiveButton();
            if (firstBtn) firstBtn.Select();
        }
    }
    
    private System.Collections.IEnumerator EnableUISoundsNextFrame()
    {
        yield return null; // Esperar un frame
        UIButtonAudio.MuteAll = false;
        // Log eliminado - operación normal
    }
    
    /// <summary>
    /// Asegura que todos los botones del menú tengan UIButtonAudio para los sonidos de navegación
    /// OPTIMIZACIÓN: Solo se ejecuta una vez, cachea el resultado
    /// </summary>
    private bool _audioComponentsEnsured = false;
    
    private void EnsureButtonAudio()
    {
        // Optimización: solo ejecutar una vez
        if (_audioComponentsEnsured) return;
        
        var menuButtons = GetComponentsInChildren<Button>(true);
        
        int addedCount = 0;
        foreach (var b in menuButtons)
        {
            if (b == null) continue;
            
            // Audio feedback (navegación)
            var audioComp = b.GetComponent<UIButtonAudio>();
            if (audioComp == null)
            {
                audioComp = b.gameObject.AddComponent<UIButtonAudio>();
                addedCount++;
            }
            
            // Forzar que el sonido de navegación esté habilitado
            audioComp.SetPlayHoverSound(true);
        }
        
        _audioComponentsEnsured = true;
        
        // Solo log si se añadieron componentes (útil para debug)
        #if UNITY_EDITOR
        if (addedCount > 0)
        {
            Debug.Log($"[MainMenu] ✅ UIButtonAudio añadido a {addedCount} botones");
        }
        #endif
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
        
        // SFX de confirmación
        AudioService.Instance?.PlaySFX("UI_Submit");

        if (!saveSystem)
            saveSystem = ServiceLocator.Get<SaveSystem>(logIfMissing: false);

        bool hasSave = saveSystem != null && saveSystem.HasSave();
        bool forcePreset = GameBootService.IsPresetOverrideActive;

        if (forcePreset)
        {
            // MODO TESTING: Descartar avances de la sesión y recargar el bootPreset desde cero.
            // Esto detiene corrutinas huérfanas, resetea el runtimePreset y re-aplica todos los sistemas.
            Debug.Log("[MainMenu] CONTINUE en modo preset/test → Recargando bootPreset desde cero");
            GameBootService.ReloadTestPreset();
        }
        else if (hasSave)
        {
            // Recargar siempre desde disco antes de continuar.
            // Durante la sesión anterior, QuestPersistenceBridge puede haber actualizado
            // runtimePreset.flags en memoria (p.ej. misiones completadas/archivadas).
            // Si el jugador no guardó, save.json tiene el estado antiguo correcto,
            // pero runtimePreset ya tiene el estado evolucionado → hay que recargar desde disco
            // para que QuestManager quede con el estado correcto antes de que cargue la escena.
            var bootProfile = GameBootService.Profile;
            if (bootProfile != null)
            {
                bootProfile.LoadProfile(saveSystem);
                Debug.Log("[MainMenu] CONTINUE → Perfil recargado desde disco");
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
        Debug.Log("[MainMenu] ========== NUEVA PARTIDA SOLICITADA ==========");

        if (_isLoading || !_inputArmed)
        {
            if (!_inputArmed)
                Debug.Log("[MainMenu] Ignorando New Game mientras el menú arma la entrada.");
            return;
        }

        if (!saveSystem)
            saveSystem = ServiceLocator.Get<SaveSystem>(logIfMissing: false);

        // Si hay una partida guardada (y no estamos en modo preset de testeo, que no toca el
        // save), pedir confirmación antes de borrarla. Reutilizamos el mismo popup que ya se usa
        // para "salir al menú principal" (ConfirmationPopupUI), solo cambia el texto.
        bool forcePreset = GameBootService.IsPresetOverrideActive;
        bool hasSave = saveSystem != null && saveSystem.HasSave();

        if (!forcePreset && hasSave)
        {
            var popup = ConfirmationPopupUI.Instance;
            if (popup != null)
            {
                string msg = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.Get("CONFIRM_NEW_GAME", "Se borrarán los datos de la partida actual. ¿Quieres continuar?")
                    : "Se borrarán los datos de la partida actual. ¿Quieres continuar?";
                popup.Show(msg, onConfirm: ProceedWithNewGame);
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[MainMenu] Había que mostrar el popup de confirmación pero ConfirmationPopupUI.Instance es NULL (¿Start.unity no está cargada?). Se procede sin confirmar.");
#endif
        }

        ProceedWithNewGame();
    }

    void ProceedWithNewGame()
    {
        _isLoading = true;

        // SFX de confirmación
        AudioService.Instance?.PlaySFX("UI_Submit");

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
                Debug.Log("[MainMenu] Llamando a GameBootService.NewGameReset()...");
                GameBootService.NewGameReset();
                Debug.Log("[MainMenu] GameBootService.NewGameReset() completado");
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

        Debug.Log("[MainMenu] Cargando escena de Nueva Partida...");
        LoadNewGameScene();
    }

    public void OnClickSettings()
    {
        // SFX de confirmación
        AudioService.Instance?.PlaySFX("UI_Submit");
        
        if (!settingsMenu)
            settingsMenu = GetComponentInChildren<SettingsMenuController>(true);

        // Fallback: obtener desde el ServiceLocator si no se encuentra como hijo
        if (!settingsMenu)
        {
            settingsMenu = ServiceLocator.Get<SettingsMenuController>(false);
            Debug.Log($"[MainMenu] OnClickSettings fallback ServiceLocator -> {(settingsMenu != null ? settingsMenu.name : "<null>")}");
        }

        if (settingsMenu != null)
        {
            if (buttonPanel != null)
                buttonPanel.SetActive(false);

            var initial = settingsMenu.GetDefaultSelection();
            SuspendMainMenuInteraction();
            settingsMenu.Show(initial, () =>
            {
                RestoreMainMenuInteraction();

                if (buttonPanel != null)
                    buttonPanel.SetActive(true);

                LogMenuState("OnClickSettings-AfterReopen");

                RestartArmAfterSettingsClose();

                // Restaurar la selección en el siguiente frame
                StartCoroutine(RestoreSelectionNextFrame(null));
            });

            if (initial != null)
            {
                var selectable = initial.GetComponent<Selectable>();
                if (selectable != null)
                    selectable.Select();
            }
        }
        else
        {
            Debug.LogWarning("[MainMenu] No se encontró SettingsMenuController en la jerarquía ni en la escena.");
        }
    }

    public void OnClickControls()
    {
        // SFX de confirmación
        AudioService.Instance?.PlaySFX("UI_Submit");

        if (!controlsMenu)
            controlsMenu = GetComponentInChildren<ControlsMenuController>(true);

        // Fallback: obtener desde el ServiceLocator si no se encuentra como hijo (mismo patrón que OnClickSettings)
        if (!controlsMenu)
        {
            controlsMenu = ServiceLocator.Get<ControlsMenuController>(false);
            Debug.Log($"[MainMenu] OnClickControls fallback ServiceLocator -> {(controlsMenu != null ? controlsMenu.name : "<null>")}");
        }

        if (controlsMenu != null)
        {
            if (buttonPanel != null)
                buttonPanel.SetActive(false);

            SuspendMainMenuInteraction();
            LogMenuState("OnClickControls-BeforeShow");
            controlsMenu.Show(() =>
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[MainMenu-Debug] Controls onClosed callback INVOCADO");
#endif
                RestoreMainMenuInteraction();

                if (buttonPanel != null)
                    buttonPanel.SetActive(true);
                else
                    Debug.LogWarning("[MainMenu-Debug] buttonPanel es NULL en el callback de cierre de Controles");

                LogMenuState("OnClickControls-AfterReopen");

                RestartArmAfterSettingsClose();

                // Restaurar la selección en el siguiente frame
                StartCoroutine(RestoreSelectionNextFrame(null));
            });
        }
        else
        {
            Debug.LogWarning("[MainMenu] No se encontró ControlsMenuController en la jerarquía ni en la escena.");
        }
    }

    void RestartArmAfterSettingsClose()
    {
        _inputArmed = false;

        if (_armRoutine != null)
        {
            StopCoroutine(_armRoutine);
            _armRoutine = null;
        }

        float delay = Mathf.Max(0.01f, inputArmDelay);
        _armRoutine = StartCoroutine(ArmMenuAfterDelay(delay));
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
        // Asegurar que timeScale esté a 1 al salir del menú principal
        Time.timeScale = 1f;

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

    System.Collections.IEnumerator RestoreSelectionNextFrame(GameObject previous)
    {
        yield return null; // esperar un frame

        // Si no tenemos selección previa, usar el primer botón activo visible
        if (previous == null)
        {
            var fallback = FindFirstActiveButton();
            if (fallback != null) previous = fallback.gameObject;
        }

        // Re-aplicar selección usando EventSystem
        if (previous != null)
        {
            var sel = previous.GetComponent<Selectable>();
            if (sel != null) sel.Select();
        }
    }

    // ===== Debug (temporal — investigando "el menú desaparece tras Controles") =====
    void LogMenuState(string tag)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var es = UnityEngine.EventSystems.EventSystem.current;
        Debug.Log($"[MainMenu-Debug] {tag} — buttonPanel.activeSelf={(buttonPanel ? buttonPanel.activeSelf.ToString() : "NULL")}, " +
                  $"buttonPanel.activeInHierarchy={(buttonPanel ? buttonPanel.activeInHierarchy.ToString() : "NULL")}, " +
                  $"rootGroup.alpha={(rootGroup ? rootGroup.alpha.ToString("F2") : "NULL")}, " +
                  $"rootGroup.interactable={(rootGroup ? rootGroup.interactable.ToString() : "NULL")}, " +
                  $"rootGroup.blocksRaycasts={(rootGroup ? rootGroup.blocksRaycasts.ToString() : "NULL")}, " +
                  $"rootGroup.GO.activeInHierarchy={(rootGroup ? rootGroup.gameObject.activeInHierarchy.ToString() : "NULL")}, " +
                  $"EventSystem.selected={(es != null && es.currentSelectedGameObject != null ? es.currentSelectedGameObject.name : "NULL")}, " +
                  $"MainMenuController.GO.activeInHierarchy={gameObject.activeInHierarchy}");
#endif
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

    Button TryFindControlsButton()
    {
        var all = GetComponentsInChildren<Button>(true);
        foreach (var b in all)
        {
            var n = b.gameObject.name.ToLowerInvariant();
            if (n.Contains("control"))
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

    // SelfTestButtons eliminado - solo útil para debug profundo
    // Si necesitas debuggear botones, reactiva temporalmente este método

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

        // Solo manipular newGameButton si está visible (en modo test está oculto)
        if (newGameButton && newGameButton.gameObject.activeInHierarchy)
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

        // Solo restaurar newGameButton si está visible
        if (newGameButton && newGameButton.gameObject.activeInHierarchy)
            newGameButton.interactable = _armSnapshotNewInteractable;

        _armSnapshotValid = false;
    }

    void SuspendMainMenuInteraction()
    {
        if (rootGroup != null)
        {
            _settingsSnapshotRaycasts = rootGroup.blocksRaycasts;
            _settingsSnapshotInteractable = rootGroup.interactable;
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }
    }

    void RestoreMainMenuInteraction()
    {
        if (rootGroup != null)
        {
            rootGroup.blocksRaycasts = _settingsSnapshotRaycasts;
            rootGroup.interactable = _settingsSnapshotInteractable;
        }
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
