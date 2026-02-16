using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// IA simple de persecución para el minijuego "Pilla Pilla".
/// Persigue al jugador constantemente usando NavMeshAgent.
/// 
/// ANIMACIONES:
/// - Si el personaje tiene NPCSimpleAnimator (como Estela), lo usa automáticamente.
/// - Si no, usa el Animator directamente con el parámetro configurado.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class ChaserAI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform target;
    [SerializeField] private NavMeshAgent agent;
    
    [Header("Configuración")]
    [SerializeField] private float chaseSpeed = 5f;
    [SerializeField] private float catchDistance = 1.2f;
    [SerializeField] private float updatePathInterval = 0.2f;

    [Header("Animación (Solo si NO tiene NPCSimpleAnimator)")]
    [Tooltip("Solo se usa si el personaje NO tiene NPCSimpleAnimator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string runAnimParam = "IsRunning";
    
    // Evento cuando atrapa al jugador
    public System.Action OnCaughtPlayer;
    
    // ✅ Propiedades públicas para configurar desde TagMinigameController
    public float ChaseSpeed
    {
        get => chaseSpeed;
        set
        {
            chaseSpeed = value;
            if (agent) agent.speed = chaseSpeed;
        }
    }
    
    public float CatchDistance
    {
        get => catchDistance;
        set => catchDistance = value;
    }
    
    public float Acceleration
    {
        get => agent != null ? agent.acceleration : 8f;
        set { if (agent) agent.acceleration = value; }
    }

    // Referencias internas
    private NPCSimpleAnimator _npcAnimator;
    private bool _useNpcAnimator;
    private bool _isChasing;
    private float _lastPathUpdate;
    private Vector3 _startPosition;
    private Quaternion _startRotation;

    void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        
        // Intentar obtener NPCSimpleAnimator (usado por Estela y otros NPCs)
        _npcAnimator = GetComponent<NPCSimpleAnimator>();
        _useNpcAnimator = _npcAnimator != null;
        
        // Solo buscar Animator si no tiene NPCSimpleAnimator
        if (!_useNpcAnimator && !animator)
        {
            animator = GetComponent<Animator>();
        }
        
        _startPosition = transform.position;
        _startRotation = transform.rotation;
        
        if (_useNpcAnimator)
        {
            Debug.Log($"[ChaserAI] Usando NPCSimpleAnimator para animaciones de {name}");
        }
        else if (animator)
        {
            Debug.Log($"[ChaserAI] Usando Animator directo con parámetro '{runAnimParam}' para {name}");
        }
    }

    void Start()
    {
        // Buscar al jugador si no está asignado (usando PlayerService para mejor rendimiento)
        if (!target)
        {
            if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null)
            {
                target = playerGo.transform;
            }
        }

        if (agent)
        {
            agent.speed = chaseSpeed;
            agent.isStopped = true;
        }
    }

    void Update()
    {
        if (!_isChasing || !target || !agent) return;

        // Actualizar destino periódicamente
        if (Time.time - _lastPathUpdate >= updatePathInterval)
        {
            agent.SetDestination(target.position);
            _lastPathUpdate = Time.time;
        }
        
        // Actualizar animación de movimiento (para NPCSimpleAnimator)
        if (_useNpcAnimator && _npcAnimator != null)
        {
            float normalizedSpeed = agent.velocity.magnitude / chaseSpeed;
            _npcAnimator.SetMovementSpeed(Mathf.Clamp01(normalizedSpeed));
        }

        // Verificar si atrapó al jugador
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        if (distanceToTarget <= catchDistance)
        {
            CatchPlayer();
        }
    }

    /// <summary>
    /// Inicia la persecución
    /// </summary>
    public void StartChasing()
    {
        Debug.Log($"[ChaserAI] 🏃 StartChasing() llamado en {name}");
        
        // ✅ FIX: Asegurar que tenemos el NavMeshAgent (puede haberse añadido dinámicamente)
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                Debug.LogError($"[ChaserAI] ❌ No hay NavMeshAgent en {name}. Añadiendo uno...");
                agent = gameObject.AddComponent<NavMeshAgent>();
            }
        }
        
        // ✅ FIX: Asegurar que tenemos el sistema de animación
        if (_npcAnimator == null)
        {
            _npcAnimator = GetComponent<NPCSimpleAnimator>();
            _useNpcAnimator = _npcAnimator != null;
        }
        if (!_useNpcAnimator && animator == null)
        {
            animator = GetComponent<Animator>();
        }
        
        if (!target)
        {
            if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null)
            {
                target = playerGo.transform;
                Debug.Log($"[ChaserAI] ✅ Target encontrado: {target.name}");
            }
            else
            {
                Debug.LogError("[ChaserAI] ❌ No se encontró al jugador para perseguir");
                return;
            }
        }

        _isChasing = true;
        
        if (agent)
        {
            // Verificar que el NavMeshAgent esté en NavMesh válido
            if (!agent.isOnNavMesh)
            {
                Debug.LogWarning($"[ChaserAI] ⚠️ Agent no está en NavMesh. Intentando warpar...");
                // Intentar colocar en NavMesh
                if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out var hit, 5f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                    Debug.Log($"[ChaserAI] ✅ Warpado a posición válida de NavMesh: {hit.position}");
                }
                else
                {
                    Debug.LogError("[ChaserAI] ❌ No se encontró NavMesh cerca del perseguidor");
                    return;
                }
            }
            
            agent.isStopped = false;
            agent.SetDestination(target.position);
            Debug.Log($"[ChaserAI] ✅ NavMeshAgent configurado. Speed: {agent.speed}, Destination: {target.position}");
        }
        else
        {
            Debug.LogError("[ChaserAI] ❌ No hay NavMeshAgent asignado");
        }

        // Activar animación de correr
        if (_useNpcAnimator && _npcAnimator != null)
        {
            _npcAnimator.SetMovementSpeed(1f); // Velocidad máxima
            Debug.Log("[ChaserAI] 🎬 Animación via NPCSimpleAnimator activada");
        }
        else if (animator && !string.IsNullOrEmpty(runAnimParam))
        {
            animator.SetBool(runAnimParam, true);
            Debug.Log($"[ChaserAI] 🎬 Animación via Animator activada (param: {runAnimParam})");
        }
        else
        {
            Debug.LogWarning("[ChaserAI] ⚠️ No hay sistema de animación disponible");
        }

        Debug.Log("[ChaserAI] ¡Comenzó la persecución!");
    }

    /// <summary>
    /// Detiene la persecución
    /// </summary>
    public void StopChasing()
    {
        _isChasing = false;

        if (agent)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        // Detener animación
        if (_useNpcAnimator && _npcAnimator != null)
        {
            _npcAnimator.SetMovementSpeed(0f);
        }
        else if (animator && !string.IsNullOrEmpty(runAnimParam))
        {
            animator.SetBool(runAnimParam, false);
        }

        Debug.Log("[ChaserAI] Persecución detenida.");
    }

    /// <summary>
    /// Reinicia el perseguidor a su posición inicial
    /// </summary>
    public void ResetToStart()
    {
        StopChasing();
        
        if (agent)
        {
            agent.Warp(_startPosition);
        }
        else
        {
            transform.position = _startPosition;
        }
        
        transform.rotation = _startRotation;
        Debug.Log("[ChaserAI] Reiniciado a posición inicial.");
    }

    /// <summary>
    /// Establece una nueva posición inicial
    /// </summary>
    public void SetStartPosition(Vector3 position, Quaternion rotation)
    {
        _startPosition = position;
        _startRotation = rotation;
    }
    
    /// <summary>
    /// Teletransporta al perseguidor a una posición específica (sin detener la persecución)
    /// </summary>
    public void TeleportTo(Vector3 position)
    {
        bool wasChasing = _isChasing;
        
        if (agent)
        {
            agent.isStopped = true;
            agent.Warp(position);
            
            // Si estaba persiguiendo, continuar
            if (wasChasing)
            {
                agent.isStopped = false;
                if (target != null)
                {
                    agent.SetDestination(target.position);
                }
            }
        }
        else
        {
            transform.position = position;
        }
        
        Debug.Log($"[ChaserAI] ⚡ Teletransportado a {position}");
    }

    private void CatchPlayer()
    {
        Debug.Log("[ChaserAI] ¡Jugador atrapado!");
        StopChasing();
        OnCaughtPlayer?.Invoke();
    }

    /// <summary>
    /// Asigna el objetivo a perseguir
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    /// <summary>
    /// Indica si está persiguiendo activamente
    /// </summary>
    public bool IsChasing => _isChasing;
    
    /// <summary>
    /// Reinicializa el NavMeshAgent después de un cambio de escena.
    /// Útil cuando el perseguidor es DontDestroyOnLoad y necesita funcionar en nuevas escenas.
    /// </summary>
    public void ReinitializeAfterSceneChange(Transform newTarget = null)
    {
        Debug.Log($"[ChaserAI] 🔄 ReinitializeAfterSceneChange llamado en {name}");
        
        bool wasChasing = _isChasing;
        
        // Actualizar target si se proporciona uno nuevo
        if (newTarget != null)
        {
            target = newTarget;
            Debug.Log($"[ChaserAI] ✅ Target actualizado a: {target.name}");
        }
        
        // Buscar el jugador si no hay target
        if (target == null)
        {
            if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null)
            {
                target = playerGo.transform;
                Debug.Log($"[ChaserAI] ✅ Target encontrado via PlayerService: {target.name}");
            }
        }
        
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
        
        if (agent != null)
        {
            // Verificar que el NavMeshAgent esté en un NavMesh válido
            if (!agent.isOnNavMesh)
            {
                Debug.LogWarning($"[ChaserAI] ⚠️ Agent no está en NavMesh después del cambio de escena");
                
                // Buscar una posición válida en NavMesh
                if (NavMesh.SamplePosition(transform.position, out var hit, 10f, NavMesh.AllAreas))
                {
                    agent.enabled = false; // Deshabilitar temporalmente para poder mover
                    transform.position = hit.position;
                    agent.enabled = true;
                    
                    // Forzar el warp
                    if (agent.isOnNavMesh)
                    {
                        agent.Warp(hit.position);
                        Debug.Log($"[ChaserAI] ✅ Warpado a NavMesh válido: {hit.position}");
                    }
                }
                else
                {
                    Debug.LogError($"[ChaserAI] ❌ No se encontró NavMesh cerca de {transform.position}");
                }
            }
            else
            {
                Debug.Log($"[ChaserAI] ✅ Agent ya está en NavMesh en posición: {transform.position}");
            }
            
            // Configurar velocidad
            agent.speed = chaseSpeed;
            
            // Si estaba persiguiendo, continuar
            if (wasChasing && target != null)
            {
                agent.isStopped = false;
                agent.SetDestination(target.position);
                Debug.Log($"[ChaserAI] 🏃 Persecución retomada hacia: {target.position}");
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        // Mostrar distancia de captura
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);
    }
}
