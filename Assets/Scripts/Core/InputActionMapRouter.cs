using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Centraliza el cambio entre los action maps de Gameplay y UI.
/// Escucha los cambios de <see cref="GameState"/> y <see cref="MenuManager"/> para
/// asegurar que, cuando se abre un menú, el mapa activo sea el de UI y no se
/// filtren inputs al jugador. Vuelve automáticamente al mapa de Gameplay cuando
/// ya no hay menús abiertos y el gameplay está permitido.
/// </summary>
[DefaultExecutionOrder(-150)]
[DisallowMultipleComponent]
public class InputActionMapRouter : MonoBehaviour
{
    public static InputActionMapRouter Instance { get; private set; }

    [Header("Referencias")]
    [Tooltip("PlayerInput que controla el asset de acciones (se buscará automáticamente si está vacío).")]
    [SerializeField] private PlayerInput playerInput;
    [Tooltip("Asset de acciones a usar si no hay PlayerInput disponible.")]
    [SerializeField] private InputActionAsset actionsAsset;
    [Tooltip("Proveedor centralizado de controles para evitar búsquedas repetidas.")]
    [SerializeField] private PlayerInputManager inputManager;

    [Header("Nombres de Action Map")]
    [SerializeField] private string gameplayMapName = "GamePlay";
    [SerializeField] private string uiMapName = "UI";

    [Header("Comportamiento")]
    [Tooltip("Si está activo, el mapa de UI se fuerza mientras GameState.CanProcessGameplayInput sea falso.")]
    [SerializeField] private bool useGameStateGate = true;
    [Tooltip("Mantener vivo entre escenas.")]
    [SerializeField] private bool dontDestroyOnLoad = true;
    [Tooltip("Mostrar logs cuando se cambie de mapa.")]
    [SerializeField] private bool debugLogs = false;

    string _lastAppliedMap;

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

        if (inputManager == null)
            ServiceLocator.TryGet(out inputManager);

        ResolvePlayerInput();
    }

    void OnEnable()
    {
        GameState.OnChanged += Apply;
        MenuManager.MenuOpened += OnMenuStateChanged;
        MenuManager.MenuClosed += OnMenuStateChanged;
        Apply();
    }

    void OnDisable()
    {
        GameState.OnChanged -= Apply;
        MenuManager.MenuOpened -= OnMenuStateChanged;
        MenuManager.MenuClosed -= OnMenuStateChanged;
    }

    void OnMenuStateChanged(MenuKind kind)
    {
        Apply();
    }

    /// <summary>Evalúa el estado del juego y selecciona el action map adecuado.</summary>
    public void Apply()
    {
        bool gameplayAllowed = !useGameStateGate || GameState.CanProcessGameplayInput;
        bool anyMenu = MenuManager.AnyOpen();

        string targetMap = (!gameplayAllowed || anyMenu) ? uiMapName : gameplayMapName;

        if (string.IsNullOrEmpty(targetMap)) return;

        ResolvePlayerInput();
        var actions = playerInput != null ? playerInput.actions : actionsAsset;
        if (actions == null)
            return;

        if (_lastAppliedMap == targetMap && playerInput != null && playerInput.currentActionMap != null && playerInput.currentActionMap.name == targetMap)
            return;

        var map = actions.FindActionMap(targetMap, throwIfNotFound: false);
        if (map == null)
        {
            Debug.LogWarning($"[InputActionMapRouter] No se encontró el action map '{targetMap}' en el asset asignado.");
            return;
        }

        map.Enable();
        if (playerInput != null)
        {
            // Si el PlayerInput está desactivado (p.ej. al cambiar de escena) el switch lanza
            // "input is not enabled". Aseguramos la activación antes de conmutar.
            if (!playerInput.enabled)
                playerInput.enabled = true;

            // Si el GameObject sigue inactivo no podemos activar el input todavía.
            if (!playerInput.gameObject.activeInHierarchy)
            {
                if (debugLogs)
                    Debug.LogWarning("[InputActionMapRouter] PlayerInput está inactivo en la jerarquía; se reintentará cuando se active.");
                return;
            }

            if (!playerInput.inputIsActive)
            {
                try
                {
                    playerInput.ActivateInput();
                }
                catch (System.InvalidOperationException ex)
                {
                    Debug.LogWarning($"[InputActionMapRouter] No se pudo activar el input aún: {ex.Message}");
                    return;
                }
            }

            playerInput.SwitchCurrentActionMap(targetMap);
        }
        else
        {
            // Deshabilitar otros mapas para evitar lecturas simultáneas
            foreach (var other in actions.actionMaps)
            {
                if (other != map) other.Disable();
            }
        }
        _lastAppliedMap = targetMap;

        if (debugLogs)
            Debug.Log($"[InputActionMapRouter] Cambiado a action map: {targetMap} (menus abiertos: {anyMenu}, gameplay permitido: {gameplayAllowed})");
    }

    void ResolvePlayerInput()
    {
        if (playerInput != null) return;

        if (inputManager == null)
            ServiceLocator.TryGet(out inputManager);

        if (inputManager != null)
        {
            playerInput = inputManager.PlayerInput;
            if (playerInput != null && playerInput.actions != null && actionsAsset == null)
                actionsAsset = playerInput.actions;

            if (actionsAsset == null)
                actionsAsset = inputManager.Controls?.asset;

            if (playerInput != null) return;
        }

#if UNITY_2022_3_OR_NEWER
        playerInput = FindFirstObjectByType<PlayerInput>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
        playerInput = FindObjectOfType<PlayerInput>(true);
#pragma warning restore 618
#endif

        if (playerInput != null && playerInput.actions != null && actionsAsset == null)
            actionsAsset = playerInput.actions;
    }
}
