using UnityEngine;
using UnityEngine.InputSystem;

/// Centraliza la activación/desactivación de acciones de juego en función del GameState.
/// Colócalo en una escena inicial/persistente. Configura las acciones a deshabilitar cuando
/// el juego no puede procesar input de gameplay (pausa, main menu, diálogos, etc.).
[DisallowMultipleComponent]
public class GameInputRouter : MonoBehaviour
{
    public static GameInputRouter Instance { get; private set; }

    [Header("Acciones a deshabilitar cuando Gameplay está bloqueado")]
    [SerializeField] private InputActionReference[] gameplayActions;

    [Header("Acciones UI que deben estar habilitadas cuando Gameplay está bloqueado (opcional)")]
    [SerializeField] private InputActionReference[] uiActions;

    [Tooltip("Si está activo, aplica cambios también en OnUpdate por seguridad")] 
    [SerializeField] private bool applyEveryFrame = false;

    bool _lastGameplayEnabled;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        GameState.OnChanged += Apply;
        Apply();
    }
    void OnDisable()
    {
        GameState.OnChanged -= Apply;
    }

    void Update()
    {
        if (applyEveryFrame)
        {
            bool now = GameState.CanProcessGameplayInput;
            if (now != _lastGameplayEnabled) Apply();
        }
    }

    public void Apply()
    {
        bool gameplayEnabled = GameState.CanProcessGameplayInput;
        _lastGameplayEnabled = gameplayEnabled;

        // Deshabilitar/rehabilitar acciones de gameplay
        if (gameplayActions != null)
        {
            foreach (var aref in gameplayActions)
            {
                var a = aref ? aref.action : null;
                if (a == null) continue;
                if (gameplayEnabled) { if (!a.enabled) a.Enable(); }
                else { if (a.enabled) a.Disable(); }
            }
        }

        // Asegurar acciones de UI habilitadas cuando gameplay está bloqueado (opcional)
        if (uiActions != null)
        {
            foreach (var aref in uiActions)
            {
                var a = aref ? aref.action : null;
                if (a == null) continue;
                if (gameplayEnabled)
                {
                    // No forzamos estado; dejamos que la UI lo gestione. Solo evitar que queden bloqueadas.
                    if (!a.enabled) a.Enable();
                }
                else
                {
                    if (!a.enabled) a.Enable();
                }
            }
        }
    }
}

