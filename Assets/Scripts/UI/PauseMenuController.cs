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

    [Header("Input")]
    public PlayerControls playerControls; // opcional: asignar en el inspector
#if ENABLE_INPUT_SYSTEM
    private InputAction _pauseAction;
    private InputAction _uiSubmitAction;
    private InputAction _uiNavigateAction;
    private bool _createdPlayerControls = false;
#endif

    // internos
    EventSystem _es;
    GameObject _defaultSelection;
    Sequence _introSeq;
    bool _isPaused;

    // bandera pública para que otros scripts puedan saber si el menú está abierto
    public static bool IsOpen { get; private set; }

    // navegación
    float _navCooldown;
    [Min(0f)] public float navRepeatDelay = 0.15f;

    void Awake()
    {
        _es = EventSystem.current;

        // resolver rootGroup preferentemente en un parent (panel raíz)
        if (rootGroup == null)
        {
            rootGroup = GetComponentInParent<CanvasGroup>();
            if (rootGroup == null) rootGroup = GetComponent<CanvasGroup>();
            if (rootGroup == null) rootGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Si por error se detectó el CanvasGroup encima de un Button, intentar usar el CanvasGroup padre (panel)
        if (rootGroup != null && rootGroup.gameObject.GetComponent<Button>() != null)
        {
            var parentCg = rootGroup.gameObject.transform.parent?.GetComponentInParent<CanvasGroup>();
            if (parentCg != null && parentCg != rootGroup)
            {
                Debug.Log("PauseMenuController: rootGroup estaba en un Button; reasignando al CanvasGroup del padre: " + parentCg.gameObject.name);
                rootGroup = parentCg;
            }
        }

        animatedItems ??= new List<RectTransform>();
        if (animatedItems.Count == 0)
        {
            var selectables = GetComponentsInChildren<Selectable>(true);
            if (selectables != null)
            {
                foreach (var s in selectables)
                {
                    if (s == null) continue;
                    if (s.transform is RectTransform rt) animatedItems.Add(rt);
                }
            }
        }

        // fallback de selección
        GameObject fallback = null;
        foreach (var rt in animatedItems) { if (rt != null) { fallback = rt.gameObject; break; } }
        _defaultSelection = resumeButton ? resumeButton.gameObject : fallback;

        // auto-asignar botones si no están enlazados en el inspector
        var buttons = GetComponentsInChildren<Button>(true);
        if ((resumeButton == null || optionsButton == null || quitToMainButton == null) && buttons != null)
        {
            foreach (var b in buttons)
            {
                if (b == null) continue;
                var btnName = b.gameObject.name.ToLowerInvariant();
                if (resumeButton == null && btnName.Contains("resume")) resumeButton = b;
                else if (optionsButton == null && btnName.Contains("option")) optionsButton = b;
                else if (quitToMainButton == null && (btnName.Contains("quit") || btnName.Contains("main"))) quitToMainButton = b;
            }
            if (resumeButton == null && buttons.Length > 0) resumeButton = buttons[0];
            if (optionsButton == null && buttons.Length > 1) optionsButton = buttons[1];
            if (quitToMainButton == null && buttons.Length > 2) quitToMainButton = buttons[2];
        }

        // enlazar listeners
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(Resume);
        }
        if (optionsButton != null)
        {
            optionsButton.onClick.RemoveAllListeners();
            optionsButton.onClick.AddListener(OnOptions);
        }
        if (quitToMainButton != null)
        {
            quitToMainButton.onClick.RemoveAllListeners();
            quitToMainButton.onClick.AddListener(OnQuitToMain);
        }

        // añadir feedback visual en botones si falta
        var uiButtons = new Button[] { resumeButton, optionsButton, quitToMainButton };
        foreach (var b in uiButtons)
        {
            if (b == null) continue;
            if (b.GetComponent<UISelectVisual>() == null)
            {
                var v = b.gameObject.AddComponent<UISelectVisual>();
                v.normalColor = Color.white;
                v.highlightColor = new Color(0.9f, 0.85f, 0.6f);
                v.selectedScale = 1.08f;
                v.animDuration = 0.12f;
            }
        }

#if !ENABLE_INPUT_SYSTEM
        // puente para Input Manager antiguo: se asegura que exista y tenga referencia
        var existingBridge = FindObjectOfType<PauseMenuInputBridge>();
        if (existingBridge == null)
        {
            var bridgeGO = new GameObject("PauseMenuInputBridge");
            var bridge = bridgeGO.AddComponent<PauseMenuInputBridge>();
            bridge.controller = this;
            DontDestroyOnLoad(bridgeGO);
        }
        else
        {
            existingBridge.controller = this;
        }
#endif

#if ENABLE_INPUT_SYSTEM
        // intentar crear PlayerControls si no asignado (opcional)
        if (playerControls == null)
        {
            try { playerControls = new PlayerControls(); Debug.Log("PauseMenuController: PlayerControls creado automáticamente."); _createdPlayerControls = true; }
            catch (System.Exception ex) { Debug.LogWarning("PauseMenuController: fallo creando PlayerControls: " + ex.Message); }
        }

        if (playerControls != null)
        {
            // Asignar acciones directamente (no se puede comparar los action-map types con null)
            _pauseAction = playerControls.GamePlay.Start;
            _uiSubmitAction = playerControls.UI.Submit;
            _uiNavigateAction = playerControls.UI.Navigate;

            if (_pauseAction != null) { _pauseAction.performed += OnPausePressed; _pauseAction.Enable(); }
            if (_uiNavigateAction != null) { _uiNavigateAction.performed += OnUINavigate; /* enabled when menu opens */ }
        }
        else Debug.LogWarning("PauseMenuController: playerControls no asignado y no se pudo crear uno automáticamente.");
#endif

        gameObject.SetActive(false);

        Debug.Log($"PauseMenuController: Buttons assigned -> Resume: {(resumeButton!=null?resumeButton.gameObject.name:"<null>")}, Options: {(optionsButton!=null?optionsButton.gameObject.name:"<null>")}, Quit: {(quitToMainButton!=null?quitToMainButton.gameObject.name:"<null>")}");
    }

    void OnEnable()
    {
        Time.timeScale = 0f;
        _isPaused = true;
        IsOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Habilitar input UI antes de fijar selección para que el EventSystem responda correctamente
        EnableUIInput();

        // Asegurar que el CanvasGroup permita interacción
        if (rootGroup != null)
        {
            rootGroup.interactable = true;
            rootGroup.blocksRaycasts = true;
        }

        EnsureUISelection();
        PlayIntro();
        // Reintentar selección unos frames después por si el EventSystem/elements tardan en activarse
        StartCoroutine(EnsureSelectionLater());

        Debug.Log("PauseMenuController: Menu opened (OnEnable)");
    }

    void OnDisable()
    {
        Time.timeScale = 1f;
        _isPaused = false;
        IsOpen = false;
        _introSeq?.Kill(); _introSeq = null;
        DisableUIInput();
    }

    void OnDestroy()
    {
        // Desuscribir callbacks de Input System para evitar MissingReferenceException si el objeto fue destruido
#if ENABLE_INPUT_SYSTEM
        try
        {
            if (_pauseAction != null)
            {
                _pauseAction.performed -= OnPausePressed;
                _pauseAction.Disable();
            }
            if (_uiNavigateAction != null)
            {
                _uiNavigateAction.performed -= OnUINavigate;
                _uiNavigateAction.Disable();
            }
            if (_uiSubmitAction != null)
            {
                _uiSubmitAction.Disable();
            }
            // Si creamos nosotros el PlayerControls, liberarlo
            if (_createdPlayerControls && playerControls != null)
            {
                try { playerControls.Disable(); playerControls.Dispose(); } catch {};
                playerControls = null;
            }
        }
        catch { }
#endif
        // Enlazamientos UI
        if (resumeButton != null) resumeButton.onClick.RemoveAllListeners();
        if (optionsButton != null) optionsButton.onClick.RemoveAllListeners();
        if (quitToMainButton != null) quitToMainButton.onClick.RemoveAllListeners();
    }

    void Start()
    {
        // Si por alguna razón las acciones no se registraron en Awake, intentarlo aquí
#if ENABLE_INPUT_SYSTEM
        if (playerControls != null && _pauseAction == null)
        {
            _pauseAction = playerControls.GamePlay.Start;
            _uiSubmitAction = playerControls.UI.Submit;
            _uiNavigateAction = playerControls.UI.Navigate;
            if (_pauseAction != null) { _pauseAction.performed += OnPausePressed; _pauseAction.Enable(); }
            if (_uiNavigateAction != null) _uiNavigateAction.performed += OnUINavigate;
        }
#endif
        EnableUIInput();
    }

    void Update()
    {
        if (!_isPaused) return;

        if (_navCooldown > 0f) _navCooldown -= Time.unscaledDeltaTime;

        KeepUIFocusForGamepad();

#if !ENABLE_INPUT_SYSTEM
        // navegación con axes antiguos
        float v = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(v) > 0.5f && _navCooldown <= 0f)
        {
            if (v > 0.5f) MoveSelection(Vector2.up);
            else if (v < -0.5f) MoveSelection(Vector2.down);
            _navCooldown = navRepeatDelay;
        }
