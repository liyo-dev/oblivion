using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using DG.Tweening;
using Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public class PauseMenuController : MonoBehaviour
{
    private static PauseMenuController _instance;
#if ENABLE_INPUT_SYSTEM
    private static InputAction _globalPauseListener;
#endif

    public static PauseMenuController Instance => _instance;

    // Asegura que si hay un PauseMenuController en la escena inicial, persista entre escenas.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsurePersistentInstance()
    {
        try
        {
            if (_instance == null)
            {
                var existing = ServiceLocator.Get<PauseMenuController>(false);
                if (existing != null)
                {
                    _instance = existing;
                    UnityEngine.Object.DontDestroyOnLoad(existing.gameObject);
                }
            }

            // Verificar que existe EventSystem en la escena
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                Debug.LogWarning("[PauseMenuController] No hay EventSystem en la escena. Asegúrate de añadir uno manualmente.");
            }

        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"EnsurePersistentInstance failed: {ex}");
        }
    }

    [Header("Refs")]
    public Button resumeButton;
    public Button optionsButton;
    public Button quitToMainButton;
    [SerializeField] private SettingsMenuController settingsMenu;

    [Header("Main Menu Scene")]
    public string mainMenuScene = "MainMenu";

    [Header("UI")]
    public CanvasGroup rootGroup;
    public List<RectTransform> animatedItems = new();
    [Min(0f)] public float introDelay = 0.05f;
    [Min(0f)] public float introStagger = 0.04f;
    [Min(0f)] public float introDuration = 0.35f;
    public float introYOffset = 40f;

    [Header("Input")]
#if ENABLE_INPUT_SYSTEM
    private InputAction _pauseAction;
