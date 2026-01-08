using UnityEngine;

/// <summary>
/// Componente que permite a un NPC ser afectado por hechizos de levitación del jugador.
/// Maneja las animaciones de elevación/voltereta y las fuerzas físicas de atracción/repulsión.
/// 
/// Debe añadirse a NPCs que queramos que sean susceptibles a la levitación.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class LevitationTarget : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Si está marcado, este NPC puede ser levitado por el jugador.")]
    [SerializeField] private bool canBeLevitated = true;
    [Tooltip("Multiplicador de fuerza de atracción (para ajustar por NPC).")]
    [SerializeField] private float pullForceMultiplier = 1f;
    [Tooltip("Multiplicador de fuerza de repulsión (para ajustar por NPC).")]
    [SerializeField] private float pushForceMultiplier = 1f;
    
    [Header("Animación")]
    [Tooltip("Nombre del estado de animación para la levitación (primera parte: elevarse).")]
    [SerializeField] private string levitationAnimState = "LevelUp_NoWeapon";
    [Tooltip("Tiempo normalizado (0-1) donde pausar la animación durante el hold.")]
    [SerializeField] private float holdPauseNormalizedTime = 0.5f;
    [Tooltip("Velocidad de elevación vertical.")]
    [SerializeField] private float liftSpeed = 3f;
    
    [Header("Física")]
    [Tooltip("Si está marcado, desactivar NavMeshAgent durante la levitación.")]
    [SerializeField] private bool disableNavMeshDuringLevitation = true;
    [Tooltip("Drag del Rigidbody durante levitación para suavizar el movimiento.")]
    [SerializeField] private float levitationDrag = 2f;
    
    [Header("VFX")]
    [Tooltip("Prefab del efecto visual que se instancia sobre el NPC mientras está levitando.")]
    [SerializeField] private GameObject levitationVFXPrefab;
    [Tooltip("Offset de posición para el VFX respecto al centro del NPC.")]
    [SerializeField] private Vector3 vfxOffset = Vector3.up;
    [Tooltip("Escala del VFX de levitación (útil para ajustar el tamaño según el NPC).")]
    [SerializeField] private Vector3 vfxScale = Vector3.one;
    [Tooltip("Si está marcado, el VFX seguirá al NPC durante la levitación.")]
    [SerializeField] private bool vfxFollowsTarget = true;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    // Referencias
    private Rigidbody _rigidbody;
    private Animator _animator;
    private UnityEngine.AI.NavMeshAgent _navAgent;
    
    // Estado
    private bool _isBeingLevitated;
    private PlayerLevitationController _currentLevitator;
    private float _targetHeight;
    private float _originalDrag;
    private bool _wasNavAgentEnabled;
    private bool _wasKinematic;
    
    // VFX
    private GameObject _currentVFXInstance;
    
    // Corrutinas
    private Coroutine _pauseAnimCoroutine;
    
    // Animator state hash
    private int _levitationStateHash;
    
    public bool CanBeLevitated => canBeLevitated && !_isBeingLevitated;
    public bool IsBeingLevitated => _isBeingLevitated;
    
    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponentInChildren<Animator>();
        _navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        
        if (!string.IsNullOrEmpty(levitationAnimState))
            _levitationStateHash = Animator.StringToHash(levitationAnimState);
    }
    
    /// <summary>
    /// Llamado por PlayerLevitationController cuando el NPC comienza a ser levitado.
    /// </summary>
    public void BeginLevitation(PlayerLevitationController levitator, MagicSpellSO spell)
    {
        if (_isBeingLevitated || !canBeLevitated) return;
        
        _isBeingLevitated = true;
        _currentLevitator = levitator;
        
        // Calcular altura objetivo
        _targetHeight = transform.position.y + spell.levitationHeight;
        
        // Configurar física
        _originalDrag = _rigidbody.linearDamping;
        _wasKinematic = _rigidbody.isKinematic;
        _rigidbody.linearDamping = levitationDrag;
        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = false;
        
        // Desactivar NavMeshAgent si existe
        if (disableNavMeshDuringLevitation && _navAgent != null)
        {
            _wasNavAgentEnabled = _navAgent.enabled;
            _navAgent.enabled = false;
        }
        
        // Iniciar animación de levitación
        if (_animator != null && _levitationStateHash != 0)
        {
            _animator.Play(_levitationStateHash, 0, 0f);
            _pauseAnimCoroutine = StartCoroutine(Co_PauseAnimationDuringHold());
        }
        
        // Instanciar VFX de levitación
        SpawnLevitationVFX();
        
        if (showDebugLogs) Debug.Log($"[LevitationTarget] {name} comenzando levitación, altura objetivo: {_targetHeight}");
    }
    
    /// <summary>
    /// Llamado cada frame por PlayerLevitationController mientras el NPC está siendo levitado.
    /// El NPC sigue la posición objetivo como si estuviera atado con un hilo elástico (como globos).
    /// </summary>
    public void UpdateLevitation(MagicSpellSO spell, Vector3 targetPosition, float followSpeed)
    {
        if (!_isBeingLevitated) return;
        
        // Calcular la posición objetivo con la altura de levitación
        Vector3 finalTargetPos = new Vector3(targetPosition.x, _targetHeight, targetPosition.z);
        
        // Calcular la distancia al objetivo
        Vector3 toTarget = finalTargetPos - transform.position;
        float distance = toTarget.magnitude;
        
        // Movimiento elástico: cuanto más lejos, más rápido se mueve (como un hilo elástico)
        float elasticSpeed = followSpeed * pullForceMultiplier * Mathf.Max(1f, distance * 0.5f);
        
        // Usar una combinación de interpolación suave y fuerza para un movimiento fluido tipo "globo"
        if (distance > 0.1f)
        {
            // Mover hacia la posición objetivo con velocidad proporcional a la distancia
            Vector3 moveDirection = toTarget.normalized;
            Vector3 targetVelocity = moveDirection * elasticSpeed;
            
            // Interpolar la velocidad actual hacia la velocidad objetivo para suavidad
            _rigidbody.linearVelocity = Vector3.Lerp(_rigidbody.linearVelocity, targetVelocity, Time.deltaTime * 8f);
        }
        else
        {
            // Cuando está cerca, reducir velocidad para evitar oscilaciones
            _rigidbody.linearVelocity = Vector3.Lerp(_rigidbody.linearVelocity, Vector3.zero, Time.deltaTime * 5f);
        }
        
        // Mantener altura objetivo si está por debajo
        float currentHeight = transform.position.y;
        if (currentHeight < _targetHeight - 0.1f)
        {
            float liftForce = Mathf.Min(liftSpeed * spell.levitationLiftSpeed, (_targetHeight - currentHeight) * 5f);
            _rigidbody.AddForce(Vector3.up * liftForce, ForceMode.Acceleration);
        }
        
        // Actualizar posición del VFX si sigue al target
        UpdateVFXPosition();
    }
    
    /// <summary>
    /// Llamado por PlayerLevitationController cuando el jugador suelta el botón.
    /// Aplica repulsión y continúa la animación de voltereta.
    /// </summary>
    public void EndLevitation(MagicSpellSO spell, Vector3 pushDirection, float pushForce)
    {
        if (!_isBeingLevitated) return;
        
        if (showDebugLogs) Debug.Log($"[LevitationTarget] {name} finalizando levitación, aplicando repulsión");
        
        // Detener la corrutina de pausa de animación
        if (_pauseAnimCoroutine != null)
        {
            StopCoroutine(_pauseAnimCoroutine);
            _pauseAnimCoroutine = null;
        }
        
        // Destruir el VFX de levitación
        DestroyLevitationVFX();
        
        // Continuar la animación desde el punto de pausa
        if (_animator != null)
        {
            _animator.Play(_levitationStateHash, 0, holdPauseNormalizedTime);
        }
        
        // Configurar física para el lanzamiento - mantener sin gravedad brevemente para el impulso
        _rigidbody.linearDamping = 0.5f; // Reducir drag para que salga disparado
        
        // Aplicar fuerza de repulsión POTENTE (tipo Stranger Things - disparado hacia afuera)
        Vector3 push = pushDirection * pushForce * pushForceMultiplier;
        // Componente vertical significativo para el efecto de "lanzamiento hacia arriba y afuera"
        push.y = pushForce * 0.8f;
        _rigidbody.AddForce(push, ForceMode.Impulse);
        
        // Aplicar torque intenso para la voltereta
        Vector3 torqueAxis = Vector3.Cross(Vector3.up, pushDirection);
        _rigidbody.AddTorque(torqueAxis * pushForce * 4f, ForceMode.Impulse);
        
        // Habilitar gravedad para que caiga después del impulso
        _rigidbody.useGravity = true;
        
        // Iniciar el proceso de finalización
        StartCoroutine(Co_FinishLevitation(spell));
    }
    
    /// <summary>
    /// Cancela la levitación inmediatamente (llamado si el levitador es destruido o desactivado).
    /// </summary>
    public void CancelLevitation()
    {
        if (!_isBeingLevitated) return;
        
        if (showDebugLogs) Debug.Log($"[LevitationTarget] {name} levitación cancelada");
        
        StopAllCoroutines();
        _pauseAnimCoroutine = null;
        
        // Destruir el VFX de levitación
        DestroyLevitationVFX();
        
        RestoreNormalState();
    }
    
    /// <summary>
    /// Corrutina que mantiene la animación pausada en el punto configurado durante el hold.
    /// Usa playback manual para no afectar otras animaciones del NPC.
    /// </summary>
    System.Collections.IEnumerator Co_PauseAnimationDuringHold()
    {
        if (_animator == null) yield break;
        
        // Esperar a que entre en el estado
        int maxWait = 10;
        int waited = 0;
        while (_animator != null && waited < maxWait)
        {
            var info = _animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash == _levitationStateHash)
                break;
            waited++;
            yield return null;
        }
        
        if (_animator == null) yield break;
        
        // Reproducir hasta el punto de pausa y mantenerlo
        while (_animator != null && _isBeingLevitated)
        {
            var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            
            // Si ya pasamos el punto de pausa, mantener la animación en ese punto
            if (stateInfo.normalizedTime >= holdPauseNormalizedTime)
            {
                // Reescribir constantemente el tiempo normalizado para "pausar" sin afectar animator.speed
                _animator.Play(_levitationStateHash, 0, holdPauseNormalizedTime);
            }
            
            yield return null;
        }
    }
    
    /// <summary>
    /// Corrutina que finaliza el proceso de levitación después de la repulsión.
    /// </summary>
    System.Collections.IEnumerator Co_FinishLevitation(MagicSpellSO spell)
    {
        // Esperar brevemente a que el impulso inicial se aplique
        yield return new WaitForSeconds(0.3f);
        
        // Esperar a que toque el suelo
        float timeout = 5f;
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            
            // Verificar si está en el suelo (raycast más largo para detectar mejor)
            if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, 0.5f))
            {
                break;
            }
            
            yield return null;
        }
        
        // Pequeña espera adicional
        yield return new WaitForSeconds(0.2f);
        
        RestoreNormalState();
    }
    
    /// <summary>
    /// Restaura el estado normal del NPC después de la levitación.
    /// </summary>
    void RestoreNormalState()
    {
        _isBeingLevitated = false;
        _currentLevitator = null;
        
        // Restaurar física
        if (_rigidbody != null)
        {
            _rigidbody.linearDamping = _originalDrag;
            _rigidbody.isKinematic = _wasKinematic;
            _rigidbody.useGravity = true;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            
            // Resetear rotación para que el NPC quede de pie
            Vector3 euler = transform.eulerAngles;
            transform.eulerAngles = new Vector3(0f, euler.y, 0f);
        }
        
        // Forzar la vuelta a la animación idle
        if (_animator != null)
        {
            // Intentar volver al estado de locomotion/idle
            // Primero probar con triggers/states comunes
            _animator.Rebind(); // Esto fuerza el reseteo del animator
            _animator.Update(0f);
            
            // O si hay un estado idle conocido
            if (_animator.HasState(0, Animator.StringToHash("Idle")))
            {
                _animator.Play("Idle", 0, 0f);
            }
            else if (_animator.HasState(0, Animator.StringToHash("Locomotion")))
            {
                _animator.Play("Locomotion", 0, 0f);
            }
        }
        
        // Restaurar NavMeshAgent
        if (_navAgent != null && _wasNavAgentEnabled)
        {
            // Intentar reposicionar en el NavMesh antes de reactivar
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }
            _navAgent.enabled = true;
        }
        
        if (showDebugLogs) Debug.Log($"[LevitationTarget] {name} estado normal restaurado");
    }
    
    /// <summary>
    /// Instancia el VFX de levitación sobre el NPC.
    /// </summary>
    void SpawnLevitationVFX()
    {
        if (levitationVFXPrefab == null) return;
        
        Vector3 spawnPos = transform.position + vfxOffset;
        _currentVFXInstance = Instantiate(levitationVFXPrefab, spawnPos, Quaternion.identity);
        
        // Aplicar escala configurada
        _currentVFXInstance.transform.localScale = vfxScale;
        
        // Si el VFX debe seguir al target, parentearlo
        if (vfxFollowsTarget)
        {
            _currentVFXInstance.transform.SetParent(transform);
            _currentVFXInstance.transform.localPosition = vfxOffset;
            // Mantener la escala después de parentear
            _currentVFXInstance.transform.localScale = vfxScale;
        }
        
        if (showDebugLogs) Debug.Log($"[LevitationTarget] VFX instanciado en {spawnPos} con escala {vfxScale}");
    }
    
    /// <summary>
    /// Actualiza la posición del VFX si no está parenteado.
    /// </summary>
    void UpdateVFXPosition()
    {
        if (_currentVFXInstance == null || vfxFollowsTarget) return;
        
        _currentVFXInstance.transform.position = transform.position + vfxOffset;
    }
    
    /// <summary>
    /// Destruye el VFX de levitación.
    /// </summary>
    void DestroyLevitationVFX()
    {
        if (_currentVFXInstance != null)
        {
            Destroy(_currentVFXInstance);
            _currentVFXInstance = null;
            
            if (showDebugLogs) Debug.Log($"[LevitationTarget] VFX destruido");
        }
    }
    
    void OnDisable()
    {
        if (_isBeingLevitated)
        {
            CancelLevitation();
        }
    }
    
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (_isBeingLevitated)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, 1f);
            Gizmos.DrawLine(transform.position, new Vector3(transform.position.x, _targetHeight, transform.position.z));
        }
    }
#endif
}

