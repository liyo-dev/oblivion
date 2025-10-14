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

    [Header("Interacción para soltar")]
    [SerializeField] private bool dropOnInteract = true;

    private Animator _animator;
    private PlayerActionManager _actionManager;
    private GameObject _carriedObject;
    private Rigidbody _carriedRigidbody;
    private PickupObject _carriedPickupObject;
    private bool _isCarrying;
    private bool _isPickingUp;

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
        if (_actionManager != null)
        {
            _actionManager.PushMode(ActionMode.Stunned);
            yield return new WaitForSeconds(0.15f); // Pequeño delay para limpiar inputs
            _actionManager.PopMode(ActionMode.Stunned);
        }
    }

    public bool IsCarrying => _isCarrying;
    public bool IsPickingUp => _isPickingUp;
    public GameObject CarriedObject => _carriedObject;

    void Update()
    {
        if (dropOnInteract && _isCarrying && Input.GetKeyDown(KeyCode.A))
            DropObject();
    }

    void OnDrawGizmos()
    {
        if (carryPoint)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(carryPoint.position, 0.05f);
        }
    }
}
