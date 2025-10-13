using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// IA simple para Spider1 - Enemigo básico débil pero peligroso en grupo
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Damageable))]
public class Spider1AI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;
    [SerializeField] private Damageable damageable;
    [SerializeField] private NavMeshAgent agent;

    [Header("Comportamiento")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float patrolRadius = 5f;
    [SerializeField] private float idleTime = 2f;
    [SerializeField] private bool patrolEnabled = true;

    [Header("Combate")]
    [SerializeField] private float damage = 5f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Configuración")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float chaseSpeedMultiplier = 1.3f;
    [SerializeField] private float modelRotationOffset = 180f; // Offset de rotación del modelo (si anda al revés)

    // Estado interno
    private enum SpiderState { Idle, Patrol, Chasing, Attacking, TakingDamage, Dead }
    private SpiderState currentState = SpiderState.Idle;
    
    private Vector3 spawnPosition;
    private Vector3 patrolTarget;
    private float lastAttackTime = -999f;
    private float idleTimer = 0f;
    private bool isAttacking = false;
    private bool isDead = false;
    private float originalSpeed;

    // Hash de animaciones
    private static readonly int AnimIdle = Animator.StringToHash("Idle");
    private static readonly int AnimWalk = Animator.StringToHash("Walk");
    private static readonly int AnimAttack = Animator.StringToHash("Attack");
    private static readonly int AnimHit = Animator.StringToHash("GetHit");
    private static readonly int AnimDeath = Animator.StringToHash("Death");

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!damageable) damageable = GetComponent<Damageable>();
        if (!agent) agent = GetComponent<NavMeshAgent>();

        // Guardar posición inicial
        spawnPosition = transform.position;

        // Buscar al jugador si no está asignado
        if (!player)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj) player = playerObj.transform;
        }
    }

    void Start()
    {
        if (damageable)
        {
            damageable.OnDamaged += OnDamageTaken;
            damageable.OnDied += OnDeath;
        }

        if (agent)
        {
            agent.speed = moveSpeed;
            originalSpeed = moveSpeed;
        }

        // Iniciar con patrulla o idle
        if (patrolEnabled)
        {
            SetNewPatrolTarget();
        }
        else
        {
            currentState = SpiderState.Idle;
            idleTimer = idleTime;
        }
    }

    void OnDestroy()
    {
        if (damageable)
        {
            damageable.OnDamaged -= OnDamageTaken;
            damageable.OnDied -= OnDeath;
        }
    }

    void Update()
    {
        if (isDead || !player) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Detectar al jugador
        if (distanceToPlayer <= detectionRange && currentState != SpiderState.TakingDamage)
        {
            // Entrar en modo persecución
            if (currentState != SpiderState.Chasing && currentState != SpiderState.Attacking)
            {
                EnterChaseMode();
            }

            // Decidir si atacar o perseguir
            if (distanceToPlayer <= attackRange && !isAttacking)
            {
                StartCoroutine(AttackPlayer());
            }
            else if (distanceToPlayer > attackRange && currentState == SpiderState.Chasing)
            {
                ChasePlayer();
            }
        }
        else if (currentState == SpiderState.Chasing)
        {
            // Perdió al jugador, volver a patrullar
            ExitChaseMode();
        }
        else if (currentState != SpiderState.TakingDamage)
        {
            // Comportamiento de patrulla/idle
            UpdatePatrolBehavior();
        }
    }

    private void UpdatePatrolBehavior()
    {
        if (currentState == SpiderState.Idle)
        {
            idleTimer -= Time.deltaTime;
            if (idleTimer <= 0f)
            {
                if (patrolEnabled)
                {
                    SetNewPatrolTarget();
                }
                else
                {
                    idleTimer = idleTime;
                }
            }

            PlayAnimation(AnimIdle);
        }
        else if (currentState == SpiderState.Patrol)
        {
            if (agent && agent.isOnNavMesh)
            {
                // Verificar si llegó al objetivo
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
                {
                    currentState = SpiderState.Idle;
                    idleTimer = idleTime;
                }
                else
                {
                    // Rotar hacia donde se mueve durante la patrulla
                    if (agent.velocity.sqrMagnitude > 0.1f)
                    {
                        LookAtDirection(agent.velocity.normalized);
                    }
                    PlayAnimation(AnimWalk);
                }
            }
        }
    }

    private void SetNewPatrolTarget()
    {
        // Generar punto aleatorio cerca de la posición de spawn
        Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
        Vector3 randomPoint = spawnPosition + new Vector3(randomCircle.x, 0, randomCircle.y);

        // Buscar el punto más cercano en el NavMesh
        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
        {
            patrolTarget = hit.position;
            
            if (agent && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = originalSpeed;
                agent.SetDestination(patrolTarget);
                currentState = SpiderState.Patrol;
            }
        }
        else
        {
            // Si no encuentra punto, quedarse idle
            currentState = SpiderState.Idle;
            idleTimer = idleTime;
        }
    }

    private void EnterChaseMode()
    {
        currentState = SpiderState.Chasing;
        
        if (agent)
        {
            agent.speed = originalSpeed * chaseSpeedMultiplier;
            agent.isStopped = false;
        }
    }

    private void ExitChaseMode()
    {
        if (agent)
        {
            agent.speed = originalSpeed;
        }

        if (patrolEnabled)
        {
            SetNewPatrolTarget();
        }
        else
        {
            currentState = SpiderState.Idle;
            idleTimer = idleTime;
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
        }
    }

    private void ChasePlayer()
    {
        if (!player || !agent || !agent.isOnNavMesh) return;

        LookAtPlayer();
        agent.isStopped = false;
        agent.SetDestination(player.position);
        PlayAnimation(AnimWalk);
    }

    private IEnumerator AttackPlayer()
    {
        if (Time.time < lastAttackTime + attackCooldown) yield break;

        isAttacking = true;
        currentState = SpiderState.Attacking;
        lastAttackTime = Time.time;

        if (agent && agent.isOnNavMesh) agent.isStopped = true;

        LookAtPlayer();
        PlayAnimation(AnimAttack);

        // Esperar al momento del golpe (mitad de la animación)
        yield return new WaitForSeconds(0.3f);

        // Aplicar daño si el jugador sigue en rango
        if (player && Vector3.Distance(transform.position, player.position) <= attackRange * 1.2f)
        {
            DamagePlayer(damage);
        }

        // Esperar a que termine la animación
        yield return new WaitForSeconds(0.4f);

        isAttacking = false;

        // Volver a perseguir si el jugador sigue cerca
        if (player && Vector3.Distance(transform.position, player.position) <= detectionRange)
        {
            currentState = SpiderState.Chasing;
        }
        else
        {
            ExitChaseMode();
        }
    }

    private void DamagePlayer(float dmg)
    {
        if (!player) return;

        // Intentar con PlayerHealthSystem primero
        var playerHealth = player.GetComponent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(dmg);
            Debug.Log($"[Spider1AI] Infligió {dmg} de daño al jugador");
            return;
        }

        // Fallback a IDamageable
        var damageable = player.GetComponent<IDamageable>();
        if (damageable != null && damageable.IsAlive)
        {
            damageable.TakeDamage(dmg);
            Debug.Log($"[Spider1AI] Infligió {dmg} de daño al jugador");
        }
    }

    private void LookAtPlayer()
    {
        if (!player) return;

        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            // Aplicar offset de rotación si el modelo está al revés
            Quaternion targetRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, modelRotationOffset, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 8f);
        }
    }

    private void LookAtDirection(Vector3 direction)
    {
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            // Aplicar offset de rotación si el modelo está al revés
            Quaternion targetRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, modelRotationOffset, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 8f);
        }
    }

    private void PlayAnimation(int animHash)
    {
        if (!animator) return;
        animator.CrossFade(animHash, 0.1f);
    }

    private void OnDamageTaken(float amount)
    {
        if (isDead) return;

        // Activar persecución al recibir daño
        if (currentState != SpiderState.Chasing && player)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer <= detectionRange * 2f) // Rango extendido al recibir daño
            {
                EnterChaseMode();
            }
        }

        StartCoroutine(TakeDamageSequence());
    }

    private IEnumerator TakeDamageSequence()
    {
        SpiderState previousState = currentState;
        currentState = SpiderState.TakingDamage;

        PlayAnimation(AnimHit);
        yield return new WaitForSeconds(0.2f);

        if (!isDead)
        {
            currentState = previousState;
        }
    }

    private void OnDeath()
    {
        if (isDead) return;

        isDead = true;
        currentState = SpiderState.Dead;

        if (agent && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        StopAllCoroutines();
        PlayAnimation(AnimDeath);

        // Desactivar colisiones después de un momento
        StartCoroutine(DisableAfterDeath());
    }

    private IEnumerator DisableAfterDeath()
    {
        yield return new WaitForSeconds(1f);

        // Desactivar colisiones
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        // Destruir después de un tiempo
        yield return new WaitForSeconds(3f);
        Destroy(gameObject);
    }

    // Debug visual
    void OnDrawGizmosSelected()
    {
        // Rango de detección
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Rango de ataque
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Radio de patrulla (desde spawn)
        if (Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(spawnPosition, patrolRadius);
        }
        else
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, patrolRadius);
        }
    }
}
