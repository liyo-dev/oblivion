using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Centraliza el bloqueo de movimiento del jugador con referencia por solicitante.
/// Deshabilita acciones de gameplay (conservando UI), CharacterController, Rigidbody y script de locomoción.
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

    PlayerInput _playerInput;
    bool _playerInputWasEnabled;
    CharacterController _charController;
    bool _charControllerWasEnabled;
    Rigidbody _rb;
    bool _rbWasKinematic;
    MonoBehaviour _movementScript;
    bool _movementScriptWasEnabled;
    PlayerControls _controls;
    bool _gameplayMapWasEnabled = true;
    bool _uiMapWasEnabled;
    string _previousActionMap;

    [Header("Action Maps")]
    [SerializeField] private string gameplayActionMapName = "GamePlay";
    [SerializeField] private string uiActionMapName = "UI";

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

        ResolveInput(player);

        if (_playerInput != null)
        {
            _playerInputWasEnabled = _playerInput.enabled;
        }

        if (_controls != null)
        {
            _gameplayMapWasEnabled = _controls.GamePlay.enabled;
            _uiMapWasEnabled = _controls.UI.enabled;
            try { _controls.GamePlay.Disable(); } catch { }
            try { _controls.UI.Enable(); } catch { }
        }

        if (_playerInput != null && _playerInput.actions != null)
        {
            _previousActionMap = _playerInput.currentActionMap != null ? _playerInput.currentActionMap.name : null;
            var uiMap = _playerInput.actions.FindActionMap(uiActionMapName, throwIfNotFound: false);
            if (uiMap != null)
            {
                _playerInput.SwitchCurrentActionMap(uiMap.name);
            }
        }

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
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
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
        if (_playerInput != null)
        {
            _playerInput.enabled = _playerInputWasEnabled;
            if (_playerInput.actions != null)
            {
                var gameplayMap = _playerInput.actions.FindActionMap(gameplayActionMapName, throwIfNotFound: false);
                if (!string.IsNullOrEmpty(_previousActionMap))
                {
                    var prev = _playerInput.actions.FindActionMap(_previousActionMap, throwIfNotFound: false);
                    if (prev != null)
                        _playerInput.SwitchCurrentActionMap(prev.name);
                }
                else if (gameplayMap != null)
                {
                    _playerInput.SwitchCurrentActionMap(gameplayMap.name);
                }
            }
        }
        _playerInput = null;
        _previousActionMap = null;

        if (_controls != null)
        {
            try
            {
                if (_gameplayMapWasEnabled) _controls.GamePlay.Enable(); else _controls.GamePlay.Disable();
                if (!_uiMapWasEnabled) _controls.UI.Disable();
            }
            catch { }
        }
        _controls = null;

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

    void ResolveInput(GameObject player)
    {
        if (player == null) return;

        if (_playerInput == null)
            _playerInput = player.GetComponent<PlayerInput>();

        if (ServiceLocator.TryGet(out PlayerInputManager pim))
        {
            if (_playerInput == null)
                _playerInput = pim.PlayerInput;
            _controls = pim.Controls;
        }

        if (_controls == null)
        {
            _controls = GamepadInputReader.ControlsOrNull;
        }
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
