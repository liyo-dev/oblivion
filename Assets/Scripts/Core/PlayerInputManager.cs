using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Núcleo de entrada del jugador. Expone una instancia compartida de <see cref="PlayerControls"/>
/// y enruta cualquier comprobación de permisos hacia <see cref="PlayerActionManager"/>.
/// Registra el servicio en <see cref="ServiceLocator"/> para que el resto de sistemas pueda
/// obtener el mismo set de acciones sin crear copias adicionales del asset de Input System.
/// </summary>
[DefaultExecutionOrder(-250)]
[DisallowMultipleComponent]
public sealed class PlayerInputManager : MonoBehaviour
{
    public static PlayerInputManager Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private PlayerActionManager actionManager;

    [Header("Comportamiento")]
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool debugLogs = false;

    private PlayerControls _controls;
    private bool _ownsControlsInstance;

    /// <summary>Acceso único al asset de acciones de gameplay.</summary>
    public PlayerControls Controls => _controls;
    public PlayerInput PlayerInput => playerInput;
    public PlayerActionManager ActionManager => actionManager;

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

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();
        if (actionManager == null)
            actionManager = GetComponentInChildren<PlayerActionManager>(true);

        // Reutiliza el asset de PlayerInput si existe. Si no, crea uno nuevo.
        _controls = new PlayerControls();
        _ownsControlsInstance = true;

        // Si hay PlayerInput, preferimos que utilice el mismo asset generado para mantener un único punto de verdad.
        if (playerInput != null && playerInput.actions == null)
            playerInput.actions = _controls.asset;

        ServiceLocator.Register(this);

        if (debugLogs)
            Debug.Log("[PlayerInputManager] Inicializado");
    }

    void OnEnable()
    {
        _controls?.Enable();
    }

    void OnDisable()
    {
        _controls?.Disable();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            ServiceLocator.Unregister(this);
            Instance = null;
        }

        // Solo eliminamos el asset si es privado a este manager.
        if (_ownsControlsInstance)
            _controls?.Dispose();
    }

    /// <summary>Comprueba si la acción solicitada está permitida según <see cref="PlayerActionManager"/>.</summary>
    public bool CanProcess(PlayerAbility ability)
    {
        if (actionManager == null) return true;
        return actionManager.CanUse(ability);
    }

    /// <summary>Acceso cómodo para obtener una acción de gameplay de forma segura.</summary>
    public InputAction GetGameplayAction(Func<PlayerControls.GamePlayActions, InputAction> selector)
    {
        if (_controls == null || selector == null) return null;
        return selector.Invoke(_controls.GamePlay);
    }

    /// <summary>
    /// Devuelve la instancia compartida de <see cref="PlayerControls"/> si existe.
    /// Si no hay <see cref="PlayerInputManager"/>, crea una nueva instancia y
    /// devuelve la propiedad de eliminación al llamador mediante <paramref name="ownsInstance"/>.
    /// </summary>
    public static PlayerControls GetSharedOrNew(out bool ownsInstance)
    {
        if (Instance != null && Instance._controls != null)
        {
            ownsInstance = false;
            return Instance._controls;
        }

        ownsInstance = true;
        return new PlayerControls();
    }
}