#endif
    }

    void EnableUIInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (playerControls != null)
        {
            // No desactivamos GamePlay aquí: mantener la acción Start activa para poder cerrar el menú
            playerControls.UI.Enable();
            Debug.Log("PauseMenuController: UI action map enabled.");
        }
        // Asegurar que la acción de pausa (Start) sigue escuchando para poder cerrar el menú
        _uiSubmitAction?.Enable();
        _uiNavigateAction?.Enable();
        if (_pauseAction != null && !_pauseAction.enabled) _pauseAction.Enable();
#else
        // nada adicional para Input Manager
#endif
    }

    void DisableUIInput()
    {
#if ENABLE_INPUT_SYSTEM
        _uiSubmitAction?.Disable();
        _uiNavigateAction?.Disable();
        if (playerControls != null)
        {
            playerControls.UI.Disable();
            Debug.Log("PauseMenuController: UI action map disabled.");
            // No reactivamos GamePlay automáticamente aquí: lo dejamos como estaba
        }
#endif
    }

#if ENABLE_INPUT_SYSTEM
    void OnPausePressed(InputAction.CallbackContext ctx)
    {
        TogglePause();
    }

    void OnUINavigate(InputAction.CallbackContext ctx)
    {
        if (!_isPaused) return;
        if (_navCooldown > 0f) return;
        Vector2 v = ctx.ReadValue<Vector2>();
        if (v.y > 0.5f) MoveSelection(Vector2.up);
        else if (v.y < -0.5f) MoveSelection(Vector2.down);
        else if (v.x > 0.5f) MoveSelection(Vector2.right);
        else if (v.x < -0.5f) MoveSelection(Vector2.left);
        _navCooldown = navRepeatDelay;
    }
