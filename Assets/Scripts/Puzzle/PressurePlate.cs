using UnityEngine;
using UnityEngine.Events;
using Game.NPC;

[RequireComponent(typeof(Collider))]
public class PressurePlate : MonoBehaviour
{
    [Header("Detección")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string[] validObjectTags = { "Throwable" };
    [SerializeField] private bool playerActivates = true;
    [SerializeField] private bool lockWhenActivated = false;

    [Header("Objeto en placa")]
    [Tooltip("Centra el objeto en la placa y lo ancla cuando cae encima")]
    [SerializeField] private bool snapObjectToPlate = true;
    [SerializeField] private float snapHeightOffset = 0.05f;

    [Header("Visual")]
    [SerializeField] private Transform plateVisual;
    [SerializeField] private float sinkAmount = 0.1f;
    [SerializeField] private float animationSpeed = 8f;

    [Header("VFX (opcional)")]
    [SerializeField] private GameObject activateVfxPrefab;
    [SerializeField] private GameObject deactivateVfxPrefab;

    [Header("Audio")]
    [SerializeField] private string activateSfxKey = "PressurePlate_Activate";
    [SerializeField] private string deactivateSfxKey = "PressurePlate_Deactivate";

    [Header("Eventos")]
    public UnityEvent onActivated;
    public UnityEvent onDeactivated;

    [Header("Estado (solo lectura)")]
    [SerializeField] private bool isActivated;

    public bool IsActivated => isActivated;
    public bool isPressed => isActivated;

    private Vector3 _originalPlateLocalPos;
    private Vector3 _targetPlateLocalPos;
    private bool _isAnimating;

    private bool _playerOnPlate;
    private int _partyMembersOnPlate;
    private Rigidbody _snappedRb;
    private bool _snappedWasKinematic;

    private Collider _trigger;
    private Coroutine _deactivationCoroutine;
    private const float DEACTIVATION_DELAY = 0.1f;

    private void Awake()
    {
        foreach (var col in GetComponents<Collider>())
        {
            if (col.isTrigger) { _trigger = col; break; }
        }

        if (_trigger == null)
        {
            var col = GetComponents<Collider>();
            if (col.Length > 0)
            {
                col[0].isTrigger = true;
                _trigger = col[0];
                Debug.LogWarning($"[PressurePlate] {name}: ningún collider era trigger, se configuró automáticamente.");
            }
        }
    }

    private void Start()
    {
        if (plateVisual != null)
        {
            _originalPlateLocalPos = plateVisual.localPosition;
            _targetPlateLocalPos = _originalPlateLocalPos;
        }
    }

    private void Update()
    {
        if (plateVisual == null || !_isAnimating) return;

        plateVisual.localPosition = Vector3.Lerp(
            plateVisual.localPosition, _targetPlateLocalPos,
            Time.deltaTime * animationSpeed
        );

        if (Vector3.Distance(plateVisual.localPosition, _targetPlateLocalPos) < 0.001f)
        {
            plateVisual.localPosition = _targetPlateLocalPos;
            _isAnimating = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (lockWhenActivated && isActivated) return;

        if (other.CompareTag(playerTag))
        {
            _playerOnPlate = true;
            if (playerActivates && !isActivated)
                Activate();
            return;
        }

        if (other.GetComponentInParent<NPCPartyMember>() != null)
        {
            _partyMembersOnPlate++;
            if (!isActivated)
                Activate();
            return;
        }

        if (!IsValidObjectTag(other.tag)) return;
        if (_snappedRb != null) return;

        var rb = other.attachedRigidbody != null ? other.attachedRigidbody : other.GetComponent<Rigidbody>();
        if (rb == null) return;

        AnclarObjeto(rb, other);

        if (!isActivated)
            Activate();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            _playerOnPlate = false;
            TryDeactivate();
            return;
        }

        if (other.GetComponentInParent<NPCPartyMember>() != null)
        {
            _partyMembersOnPlate = Mathf.Max(0, _partyMembersOnPlate - 1);
            TryDeactivate();
            return;
        }

        if (_snappedRb == null) return;

        var rb = other.attachedRigidbody != null ? other.attachedRigidbody : other.GetComponent<Rigidbody>();
        if (rb != _snappedRb) return;

        SoltarObjeto();
        TryDeactivate();
    }

    private bool IsValidObjectTag(string tag)
    {
        foreach (var t in validObjectTags)
            if (tag == t) return true;
        return false;
    }

    private void AnclarObjeto(Rigidbody rb, Collider col)
    {
        _snappedRb = rb;
        _snappedWasKinematic = rb.isKinematic;

        if (snapObjectToPlate && _trigger != null)
        {
            float plateTop = _trigger.bounds.max.y;
            float objHalfHeight = col.bounds.extents.y;
            Vector3 snapPos = new Vector3(
                transform.position.x,
                plateTop + objHalfHeight + snapHeightOffset,
                transform.position.z
            );
            rb.transform.position = snapPos;
        }

        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void SoltarObjeto()
    {
        if (_snappedRb == null) return;
        _snappedRb.isKinematic = _snappedWasKinematic;
        if (!_snappedWasKinematic) _snappedRb.WakeUp();
        _snappedRb = null;
    }

    private void Activate()
    {
        if (isActivated) return;

        if (_deactivationCoroutine != null)
        {
            StopCoroutine(_deactivationCoroutine);
            _deactivationCoroutine = null;
        }

        isActivated = true;

        if (plateVisual != null)
        {
            _targetPlateLocalPos = _originalPlateLocalPos + Vector3.down * sinkAmount;
            _isAnimating = true;
        }

        AudioService.Instance?.PlaySFX(activateSfxKey, worldPosition: transform.position);
        if (activateVfxPrefab != null)
            Instantiate(activateVfxPrefab, transform.position, Quaternion.identity);
        onActivated.Invoke();
        SendMessageUpwards("OnPlateStateChanged", this, SendMessageOptions.DontRequireReceiver);
    }

    private void TryDeactivate()
    {
        if (!isActivated || lockWhenActivated) return;
        if (_snappedRb != null) return;
        if (_playerOnPlate && playerActivates) return;
        if (_partyMembersOnPlate > 0) return;

        if (_deactivationCoroutine != null) StopCoroutine(_deactivationCoroutine);
        _deactivationCoroutine = StartCoroutine(Co_DelayedDeactivate());
    }

    private System.Collections.IEnumerator Co_DelayedDeactivate()
    {
        yield return new WaitForSeconds(DEACTIVATION_DELAY);

        if (_snappedRb != null || (_playerOnPlate && playerActivates) || _partyMembersOnPlate > 0 || lockWhenActivated)
        {
            _deactivationCoroutine = null;
            yield break;
        }

        isActivated = false;
        _deactivationCoroutine = null;

        if (plateVisual != null)
        {
            _targetPlateLocalPos = _originalPlateLocalPos;
            _isAnimating = true;
        }

        AudioService.Instance?.PlaySFX(deactivateSfxKey, worldPosition: transform.position);
        if (deactivateVfxPrefab != null)
            Instantiate(deactivateVfxPrefab, transform.position, Quaternion.identity);
        onDeactivated.Invoke();
        SendMessageUpwards("OnPlateStateChanged", this, SendMessageOptions.DontRequireReceiver);
    }

    public void ForceActivate() { if (!isActivated) Activate(); }
    public void ForceDeactivate() { if (isActivated && !lockWhenActivated) TryDeactivate(); }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isActivated ? Color.green : Color.yellow;
        var col = GetComponent<Collider>();
        if (col == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        if (col is BoxCollider box)
            Gizmos.DrawWireCube(box.center, box.size);
        else if (col is SphereCollider sphere)
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
    }
#endif
}
