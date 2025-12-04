// PauseMenuController.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using DG.Tweening;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
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
#if UNITY_2022_3_OR_NEWER
                var existing = UnityEngine.Object.FindFirstObjectByType<PauseMenuController>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
                var existing = UnityEngine.Object.FindObjectOfType<PauseMenuController>(true);
#pragma warning restore 618
#endif
                if (existing != null)
                {
                    _instance = existing;
                    UnityEngine.Object.DontDestroyOnLoad(existing.gameObject);
                }
            }

            // Si no hay EventSystem en la escena, crear uno persistente para navegación
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
                UnityEngine.Object.DontDestroyOnLoad(es);
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

    [Header("Navegación UI (orden explícito)")]
    public List<Selectable> orderedButtons = new();
    public bool clampNavigationNoWrap = true;

    [Header("Input")]
    public PlayerControls playerControls; // opcional: asignar en el inspector
#if ENABLE_INPUT_SYSTEM
    private InputAction _pauseAction;
    private InputAction _uiSubmitAction;
    private InputAction _uiNavigateAction;
    private bool _createdPlayerControls = false;
    private InputAction _dpadUpAction; // añadido
    private InputAction _dpadDownAction; // añadido
#endif

    EventSystem _es;
    GameObject _defaultSelection;
    Sequence _introSeq;
    bool _isPaused;

    // Snapshots para suspender interacción cuando abrimos Settings desde Pause
    bool _settingsSnapshotRaycasts;
    bool _settingsSnapshotInteractable;
    bool _settingsButtonsSnapshotValid = false;
    bool _resumeButtonActiveSnapshot;
    bool _optionsButtonActiveSnapshot;
    bool _quitButtonActiveSnapshot;

    public static bool IsOpen { get; private set; }

    float _navCooldown;
    int _navHeldSign; // -1,0,1 para evitar saltos al mantener el stick
    [Min(0f)] public float navRepeatDelay = 0.15f;
    [Range(0f, 1f)] public float navDeadzone = 0.3f;
    [Header("Debug")]
    public bool inputDebug = false;

    bool _pauseRequestPending; // consolidar triggers de pausa en Update
    float _lastPauseInputTime;
    bool _pausePressedViaEvent;
    bool _cancelPressedViaEvent;

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

        BuildOrderedButtonsIfEmpty();
        FixExplicitNavigation();


#if ENABLE_INPUT_SYSTEM
        if (playerControls == null)
        {
            try { playerControls = new PlayerControls(); _createdPlayerControls = true; }
            catch { }
        }

        if (playerControls != null)
        {
            _pauseAction = playerControls.GamePlay.Start;
            _uiSubmitAction = playerControls.UI.Submit;
            _uiNavigateAction = playerControls.UI.Navigate;

            _pauseAction?.Enable();
            _pauseAction.performed += OnPausePressed;
            // Habilitar navigate (se leerá por polling en Update)
            _uiNavigateAction?.Enable();

            // Registrar D-Pad explícito (algunos gamepads pueden enviar dpad a acciones separadas)
            _dpadUpAction = playerControls.GamePlay.DPadUp;
            _dpadDownAction = playerControls.GamePlay.DPadDown;
            _dpadUpAction?.Enable();
            _dpadDownAction?.Enable();
            // No subscripciones a performed: usamos polling en Update
        }
