using UnityEngine;
using Invector.vCharacterController;

[RequireComponent(typeof(Animator))]
public class PlayerCarrySystem : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private Transform carryPoint;

    [Header("Animaciones - Nombres de Estados")]
    [SerializeField] private string pickupStateName = "CarryStart_NoWeapon";
    [SerializeField] private string carryMoveStateName = "CarryMoveIdle_NoWeapon";
    [SerializeField] private string throwStateName = "CarryThrow_NoWeapon";
    [SerializeField] private string locomotionStateName = "Locomotion";

    [Header("Configuración de Animación")]
    [SerializeField] private int animatorLayer = 1;   // UpperBody
    [SerializeField] private float transitionDuration = 0.2f;
    [SerializeField] private float attachDelay = 0.5f;
    [SerializeField] private float throwAnimationDuration = 0.3f;
    [SerializeField] private float dropCooldown = 0.5f; // Cooldown después de soltar para evitar interacciones

    [Header("Interacción para soltar")]
    [SerializeField] private bool dropOnInteract = true;

    /// <summary>
    /// Evento disparado cuando el player suelta un objeto.
    /// Parámetro: GameObject que fue soltado.
    /// </summary>
    public System.Action<GameObject> OnObjectDropped;

    private Animator _animator;
    private PlayerActionManager _actionManager;
    private GameObject _carriedObject;
    private Rigidbody _carriedRigidbody;
    private PickupObject _carriedPickupObject;
    private Collider[] _carriedColliders;
    private bool _isCarrying;
    private bool _isPickingUp;
    private float _lastDropTime = -999f;
    
    /// <summary>
    /// Indica si el jugador acaba de soltar un objeto recientemente.
    /// Usado por otros sistemas para evitar interacciones inmediatas.
    /// </summary>
    public bool JustDroppedObject => (Time.time - _lastDropTime) < dropCooldown;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _actionManager = GetComponent<PlayerActionManager>();

        if (carryPoint == null)
        {
            var cp = new GameObject("CarryPoint").transform;
            cp.SetParent(transform);
            cp.localPosition = new Vector3(0, 1.2f, 0.5f);
            carryPoint = cp;
        }
    }

    public bool TryPickupOrDrop(GameObject obj)
    {
        // Verificar con el ActionManager si podemos interactuar
        if (_actionManager != null && !_actionManager.CanUse(PlayerAbility.Carry))
            return false;

        if (_isCarrying) { DropObject(); return false; }
        PickupObject(obj);
        return true;
    }

    public void PickupObject(GameObject obj)
    {
        if (_isCarrying || _isPickingUp || obj == null) return;

        // Verificar permiso con el ActionManager
        if (_actionManager != null && !_actionManager.CanUse(PlayerAbility.Carry))
            return;

        // Si te pasan un hijo, sube al raíz que tiene el PickupObject
        var pickup = obj.GetComponentInParent<PickupObject>();
        if (pickup != null) obj = pickup.gameObject;

        _carriedObject = obj;
        _carriedRigidbody = obj.GetComponent<Rigidbody>();
        _carriedPickupObject = obj.GetComponent<PickupObject>();

        _isPickingUp = true;

        if (_animator != null)
        {
            _animator.CrossFade(pickupStateName, transitionDuration, animatorLayer);
            // Subir el peso de la capa UpperBody a 1
            if (animatorLayer > 0)
                _animator.SetLayerWeight(animatorLayer, 1f);
        }

        Invoke(nameof(AttachObject), attachDelay);
    }

    private void AttachObject()
    {
        if (_carriedObject == null) return;

        _isPickingUp = false;
        _isCarrying = true;

        if (_carriedRigidbody != null)
        {
            _carriedRigidbody.isKinematic = true;
            _carriedRigidbody.useGravity = false;
        }
        
        // Desactivar colliders para evitar colisiones con el player
        _carriedColliders = _carriedObject.GetComponentsInChildren<Collider>();
        foreach (var col in _carriedColliders)
        {
            if (col != null)
                col.enabled = false;
        }

        _carriedObject.transform.SetParent(carryPoint, worldPositionStays:false);
        _carriedObject.transform.localPosition = Vector3.zero;
        _carriedObject.transform.localRotation = Quaternion.identity;

        if (_animator != null)
            _animator.CrossFade(carryMoveStateName, transitionDuration, animatorLayer);

        // Notificar al ActionManager que estamos en modo Carrying
        if (_actionManager != null)
            _actionManager.PushMode(ActionMode.Carrying);
    }

    public void DropObject()
    {
        if (!_isCarrying || _carriedObject == null) return;

        _isCarrying = false;

        if (_animator != null)
            _animator.CrossFade(throwStateName, transitionDuration, animatorLayer);

        Invoke(nameof(PhysicallyDropObject), throwAnimationDuration);
    }

    private void PhysicallyDropObject()
    {
        if (_carriedObject == null) return;

        _carriedPickupObject?.OnDropped();

        _carriedObject.transform.SetParent(null);
        
        // Reactivar colliders
        if (_carriedColliders != null)
        {
            foreach (var col in _carriedColliders)
            {
                if (col != null)
                    col.enabled = true;
            }
            _carriedColliders = null;
        }

        if (_carriedRigidbody != null)
        {
            _carriedRigidbody.isKinematic = false;
            _carriedRigidbody.useGravity = true;
            _carriedRigidbody.linearVelocity = transform.forward * 3f + Vector3.up * 1f;
        }

        // Bajar el peso de la capa UpperBody a 0 para volver a la animación base
        if (_animator != null && animatorLayer > 0)
        {
            _animator.SetLayerWeight(animatorLayer, 0f);
        }

        // Disparar evento antes de limpiar la referencia
        OnObjectDropped?.Invoke(_carriedObject);

        // Marcar el tiempo de drop para cooldown
        _lastDropTime = Time.time;

        _carriedObject = null;
        _carriedRigidbody = null;
        _carriedPickupObject = null;

        if (_actionManager != null)
            _actionManager.PopMode(ActionMode.Carrying);
        
        StartCoroutine(ClearInputBufferAfterDrop());
    }

    private System.Collections.IEnumerator ClearInputBufferAfterDrop()
    {
        // Bloquear brevemente las acciones para limpiar el buffer de inputs
        // y evitar que se abran diálogos automáticamente
        if (_actionManager != null)
        {
            _actionManager.PushMode(ActionMode.Stunned);
            yield return new WaitForSeconds(dropCooldown); // Cooldown configurable
            _actionManager.PopMode(ActionMode.Stunned);
        }
    }

    void LateUpdate()
    {
        if (_isCarrying && _carriedObject != null && carryPoint != null)
        {
            _carriedObject.transform.position = carryPoint.position;
            // Mantener el objeto recto: solo hereda la rotación Y del player (yaw),
            // sin inclinarse con la animación del carryPoint
            float playerYaw = transform.eulerAngles.y;
            _carriedObject.transform.rotation = Quaternion.Euler(0f, playerYaw, 0f);
        }
    }

    public bool IsCarrying => _isCarrying;
    public bool IsPickingUp => _isPickingUp;
    public GameObject CarriedObject => _carriedObject;

    void OnDrawGizmos()
    {
        if (carryPoint)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(carryPoint.position, 0.05f);
        }
    }
}
