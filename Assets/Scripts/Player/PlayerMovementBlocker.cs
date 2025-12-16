using Invector.vCharacterController;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Permite congelar y restaurar el movimiento del jugador desde UnityEvents o cualquier otro script.
/// Desactiva los componentes clave del jugador y deja el rigidbody/animator en un estado neutro.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerMovementBlocker : MonoBehaviour
{
    [Header("Referencia opcional al player")]
    [SerializeField] private GameObject playerRoot;

    [Header("Opciones")]
    [Tooltip("Desactiva el CharacterController mientras el movimiento esté bloqueado.")]
    [SerializeField] private bool disableCharacterController = true;
    [Tooltip("Desactiva el controlador de input (vThirdPersonInput) para evitar lecturas residuales.")]
    [SerializeField] private bool disableThirdPersonInput = true;
    [Tooltip("Desactiva la gravedad en el Rigidbody mientras el jugador está bloqueado.")]
    [SerializeField] private bool suspendGravity = false;

    [Header("Eventos")]
    public UnityEvent onMovementBlocked;
    public UnityEvent onMovementUnblocked;

    vThirdPersonInput _thirdPersonInput;
    vThirdPersonController _thirdPersonController;
    CharacterController _characterController;
    Rigidbody _rigidbody;
    Animator _animator;
    bool _isBlocked;

    void Awake()
    {
        if (playerRoot == null)
            PlayerService.TryGetPlayer(out playerRoot);

        CacheDependencies();
    }

    void CacheDependencies()
    {
        if (playerRoot == null)
            return;

        _thirdPersonInput = playerRoot.GetComponent<vThirdPersonInput>() ?? playerRoot.GetComponentInChildren<vThirdPersonInput>(true);
        _thirdPersonController = playerRoot.GetComponent<vThirdPersonController>() ?? playerRoot.GetComponentInChildren<vThirdPersonController>(true);
        _characterController = playerRoot.GetComponent<CharacterController>() ?? playerRoot.GetComponentInChildren<CharacterController>(true);
        _rigidbody = playerRoot.GetComponent<Rigidbody>() ?? playerRoot.GetComponentInChildren<Rigidbody>(true);
        _animator = playerRoot.GetComponentInChildren<Animator>(true);
    }

    /// <summary>
    /// Activa o desactiva el bloqueo. Pensado para ser llamado desde UnityEvents.
    /// </summary>
    public void SetMovementBlocked(bool blocked)
    {
        if (blocked)
            BlockMovement();
        else
            RestoreMovement();
    }

    /// <summary>
    /// Bloquea el movimiento del jugador hasta que se llame a <see cref="RestoreMovement"/>.
    /// </summary>
    [ContextMenu("Block Movement")]
    public void BlockMovement()
    {
        if (_isBlocked)
            return;

        CacheDependencies();

        if (playerRoot == null)
        {
            Debug.LogWarning("[PlayerMovementBlocker] No se encontró el Player para bloquear el movimiento.");
            return;
        }

        _isBlocked = true;

        if (_animator != null)
        {
            _animator.SetFloat(vAnimatorParameters.InputMagnitude, 0f);
            _animator.SetFloat(vAnimatorParameters.InputHorizontal, 0f);
            _animator.SetFloat(vAnimatorParameters.InputVertical, 0f);
            _animator.SetBool(vAnimatorParameters.IsSprinting, false);
        }

        if (disableThirdPersonInput && _thirdPersonInput != null)
            _thirdPersonInput.enabled = false;

        // No tocar linearVelocity si el Rigidbody es kinematic
        if (_rigidbody != null && !_rigidbody.isKinematic)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        if (suspendGravity && _rigidbody != null)
            _rigidbody.useGravity = false;

        if (disableCharacterController && _characterController != null)
            _characterController.enabled = false;

        onMovementBlocked?.Invoke();
    }

    /// <summary>
    /// Restaura el movimiento del jugador y re-activa los componentes.
    /// </summary>
    [ContextMenu("Restore Movement")]
    public void RestoreMovement()
    {
        if (!_isBlocked)
            return;

        CacheDependencies();

        if (playerRoot == null)
            return;

        _isBlocked = false;

        if (disableCharacterController && _characterController != null && !_characterController.enabled)
        {
            _characterController.enabled = true;
            _characterController.SimpleMove(Vector3.zero);
        }

        if (suspendGravity && _rigidbody != null)
            _rigidbody.useGravity = true;

        // No tocar linearVelocity si el Rigidbody es kinematic
        if (_rigidbody != null && !_rigidbody.isKinematic)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        if (disableThirdPersonInput && _thirdPersonInput != null)
        {
            // Reiniciar el estado interno del input forzando el ciclo enable/disable.
            _thirdPersonInput.enabled = false;
            _thirdPersonInput.enabled = true;

            // Reiniciar InputActionAsset si existe.
            var inputActionsField = typeof(vThirdPersonInput).GetField("inputActions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (inputActionsField?.GetValue(_thirdPersonInput) is InputActionAsset actions)
            {
                actions.Disable();
                actions.Enable();
            }
        }

        onMovementUnblocked?.Invoke();
    }
}