#endif

        _defaultSelection = resumeButton ? resumeButton.gameObject : orderedButtons.Count > 0 ? orderedButtons[0]?.gameObject : null;
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
        GamepadInputReader.EnsureInputEventsSubscribed();
        GamepadInputReader.OnInput += HandleGamepadInput;
        if (rootGroup != null) { rootGroup.interactable = true; rootGroup.blocksRaycasts = true; }

        ResetNavigationState();
        EnsureUISelection();
        PlayIntro();
        StartCoroutine(EnsureSelectionLater());
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
        if (GameState.Is(GamePhase.PauseMenu)) GameState.Pop(GamePhase.PauseMenu);

        GamepadInputReader.OnInput -= HandleGamepadInput;
    }

    void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        // Cerrar el menú de pausa persistente al cambiar de escena para evitar que quede abierto.
        if (gameObject == null) return;

        // Cancelar cualquier solicitud de pausa que haya quedado pendiente entre escenas
        _pauseRequestPending = false;
        _pausePressedViaEvent = false;
        _cancelPressedViaEvent = false;

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

    void ResetNavigationState()
    {
        _navCooldown = 0f;
        _navHeldSign = 0;
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
        EnsureUISelection();
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
        _pausePressedViaEvent = false;
        _cancelPressedViaEvent = false;

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

    void BuildOrderedButtonsIfEmpty()
    {
        if (orderedButtons == null) orderedButtons = new List<Selectable>();
        orderedButtons.RemoveAll(s => s == null);
        if (orderedButtons.Count == 0)
        {
            var all = new List<Selectable>(GetComponentsInChildren<Selectable>(true));
            all.RemoveAll(s => s == null);
            all.Sort((a, b) =>
            {
                var ra = a.transform as RectTransform;
                var rb = b.transform as RectTransform;
                return -ra.position.y.CompareTo(rb.position.y);
            });
            orderedButtons.AddRange(all);
        }
    }

    void FixExplicitNavigation()
    {
        for (int i = 0; i < orderedButtons.Count; i++)
        {
            var s = orderedButtons[i];
            if (!s) continue;
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            if (i > 0) nav.selectOnUp = orderedButtons[i - 1];
            else if (!clampNavigationNoWrap) nav.selectOnUp = orderedButtons[^1];
            if (i < orderedButtons.Count - 1) nav.selectOnDown = orderedButtons[i + 1];
            else if (!clampNavigationNoWrap) nav.selectOnDown = orderedButtons[0];
            s.navigation = nav;
        }
    }

    void EnableUIInput()
    {
#if ENABLE_INPUT_SYSTEM
        playerControls?.UI.Enable();
        // Asegurar que D-Pad también esté activo para navegación
        _dpadUpAction?.Enable();
        _dpadDownAction?.Enable();
#endif
    }

    void DisableUIInput()
    {
#if ENABLE_INPUT_SYSTEM
        playerControls?.UI.Disable();
        _dpadUpAction?.Disable();
        _dpadDownAction?.Disable();
#endif
    }

    void EnsureUISelection()
    {
        if (_es == null) _es = EventSystem.current;
        if (_es == null) return;

        var toSelect = _defaultSelection ?? orderedButtons[0]?.gameObject;
        if (toSelect == null) return;

        _es.SetSelectedGameObject(null);
        _es.SetSelectedGameObject(toSelect);
        toSelect.GetComponent<Selectable>()?.Select();
    }

    System.Collections.IEnumerator EnsureSelectionLater()
    {
        yield return new WaitForEndOfFrame();
        if (_es == null) _es = EventSystem.current;
        if (_es == null)
        {
            if (inputDebug) Debug.LogWarning("[PauseMenu] EnsureSelectionLater: EventSystem.current is null, cannot set selection.");
            yield break;
        }

        GameObject toSelect = _defaultSelection;
        if (toSelect == null && orderedButtons != null && orderedButtons.Count > 0)
            toSelect = orderedButtons[0]?.gameObject;

        if (toSelect != null)
        {
            try { _es.SetSelectedGameObject(toSelect); }
            catch (System.Exception ex) { Debug.LogWarning($"[PauseMenu] EnsureSelectionLater: failed to set selection: {ex}"); }
        }
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

    void MoveSelection(Vector2 dir)
    {
        if (_es == null) _es = EventSystem.current;
        if (_es == null) return;
        var current = _es.currentSelectedGameObject;
        var sel = current ? current.GetComponent<Selectable>() : null;
        Selectable next = null;
        if (sel == null) next = orderedButtons.Count > 0 ? orderedButtons[0] : null;
        else
        {
            if (dir == Vector2.up) next = sel.FindSelectableOnUp();
            else if (dir == Vector2.down) next = sel.FindSelectableOnDown();
        }
        if (next != null)
        {
            _es.SetSelectedGameObject(next.gameObject);
            next.Select();
        }
    }

    void Update()
    {
        if (_pauseRequestPending || WasPausePressedThisFrame())
        {
            ProcessPauseRequest();
            return;
        }        // Usar unscaledDeltaTime porque pausamos el juego con Time.timeScale = 0
        if (_isPaused && WasCancelPressedThisFrame())
        {
            if (settingsMenu != null && settingsMenu.gameObject.activeInHierarchy)
            {
                settingsMenu.Close();
                EnsureUISelection();
                return;
            }

            TogglePause();
            return;
        }

        if (_navCooldown > 0f)
        {
            _navCooldown -= Time.unscaledDeltaTime;
            if (_navCooldown < 0f) _navCooldown = 0f;
        }
        else
        {
            _navHeldSign = 0; // soltar bloqueo cuando expira cooldown
        }

#if ENABLE_INPUT_SYSTEM
        if (_isPaused && _navCooldown <= 0f)
        {
            bool moved = false;
            try
            {
                // 1) UI.Navigate (vector) - preferido
                if (playerControls != null)
                {
                    var nav = playerControls.UI.Navigate;
                    if (nav != null && nav.enabled)
                    {
                        Vector2 v = nav.ReadValue<Vector2>();
                        if (inputDebug && (v.y > navDeadzone || v.y < -navDeadzone)) Debug.Log($"PauseMenu: UI.Navigate -> {v}");
                        moved = ConsumeStick(v.y);
                    }

                    // 2) Si no movimos con Navigate, chequear acciones DPad (botones)
                    if (!moved)
                    {
                        var dUp = playerControls.GamePlay.DPadUp;
                        var dDown = playerControls.GamePlay.DPadDown;
                        if (dUp != null && dUp.enabled)
                        {
                            var valUp = dUp.ReadValue<float>();
                            if (valUp > 0.5f && ConsumeStick(+1f)) { if (inputDebug) Debug.Log("PauseMenu: DPadUp action"); moved = true; }
                        }
                        if (!moved && dDown != null && dDown.enabled)
                        {
                            var valDown = dDown.ReadValue<float>();
                            if (valDown > 0.5f && ConsumeStick(-1f)) { if (inputDebug) Debug.Log("PauseMenu: DPadDown action"); moved = true; }
                        }
                    }
                }

                // 3) Fallback a Gamepad.current si no hubo movimiento
                if (!moved)
                {
                    var gp = UnityEngine.InputSystem.Gamepad.current;
                    if (gp != null)
                    {
                        var d = gp.dpad.ReadValue();
                        if (d.y > 0.5f && ConsumeStick(+1f)) { if (inputDebug) Debug.Log("PauseMenu: Gamepad.current dpad up"); moved = true; }
                        else if (d.y < -0.5f && ConsumeStick(-1f)) { if (inputDebug) Debug.Log("PauseMenu: Gamepad.current dpad down"); moved = true; }
                        else
                        {
                            var s = gp.leftStick.ReadValue();
                            if (s.y > 0.5f && ConsumeStick(+1f)) { if (inputDebug) Debug.Log("PauseMenu: Gamepad.current leftStick up"); moved = true; }
                            else if (s.y < -0.5f && ConsumeStick(-1f)) { if (inputDebug) Debug.Log("PauseMenu: Gamepad.current leftStick down"); moved = true; }
                        }
                    }

                    // Si no hay Gamepad, comprobar Joystick (algunos mandos genéricos aparecen como Joystick)
                    if (!moved)
                    {
                        var js = UnityEngine.InputSystem.Joystick.current;
                        if (js != null)
                        {
                            try
                            {
                                var s = js.stick.ReadValue();
                                if (s.y > 0.5f && ConsumeStick(+1f)) { if (inputDebug) Debug.Log("PauseMenu: Joystick.current stick up"); moved = true; }
                                else if (s.y < -0.5f && ConsumeStick(-1f)) { if (inputDebug) Debug.Log("PauseMenu: Joystick.current stick down"); moved = true; }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (System.Exception)
            {
                // lectura defensiva: si InputSystem cambia en runtime, evitar crash
            }
        }
#endif
    }

    bool WasPausePressedThisFrame()
    {
        if (_pausePressedViaEvent)
        {
            _pausePressedViaEvent = false;
            return true;
        }
        return false;
    }

    bool WasCancelPressedThisFrame()
    {
        if (_cancelPressedViaEvent)
        {
            _cancelPressedViaEvent = false;
            return true;
        }
        return false;
    }

    private void HandleGamepadInput(GamepadInputReader.InputEvent input)
    {
        if (input.Phase != InputActionPhase.Performed) return;

        if (input.Type == GamepadInputReader.InputEventType.Start)
            _pausePressedViaEvent = true;
        else if (input.Type == GamepadInputReader.InputEventType.Cancel)
            _cancelPressedViaEvent = true;
    }

    bool ConsumeStick(float y)
    {
        if (Mathf.Abs(y) <= navDeadzone)
        {
            _navHeldSign = 0;
            return false;
        }

        int sign = y > 0f ? 1 : -1;
        if (_navHeldSign == sign)
            return false; // ya se movió en esta dirección, esperar a soltar

        _navHeldSign = sign;
        if (sign > 0) MoveSelection(Vector2.up);
        else MoveSelection(Vector2.down);
        _navCooldown = navRepeatDelay;
        return true;
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
                // Asegurar selección en el menú de pausa
                EnsureUISelection();
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

        // Ocultar botones principales del Pause para evitar interferencias con inputs
        if (!_settingsButtonsSnapshotValid)
        {
            _resumeButtonActiveSnapshot = resumeButton ? resumeButton.gameObject.activeSelf : false;
            _optionsButtonActiveSnapshot = optionsButton ? optionsButton.gameObject.activeSelf : false;
            _quitButtonActiveSnapshot = quitToMainButton ? quitToMainButton.gameObject.activeSelf : false;
            _settingsButtonsSnapshotValid = true;
        }

        if (resumeButton) resumeButton.gameObject.SetActive(false);
        if (optionsButton) optionsButton.gameObject.SetActive(false);
        if (quitToMainButton) quitToMainButton.gameObject.SetActive(false);
    }

    void EnsureSettingsMenuClosed()
    {
        if (settingsMenu == null)
            settingsMenu = GetComponentInChildren<SettingsMenuController>(true);

        if (settingsMenu != null && settingsMenu.IsVisible)
            settingsMenu.Close();

        if (_settingsButtonsSnapshotValid)
            RestorePauseInteraction();
    }

    void RestorePauseInteraction()
    {
        if (rootGroup != null)
        {
            rootGroup.blocksRaycasts = _settingsSnapshotRaycasts;
            rootGroup.interactable = _settingsSnapshotInteractable;
        }

        if (_settingsButtonsSnapshotValid)
        {
            if (resumeButton) resumeButton.gameObject.SetActive(_resumeButtonActiveSnapshot);
            if (optionsButton) optionsButton.gameObject.SetActive(_optionsButtonActiveSnapshot);
            if (quitToMainButton) quitToMainButton.gameObject.SetActive(_quitButtonActiveSnapshot);
            _settingsButtonsSnapshotValid = false;
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

