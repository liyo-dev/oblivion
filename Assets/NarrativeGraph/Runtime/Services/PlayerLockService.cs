using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Centraliza el bloqueo de movimiento del jugador con referencia por solicitante.
/// Deshabilita acciones de gameplay, CharacterController, Rigidbody y script de locomoción.
/// Usa el sistema centralizado de PlayerInputManager para gestionar UI/Gameplay.
/// </summary>
[DefaultExecutionOrder(-275)]
public class PlayerLockService : MonoBehaviour
{
    static PlayerLockService _instance;
    public static bool HasInstance => _instance != null;
    public static PlayerLockService Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("PlayerLockService");
                _instance = go.AddComponent<PlayerLockService>();
                DontDestroyOnLoad(go);
                ServiceLocator.Register(_instance);
            }
            return _instance;
        }
    }

    readonly HashSet<object> _owners = new HashSet<object>();

    CharacterController _charController;
    bool _charControllerWasEnabled;
    Rigidbody _rb;
    MonoBehaviour _movementScript;
    bool _movementScriptWasEnabled;
    Invector.vCharacterController.vThirdPersonMotor _lockedMotor;
    bool _motorWasLocked;
    Invector.vCharacterController.vThirdPersonInput _suppressedInput;

    public bool IsLocked => _owners.Count > 0;

    /// <summary>
    /// Método de emergencia para forzar el desbloqueo del player.
    /// Solo usar en debug si el player queda bloqueado por un bug.
    /// </summary>
    public void ForceUnlock()
    {
        if (_owners.Count == 0)
        {
            Debug.LogWarning("[PlayerLockService] ⚠️ ForceUnlock() llamado pero no hay locks activos");
            return;
        }

        Debug.LogWarning($"[PlayerLockService] 🚨 FORCE UNLOCK - Limpiando {_owners.Count} locks forzadamente");
        _owners.Clear();
        _lockedMotor = null; // evitar restaurar lockMovement al estado bloqueado
        ReleaseHardLock();
    }

    public void Acquire(object owner)
    {
        if (owner == null) owner = this;
        if (_owners.Contains(owner))
        {
            Debug.LogWarning($"[PlayerLockService] ⚠️ Owner ya tenía un lock: {owner?.GetType().Name ?? "null"}");
            return;
        }
        
        _owners.Add(owner);
        Debug.Log($"[PlayerLockService] 🔒 Acquire de {owner?.GetType().Name ?? "null"}. Total locks: {_owners.Count}");

        if (_owners.Count == 1)
        {
            Debug.Log("[PlayerLockService] 🚫 Primer lock - Deshabilitando movimiento del jugador");
            ApplyHardLock();
        }
    }

    public void Release(object owner)
    {
        if (owner == null) owner = this;
        
        if (!_owners.Contains(owner))
        {
            Debug.LogWarning($"[PlayerLockService] ⚠️ Intento de Release de owner no registrado: {owner?.GetType().Name ?? "null"}");
            return;
        }
        
        _owners.Remove(owner);
        Debug.Log($"[PlayerLockService] 🔓 Release de {owner?.GetType().Name ?? "null"}. Locks restantes: {_owners.Count}");

        if (_owners.Count == 0)
        {
            Debug.Log("[PlayerLockService] ✅ Todos los locks liberados - Reactivando movimiento del jugador");
            ReleaseHardLock();
        }
    }

    void ApplyHardLock()
    {
        if (!PlayerService.TryGetPlayer(out var player, true) || player == null)
            return;

        // Cambiar a modo UI usando el sistema centralizado
        if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
            pim.PushUIMode();

        _charController = player.GetComponent<CharacterController>();
        if (_charController != null)
        {
            _charControllerWasEnabled = _charController.enabled;
            _charController.enabled = false;
        }

        _rb = player.GetComponent<Rigidbody>();
        if (_rb != null)
        {
            // Solo modificar velocidad si NO es kinematic
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            
            // NO PONER EN KINEMATIC - dejar que los scripts de Invector se deshabiliten
            // _rb.isKinematic = true; // ❌ ESTO CAUSA LOS WARNINGS
        }

        // Bloquear movimiento sin deshabilitar el controller completo.
        // Deshabilitar vThirdPersonController para UpdateMotor() que gestiona
        // CheckGround() y ControlMaterialPhysics(). Sin esto el CapsuleCollider
        // queda con slippyPhysics (fricción 0) y el player cae a través del suelo.
        _lockedMotor = player.GetComponent<Invector.vCharacterController.vThirdPersonMotor>();
        if (_lockedMotor != null)
        {
            _motorWasLocked = _lockedMotor.lockMovement;
            _lockedMotor.lockMovement = true;
            Debug.Log("[PlayerLockService] lockMovement=true en vThirdPersonMotor");
        }
        else
        {
            // Fallback: deshabilitar el script si no se encuentra vThirdPersonMotor
            _movementScript = player.GetComponents<MonoBehaviour>()
                .FirstOrDefault(m => m != null && m.enabled && m != this && !(m is PlayerActionManager) && (
                    m.GetType().Name == "vThirdPersonController" ||
                    m.GetType().Name == "vThirdPersonInput" ||
                    m.GetType().Name == "ThirdPersonController" ||
                    m.GetType().Name == "ThirdPersonInput"
                ));
            if (_movementScript != null)
            {
                _movementScriptWasEnabled = _movementScript.enabled;
                _movementScript.enabled = false;
                Debug.Log($"[PlayerLockService] Fallback: script '{_movementScript.GetType().Name}' DESHABILITADO");
            }
            else
            {
                Debug.LogWarning("[PlayerLockService] No se encontró vThirdPersonMotor ni script de movimiento");
            }
        }

        // Pone cc.input a cero de inmediato y bloquea jump/sprint en vThirdPersonInput.
        // Llamar MoveInput() explícitamente para zerear cc.input en este frame sin esperar al
        // próximo Update() — evita que un FixedUpdate intermedio aplique movimiento residual.
        _suppressedInput = player.GetComponent<Invector.vCharacterController.vThirdPersonInput>();
        if (_suppressedInput != null)
        {
            _suppressedInput.SuppressMoveInput = true;
            _suppressedInput.MoveInput();
        }
    }

    void ReleaseHardLock()
    {
        // Restaurar modo Gameplay usando el sistema centralizado
        if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
            pim.PopUIMode();

        if (_charController != null)
        {
            _charController.enabled = _charControllerWasEnabled;
        }
        _charController = null;

        // NO restaurar isKinematic ya que nunca lo cambiamos
        // if (_rb != null)
        // {
        //     _rb.isKinematic = _rbWasKinematic;
        // }
        _rb = null;

        if (_lockedMotor != null)
        {
            _lockedMotor.lockMovement = _motorWasLocked;
            Debug.Log("[PlayerLockService] lockMovement restaurado en vThirdPersonMotor");
            _lockedMotor = null;
        }
        else if (_movementScript != null)
        {
            _movementScript.enabled = _movementScriptWasEnabled;
            Debug.Log($"[PlayerLockService] Script de movimiento '{_movementScript.GetType().Name}' RESTAURADO");
        }
        _movementScript = null;

        // Restaurar SuppressMoveInput y añadir gracia para evitar salto al cerrar UI
        if (_suppressedInput != null)
        {
            _suppressedInput.SuppressMoveInput = false;
            _suppressedInput = null;
        }
        Core.GamepadInputReader.IgnoreJumpButton(0.3f);
    }


    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        // Suscribirse a cambios de escena para auto-limpieza
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    /// <summary>
    /// Al cargar una nueva escena, limpiar locks huérfanos (de objetos destruidos).
    /// Esto previene que el player quede bloqueado en escenas de testeo.
    /// </summary>
    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Solo limpiar en carga normal (no aditiva)
        if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single)
        {
            Debug.Log($"[PlayerLockService] 🔍 Escena cargada '{scene.name}' - Verificando locks...");
            
            // Verificar si hay owners destruidos/huérfanos
            var deadOwners = new List<object>();
            foreach (var owner in _owners)
            {
                // Si el owner es un MonoBehaviour/GameObject destruido, marcarlo
                if (owner is UnityEngine.Object unityObj && unityObj == null)
                {
                    deadOwners.Add(owner);
                }
            }
            
            if (deadOwners.Count > 0)
            {
                Debug.LogWarning($"[PlayerLockService] 🧹 Limpiando {deadOwners.Count} locks huérfanos al cargar escena '{scene.name}'");
                foreach (var dead in deadOwners)
                {
                    _owners.Remove(dead);
                }
            }
            
            // NUEVO: En modo testeo o cuando no hay cinemáticas aditivas, limpiar todos los locks
            // Esto previene que el player quede bloqueado cuando se skipean cinemáticas en grafos narrativos
            bool isTestingMode = GameBootService.IsAvailable && 
                                 GameBootService.Profile != null && 
                                 GameBootService.Profile.ShouldBootFromPreset();
            
            if (isTestingMode && _owners.Count > 0)
            {
                Debug.LogWarning($"[PlayerLockService] 🧪 Modo testeo detectado - Limpiando {_owners.Count} locks al cargar escena '{scene.name}'");
                _owners.Clear();
            }
            
            // Si ya no quedan locks, liberar el player
            if (_owners.Count == 0)
            {
                Debug.Log("[PlayerLockService] ✅ Todos los locks limpiados - Reactivando movimiento del jugador");
                ReleaseHardLock();
            }
            else if (_owners.Count > 0)
            {
                Debug.Log($"[PlayerLockService] ⚠️ {_owners.Count} locks aún activos tras limpieza");
            }
        }
    }
}
