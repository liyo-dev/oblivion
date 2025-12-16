using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using EasyTransition;
using DG.Tweening;
using Core;

/// <summary>
/// Gestiona la pantalla de Game Over simple con dos opciones:
/// - Cargar última partida guardada (solo si existe)
/// - Volver al menú principal
/// </summary>
public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    [Header("UI")]
    [Tooltip("Referencia al objeto de UI que actúa como pantalla de Game Over.")]
    [SerializeField] private GameObject gameOverUI;
    [SerializeField] private Button loadLastSaveButton;
    [SerializeField] private Button backToMenuButton;
    [SerializeField] private CanvasGroup rootGroup;

    [Header("Escenas")]
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("Transiciones")]
    [SerializeField] private TransitionManager transitionManager;
    [SerializeField] private TransitionSettings reloadTransitionSettings;
    [SerializeField] private TransitionSettings mainMenuTransitionSettings;
    [SerializeField] private float transitionStartDelay = 0f;

    [Header("Comportamiento")]
    [Tooltip("Si está activado, al mostrar GameOver se pausará el juego.")]
    [SerializeField] private bool pauseOnGameOver = true;
    [Tooltip("Retraso en segundos antes de mostrar la UI.")]
    [SerializeField] private float delayBeforeShow = 0.75f;

    private bool _isGameOverShown = false;
    private Coroutine _showCoroutine = null;

    public bool IsShown => _isGameOverShown;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsurePersistentIfPlacedInStartScene()
    {
        try
        {
            if (ServiceLocator.TryGet(out GameOverManager existing) && existing != null)
            {
                if (existing.transform.root != null)
                    UnityEngine.Object.DontDestroyOnLoad(existing.transform.root.gameObject);
                else
                    UnityEngine.Object.DontDestroyOnLoad(existing.gameObject);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[GameOverManager] EnsurePersistentIfPlacedInStartScene failed: {ex}");
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ServiceLocator.Register(this);
        SceneManager.sceneLoaded += HandleSceneLoaded;

        // Persistir si está en la escena inicial
        if (gameObject.scene.isLoaded && gameObject.scene.buildIndex == 0)
        {
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
        }

        if (gameOverUI != null)
            gameOverUI.SetActive(false);

        if (rootGroup == null && gameOverUI != null)
            rootGroup = gameOverUI.GetComponent<CanvasGroup>() ?? gameOverUI.AddComponent<CanvasGroup>();

        // Estado inicial: no interactivo
        if (rootGroup != null)
        {
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }

        SetButtonsInteractable(false);
    }

    private void OnDestroy()
    {
        // Detener y limpiar todas las animaciones activas de DOTween
        DOTween.KillAll();
        DOTween.Clear();

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this)
        {
            Instance = null;
        }
        ServiceLocator.Unregister(this);
    }

    void OnEnable()
    {
        GamepadInputReader.EnsureInputEventsSubscribed();
        GamepadInputReader.OnInput += HandleGamepadInput;
    }

    void OnDisable()
    {
        GamepadInputReader.OnInput -= HandleGamepadInput;
    }

    private void HandleGamepadInput(GamepadInputReader.InputEvent input)
    {
        if (!_isGameOverShown || gameOverUI == null || !gameOverUI.activeInHierarchy)
            return;

        if (input.Phase != InputActionPhase.Performed)
            return;

        // Cancel = volver al menú principal
        if (input.Type == GamepadInputReader.InputEventType.Cancel)
        {
            OnBackToMainMenu();
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (loadLastSaveButton != null && gameOverUI != null && 
            loadLastSaveButton.transform.IsChildOf(gameOverUI.transform))
        {
            loadLastSaveButton.interactable = interactable;
        }

        if (backToMenuButton != null && gameOverUI != null && 
            backToMenuButton.transform.IsChildOf(gameOverUI.transform))
        {
            backToMenuButton.interactable = interactable;
        }
    }

    /// <summary>
    /// Muestra la pantalla de Game Over. Pausa el juego si está configurado.
    /// </summary>
    public void ShowGameOver()
    {
        if (_isGameOverShown || _showCoroutine != null) 
            return;

        _showCoroutine = StartCoroutine(ShowGameOverRoutine());
    }

    private System.Collections.IEnumerator ShowGameOverRoutine()
    {
        if (delayBeforeShow > 0f)
        {
            yield return new WaitForSecondsRealtime(delayBeforeShow);
        }

        _showCoroutine = null;
        _isGameOverShown = true;

        if (gameOverUI != null)
            gameOverUI.SetActive(true);

        if (pauseOnGameOver)
            Time.timeScale = 0f;

        // Configurar botones según si hay save o no
        ConfigureButtons();

        // Activar interacción
        if (rootGroup != null)
        {
            rootGroup.blocksRaycasts = true;
            rootGroup.interactable = true;
        }

        Debug.Log("[GameOverManager] Game Over mostrado");
    }

    private void ConfigureButtons()
    {
        var saveSystem = ServiceLocator.Get<SaveSystem>(logIfMissing: false);
        bool hasSave = saveSystem != null && saveSystem.HasSave();

        // El botón de cargar solo está activo si hay save
        if (loadLastSaveButton != null && gameOverUI != null && 
            loadLastSaveButton.transform.IsChildOf(gameOverUI.transform))
        {
            loadLastSaveButton.gameObject.SetActive(hasSave);
            if (hasSave)
            {
                loadLastSaveButton.interactable = true;
                loadLastSaveButton.onClick.RemoveListener(OnLoadLastSave);
                loadLastSaveButton.onClick.AddListener(OnLoadLastSave);
            }
        }

        // El botón de volver al menú siempre está activo
        if (backToMenuButton != null && gameOverUI != null && 
            backToMenuButton.transform.IsChildOf(gameOverUI.transform))
        {
            backToMenuButton.interactable = true;
            backToMenuButton.onClick.RemoveListener(OnBackToMainMenu);
            backToMenuButton.onClick.AddListener(OnBackToMainMenu);
        }
    }

    /// <summary>
    /// Oculta la pantalla de Game Over y reanuda el juego.
    /// </summary>
    public void HideGameOver(bool resumeTime = true)
    {
        if (_showCoroutine != null)
        {
            StopCoroutine(_showCoroutine);
            _showCoroutine = null;
        }

        if (!_isGameOverShown) 
            return;

        _isGameOverShown = false;

        if (gameOverUI != null)
            gameOverUI.SetActive(false);

        if (pauseOnGameOver && resumeTime)
            Time.timeScale = 1f;

        // Desactivar interacción y remover listeners
        if (rootGroup != null)
        {
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }

        if (loadLastSaveButton != null && gameOverUI != null && 
            loadLastSaveButton.transform.IsChildOf(gameOverUI.transform))
        {
            loadLastSaveButton.onClick.RemoveListener(OnLoadLastSave);
        }

        if (backToMenuButton != null && gameOverUI != null && 
            backToMenuButton.transform.IsChildOf(gameOverUI.transform))
        {
            backToMenuButton.onClick.RemoveListener(OnBackToMainMenu);
        }

        SetButtonsInteractable(false);

        Debug.Log("[GameOverManager] Game Over ocultado");
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ForceResetState($"[GameOverManager] Scene '{scene.name}' loaded -> forzando estado cerrado");
    }

    private void ForceResetState(string reason = null)
    {
        if (_showCoroutine != null)
        {
            StopCoroutine(_showCoroutine);
            _showCoroutine = null;
        }

        _isGameOverShown = false;

        if (gameOverUI != null)
            gameOverUI.SetActive(false);

        if (rootGroup != null)
        {
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }

        if (pauseOnGameOver)
            Time.timeScale = 1f;

        SetButtonsInteractable(false);

        if (!string.IsNullOrEmpty(reason))
            Debug.Log(reason);
    }

    /// <summary>
    /// Reinicia la escena actual. Asegura que Time.timeScale se restablece.
    /// </summary>
    public void RestartLevel()
    {
        // Asegurar que el menú queda completamente oculto antes de recargar
        HideGameOver(resumeTime: false);

        // Antes de recargar la escena, asegurarnos de que el runtimePreset contiene los valores actuales de HP/MP
        try
        {
            var profile = GameBootService.Profile;
            if (profile != null)
            {
                var preset = profile.GetActivePresetResolved();
                if (preset != null)
                {
                    // Intentar obtener PlayerHealthSystem del jugador sin usar FindObject directamente
                    ServiceLocator.TryGet(out PlayerHealthSystem phs);
                    if (phs == null)
                    {
                        var playerGo = GameObject.FindWithTag("Player");
                        if (playerGo != null)
                            phs = playerGo.GetComponent<PlayerHealthSystem>();

                        if (phs != null)
                            ServiceLocator.Register(phs);
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
                        ServiceLocator.TryGet(out ManaPool mana);
                        if (mana == null)
                        {
                            var playerGo = GameObject.FindWithTag("Player");
                            if (playerGo != null)
                                mana = playerGo.GetComponentInChildren<ManaPool>() ?? playerGo.GetComponent<ManaPool>();

                            if (mana != null)
                                ServiceLocator.Register(mana);
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
        LoadSceneWithTransition(active.name, reloadTransitionSettings, mainMenuTransitionSettings, "Reiniciar nivel");
    }


    private TransitionSettings ResolveTransitionSettings(TransitionSettings preferred, TransitionSettings fallback)
    {
        if (preferred != null) return preferred;
        return fallback;
    }

    private TransitionManager ResolveTransitionManager()
    {
        if (transitionManager != null) return transitionManager;

        if (ServiceLocator.TryGet(out TransitionManager cached) && cached != null)
        {
            transitionManager = cached;
            return transitionManager;
        }

        try
        {
            transitionManager = TransitionManager.Instance();
            if (transitionManager != null)
            {
                ServiceLocator.Register(transitionManager);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[GameOverManager] TransitionManager.Instance() falló: {ex.Message}");
        }

        return transitionManager;
    }

    private void ResetProfileForPresetReload()
    {
        if (!GameBootService.IsAvailable) return;
        var profile = GameBootService.Profile;
        if (profile == null) return;

        if (profile.ShouldBootFromPreset() && profile.bootPreset != null)
        {
            profile.EnsureRuntimePresetFromTemplate(profile.bootPreset);
            Debug.Log("[GameOverManager] RuntimePreset restablecido desde bootPreset antes de recargar la escena actual");
        }
        else
        {
            GameBootService.NewGameReset();
            Debug.LogWarning("[GameOverManager] No hay bootPreset activo; se realizó NewGameReset para limpiar el runtimePreset antes de recargar");
        }
    }

    private void LoadSceneWithTransition(string targetScene, TransitionSettings preferredSettings, TransitionSettings fallbackSettings, string context)
    {
        var settings = ResolveTransitionSettings(preferredSettings, fallbackSettings);
        var tm = ResolveTransitionManager();

        if (tm != null && settings != null)
        {
            Debug.Log($"[GameOverManager] {context} usando TransitionManager -> '{targetScene}'");
            tm.Transition(targetScene, settings, transitionStartDelay);
            return;
        }

        if (tm == null)
            Debug.LogWarning($"[GameOverManager] No se encontró TransitionManager para {context}. Carga directa de '{targetScene}'.");
        else
            Debug.LogWarning($"[GameOverManager] TransitionSettings no configurado ({context}). Carga directa de '{targetScene}'.");

        SceneManager.LoadScene(targetScene);
    }

    /// <summary>
    /// Llamado desde el botón "Cargar partida" en la UI. Reutiliza la lógica de MainMenuController.OnContinue().
    /// </summary>
    public void OnLoadLastSave()
    {
        if (!_isGameOverShown)
        {
            Debug.LogWarning("[GameOverManager] OnLoadLastSave ignorado porque el menú no está visible.");
            return;
        }

        Debug.Log("[GameOverManager] OnLoadLastSave invoked -> recargar escena actual usando transición configurada");

        // Cerrar el panel antes de iniciar la recarga para que los flags internos se reinicien
        HideGameOver(resumeTime: false);

        var saveSystem = ServiceLocator.Get<SaveSystem>(logIfMissing: false);
        bool hasSave = saveSystem != null && saveSystem.HasSave();
        bool forcePreset = GameBootService.IsPresetOverrideActive;
        Debug.Log($"[GameOverManager] SaveSystem found={(saveSystem!=null)}, HasSave={hasSave}, ForcePreset={forcePreset}");

        if (forcePreset)
        {
            Debug.Log("[GameOverManager] Modo preset/test activo -> se omite la carga de save al recargar la escena actual.");
            ResetProfileForPresetReload();
        }
        else if (hasSave)
        {
            if (GameBootService.IsAvailable)
                GameBootService.Profile?.LoadProfile(saveSystem);
        }
        else
        {
            Debug.LogWarning("[GameOverManager] No hay partida guardada; se recargará la escena actual igualmente.");
            ResetProfileForPresetReload();
        }

        string sceneToReload = SceneManager.GetActiveScene().name;
        LoadSceneWithTransition(sceneToReload, reloadTransitionSettings, mainMenuTransitionSettings, "Recargar escena actual");
    }


    /// <summary>
    /// Llamado desde el botón "Volver al menú principal".
    /// </summary>
    public void OnBackToMainMenu()
    {
        if (!_isGameOverShown)
        {
            Debug.LogWarning("[GameOverManager] OnBackToMainMenu ignorado porque el menú no está visible.");
            return;
        }

        Debug.Log($"[GameOverManager] OnBackToMainMenu invoked. mainMenuScene='{mainMenuScene}'");

        // Evitar quedar marcados como mostrados cuando la escena cambie
        HideGameOver(resumeTime: false);

        MainMenuController.RequestInputDebounce();
        LoadSceneWithTransition(mainMenuScene, mainMenuTransitionSettings, reloadTransitionSettings, "Volver al menú principal");
    }


    /// <summary>
    /// Intenta localizar una instancia existente incluso si está inactiva (por ejemplo, si el panel está en un prefab desactivado).
    /// </summary>
    private static bool TryResolveInstance()
    {
        if (Instance != null) return true;
        if (ServiceLocator.TryGet(out GameOverManager found) && found != null)
        {
            Instance = found;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Asegura que la instancia está activa y habilitada antes de mostrar el menú.
    /// </summary>
    private static bool EnsureInstanceReady()
    {
        if (!TryResolveInstance())
            return false;

        if (!Instance.gameObject.activeSelf)
            Instance.gameObject.SetActive(true);

        if (!Instance.enabled)
            Instance.enabled = true;

        return true;
    }

    /// <summary>
    /// Modo helper para notificar Game Over desde otros scripts de forma segura.
    /// </summary>
    public static void NotifyGameOver()
    {
        if (!EnsureInstanceReady())
        {
            Debug.LogWarning("[GameOverManager] No se encontró ninguna instancia para mostrar Game Over.");
            return;
        }

        Instance.ShowGameOver();
    }
}
