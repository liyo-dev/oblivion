using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Core;

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
    bool _rbWasKinematic;
    MonoBehaviour _movementScript;
    bool _movementScriptWasEnabled;

    public bool IsLocked => _owners.Count > 0;

    public void Acquire(object owner)
    {
        if (owner == null) owner = this;
        if (_owners.Contains(owner)) return;
        _owners.Add(owner);

        if (_owners.Count == 1)
        {
            ApplyHardLock();
        }
    }

    public void Release(object owner)
    {
        if (owner == null) owner = this;
        if (!_owners.Contains(owner)) return;
        _owners.Remove(owner);

        if (_owners.Count == 0)
        {
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
            _rbWasKinematic = _rb.isKinematic;
            
            // Solo modificar velocidad si NO es kinematic
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            
            _rb.isKinematic = true;
        }

        // Buscar específicamente los scripts de movimiento del jugador
        _movementScript = player.GetComponents<MonoBehaviour>()
            .FirstOrDefault(m => m != null && m.enabled && m != this && !(m is PlayerActionManager) && (
                m.GetType().Name == "ThirdPersonController" ||
                m.GetType().Name == "ThirdPersonInput"
            ));
        if (_movementScript != null)
        {
            _movementScriptWasEnabled = _movementScript.enabled;
            _movementScript.enabled = false;
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

        if (_rb != null)
        {
            _rb.isKinematic = _rbWasKinematic;
        }
        _rb = null;

        if (_movementScript != null)
        {
            _movementScript.enabled = _movementScriptWasEnabled;
        }
        _movementScript = null;
    }


    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        ServiceLocator.Register(this);
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            ServiceLocator.Unregister(this);
            _instance = null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap() => _ = Instance;
}