#endif

    EventSystem _es;
    Sequence _introSeq;
    bool _isPaused;

    // Snapshots para suspender interacción cuando abrimos Settings desde Pause
    bool _settingsSnapshotRaycasts;
    bool _settingsSnapshotInteractable;

    public static bool IsOpen { get; private set; }
    
    [Header("Debug")]
    public bool inputDebug;

    bool _pauseRequestPending;
    float _lastPauseInputTime;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.Log("[PauseMenuController] Duplicate detected in scene, destroying extra instance.");
            Destroy(gameObject);
            return;
        }

        _instance = this;

        // Registrar como servicio para que los managers de entrada puedan localizarlo rápidamente.
        ServiceLocator.Register(this);
        DontDestroyOnLoad(gameObject);

        SceneManager.activeSceneChanged += HandleActiveSceneChanged;

        _es = EventSystem.current;

        if (rootGroup == null)
        {
            rootGroup = GetComponentInParent<CanvasGroup>() ?? GetComponent<CanvasGroup>();
            if (rootGroup == null) rootGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (animatedItems.Count == 0)
        {
            var selectables = GetComponentsInChildren<Selectable>(true);
            foreach (var s in selectables)
                if (s && s.transform is RectTransform rt) animatedItems.Add(rt);
        }

        // Auto-asignar botones si faltan
        var buttons = GetComponentsInChildren<Button>(true);
        if ((resumeButton == null || optionsButton == null || quitToMainButton == null) && buttons != null)
        {
            foreach (var b in buttons)
            {
                var btnName = b.gameObject.name.ToLowerInvariant();
                if (resumeButton == null && btnName.Contains("resume")) resumeButton = b;
                else if (optionsButton == null && btnName.Contains("option")) optionsButton = b;
                else if (quitToMainButton == null && (btnName.Contains("quit") || btnName.Contains("main"))) quitToMainButton = b;
            }
        }

        if (settingsMenu == null)
            settingsMenu = GetComponentInChildren<SettingsMenuController>(true);

        // Listeners
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (optionsButton != null) optionsButton.onClick.AddListener(OnOptions);
        if (quitToMainButton != null) quitToMainButton.onClick.AddListener(OnQuitToMain);

        // Efectos visuales en botones
        var uiButtons = new Button[] { resumeButton, optionsButton, quitToMainButton };
        foreach (var b in uiButtons)
        {
            if (!b) continue;
            if (!b.GetComponent<UISelectVisual>())
            {
                var v = b.gameObject.AddComponent<UISelectVisual>();
                v.normalColor = Color.white;
                v.highlightColor = new Color(0.95f, 0.9f, 0.7f);
                v.selectedScale = 1.1f;
                v.animDuration = 0.12f;
                v.enablePulse = true;
                v.enableShadowPunch = true;
            }
        }

#if ENABLE_INPUT_SYSTEM
        // Intentar inicializar el pause action si PlayerInputManager existe
        // Si no existe (ej: en MainMenu), simplemente no se suscribe
        if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
        {
            _pauseAction = pim.Controls.GamePlay.Start;
            _pauseAction?.Enable();
            _pauseAction.performed += OnPausePressed;
        }
#endif

        gameObject.SetActive(false);
    }


    void OnEnable()
    {
        Time.timeScale = 0f;
        _isPaused = true;
        IsOpen = true;
        MenuManager.RegisterOpen(MenuKind.Pause);
        GameState.Push(GamePhase.PauseMenu);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureSettingsMenuClosed();
        EnableUIInput();
        
        if (rootGroup != null) 
        { 
            rootGroup.interactable = true; 
            rootGroup.blocksRaycasts = true; 
        }

        PlayIntro();
        
        // Seleccionar el primer botón después de un frame
        StartCoroutine(SelectFirstButtonNextFrame());
    }
    
    System.Collections.IEnumerator SelectFirstButtonNextFrame()
    {
        yield return null;
        
        if (resumeButton && resumeButton.interactable)
        {
            resumeButton.Select();
            if (_es != null)
                _es.SetSelectedGameObject(resumeButton.gameObject);
        }
    }

    void OnDisable()
    {
        Time.timeScale = 1f;
        _isPaused = false;
        IsOpen = false;
        MenuManager.Close(MenuKind.Pause);
        _introSeq?.Kill();
        DisableUIInput();
        
        // Seguridad: si se desactiva externamente, asegurarse de liberar el GameState
        if (GameState.Is(GamePhase.PauseMenu)) 
            GameState.Pop(GamePhase.PauseMenu);
    }

    void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        // Cerrar el menú de pausa persistente al cambiar de escena para evitar que quede abierto.
        if (gameObject == null) return;

        // Cancelar cualquier solicitud de pausa que haya quedado pendiente entre escenas
        _pauseRequestPending = false;

        if (gameObject.activeSelf)
        {
            Resume();
        }

        // Asegurar que el estado quede limpio incluso si ya estaba desactivado
        Time.timeScale = 1f;
        _isPaused = false;
        IsOpen = false;
        MenuManager.Close(MenuKind.Pause);
        if (GameState.Is(GamePhase.PauseMenu)) GameState.Pop(GamePhase.PauseMenu);

        if (rootGroup != null)
        {
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

#if ENABLE_INPUT_SYSTEM
    void OnPausePressed(InputAction.CallbackContext ctx)
    {
        // Proteger contra callbacks que todavía se disparen después de que el objeto haya sido destruido
        // (InputSystem puede invocar callbacks en objetos cuyos bindings no fueron limpiados).
        // Hacemos una comprobación rápida y atrapamos MissingReferenceException por seguridad.
        if (this == null) return;
        try
        {
            RequestPauseToggle();
        }
        catch (MissingReferenceException)
        {
            // El objeto Unity fue destruido; ignorar el callback.
        }
    }
#endif

    public void TogglePause()
    {
        if (gameObject.activeInHierarchy) Resume();
        else ShowPauseMenu();
    }

    public void ShowPauseMenu()
    {
        Debug.Log("[PauseMenu] ShowPauseMenu called");
        if (!GameState.CanOpenPause) return;
        if (!MenuManager.IsOpen(MenuKind.Pause) && !MenuManager.TryOpen(MenuKind.Pause))
            return;
        gameObject.SetActive(true);
        EnsureSettingsMenuClosed();
    }

    public void Resume()
    {
        _es?.SetSelectedGameObject(null);
        if (rootGroup != null)
        {
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
        if (GameState.Is(GamePhase.PauseMenu)) GameState.Pop(GamePhase.PauseMenu);
    }

    /// <summary>
    /// Cierra y reinicia el estado del menú de pausa de forma defensiva, sin depender
    /// de los callbacks de Unity (OnDisable/OnDestroy). Útil antes de cambiar de escena
    /// para evitar que reaparezca tras cargas desde el menú principal.
    /// </summary>
    public void ForceCloseAndReset()
    {
        _pauseRequestPending = false;

        Time.timeScale = 1f;
        _isPaused = false;
        IsOpen = false;
        MenuManager.Close(MenuKind.Pause);

        if (GameState.Is(GamePhase.PauseMenu))
            GameState.Pop(GamePhase.PauseMenu);

        if (rootGroup != null)
        {
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    void EnableUIInput()
    {
#if ENABLE_INPUT_SYSTEM
        // Cambiar a modo UI centralizado
        if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
            pim.PushUIMode();
#endif
    }

    void DisableUIInput()
    {
#if ENABLE_INPUT_SYSTEM
        // Restaurar modo Gameplay centralizado
        if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
            pim.PopUIMode();
#endif
    }

    void PlayIntro()
    {
        if (rootGroup == null) return;
        rootGroup.alpha = 0f;
        _introSeq?.Kill();
        _introSeq = DOTween.Sequence().SetUpdate(true);
        _introSeq.AppendInterval(introDelay);
        _introSeq.Append(DOTween.To(() => rootGroup.alpha, a => rootGroup.alpha = a, 1f, 0.2f));

        float delayAcc = 0f;
        foreach (var rt in animatedItems)
        {
            if (!rt) continue;
            Vector2 finalPos = rt.anchoredPosition;
            rt.anchoredPosition = finalPos + new Vector2(0f, -introYOffset);
            CanvasGroup cg = rt.GetComponent<CanvasGroup>() ?? rt.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            _introSeq.Insert(introDelay + delayAcc, rt.DOAnchorPos(finalPos, introDuration).SetEase(Ease.OutCubic));
            _introSeq.Insert(introDelay + delayAcc, cg.DOFade(1f, introDuration * 0.9f));
            delayAcc += introStagger;
        }
    }

    void Update()
    {
        // Verificar que siempre haya algo seleccionado cuando el menú está activo
        if (_isPaused && _es != null && _es.currentSelectedGameObject == null)
        {
            StartCoroutine(SelectFirstButtonNextFrame());
        }

        // Procesar solicitud de toggle de pausa
        if (_pauseRequestPending || WasPausePressedThisFrame())
        {
            ProcessPauseRequest();
            return;
        }
        
        // Cerrar con Cancel/B button
        if (_isPaused && WasCancelPressedThisFrame())
        {
            // Si settings está abierto, cerrar settings primero
            if (settingsMenu != null && settingsMenu.gameObject.activeInHierarchy)
            {
                settingsMenu.Close();
                StartCoroutine(SelectFirstButtonNextFrame());
                return;
            }

            // Sino, cerrar el menú de pausa
            TogglePause();
            return;
        }
    }

    bool WasPausePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
        {
            return pim.Controls.GamePlay.Start.WasPressedThisFrame();
        }
#endif
        return false;
    }

    bool WasCancelPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
        {
            return pim.Controls.UI.Cancel.WasPressedThisFrame();
        }
#endif
        return false;
    }

    void RequestPauseToggle()
    {
        if (ShouldThrottlePauseInput()) return;

        _pauseRequestPending = true;

        // Si el objeto está inactivo, Update no se ejecutará, así que procesar ya.
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            Debug.Log("[PauseMenuController] Procesando solicitud de pausa inmediatamente.");
            ExecutePauseRequest();

        }
    }

    void ProcessPauseRequest()
    {
        if (ShouldThrottlePauseInput()) return;
        ExecutePauseRequest();
    }

    void ExecutePauseRequest()
    {
        _pauseRequestPending = false;

        Debug.Log("[PauseMenuController] Verificando condiciones para abrir/cerrar el menú de pausa...");

        // Si el GameState quedó marcado como PauseMenu pero no hay menú activo, limpiarlo
        if (!_isPaused && !gameObject.activeInHierarchy && GameState.Is(GamePhase.PauseMenu))
        {
            Debug.LogWarning("[PauseMenuController] Corrigiendo registro obsoleto de GamePhase.PauseMenu.");
            GameState.Pop(GamePhase.PauseMenu);
            MenuManager.Close(MenuKind.Pause);
        }

        // Si el menú de pausa ya está abierto, cerrar en lugar de volver a abrir
        if (_isPaused)
        {
            Debug.Log("[PauseMenuController] Menú de pausa ya abierto, cerrándolo.");
            TogglePause();
            return;
        }

        // Si hay otro menú abierto, solo cerrarlo con este input y no abrir pausa
        if (MenuManager.AnyOpen())
        {
            Debug.Log("[PauseMenuController] Otro menú está abierto, ignorando apertura de pausa.");
            return;
        }

        // Verificar si el estado permite abrir pausa
        Debug.Log("[PauseMenuController] GameState.CanOpenPause: " + GameState.CanOpenPause);
        if (GameState.CanOpenPause)
        {
            Debug.Log("[PauseMenuController] Intentando abrir el menú de pausa...");
            bool menuOpened = MenuManager.TryOpen(MenuKind.Pause);
            Debug.Log("[PauseMenuController] Resultado de MenuManager.TryOpen: " + menuOpened);
            if (menuOpened)
            {
                Debug.Log("[PauseMenuController] Menú de pausa abierto correctamente.");
                ShowPauseMenu();
            }
            else
            {
                Debug.LogWarning("[PauseMenuController] Fallo al abrir el menú de pausa desde MenuManager.");
            }
        }
        else
        {
            Debug.LogWarning("[PauseMenuController] No se puede abrir el menú de pausa debido al estado del juego.");
        }
    }

    bool ShouldThrottlePauseInput()
    {
        // Evitar doble toggle en el mismo frame por callbacks múltiples (InputAction + polling)
        if (Time.unscaledTime - _lastPauseInputTime < 0.05f)
            return true;

        _lastPauseInputTime = Time.unscaledTime;
        Debug.Log("[PauseMenuController] Pausa input time updated to: " + _lastPauseInputTime);
        return false;
    }

    public void OnOptions()
    {
        if (settingsMenu == null)
            settingsMenu = GetComponentInChildren<SettingsMenuController>(true);

        if (settingsMenu != null)
        {
            // Suspendemos la interacción del menú de pausa igual que hace MainMenu
            SuspendPauseInteraction();

            var es = EventSystem.current;
            var previous = es ? es.currentSelectedGameObject : null;
            var initial = settingsMenu.GetDefaultSelection();

            settingsMenu.Show(initial, () =>
            {
                // Al cerrar settings, restaurar la interacción del pause menu
                RestorePauseInteraction();
                if (es != null && previous != null)
                    es.SetSelectedGameObject(previous);
                // Reseleccionar el primer botón del pause menu
                StartCoroutine(SelectFirstButtonNextFrame());
            });

            if (es != null && initial != null)
                es.SetSelectedGameObject(initial);
        }
        else
        {
            Debug.LogWarning("[PauseMenu] SettingsMenuController reference not found!");
        }
    }

    void SuspendPauseInteraction()
    {
        if (rootGroup != null)
        {
            _settingsSnapshotRaycasts = rootGroup.blocksRaycasts;
            _settingsSnapshotInteractable = rootGroup.interactable;
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }
    }

    void EnsureSettingsMenuClosed()
    {
        if (settingsMenu == null)
            settingsMenu = GetComponentInChildren<SettingsMenuController>(true);

        if (settingsMenu != null && settingsMenu.IsVisible)
            settingsMenu.Close();

        RestorePauseInteraction();
    }

    void RestorePauseInteraction()
    {
        if (rootGroup != null)
        {
            rootGroup.blocksRaycasts = _settingsSnapshotRaycasts;
            rootGroup.interactable = _settingsSnapshotInteractable;
        }
    }

    public void OnQuitToMain()
    {
        // Cierra el menú antes de cambiar de escena para que no quede activo en el MainMenu.
        // Usar el cierre defensivo para limpiar flags y GameState incluso si ya estaba
        // activo al cambiar de escena. Esto evita que permanezca visible al cargar
        // nuevamente una partida desde el menú principal.
        ForceCloseAndReset();
        Time.timeScale = 1f;
        MainMenuController.RequestInputDebounce();
        UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuScene);
    }

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        if (_instance == this)
        {
            ServiceLocator.Unregister(this);
            _instance = null;
        }
    }
}

