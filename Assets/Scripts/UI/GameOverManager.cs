using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Gestiona la pantalla de Game Over (mostrar/ocultar, pausar el juego y reiniciar escena).
/// Asignar en el inspector un GameObject que contenga la UI de Game Over (Canvas con panel).
/// Nota: versión simplificada — no gestiona animaciones ni desactiva componentes del jugador.
/// </summary>
public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    [Tooltip("Referencia al objeto de UI que actúa como pantalla de Game Over. Puede estar desactivado por defecto.")]
    [SerializeField] private GameObject gameOverUI;

    [Tooltip("Si está activado, al mostrar GameOver se pausará el juego con Time.timeScale = 0.")]
    [SerializeField] private bool pauseOnGameOver = true;

    [Header("Escenas")]
    [Tooltip("Nombre de la escena de mundo que se carga al 'Cargar partida'. Igual que en MainMenuController.worldScene.")]
    [SerializeField] private string worldScene = "MainWorld";

    [Tooltip("Nombre de la escena del menú principal.")]
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("UI / Navegación")]
    public Button loadLastSaveButton; // asignar desde inspector
    public Button backToMenuButton; // asignar desde inspector
    public CanvasGroup rootGroup; // opcional
    public RectTransform[] selectableItems; // opcional: orden de selección

    [Header("Comportamiento")]
    [Tooltip("Retraso en segundos antes de mostrar la UI de Game Over para permitir que la animación de muerte y la barra de vida terminen.")]
    [SerializeField] private float delayBeforeShow = 0.75f;

    // internos para mantener foco
    EventSystem _es;
    GameObject _defaultSelection;

    private bool _isGameOverShown = false;
    private Coroutine _showCoroutine = null;
    // Solo cuando AttachUIButtonListeners() haya sido llamado permitimos ejecutar las acciones públicas.
    bool _allowActions = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Opcional: no destruir al cambiar de escena si quieres persistencia
        // DontDestroyOnLoad(gameObject);

        if (gameOverUI != null)
            gameOverUI.SetActive(false);

        // Preparar refs UI
        _es = EventSystem.current;
        if (rootGroup == null && gameOverUI != null)
            rootGroup = gameOverUI.GetComponent<CanvasGroup>() ?? gameOverUI.AddComponent<CanvasGroup>();

        // Si no se han proporcionado selectableItems, rellenar desde child Selectables
        if (selectableItems == null || selectableItems.Length == 0)
        {
#pragma warning disable 300
            var selectables = gameOverUI != null ? gameOverUI.GetComponentsInChildren<Selectable>(true) : GetComponentsInChildren<Selectable>(true);
            if (selectables != null && selectables.Length > 0)
            {
                selectableItems = new RectTransform[selectables.Length];
                for (int i = 0; i < selectables.Length; i++)
                {
                    var rt = selectables[i].transform as RectTransform;
                    selectableItems[i] = rt;
                }
            }
#pragma warning restore 300
        }

        // Default selection prefer LoadLastSaveButton
        GameObject fallback = null;
        if (selectableItems != null)
        {
            for (int i = 0; i < selectableItems.Length; i++)
            {
                var rt = selectableItems[i];
                if (rt != null) { fallback = rt.gameObject; break; }
            }
        }
        _defaultSelection = loadLastSaveButton ? loadLastSaveButton.gameObject : fallback;

        // No sobrescribimos ni re-sincronizamos los UnityEvents en Awake. Los listeners se añadirán
        // y quitarán cuando el menú se muestre/oculte para evitar callbacks fuera de contexto.
        // Asegurar estado inicial no interactivo para evitar que botones respondan si el GameObject
        // se activa inadvertidamente.
        if (rootGroup != null)
        {
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }
        // Solo afectar botones que pertenecen al panel de GameOver para no interferir con HUD
        if (loadLastSaveButton != null)
        {
            if (gameOverUI != null && loadLastSaveButton.transform.IsChildOf(gameOverUI.transform))
                loadLastSaveButton.interactable = false;
            else
                Debug.LogWarning("[GameOverManager] loadLastSaveButton no es hijo de gameOverUI; no se modificará en Awake.");
        }
        if (backToMenuButton != null)
        {
            if (gameOverUI != null && backToMenuButton.transform.IsChildOf(gameOverUI.transform))
                backToMenuButton.interactable = false;
            else
                Debug.LogWarning("[GameOverManager] backToMenuButton no es hijo de gameOverUI; no se modificará en Awake.");
        }
    }

    private void OnEnable()
    {
        // No manejar la pausa/cursores aquí: OnEnable se llama cuando el GO se activa, pero
        // el menú de Game Over puede estar oculto. Usar ShowGameOver/HideGameOver para eso.
    }

    private void OnDisable()
    {
        // No manejar la reanudación aquí por la misma razón.
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        // Solo procesar inputs cuando el Game Over esté realmente mostrado, la UI esté activa y las acciones estén permitidas
        if (!_isGameOverShown || !_allowActions || (gameOverUI != null && !gameOverUI.activeInHierarchy)) return;
#else
        if (!_isGameOverShown || !_allowActions || (gameOverUI != null && !gameOverUI.activeInHierarchy)) return;
#endif

        KeepUIFocusForGamepad();

        // Cancel/back behavior: llevar al menú principal
#if ENABLE_INPUT_SYSTEM
        if (Gamepad.current != null)
        {
            var gp = Gamepad.current;
            // Start / buttonSouth (A) -> continuar (ocultar GameOver)
            if (gp.startButton.wasPressedThisFrame || gp.buttonSouth.wasPressedThisFrame)
            {
                HideGameOver();
                return;
            }
            // buttonEast (B) -> volver al menú principal (cancel)
            if (gp.buttonEast.wasPressedThisFrame)
            {
                OnBackToMainMenu();
                return;
            }
        }
        else if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                OnBackToMainMenu();
                return;
            }
            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                HideGameOver();
                return;
            }
        }
