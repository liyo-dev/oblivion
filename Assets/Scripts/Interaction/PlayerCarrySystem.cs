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
    private vThirdPersonController _controller;
    private GameObject _carriedObject;
    private Rigidbody _carriedRigidbody;
    private PickupObject _carriedPickupObject;
    private bool _isCarrying;
    private bool _isPickingUp;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _controller = GetComponent<vThirdPersonController>();

        if (carryPoint == null)
        {
            var cp = new GameObject("CarryPoint").transform;
            cp.SetParent(transform);
            cp.localPosition = new Vector3(0, 1.2f, 0.5f);
            carryPoint = cp;
        }

        // MUY IMPORTANTE: que la capa UpperBody pese 1
        if (_animator != null && animatorLayer > 0)
            _animator.SetLayerWeight(animatorLayer, 1f);
    }

    public bool TryPickupOrDrop(GameObject obj)
    {
        if (_isCarrying) { DropObject(); return false; }
        PickupObject(obj);
        return true;
    }

    public void PickupObject(GameObject obj)
    {
        if (_isCarrying || _isPickingUp || obj == null) return;

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

        if (_animator != null)
        {
            _animator.CrossFade(locomotionStateName, transitionDuration, animatorLayer);
            // Bajar el peso de la capa UpperBody a 0
            if (animatorLayer > 0)
                _animator.SetLayerWeight(animatorLayer, 0f);
        }

        _carriedObject = null;
        _carriedRigidbody = null;
        _carriedPickupObject = null;
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
