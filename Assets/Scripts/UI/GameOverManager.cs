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

        // Asegurar que los botones están enlazados correctamente en tiempo de ejecución.
        // Esto previene que un botón mal enlazado en la escena (por ejemplo apuntando a OnBackToMainMenu)
        // haga que "Continuar" lleve al menú principal por error.
        if (loadLastSaveButton != null)
        {
            try
            {
                // Reemplazar el UnityEvent elimina listeners serializados y runtime
                loadLastSaveButton.onClick = new Button.ButtonClickedEvent();
                loadLastSaveButton.onClick.AddListener(OnContinueButtonPressed);
                Debug.Log("[GameOverManager] loadLastSaveButton wired to OnContinueButtonPressed at Awake");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameOverManager] No se pudo re-sincronizar loadLastSaveButton: {e}");
            }
        }

        if (backToMenuButton != null)
        {
            try
            {
                backToMenuButton.onClick = new Button.ButtonClickedEvent();
                backToMenuButton.onClick.AddListener(OnBackToMainMenu);
                Debug.Log("[GameOverManager] backToMenuButton wired to OnBackToMainMenu at Awake");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameOverManager] No se pudo re-sincronizar backToMenuButton: {e}");
            }
        }

        if (worldScene == mainMenuScene)
        {
            Debug.LogWarning($"[GameOverManager] Atención: worldScene == mainMenuScene ('{worldScene}'). Esto hará que 'Continuar' cargue el menú principal. Revise la configuración.");
        }
    }

    private void OnEnable()
    {
        // Mostrar cursor y pausar juego
        if (pauseOnGameOver)
            Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureUISelection();
    }

    private void OnDisable()
    {
        // Reanudar juego
        if (pauseOnGameOver)
            Time.timeScale = 1f;

        // limpiar selección si estaba apuntando aquí
        if (_es != null && _es.currentSelectedGameObject == gameObject)
            _es.SetSelectedGameObject(null);
    }

    void Update()
    {
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

        // Cerrar el menú de pausa si está abierto para evitar que su InputAction siga intentando
        // invocar callbacks mientras GameOver está activo o si el objeto del PauseMenu fue destruido.
        var pause = UnityEngine.Object.FindFirstObjectByType<PauseMenuController>();
        if (pause != null)
        {
            try
            {
                // Asegurarse de que el menú de pausa se cierre
                pause.Resume();
                pause.gameObject.SetActive(false);
            }
            catch (System.Exception ex) { Debug.LogWarning($"[GameOverManager] Error cerrando PauseMenuController: {ex}"); }
        }

        if (pauseOnGameOver)
            Time.timeScale = 0f;

        // Asegurar selección inicial del UI
        EnsureUISelection();

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
        Debug.Log("[GameOverManager] OnContinueButtonPressed invoked -> ocultando GameOver (resumir juego)");
        // Siempre resumimos la sesión actual. Si quieres cargar una partida guardada, usa OnLoadLastSave desde el botón correspondiente.
        HideGameOver();
    }

    /// <summary>
    /// Llamado desde el botón "Cargar partida" en la UI. Reutiliza la lógica de MainMenuController.OnContinue().
    /// </summary>
    public void OnLoadLastSave()
    {
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