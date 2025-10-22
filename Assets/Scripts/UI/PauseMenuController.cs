// PauseMenuController.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using DG.Tweening;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PauseMenuController : MonoBehaviour
{
    // Asegura que si hay un PauseMenuController en la escena inicial, persista entre escenas.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsurePersistentInstance()
    {
        try
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
                if (existing.transform.root != null)
                {
                    UnityEngine.Object.DontDestroyOnLoad(existing.transform.root.gameObject);
                }
                else
                {
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

    public static bool IsOpen { get; private set; }

    float _navCooldown;
    [Min(0f)] public float navRepeatDelay = 0.15f;
    [Range(0f,1f)] public float navDeadzone = 0.3f;
    [Header("Debug")]
    public bool inputDebug = false;

    void Awake()
    {
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

#if !ENABLE_INPUT_SYSTEM
        var bridgeGO = new GameObject("PauseMenuInputBridge");
        var bridge = bridgeGO.AddComponent<PauseMenuInputBridge>();
        bridge.controller = this;
        DontDestroyOnLoad(bridgeGO);
#endif

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
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnableUIInput();
        if (rootGroup != null) { rootGroup.interactable = true; rootGroup.blocksRaycasts = true; }

        EnsureUISelection();
        PlayIntro();
        StartCoroutine(EnsureSelectionLater());
    }

    void OnDisable()
    {
        Time.timeScale = 1f;
        _isPaused = false;
        IsOpen = false;
        _introSeq?.Kill();
        DisableUIInput();
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
            TogglePause();
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
        gameObject.SetActive(true);
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
        var toSelect = _defaultSelection ?? orderedButtons[0]?.gameObject;
        if (toSelect) _es.SetSelectedGameObject(toSelect);
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
        // Usar unscaledDeltaTime porque pausamos el juego con Time.timeScale = 0
        if (_navCooldown > 0f)
        {
            _navCooldown -= Time.unscaledDeltaTime;
            if (_navCooldown < 0f) _navCooldown = 0f;
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
                        if (v.y > navDeadzone) { MoveSelection(Vector2.up); _navCooldown = navRepeatDelay; moved = true; }
                        else if (v.y < -navDeadzone) { MoveSelection(Vector2.down); _navCooldown = navRepeatDelay; moved = true; }
                    }

                    // 2) Si no movimos con Navigate, chequear acciones DPad (botones)
                    if (!moved)
                    {
                        var dUp = playerControls.GamePlay.DPadUp;
                        var dDown = playerControls.GamePlay.DPadDown;
                        if (dUp != null && dUp.enabled)
                        {
                            var valUp = dUp.ReadValue<float>();
                            if (valUp > 0.5f) { if (inputDebug) Debug.Log("PauseMenu: DPadUp action"); MoveSelection(Vector2.up); _navCooldown = navRepeatDelay; moved = true; }
                        }
                        if (!moved && dDown != null && dDown.enabled)
                        {
                            var valDown = dDown.ReadValue<float>();
                            if (valDown > 0.5f) { if (inputDebug) Debug.Log("PauseMenu: DPadDown action"); MoveSelection(Vector2.down); _navCooldown = navRepeatDelay; moved = true; }
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
                        if (d.y > 0.5f) { if (inputDebug) Debug.Log("PauseMenu: Gamepad.current dpad up"); MoveSelection(Vector2.up); _navCooldown = navRepeatDelay; moved = true; }
                        else if (d.y < -0.5f) { if (inputDebug) Debug.Log("PauseMenu: Gamepad.current dpad down"); MoveSelection(Vector2.down); _navCooldown = navRepeatDelay; moved = true; }
                        else
                        {
                            var s = gp.leftStick.ReadValue();
                            if (s.y > 0.5f) { if (inputDebug) Debug.Log("PauseMenu: Gamepad.current leftStick up"); MoveSelection(Vector2.up); _navCooldown = navRepeatDelay; moved = true; }
                            else if (s.y < -0.5f) { if (inputDebug) Debug.Log("PauseMenu: Gamepad.current leftStick down"); MoveSelection(Vector2.down); _navCooldown = navRepeatDelay; moved = true; }
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
                                if (s.y > 0.5f) { if (inputDebug) Debug.Log("PauseMenu: Joystick.current stick up"); MoveSelection(Vector2.up); _navCooldown = navRepeatDelay; moved = true; }
                                else if (s.y < -0.5f) { if (inputDebug) Debug.Log("PauseMenu: Joystick.current stick down"); MoveSelection(Vector2.down); _navCooldown = navRepeatDelay; moved = true; }
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

    public void OnOptions()
    {
        var optionsPanel = transform.Find("OptionsPanel");
        if (optionsPanel != null) optionsPanel.gameObject.SetActive(true);
    }

    public void OnQuitToMain()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuScene);
    }
}

#if !ENABLE_INPUT_SYSTEM
public class PauseMenuInputBridge : MonoBehaviour
{
    public PauseMenuController controller;
    void Update()
    {
        if (controller == null) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7))
            controller.TogglePause();
    }
}
#endif
