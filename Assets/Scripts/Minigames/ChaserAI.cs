using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// IA simple de persecución para el minijuego "Pilla Pilla".
/// Persigue al jugador constantemente usando NavMeshAgent.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class ChaserAI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform target;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

    [Header("Configuración")]
    [SerializeField] private float chaseSpeed = 5f;
    [SerializeField] private float catchDistance = 1.2f;
    [SerializeField] private float updatePathInterval = 0.2f;

    [Header("Animación")]
    [SerializeField] private string runAnimParam = "IsRunning";
    
    // Evento cuando atrapa al jugador
    public System.Action OnCaughtPlayer;

    private bool isChasing = false;
    private float lastPathUpdate;
    private Vector3 startPosition;
    private Quaternion startRotation;

    void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponent<Animator>();
        
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void Start()
    {
        // Buscar al jugador si no está asignado
        if (!target)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj) target = playerObj.transform;
        }

        if (agent)
        {
            agent.speed = chaseSpeed;
            agent.isStopped = true;
        }
    }

    void Update()
    {
        if (!isChasing || !target || !agent) return;

        // Actualizar destino periódicamente
        if (Time.time - lastPathUpdate >= updatePathInterval)
        {
            agent.SetDestination(target.position);
            lastPathUpdate = Time.time;
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
        if (!target)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj) target = playerObj.transform;
        }

        isChasing = true;
        
        if (agent)
        {
            agent.isStopped = false;
            agent.SetDestination(target.position);
        }

        if (animator && !string.IsNullOrEmpty(runAnimParam))
        {
            animator.SetBool(runAnimParam, true);
        }

        Debug.Log("[ChaserAI] ¡Comenzó la persecución!");
    }

    /// <summary>
    /// Detiene la persecución
    /// </summary>
    public void StopChasing()
    {
        isChasing = false;

        if (agent)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (animator && !string.IsNullOrEmpty(runAnimParam))
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
            agent.Warp(startPosition);
        }
        else
        {
            transform.position = startPosition;
        }
        
        transform.rotation = startRotation;
        Debug.Log("[ChaserAI] Reiniciado a posición inicial.");
    }

    /// <summary>
    /// Establece una nueva posición inicial
    /// </summary>
    public void SetStartPosition(Vector3 position, Quaternion rotation)
    {
        startPosition = position;
        startRotation = rotation;
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
    public bool IsChasing => isChasing;

    void OnDrawGizmosSelected()
    {
        // Mostrar distancia de captura
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);
    }
}
