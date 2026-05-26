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

    [Header("Estado")]
    [SerializeField] private bool isActivated;
    
    private Vector3 _originalPlatePosition;
    private Vector3 _targetPlatePosition;
    private HashSet<Rigidbody> _objectsOnPlate = new HashSet<Rigidbody>();
    private Dictionary<Rigidbody, RigidbodyState> _originalRigidbodyStates = new Dictionary<Rigidbody, RigidbodyState>();
    private bool _isAnimating;
    private bool _playerOnPlate;
    
    // Cooldown para evitar spam de camera shake y activaciones
    private float _lastCameraShakeTime = -999f;
    private float _lastActivationTime = -999f;
    private const float CAMERA_SHAKE_COOLDOWN = 0.5f;
    private const float DEACTIVATION_DELAY = 0.15f; // Reducido de 0.3f a 0.15f para respuesta más rápida
    private Coroutine _deactivationCoroutine;
    
    // Estructura para guardar el estado original del Rigidbody
    private struct RigidbodyState
    {
        public bool isKinematic;
        public RigidbodyConstraints constraints;
        public Transform originalParent;
    } // Rastrea si el jugador está en la placa

    // Propiedad pública para consultar estado (compatibilidad con PressurePuzzleController)
    public bool IsActivated => isActivated;
    public bool isPressed => isActivated; // Alias para compatibilidad con código legacy

    private void Start()
    {
        // Guardar posición original de la placa visual
        if (plateVisual != null)
        {
            _originalPlatePosition = plateVisual.localPosition;
            _targetPlatePosition = _originalPlatePosition;
        }
        
        // Verificar que el collider sea trigger
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[PressurePlate] El collider en {name} debería ser trigger. Configurándolo automáticamente.");
            col.isTrigger = true;
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
        
        // Verificación periódica: remover objetos que ya no están en el trigger
        // Esto ayuda cuando objetos congelados son movidos con levitación
        if (_objectsOnPlate.Count > 0)
        {
            // Crear lista temporal para evitar modificar durante iteración
            var toRemove = new System.Collections.Generic.List<Rigidbody>();
            
            foreach (var rb in _objectsOnPlate)
            {
                // Si el objeto es null o está muy lejos de la placa, marcarlo para remover
                if (rb == null)
                {
                    toRemove.Add(rb);
                }
                else
                {
                    // Verificar distancia al trigger
                    float distanceToPlate = Vector3.Distance(rb.position, transform.position);
                    var col = GetComponent<Collider>();
                    float maxDistance = col != null ? col.bounds.extents.magnitude * 2f : 2f;
                    
                    if (distanceToPlate > maxDistance)
                    {
                        Debug.Log($"[PressurePlate] ⚠️ Objeto {rb.name} está lejos de la placa (distancia: {distanceToPlate:F2}m) - removiendo");
                        toRemove.Add(rb);
                    }
                }
            }
            
            // Remover objetos marcados
            foreach (var rb in toRemove)
            {
                if (_objectsOnPlate.Remove(rb))
                {
                    Debug.Log($"[PressurePlate] 🧹 Limpieza: Objeto removido de la lista (Total: {_objectsOnPlate.Count})");
                    
                    // Restaurar estado si estaba guardado
                    if (_originalRigidbodyStates.ContainsKey(rb))
                    {
                        _originalRigidbodyStates.Remove(rb);
                    }
                }
            }
            
            // Si ya no quedan objetos y está activado, desactivar
            if (_objectsOnPlate.Count == 0 && isActivated && !lockWhenActivated)
            {
                if (!_playerOnPlate || !playerCanActivate)
                {
                    Debug.Log($"[PressurePlate] 🔄 Desactivando por limpieza automática");
                    Deactivate();
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[PressurePlate] 🔍 OnTriggerEnter: {other.name} (Tag: {other.tag})");
        
        if (lockWhenActivated && isActivated)
        {
            Debug.Log($"[PressurePlate] ⚠️ Ya está bloqueado y activado");
            return;
        }
        
        // Detectar jugador
        if (detectPlayer && other.CompareTag(playerTag))
        {
            Debug.Log($"[PressurePlate] 👤 JUGADOR detectado! playerCanActivate={playerCanActivate}, isActivated={isActivated}");
            
            OnPlayerEnter();
            
            // Si el jugador puede activar, continuar con la activación
            if (playerCanActivate && !isActivated)
            {
                Debug.Log($"[PressurePlate] ✅ ACTIVANDO por jugador...");
                Activate();
            }
            else
            {
                Debug.Log($"[PressurePlate] ❌ NO activa: playerCanActivate={playerCanActivate}, isActivated={isActivated}");
            }
            
            return;
        }
        
        Debug.Log($"[PressurePlate] 🔍 No es jugador, verificando Rigidbody...");
        
        // Verificar si el objeto tiene Rigidbody
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        
        // Verificar si solo aceptamos objetos con PickupObject
        if (onlyPickupObjects)
        {
            var pickup = rb.GetComponent<PickupObject>();
            if (pickup == null) return;
        }
        
        // Verificar masa mínima
        if (rb.mass < minimumMass) return;
        
        // Verificar si ya está en la lista (evitar duplicados)
        if (_objectsOnPlate.Contains(rb))
        {
            Debug.Log($"[PressurePlate] ⚠️ Objeto {rb.name} ya está en la placa, ignorando");
            return;
        }
        
        // Añadir a la lista de objetos en la placa
        _objectsOnPlate.Add(rb);
        
        // Guardar estado original del Rigidbody
        if (!_originalRigidbodyStates.ContainsKey(rb))
        {
            _originalRigidbodyStates[rb] = new RigidbodyState
            {
                isKinematic = rb.isKinematic,
                constraints = rb.constraints,
                originalParent = rb.transform.parent
            };
        }
        
        // Congelar el objeto en la placa
        if (freezeObjectOnPlate)
        {
            rb.isKinematic = true; // Desactiva física para que no se mueva
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Hacer hijo de la placa (opcional)
        if (parentObjectToPlate && plateVisual != null)
        {
            rb.transform.SetParent(plateVisual);
        }
        
        Debug.Log($"[PressurePlate] 📦 Objeto añadido: {rb.name} (Total: {_objectsOnPlate.Count})");
        
        // Activar el interruptor si no estaba activado
        if (!isActivated)
        {
            Activate();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Detectar cuando el jugador sale
        if (detectPlayer && other.CompareTag(playerTag))
        {
            OnPlayerExit();
            
            // Si el jugador puede activar y lo activó, desactivar al salir (solo si no hay objetos)
            // PERO no desactivar si está bloqueado
            if (playerCanActivate && isActivated && _objectsOnPlate.Count == 0 && !lockWhenActivated)
            {
                Deactivate();
            }
            
            return;
        }
        
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        
        // Solo procesar si el objeto estaba en nuestra lista
        if (!_objectsOnPlate.Contains(rb)) return;
        
        // Remover de la lista
        _objectsOnPlate.Remove(rb);
        
        Debug.Log($"[PressurePlate] 📦 Objeto removido: {rb.name} (Restantes: {_objectsOnPlate.Count})");
        
        // Restaurar estado original del Rigidbody
        if (_originalRigidbodyStates.TryGetValue(rb, out RigidbodyState originalState))
        {
            // Restaurar propiedades físicas
            rb.isKinematic = originalState.isKinematic;
            rb.constraints = originalState.constraints;
            
            // Asegurarse de que el objeto puede moverse de nuevo si no era kinematic
            if (!originalState.isKinematic)
            {
                rb.WakeUp(); // Despertar el Rigidbody por si estaba dormido
            }
            
            // Restaurar padre original
            if (parentObjectToPlate)
            {
                rb.transform.SetParent(originalState.originalParent);
            }
            
            _originalRigidbodyStates.Remove(rb);
            
            Debug.Log($"[PressurePlate] ✅ Estado restaurado para {rb.name}: isKinematic={rb.isKinematic}");
        }
        
        // Desactivar el interruptor si no hay más objetos y el jugador no está (o no puede activar)
        // IMPORTANTE: Solo si NO está bloqueado con lockWhenActivated
        if (_objectsOnPlate.Count == 0 && isActivated && !lockWhenActivated)
        {
            // Solo desactivar si el jugador no está O si el jugador no puede activar
            if (!_playerOnPlate || !playerCanActivate)
            {
                Debug.Log($"[PressurePlate] 🔄 {name} Intentando desactivar - Objetos: {_objectsOnPlate.Count}, Player: {_playerOnPlate}, CanActivate: {playerCanActivate}, Locked: {lockWhenActivated}");
                Deactivate();
            }
            else
            {
                Debug.Log($"[PressurePlate] ⚠️ {name} No se desactiva porque el jugador está en la placa y puede activarla");
            }
        }
        else if (_objectsOnPlate.Count == 0 && lockWhenActivated)
        {
            Debug.Log($"[PressurePlate] 🔒 {name} No se desactiva porque lockWhenActivated está activo");
        }
    }

    /// <summary>
    /// Activa el interruptor y ejecuta todas las acciones
    /// </summary>
    private void Activate()
    {
        // Si ya está activado, no hacer nada
        if (isActivated)
        {
            Debug.Log($"[PressurePlate] ⚠️ {name} Ya está activado, ignorando");
            return;
        }
        
        // Cancelar cualquier desactivación pendiente
        if (_deactivationCoroutine != null)
        {
            StopCoroutine(_deactivationCoroutine);
            _deactivationCoroutine = null;
        }
        
        isActivated = true;
        _lastActivationTime = Time.time;
        
        Debug.Log($"[PressurePlate] 🔴 {name} ACTIVADO");
        
        // Feedback visual - hundir placa
        if (plateVisual != null)
        {
            _targetPlatePosition = _originalPlatePosition + Vector3.down * sinkAmount;
            _isAnimating = true;
        }
        
        // Feedback de cámara (con cooldown para evitar spam)
        if (cameraShakeIntensity > 0f && cameraShakeDuration > 0f)
        {
            if (Time.time - _lastCameraShakeTime >= CAMERA_SHAKE_COOLDOWN)
            {
                FeedbackService.CameraShake(cameraShakeIntensity, cameraShakeDuration);
                _lastCameraShakeTime = Time.time;
            }
        }
        
        // Feedback de audio
        if (!string.IsNullOrEmpty(activateSfxKey))
        {
            AudioService.Instance?.PlaySFX(activateSfxKey, worldPosition: transform.position);
        }
        
        // Notificar a los oyentes
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
        Debug.Log($"[PressurePlate] ⏳ {name} Iniciando desactivación retardada (delay: {DEACTIVATION_DELAY}s)");
        
        yield return new WaitForSeconds(DEACTIVATION_DELAY);
        
        // Verificar de nuevo si todavía debemos desactivar (por si algo entró durante el delay)
        if (_objectsOnPlate.Count > 0 || (_playerOnPlate && playerCanActivate))
        {
            Debug.Log($"[PressurePlate] ⚠️ {name} Desactivación cancelada - Objetos en placa: {_objectsOnPlate.Count}, Player: {_playerOnPlate}");
            _deactivationCoroutine = null;
            yield break;
        }
        
        // Verificación adicional: lockWhenActivated por si acaso
        if (lockWhenActivated)
        {
            Debug.Log($"[PressurePlate] ⚠️ {name} Desactivación cancelada - lockWhenActivated está activo");
            _deactivationCoroutine = null;
            yield break;
        }
        
        isActivated = false;
        _deactivationCoroutine = null;
        
        Debug.Log($"[PressurePlate] ⚪ {name} DESACTIVADO");
        
        // Feedback visual - elevar placa
        if (plateVisual != null)
        {
            _targetPlatePosition = _originalPlatePosition;
            _isAnimating = true;
        }
        
        // Feedback de audio
        if (!string.IsNullOrEmpty(deactivateSfxKey))
        {
            AudioService.Instance?.PlaySFX(deactivateSfxKey, worldPosition: transform.position);
        }
        
        // Notificar a los oyentes
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
        if (_playerOnPlate) return; // Ya está encima
        
        _playerOnPlate = true;
        
        Debug.Log($"[PressurePlate] 👤 {name} - Jugador detectado {(playerCanActivate ? "(puede activar)" : "(necesita objeto)")}");
        
        // Feedback visual - la placa se hunde
        if (plateVisual != null && !isActivated)
        {
            // Si el jugador puede activar, hundir completamente. Si no, solo un poco.
            float sinkAmountToUse = playerCanActivate ? sinkAmount : playerSinkAmount;
            _targetPlatePosition = _originalPlatePosition + Vector3.down * sinkAmountToUse;
            _isAnimating = true;
        }
        
        // Feedback de audio
        if (!string.IsNullOrEmpty(playerStepSfxKey))
        {
            AudioService.Instance?.PlaySFX(playerStepSfxKey, worldPosition: transform.position);
        }
        
        // Callback para comportamiento personalizado
        OnPlayerSteppedOn();
    }

    /// <summary>
    /// Llamado cuando el jugador sale de la placa
    /// </summary>
    private void OnPlayerExit()
    {
        if (!_playerOnPlate) return; // No estaba encima
        
        _playerOnPlate = false;
        
        Debug.Log($"[PressurePlate] 👤 {name} - Jugador salió");
        
        // Volver la placa a su posición original (solo si no está activada)
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
