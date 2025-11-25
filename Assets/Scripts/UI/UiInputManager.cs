using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Punto único de entrada para inputs de UI. Se asegura de que las peticiones de apertura/cierre
/// de menús pasen por <see cref="MenuManager"/>, evitando solapes entre menús y respetando los
/// locks de <see cref="GameState"/>.
/// </summary>
[DefaultExecutionOrder(-175)]
[DisallowMultipleComponent]
public sealed class UiInputManager : MonoBehaviour
{
    public static UiInputManager Instance { get; private set; }

    [Header("Acciones UI")]
    [Tooltip("Acción del Input System que dispara el menú de pausa (Start/Escape).")]
    [SerializeField] private InputActionReference pauseAction;

    [Header("Opciones")]
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool debugLogs = false;

    private PauseMenuController _pauseMenu;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        ServiceLocator.Register(this);
    }

    void OnEnable()
    {
        BindAction(pauseAction, OnPauseRequested);
    }

    void OnDisable()
    {
        UnbindAction(pauseAction, OnPauseRequested);
    }

    void Update()
    {
        // Si no hay InputAction configurado en el inspector, utiliza un fallback por polling
        // para que Start/Escape sigan abriendo la pausa.
        if (pauseAction != null && pauseAction.action != null && pauseAction.action.enabled)
            return;

            if (WasPausePressed())
            {
                //HandlePauseRequest();
            }    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            ServiceLocator.Unregister(this);
            Instance = null;
        }
    }

    private void BindAction(InputActionReference actionRef, System.Action<InputAction.CallbackContext> callback)
    {
        if (actionRef == null || actionRef.action == null || callback == null) return;
        actionRef.action.performed += callback;
        if (!actionRef.action.enabled) actionRef.action.Enable();
    }

    private void UnbindAction(InputActionReference actionRef, System.Action<InputAction.CallbackContext> callback)
    {
        if (actionRef == null || actionRef.action == null || callback == null) return;
        actionRef.action.performed -= callback;
    }

    private void OnPauseRequested(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        //HandlePauseRequest();
    }

    private void EnsurePauseMenu()
    {
        if (_pauseMenu != null) return;
        _pauseMenu = ServiceLocator.Get<PauseMenuController>(logIfMissing: false);
        if (_pauseMenu == null)
        {
#if UNITY_2022_3_OR_NEWER
            _pauseMenu = FindFirstObjectByType<PauseMenuController>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
            _pauseMenu = FindObjectOfType<PauseMenuController>(true);
#pragma warning restore 618
#endif
        }
    }

    private void HandlePauseRequest()
    {
        EnsurePauseMenu();

        if (_pauseMenu == null)
        {
            if (debugLogs) Debug.LogWarning("[UiInputManager] No se encontró PauseMenuController activo.");
            return;
        }

        // Si hay otro menú abierto que no sea pausa, no permitir abrir pausa para evitar solapes.
        if (!MenuManager.IsOpen(MenuKind.Pause) && MenuManager.AnyOpen())
        {
            if (debugLogs) Debug.Log("[UiInputManager] Pausa ignorada porque hay otro menú abierto.");
            return;
        }

        if (MenuManager.IsOpen(MenuKind.Pause))
        {
            MenuManager.Close(MenuKind.Pause);
            _pauseMenu.Resume();
            if (debugLogs) Debug.Log("[UiInputManager] Cerrando pausa");
            return;
        }

        if (!GameState.CanOpenPause)
            return;

        if (MenuManager.TryOpen(MenuKind.Pause))
        {
            _pauseMenu.ShowPauseMenu();
            if (debugLogs) Debug.Log("[UiInputManager] Abriendo pausa");
        }
    }

    private bool WasPausePressed()
    {
#if ENABLE_INPUT_SYSTEM
        try
        {
            var gp = Gamepad.current;
            if (gp != null && gp.startButton.wasPressedThisFrame)
                return true;

            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
                return true;
        }
        catch { }
#endif

        return Input.GetKeyDown(KeyCode.Escape)
            || Input.GetKeyDown(KeyCode.Backspace)
            || Input.GetKeyDown(KeyCode.JoystickButton7);
    }
}
