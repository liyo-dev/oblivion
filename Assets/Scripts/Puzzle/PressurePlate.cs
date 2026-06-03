using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Sendero.Core.Feedback;

/// <summary>
/// Interruptor de presión que se activa al colocar objetos con Rigidbody encima.
/// Lanza eventos onActivated/onDeactivated que los GOs oyentes gestionan.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PressurePlate : MonoBehaviour
{
    [Header("Configuración del Interruptor")]
    [Tooltip("Si es true, solo acepta objetos con el componente PickupObject")]
    [SerializeField] private bool onlyPickupObjects = true;
    
    [Tooltip("Si es true, el interruptor permanece activo una vez presionado")]
    [SerializeField] private bool lockWhenActivated;
    
    [Tooltip("Masa mínima necesaria para activar el interruptor (para filtrar objetos muy ligeros)")]
    [SerializeField] private float minimumMass = 0.1f;
    
    [Tooltip("Si es true, congela el objeto que activa la placa para que no se mueva ni se caiga")]
    [SerializeField] private bool freezeObjectOnPlate = true;
    
    [Tooltip("Si es true, hace al objeto hijo de la placa para que se mueva con ella")]
    [SerializeField] private bool parentObjectToPlate = false;
    
    [Header("Detección del Jugador")]
    [Tooltip("Si es true, detecta cuando el jugador entra y da feedback (sin activar)")]
    [SerializeField] private bool detectPlayer = true;
    
    [Tooltip("Si es true, el jugador puede activar el interruptor (además del feedback). Si es false, solo da feedback.")]
    [SerializeField] private bool playerCanActivate = true;
    
    [Tooltip("Tag del jugador para detectarlo")]
    [SerializeField] private string playerTag = "Player";
    
    [Tooltip("SFX al detectar jugador sin objeto correcto")]
    [SerializeField] private string playerStepSfxKey = "PressurePlate_PlayerStep";
    
    [Tooltip("Cuánto se hunde la placa cuando el jugador la pisa (menos que con objeto)")]
    [SerializeField] private float playerSinkAmount = 0.05f;
    
    [Header("Feedback Visual")]
    [Tooltip("Cuánto se hunde la placa cuando se activa (en unidades locales Y)")]
    [SerializeField] private float sinkAmount = 0.2f;
    
    [Tooltip("Velocidad de animación de hundimiento/elevación")]
    [SerializeField] private float animationSpeed = 5f;
    
    [Tooltip("GameObject que contiene el mesh de la placa (se hundirá)")]
    [SerializeField] private Transform plateVisual;
    
    [Header("Feedback de Cámara y Audio")]
    [Tooltip("Intensidad del shake de cámara al activar")]
    [SerializeField] private float cameraShakeIntensity = 0.3f;
    
    [Tooltip("Duración del shake de cámara")]
    [SerializeField] private float cameraShakeDuration = 0.2f;
    
    [Tooltip("Clave de SFX al activar el interruptor")]
    [SerializeField] private string activateSfxKey = "PressurePlate_Activate";
    
    [Tooltip("Clave de SFX al desactivar el interruptor")]
    [SerializeField] private string deactivateSfxKey = "PressurePlate_Deactivate";
    
    [Header("Eventos")]
    [Tooltip("Se invoca cuando la placa se activa. Suscribir los GOs que reaccionen (puerta, plataforma, antorcha…)")]
    public UnityEvent onActivated;

    [Tooltip("Se invoca cuando la placa se desactiva. Suscribir los GOs que reviertan su acción.")]
    public UnityEvent onDeactivated;

    [Header("VFX")]
    [Tooltip("Partículas al activar la placa (sustituye animación de pulsar)")]
    [SerializeField] private ParticleSystem activateVfx;

    [Tooltip("Partículas al desactivar la placa (opcional)")]
    [SerializeField] private ParticleSystem deactivateVfx;

    [Header("Estado")]
    [SerializeField] private bool isActivated;

    private Vector3 _originalPlatePosition;
    private Vector3 _targetPlatePosition;
    private HashSet<Rigidbody> _objectsOnPlate = new HashSet<Rigidbody>();
    private Dictionary<Rigidbody, RigidbodyState> _originalRigidbodyStates = new Dictionary<Rigidbody, RigidbodyState>();
    private bool _isAnimating;
    private bool _playerOnPlate;
    private Collider _collider;
    private PlayerCarrySystem _playerCarrySystem;
    private readonly List<Rigidbody> _toRemove = new List<Rigidbody>();

    // Cooldown para evitar spam de camera shake y activaciones
    private float _lastCameraShakeTime = -999f;
    private float _lastActivationTime = -999f;
    private const float CAMERA_SHAKE_COOLDOWN = 0.5f;
    private const float DEACTIVATION_DELAY = 0.15f;
    private Coroutine _deactivationCoroutine;

    // Estructura para guardar el estado original del Rigidbody
    private struct RigidbodyState
    {
        public bool isKinematic;
        public RigidbodyConstraints constraints;
        public Transform originalParent;
    }

    // Propiedad pública para consultar estado (compatibilidad con PressurePuzzleController)
    public bool IsActivated => isActivated;
    public bool isPressed => isActivated; // Alias para compatibilidad con código legacy

    private void Start()
    {
        if (plateVisual != null)
        {
            _originalPlatePosition = plateVisual.localPosition;
            _targetPlatePosition = _originalPlatePosition;
        }

        _collider = GetComponent<Collider>();
        if (_collider != null && !_collider.isTrigger)
        {
            Debug.LogWarning($"[PressurePlate] El collider en {name} debería ser trigger. Configurándolo automáticamente.");
            _collider.isTrigger = true;
        }
    }

    private void Update()
    {
        // Animar la placa visual
        if (plateVisual != null && _isAnimating)
        {
            plateVisual.localPosition = Vector3.Lerp(
                plateVisual.localPosition,
                _targetPlatePosition,
                Time.deltaTime * animationSpeed
            );
            
            // Detener animación cuando está cerca del objetivo
            if (Vector3.Distance(plateVisual.localPosition, _targetPlatePosition) < 0.001f)
            {
                plateVisual.localPosition = _targetPlatePosition;
                _isAnimating = false;
            }
        }
        
        if (_objectsOnPlate.Count > 0)
        {
            float maxDistance = _collider != null ? _collider.bounds.extents.magnitude * 2f : 2f;

            _toRemove.Clear();
            foreach (var rb in _objectsOnPlate)
            {
                if (rb == null || Vector3.Distance(rb.position, transform.position) > maxDistance)
                    _toRemove.Add(rb);
            }

            foreach (var rb in _toRemove)
            {
                if (_objectsOnPlate.Remove(rb))
                    _originalRigidbodyStates.Remove(rb);
            }

            if (_objectsOnPlate.Count == 0 && isActivated && !lockWhenActivated)
            {
                if (!_playerOnPlate || !playerCanActivate)
                    Deactivate();
            }
        }
    }

    private PlayerCarrySystem GetCarrySystem()
    {
        if (_playerCarrySystem == null)
        {
            var player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
                _playerCarrySystem = player.GetComponent<PlayerCarrySystem>();
        }
        return _playerCarrySystem;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (lockWhenActivated && isActivated) return;

        if (detectPlayer && other.CompareTag(playerTag))
        {
            OnPlayerEnter();
            if (playerCanActivate && !isActivated)
                Activate();
            return;
        }

        var rb = other.attachedRigidbody;
        if (rb == null) return;

        // Ignorar objetos que el jugador está transportando actualmente
        var carry = GetCarrySystem();
        if (carry != null && carry.CarriedObject == rb.gameObject) return;

        if (onlyPickupObjects)
        {
            var pickup = rb.GetComponent<PickupObject>();
            if (pickup == null) return;
        }

        if (rb.mass < minimumMass) return;
        
        if (_objectsOnPlate.Contains(rb)) return;

        _objectsOnPlate.Add(rb);

        if (!_originalRigidbodyStates.ContainsKey(rb))
        {
            _originalRigidbodyStates[rb] = new RigidbodyState
            {
                isKinematic = rb.isKinematic,
                constraints = rb.constraints,
                originalParent = rb.transform.parent
            };
        }

        if (freezeObjectOnPlate)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (parentObjectToPlate && plateVisual != null)
            rb.transform.SetParent(plateVisual);

        if (!isActivated)
            Activate();
    }

    private void OnTriggerExit(Collider other)
    {
        if (detectPlayer && other.CompareTag(playerTag))
        {
            OnPlayerExit();
            if (playerCanActivate && isActivated && _objectsOnPlate.Count == 0 && !lockWhenActivated)
                Deactivate();
            return;
        }

        var rb = other.attachedRigidbody;
        if (rb == null) return;
        if (!_objectsOnPlate.Contains(rb)) return;

        _objectsOnPlate.Remove(rb);

        if (_originalRigidbodyStates.TryGetValue(rb, out RigidbodyState originalState))
        {
            rb.isKinematic = originalState.isKinematic;
            rb.constraints = originalState.constraints;
            if (!originalState.isKinematic)
                rb.WakeUp();
            if (parentObjectToPlate)
                rb.transform.SetParent(originalState.originalParent);
            _originalRigidbodyStates.Remove(rb);
        }

        if (_objectsOnPlate.Count == 0 && isActivated && !lockWhenActivated)
        {
            if (!_playerOnPlate || !playerCanActivate)
                Deactivate();
        }
    }

    /// <summary>
    /// Activa el interruptor y ejecuta todas las acciones
    /// </summary>
    private void Activate()
    {
        if (isActivated) return;

        if (_deactivationCoroutine != null)
        {
            StopCoroutine(_deactivationCoroutine);
            _deactivationCoroutine = null;
        }
        
        isActivated = true;
        _lastActivationTime = Time.time;

        if (plateVisual != null)
        {
            _targetPlatePosition = _originalPlatePosition + Vector3.down * sinkAmount;
            _isAnimating = true;
        }
        
        if (cameraShakeIntensity > 0f && cameraShakeDuration > 0f &&
            Time.time - _lastCameraShakeTime >= CAMERA_SHAKE_COOLDOWN)
        {
            FeedbackService.CameraShake(cameraShakeIntensity, cameraShakeDuration);
            _lastCameraShakeTime = Time.time;
        }
        
        if (!string.IsNullOrEmpty(activateSfxKey))
            AudioService.Instance?.PlaySFX(activateSfxKey, worldPosition: transform.position);

        activateVfx?.Play();

        onActivated.Invoke();
        
        // Notificar cambio de estado (para PressurePuzzleController y similares)
        SendMessageUpwards("OnPlateStateChanged", this, SendMessageOptions.DontRequireReceiver);
        
        // Callback personalizado
        OnActivated();
    }

    /// <summary>
    /// Desactiva el interruptor (si no está bloqueado)
    /// </summary>
    private void Deactivate()
    {
        if (lockWhenActivated) return;
        if (!isActivated) return;
        
        // Usar delay para evitar desactivaciones por rebote
        if (_deactivationCoroutine != null)
        {
            StopCoroutine(_deactivationCoroutine);
        }
        _deactivationCoroutine = StartCoroutine(Co_DelayedDeactivation());
    }
    
    /// <summary>
    /// Corrutina que espera un pequeño delay antes de desactivar para evitar rebotes
    /// </summary>
    private System.Collections.IEnumerator Co_DelayedDeactivation()
    {
        yield return new WaitForSeconds(DEACTIVATION_DELAY);

        if (_objectsOnPlate.Count > 0 || (_playerOnPlate && playerCanActivate) || lockWhenActivated)
        {
            _deactivationCoroutine = null;
            yield break;
        }

        isActivated = false;
        _deactivationCoroutine = null;

        if (plateVisual != null)
        {
            _targetPlatePosition = _originalPlatePosition;
            _isAnimating = true;
        }
        
        if (!string.IsNullOrEmpty(deactivateSfxKey))
            AudioService.Instance?.PlaySFX(deactivateSfxKey, worldPosition: transform.position);

        deactivateVfx?.Play();

        onDeactivated.Invoke();
        
        // Notificar cambio de estado (para PressurePuzzleController y similares)
        SendMessageUpwards("OnPlateStateChanged", this, SendMessageOptions.DontRequireReceiver);
        
        // Callback personalizado
        OnDeactivated();
    }

    /// <summary>
    /// Callback que se llama al activar el interruptor.
    /// Sobrescribe en una clase hija para comportamiento personalizado.
    /// </summary>
    protected virtual void OnActivated()
    {
        // Opcional: disparar evento del sistema
        // EventBus.Trigger("PressurePlateActivated", gameObject);
    }

    /// <summary>
    /// Callback que se llama al desactivar el interruptor.
    /// Sobrescribe en una clase hija para comportamiento personalizado.
    /// </summary>
    protected virtual void OnDeactivated()
    {
        // Opcional: disparar evento del sistema
        // EventBus.Trigger("PressurePlateDeactivated", gameObject);
    }

    /// <summary>
    /// Activa el interruptor manualmente desde código externo
    /// </summary>
    public void ForceActivate()
    {
        if (!isActivated)
        {
            Activate();
        }
    }

    /// <summary>
    /// Desactiva el interruptor manualmente desde código externo
    /// </summary>
    public void ForceDeactivate()
    {
        if (isActivated && !lockWhenActivated)
        {
            Deactivate();
        }
    }

    /// <summary>
    /// Llamado cuando el jugador entra en la placa (sin activar)
    /// </summary>
    private void OnPlayerEnter()
    {
        if (_playerOnPlate) return;

        _playerOnPlate = true;

        if (plateVisual != null && !isActivated)
        {
            float sinkAmountToUse = playerCanActivate ? sinkAmount : playerSinkAmount;
            _targetPlatePosition = _originalPlatePosition + Vector3.down * sinkAmountToUse;
            _isAnimating = true;
        }

        if (!string.IsNullOrEmpty(playerStepSfxKey))
            AudioService.Instance?.PlaySFX(playerStepSfxKey, worldPosition: transform.position);

        OnPlayerSteppedOn();
    }

    private void OnPlayerExit()
    {
        if (!_playerOnPlate) return;

        _playerOnPlate = false;

        if (plateVisual != null && !isActivated)
        {
            _targetPlatePosition = _originalPlatePosition;
            _isAnimating = true;
        }
    }

    /// <summary>
    /// Callback que se llama cuando el jugador pisa la placa.
    /// Sobrescribe en una clase hija para comportamiento personalizado (ej: mostrar hint UI).
    /// </summary>
    protected virtual void OnPlayerSteppedOn()
    {
        // Opcional: mostrar hint al jugador
        // HintSystem.ShowHint("Busca un objeto pesado para activar esta placa");
    }

    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Dibujar área de detección
        Gizmos.color = isActivated ? Color.green : Color.yellow;
        var col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            if (col is BoxCollider box)
            {
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
        }
    }
    #endif
}