#endif

    // Nuevo: togglear el menú de pausa de forma segura
    public void TogglePause()
    {
        // Acceder a propiedades de UnityEngine.Object puede lanzar MissingReferenceException si el objeto fue destruido
        bool activeInHierarchy;
        try
        {
            if (gameObject == null) return; // objeto nativo destruido
            activeInHierarchy = gameObject.activeInHierarchy;
        }
        catch
        {
            // El objeto fue destruido; ignorar el callback
            return;
        }

        Debug.Log($"PauseMenuController.TogglePause called. active={activeInHierarchy}, _isPaused={_isPaused}, IsOpen={IsOpen}");
        if (activeInHierarchy)
        {
            Resume();
            Debug.Log("PauseMenuController: menu closed via TogglePause");
        }
        else
        {
            ShowPauseMenu();
            Debug.Log("PauseMenuController: menu opened via TogglePause");
        }
    }

    public void ShowPauseMenu()
    {
        gameObject.SetActive(true);
        EnsureUISelection();
    }

    public void Resume()
    {
        // Limpiar selección del EventSystem para evitar que queden objetos seleccionados tras cerrar
        if (_es == null) _es = EventSystem.current;
        if (_es != null) _es.SetSelectedGameObject(null);

        // Desactivar interactividad del CanvasGroup
        if (rootGroup != null)
        {
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
        Debug.Log("PauseMenuController: Resume called, menu closed.");
    }

    void EnsureUISelection()
    {
        if (_es == null) _es = EventSystem.current;
        if (_es == null) return;

        // Intentar seleccionar el defaultSelection, forzándolo con ExecuteEvents y reintentando unas veces
        GameObject toSelect = _defaultSelection;
        if (toSelect == null)
        {
            var first = GetComponentInChildren<Selectable>(true);
            if (!ReferenceEquals(first, null)) toSelect = first.gameObject;
        }
        if (toSelect == null) return;

        // Ejecutar el set seleccionado y disparar el handler de selección
        TrySelect(toSelect);
        // Reintentar en los siguientes frames para cubrir casos en que el EventSystem tarda en actualizar
        StartCoroutine(RetrySelect(toSelect, 2));
    }

    void TrySelect(GameObject toSelect)
    {
        if (_es == null) _es = EventSystem.current;
        if (_es == null || toSelect == null) return;
        _es.SetSelectedGameObject(null);
        _es.SetSelectedGameObject(toSelect);
        var sel = toSelect.GetComponent<Selectable>();
        sel?.Select();
        // también ejecutar el evento de selección para que los handlers reaccionen
        var data = new BaseEventData(_es);
        UnityEngine.EventSystems.ExecuteEvents.Execute(toSelect, data, UnityEngine.EventSystems.ExecuteEvents.selectHandler);
        Debug.Log("PauseMenuController: Selected default UI element -> " + toSelect.name);
    }

    System.Collections.IEnumerator RetrySelect(GameObject toSelect, int attempts)
    {
        for (int i = 0; i < attempts; i++)
        {
            yield return null;
            TrySelect(toSelect);
        }
    }

    System.Collections.IEnumerator EnsureSelectionLater()
    {
        // esperar un par de frames y reintentar la selección
        yield return null;
        yield return null;
        if (_es == null) _es = EventSystem.current;
        if (_es == null) yield break;
        var toSelect = _defaultSelection ?? GetComponentInChildren<Selectable>(true)?.gameObject;
        if (toSelect != null)
        {
            TrySelect(toSelect);
            Debug.Log("PauseMenuController: EnsureSelectionLater attempted selection -> " + toSelect.name);
        }
    }

    void KeepUIFocusForGamepad()
    {
        if (_es == null) _es = EventSystem.current;
        if (_es == null) return;
        bool wantsPadFocus = false;
#if ENABLE_INPUT_SYSTEM
        if (_uiNavigateAction != null && _uiNavigateAction.triggered) wantsPadFocus = true;
        if (_uiSubmitAction != null && _uiSubmitAction.triggered) wantsPadFocus = true;
#else
        wantsPadFocus = Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f || Input.GetButtonDown("Submit");
#endif
        if (wantsPadFocus && (_es.currentSelectedGameObject == null || !_es.currentSelectedGameObject.activeInHierarchy)) EnsureUISelection();
    }

    void PlayIntro()
    {
        if (rootGroup == null) return;
        try { rootGroup.alpha = 0f; } catch { return; }
        _introSeq?.Kill();
        _introSeq = DOTween.Sequence().SetUpdate(true);
        _introSeq.AppendInterval(introDelay);
        _introSeq.Append(DOTween.To(() => rootGroup.alpha, a => rootGroup.alpha = a, 1f, 0.2f));
        float delayAcc = 0f;
        foreach (var rt in animatedItems)
        {
            if (rt == null) continue;
            Vector2 finalPos = rt.anchoredPosition;
            rt.anchoredPosition = finalPos + new Vector2(0f, -introYOffset);
            CanvasGroup cg = null;
            try { cg = rt.GetComponent<CanvasGroup>(); if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>(); }
            catch { cg = null; }
            if (cg != null) { cg.alpha = 0f; _introSeq.Insert(introDelay + delayAcc, rt.DOAnchorPos(finalPos, introDuration).SetEase(Ease.OutCubic)); _introSeq.Insert(introDelay + delayAcc, cg.DOFade(1f, introDuration * 0.9f)); }
            else { _introSeq.Insert(introDelay + delayAcc, rt.DOAnchorPos(finalPos, introDuration).SetEase(Ease.OutCubic)); }
            delayAcc += introStagger;
        }
    }

    void MoveSelection(Vector2 dir)
    {
        if (_es == null) _es = EventSystem.current;
        if (_es == null) return;
        var current = _es.currentSelectedGameObject;
        Selectable sel = null;
        if (current != null) sel = current.GetComponent<Selectable>();
        Selectable next = null;
        if (sel == null) next = GetComponentInChildren<Selectable>(true);
        else
        {
            if (dir == Vector2.up) next = sel.FindSelectableOnUp();
            else if (dir == Vector2.down) next = sel.FindSelectableOnDown();
            else if (dir == Vector2.left) next = sel.FindSelectableOnLeft();
            else if (dir == Vector2.right) next = sel.FindSelectableOnRight();
        }
        if (next != null) _es.SetSelectedGameObject(next.gameObject);
    }

    // ---------------- Botones ----------------
    public void OnOptions()
    {
        var optionsPanel = transform.Find("OptionsPanel");
        if (optionsPanel != null) optionsPanel.gameObject.SetActive(true);
    }

    public void OnQuitToMain()
    {
        Time.timeScale = 1f;
        var loaderType = System.Type.GetType("SceneTransitionLoader, Assembly-CSharp");
        if (loaderType != null)
        {
            var method = loaderType.GetMethod("Load", new[] { typeof(string) });
            method?.Invoke(null, new object[] { mainMenuScene });
        }
        else UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuScene);
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
        {
            Debug.Log("PauseMenuInputBridge: input detected.");
            controller.TogglePause();
        }
    }
}
#endif