#else
        // Legacy input: Submit resumes, Cancel/back goes to main menu
        if (Input.GetButtonDown("Submit"))
        {
            HideGameOver();
            return;
        }
        if (Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.Escape))
        {
            OnBackToMainMenu();
            return;
        }
#endif
    }

// ---------------- UI Focus Keeper ----------------
#pragma warning disable 300
    void EnsureUISelection()
    {
        if (_es == null) _es = EventSystem.current;
        if (_es == null) return;

        if (_es.currentSelectedGameObject == null || !_es.currentSelectedGameObject.activeInHierarchy)
        {
            var toSelect = _defaultSelection;
            if (toSelect == null)
            {
                var firstSel = gameOverUI != null ? gameOverUI.GetComponentInChildren<Selectable>(true) : GetComponentInChildren<Selectable>(true);
                if (!ReferenceEquals(firstSel, null)) toSelect = firstSel.gameObject;
            }
            if (toSelect != null)
            {
                _defaultSelection = toSelect;
                _es.SetSelectedGameObject(toSelect);
            }
        }
    }
#pragma warning restore 300

    void KeepUIFocusForGamepad()
    {
        if (_es == null) _es = EventSystem.current;
        if (_es == null) return;

        bool wantsPadFocus = false;

#if ENABLE_INPUT_SYSTEM
        if (Gamepad.current != null)
        {
            var gp = Gamepad.current;
            wantsPadFocus =
                gp.dpad.up.wasPressedThisFrame || gp.dpad.down.wasPressedThisFrame ||
                Mathf.Abs(gp.leftStick.ReadValue().y) > 0.25f ||
                gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame;
        }
        else
        {
            wantsPadFocus = Keyboard.current != null && (
                Keyboard.current.upArrowKey.wasPressedThisFrame ||
                Keyboard.current.downArrowKey.wasPressedThisFrame ||
                Keyboard.current.wKey.wasPressedThisFrame ||
                Keyboard.current.sKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.spaceKey.wasPressedThisFrame
            );
        }
#else
        wantsPadFocus = Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f || Input.GetButtonDown("Submit");
#endif

        if (wantsPadFocus && (_es.currentSelectedGameObject == null || !_es.currentSelectedGameObject.activeInHierarchy))
            EnsureUISelection();
    }

    // Attach/detach listeners: solo añadimos/quitan nuestro callback, no tocamos listeners serializados.
    void AttachUIButtonListeners()
    {
        if (rootGroup != null)
        {
            rootGroup.blocksRaycasts = true;
            rootGroup.interactable = true;
        }

        // Solo afectar botones que pertenecen al panel de GameOver
        if (loadLastSaveButton != null)
        {
            if (gameOverUI != null && loadLastSaveButton.transform.IsChildOf(gameOverUI.transform))
            {
                loadLastSaveButton.onClick.RemoveListener(OnContinueButtonPressed);
                loadLastSaveButton.onClick.AddListener(OnContinueButtonPressed);
                loadLastSaveButton.interactable = true;
            }
            else
            {
                Debug.LogWarning("[GameOverManager] loadLastSaveButton no pertenece a gameOverUI — no se añadirá listener");
            }
        }

        if (backToMenuButton != null)
        {
            if (gameOverUI != null && backToMenuButton.transform.IsChildOf(gameOverUI.transform))
            {
                backToMenuButton.onClick.RemoveListener(OnBackToMainMenu);
                backToMenuButton.onClick.AddListener(OnBackToMainMenu);
                backToMenuButton.interactable = true;
            }
            else
            {
                Debug.LogWarning("[GameOverManager] backToMenuButton no pertenece a gameOverUI — no se añadirá listener");
            }
        }

        // Permitir la ejecución de las acciones públicas ahora que los listeners están adjuntos
        _allowActions = true;
    }

    void DetachUIButtonListeners()
    {
        if (rootGroup != null)
        {
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }

        if (loadLastSaveButton != null && gameOverUI != null && loadLastSaveButton.transform.IsChildOf(gameOverUI.transform))
        {
            loadLastSaveButton.onClick.RemoveListener(OnContinueButtonPressed);
            loadLastSaveButton.interactable = false;
        }

        if (backToMenuButton != null && gameOverUI != null && backToMenuButton.transform.IsChildOf(gameOverUI.transform))
        {
            backToMenuButton.onClick.RemoveListener(OnBackToMainMenu);
            backToMenuButton.interactable = false;
        }

        // Desactivar la ejecución de acciones públicas
        _allowActions = false;
    }

    /// <summary>
    /// Muestra la pantalla de Game Over. Pausa el juego si está configurado.
    /// </summary>
    public void ShowGameOver()
    {
        // Evitar reentradas: si ya está mostrado o en proceso de mostrarse, ignorar
        if (_isGameOverShown || _showCoroutine != null) return;

        // Iniciar la coroutine que espera en tiempo real para permitir animaciones/efectos
        _showCoroutine = StartCoroutine(ShowGameOverRoutine());
    }

    private System.Collections.IEnumerator ShowGameOverRoutine()
    {
        if (delayBeforeShow > 0f)
        {
            // Esperar en tiempo real para que animaciones y barras puedan terminar aunque Time.timeScale siga en 1
            yield return new WaitForSecondsRealtime(delayBeforeShow);
        }

        _showCoroutine = null;
        _isGameOverShown = true;

        if (gameOverUI != null)
            gameOverUI.SetActive(true);

        if (pauseOnGameOver)
            Time.timeScale = 0f;

        // Asegurar selección inicial del UI
        EnsureUISelection();

        // Adjuntar solo nuestros listeners y activar la interacción
        AttachUIButtonListeners();

        Debug.Log("[GameOverManager] Game Over mostrado");
    }

    /// <summary>
    /// Oculta la pantalla de Game Over y reanuda el juego.
    /// </summary>
    public void HideGameOver()
    {
        // Si estamos en espera para mostrar, cancelarla
        if (_showCoroutine != null)
        {
            StopCoroutine(_showCoroutine);
            _showCoroutine = null;
        }

        if (!_isGameOverShown) return;
        _isGameOverShown = false;

        if (gameOverUI != null)
            gameOverUI.SetActive(false);

        if (pauseOnGameOver)
            Time.timeScale = 1f;

        // Quitar nuestros listeners y desactivar interacción
        DetachUIButtonListeners();

        Debug.Log("[GameOverManager] Game Over ocultado");
    }

    /// <summary>
    /// Reinicia la escena actual. Asegura que Time.timeScale se restablece.
    /// </summary>
    public void RestartLevel()
    {
        if (pauseOnGameOver)
            Time.timeScale = 1f;

        // Antes de recargar la escena, asegurarnos de que el runtimePreset contiene los valores actuales de HP/MP
        try
        {
            var profile = GameBootService.Profile;
            if (profile != null)
            {
                var preset = profile.GetActivePresetResolved();
                if (preset != null)
                {
                    // Intentar obtener PlayerHealthSystem del jugador
                    PlayerHealthSystem phs = UnityEngine.Object.FindFirstObjectByType<PlayerHealthSystem>();
                    if (phs == null)
                    {
                        var playerGo = GameObject.FindWithTag("Player");
                        if (playerGo != null)
                            phs = playerGo.GetComponent<PlayerHealthSystem>();
                    }

                    if (phs != null)
                    {
                        preset.currentHP = phs.CurrentHealth;
                        preset.maxHP = phs.MaxHealth;
                        Debug.Log($"[GameOverManager] Runtime preset HP actualizado: {preset.currentHP}/{preset.maxHP}");
                    }
                    else
                    {
                        Debug.LogWarning("[GameOverManager] No se encontró PlayerHealthSystem para sincronizar HP antes de reiniciar");
                    }

                    // Manejar Maná: si el preset no tiene la ability de magia, respetar 0/0
                    if (preset.abilities != null && !preset.abilities.magic)
                    {
                        preset.currentMP = 0f;
                        preset.maxMP = 0f;
                        Debug.Log("[GameOverManager] Preset indica que no tiene magia -> MP seteado a 0/0 antes de reiniciar");
                    }
                    else
                    {
                        // Obtener ManaPool del jugador
                        ManaPool mana = UnityEngine.Object.FindFirstObjectByType<ManaPool>();
                        if (mana == null)
                        {
                            var playerGo = GameObject.FindWithTag("Player");
                            if (playerGo != null)
                                mana = playerGo.GetComponentInChildren<ManaPool>() ?? playerGo.GetComponent<ManaPool>();
                        }

                        if (mana != null)
                        {
                            preset.maxMP = mana.Max;
                            preset.currentMP = mana.Current;
                            Debug.Log($"[GameOverManager] Runtime preset MP actualizado: {preset.currentMP}/{preset.maxMP}");
                        }
                        else
                        {
                            Debug.LogWarning("[GameOverManager] No se encontró ManaPool para sincronizar MP antes de reiniciar (dejando valores del preset)");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[GameOverManager] Profile disponible pero GetActivePresetResolved() devolvió null");
                }
            }
            else
            {
                Debug.LogWarning("[GameOverManager] GameBootService.Profile no está disponible al reiniciar nivel");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GameOverManager] Error sincronizando preset antes de reiniciar: {e}");
        }

        var active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.name);
    }

    /// <summary>
    /// Llamado desde el botón "Continuar" en la UI. Wrapper público que garantiza la lógica correcta
    /// y deja un log claro para depuración del mapeo del botón.
    /// </summary>
    public void OnContinueButtonPressed()
    {
        if (!_allowActions || !_isGameOverShown)
        {
            Debug.LogWarning("[GameOverManager] OnContinueButtonPressed ignorado porque el menú no permite acciones ahora.");
            Debug.Log(new System.Diagnostics.StackTrace().ToString());
            return;
        }

        Debug.Log("[GameOverManager] OnContinueButtonPressed invoked -> ocultando GameOver (resumir juego)");
        HideGameOver();
    }

    /// <summary>
    /// Llamado desde el botón "Cargar partida" en la UI. Reutiliza la lógica de MainMenuController.OnContinue().
    /// </summary>
    public void OnLoadLastSave()
    {
        if (!_allowActions || !_isGameOverShown)
        {
            Debug.LogWarning("[GameOverManager] OnLoadLastSave ignorado porque el menú no permite acciones ahora.");
            Debug.Log(new System.Diagnostics.StackTrace().ToString());
            return;
        }

        Debug.Log($"[GameOverManager] OnLoadLastSave invoked. worldScene='{worldScene}', mainMenuScene='{mainMenuScene}'");
        // Restaurar timescale antes de hacer la carga/scene load
        if (pauseOnGameOver)
            Time.timeScale = 1f;

        var saveSystem = UnityEngine.Object.FindFirstObjectByType<SaveSystem>();
        bool hasSave = saveSystem != null && saveSystem.HasSave();
        Debug.Log($"[GameOverManager] SaveSystem found={ (saveSystem!=null) }, HasSave={hasSave}");

        if (saveSystem != null && saveSystem.HasSave())
        {
            if (GameBootService.IsAvailable)
                GameBootService.Profile?.LoadProfile(saveSystem);

            Debug.Log($"[GameOverManager] Loading scene '{worldScene}' from save");
            // Llamada dinámica a SceneTransitionLoader.Load para evitar dependencia dura
            var loaderType = System.Type.GetType("SceneTransitionLoader, Assembly-CSharp");
            if (loaderType != null)
            {
                var method = loaderType.GetMethod("Load", new System.Type[] { typeof(string) });
                method?.Invoke(null, new object[] { worldScene });
            }
            else
            {
                SceneManager.LoadScene(worldScene);
            }
        }
        else
        {
            Debug.Log("[GameOverManager] No hay partida guardada; empezar nueva partida");
            // Si no hay save, empezar nueva partida (igual que MainMenuController)
            GameBootService.NewGameReset();

            Debug.Log($"[GameOverManager] Loading scene '{worldScene}' for new game");
            var loaderType = System.Type.GetType("SceneTransitionLoader, Assembly-CSharp");
            if (loaderType != null)
            {
                var method = loaderType.GetMethod("Load", new System.Type[] { typeof(string) });
                method?.Invoke(null, new object[] { worldScene });
            }
            else
            {
                SceneManager.LoadScene(worldScene);
            }
        }
    }

    /// <summary>
    /// Llamado desde el botón "Volver al menú principal".
    /// </summary>
    public void OnBackToMainMenu()
    {
        if (!_allowActions || !_isGameOverShown)
        {
            Debug.LogWarning("[GameOverManager] OnBackToMainMenu ignorado porque el menú no permite acciones ahora.");
            Debug.Log(new System.Diagnostics.StackTrace().ToString());
            return;
        }

        Debug.Log($"[GameOverManager] OnBackToMainMenu invoked. mainMenuScene='{mainMenuScene}'");
        if (pauseOnGameOver)
            Time.timeScale = 1f;


        var loaderType = System.Type.GetType("SceneTransitionLoader, Assembly-CSharp");
        if (loaderType != null)
        {
            var method = loaderType.GetMethod("Load", new System.Type[] { typeof(string) });
            method?.Invoke(null, new object[] { mainMenuScene });
        }
        else
        {
            SceneManager.LoadScene(mainMenuScene);
        }
    }

    /// <summary>
    /// Modo helper para notificar Game Over desde otros scripts de forma segura.
    /// </summary>
    public static void NotifyGameOver()
    {
        if (Instance != null)
            Instance.ShowGameOver();
        else
            Debug.LogWarning("[GameOverManager] No hay instancia disponible para mostrar Game Over.");
    }
}
