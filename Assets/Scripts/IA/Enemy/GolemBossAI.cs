using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// IA del Boss Golem - Un coloso lento pero devastador con ataques a distancia.
/// Estados del Animator: Idle, Walk, Attack01, Attack02, GetHit, Die, Victory
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Damageable))]
public class GolemBossAI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;
    [SerializeField] private Damageable damageable;
    [SerializeField] private NavMeshAgent agent;

    [Header("Configuración General")]
    [SerializeField] private float detectionRange = 25f;
    [SerializeField] private float meleeRange = 4f;
    [SerializeField] private float rangedMinRange = 8f;
    [SerializeField] private float rangedMaxRange = 20f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Ataques Cuerpo a Cuerpo")]
    [Tooltip("Daño del puñetazo (Attack01)")]
    [SerializeField] private float punchDamage = 25f;
    [Tooltip("Daño del golpe de suelo (Attack02)")]
    [SerializeField] private float slamDamage = 35f;
    [SerializeField] private float slamRadius = 5f;

    [Header("Ataques a Distancia - Lanzar Roca")]
    [SerializeField] private GameObject rockProjectilePrefab;
    [SerializeField] private Transform rockSpawnPoint;
    [SerializeField] private float rockDamage = 20f;
    [SerializeField] private float rockSpeed = 15f;
    [Tooltip("VFX de arrancar la roca del suelo")]
    [SerializeField] private GameObject rockPickupVFX;

    [Header("Ataques a Distancia - Lluvia de Rocas (Fase 2+)")]
    [SerializeField] private GameObject fallingRockPrefab;
    [SerializeField] private int rockRainCount = 5;
    [SerializeField] private float rockRainRadius = 8f;
    [SerializeField] private float rockRainDamage = 15f;

    [Header("Ataque Especial - Onda Sísmica (Fase 3)")]
    [SerializeField] private GameObject shockwaveVFX;
    [SerializeField] private float shockwaveDamage = 40f;
    [SerializeField] private float shockwaveRadius = 10f;
    [SerializeField] private float shockwaveKnockback = 15f;

    [Header("Cooldowns")]
    [SerializeField] private float punchCooldown = 2f;
    [SerializeField] private float slamCooldown = 4f;
    [SerializeField] private float rockThrowCooldown = 3f;
    [SerializeField] private float rockRainCooldown = 12f;
    [SerializeField] private float shockwaveCooldown = 15f;

    [Header("Fases")]
    [SerializeField] private float phase2HealthPercent = 0.66f;
    [SerializeField] private float phase3HealthPercent = 0.33f;

    [Header("Movimiento")]
    [SerializeField] private float walkSpeed = 2.5f;
    [SerializeField] private float chaseSpeed = 3.5f;

    [Header("Combat Control")]
    [Tooltip("Permite iniciar el combate. Se activa externamente después de la presentación.")]
    public bool canStartCombat = false;

    [Header("DEBUG")]
    [SerializeField] private bool debugMode = false;

    // Estado interno
    private enum BossPhase { Phase1, Phase2, Phase3 }
    private enum BossState { Idle, Walking, Attacking, TakingDamage, Dead }
    
    private BossPhase currentPhase = BossPhase.Phase1;
    private BossState currentState = BossState.Idle;
    
    private float lastPunchTime = -999f;
    private float lastSlamTime = -999f;
    private float lastRockThrowTime = -999f;
    private float lastRockRainTime = -999f;
    private float lastShockwaveTime = -999f;
    
    private bool isAttacking = false;
    private bool hasSpawned = false;
    private bool isDead = false;

    // Buffer para OverlapSphere (evita allocations)
    private static Collider[] _overlapBuffer = new Collider[16];

    // Hashes de animaciones
    private static readonly int AnimIdle = Animator.StringToHash("Idle");
    private static readonly int AnimWalk = Animator.StringToHash("Walk");
    private static readonly int AnimAttack01 = Animator.StringToHash("Attack01");
    private static readonly int AnimAttack02 = Animator.StringToHash("Attack02");
    private static readonly int AnimGetHit = Animator.StringToHash("GetHit");
    private static readonly int AnimDie = Animator.StringToHash("Die");
    private static readonly int AnimVictory = Animator.StringToHash("Victory");

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!damageable) damageable = GetComponent<Damageable>();
        if (!agent) agent = GetComponent<NavMeshAgent>();
        
        if (!player && PlayerService.Player != null)
        {
            player = PlayerService.Player.transform;
        }
    }

    void Start()
    {
        if (damageable)
        {
            damageable.OnDamaged += OnDamageTaken;
            damageable.OnDied += OnDeath;
        }

        // Configurar velocidad inicial
        if (agent)
        {
            agent.speed = walkSpeed;
        }

        StartCoroutine(SpawnSequence());
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
        if (!hasSpawned || isDead || !player || !canStartCombat) return;

        UpdatePhase();
        UpdateBehavior();
    }

    private IEnumerator SpawnSequence()
    {
        currentState = BossState.Idle;
        
        if (agent && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
        
        PlayAnimation(AnimIdle);
        yield return new WaitForSeconds(1f);
        
        hasSpawned = true;
        
        if (agent && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
        
        if (debugMode) Debug.Log("[GolemBossAI] Spawn completado, listo para combate");
    }

    #region Fases

    private void UpdatePhase()
    {
        if (!damageable) return;

        float healthPercent = damageable.Current / damageable.Max;
        BossPhase newPhase = currentPhase;

        if (healthPercent <= phase3HealthPercent)
            newPhase = BossPhase.Phase3;
        else if (healthPercent <= phase2HealthPercent)
            newPhase = BossPhase.Phase2;
        else
            newPhase = BossPhase.Phase1;

        if (newPhase != currentPhase)
        {
            currentPhase = newPhase;
            OnPhaseChanged();
        }
    }

    private void OnPhaseChanged()
    {
        Debug.Log($"[GolemBossAI] Cambiando a {currentPhase}");
        
        switch (currentPhase)
        {
            case BossPhase.Phase2:
                // Más agresivo, habilita lluvia de rocas
                if (agent) agent.speed = chaseSpeed;
                StartCoroutine(PhaseTransitionRoar());
                break;
            
            case BossPhase.Phase3:
                // Modo furia, habilita onda sísmica
                if (agent) agent.speed = chaseSpeed * 1.2f;
                StartCoroutine(PhaseTransitionRoar());
                break;
        }
    }

    private IEnumerator PhaseTransitionRoar()
    {
        isAttacking = true;
        if (agent && agent.isOnNavMesh) agent.isStopped = true;

        // Rugido/Grito de transición usando Attack02 (golpe de suelo)
        PlayAnimation(AnimAttack02);
        
        // Pequeña onda de choque visual
        if (shockwaveVFX)
        {
            Instantiate(shockwaveVFX, transform.position, Quaternion.identity);
        }

        yield return new WaitForSeconds(2f);

        isAttacking = false;
        if (agent && agent.isOnNavMesh) agent.isStopped = false;
    }

    #endregion

    #region Comportamiento Principal

    private void UpdateBehavior()
    {
        if (isAttacking || currentState == BossState.TakingDamage) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Fuera de rango de detección
        if (distanceToPlayer > detectionRange)
        {
            currentState = BossState.Idle;
            PlayAnimation(AnimIdle);
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        // Mirar hacia el jugador
        LookAtPlayer();

        // Decidir acción según distancia y fase
        if (distanceToPlayer <= meleeRange)
        {
            // Ataque cuerpo a cuerpo
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
            DecideMeleeAttack();
        }
        else if (distanceToPlayer >= rangedMinRange && distanceToPlayer <= rangedMaxRange)
        {
            // Ataque a distancia (preferido por el Golem)
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
            DecideRangedAttack();
        }
        else if (distanceToPlayer < rangedMinRange)
        {
            // Demasiado cerca para ataque a distancia, retroceder o atacar cuerpo a cuerpo
            if (CanMeleeAttack())
            {
                DecideMeleeAttack();
            }
            else
            {
                // Retroceder un poco
                MoveAwayFromPlayer();
            }
        }
        else
        {
            // Perseguir al jugador
            ChasePlayer();
        }

        // Ataques especiales según fase
        TrySpecialAttacks(distanceToPlayer);
    }

    private void DecideMeleeAttack()
    {
        bool canPunch = Time.time >= lastPunchTime + punchCooldown;
        bool canSlam = Time.time >= lastSlamTime + slamCooldown;

        if (canSlam && (currentPhase != BossPhase.Phase1 || Random.value > 0.7f))
        {
            // En fase 2+ priorizar slam, en fase 1 es menos frecuente
            StartCoroutine(SlamAttack());
        }
        else if (canPunch)
        {
            StartCoroutine(PunchAttack());
        }
        else
        {
            PlayAnimation(AnimIdle);
        }
    }

    private void DecideRangedAttack()
    {
        bool canThrowRock = Time.time >= lastRockThrowTime + rockThrowCooldown;
        bool canRockRain = Time.time >= lastRockRainTime + rockRainCooldown && currentPhase != BossPhase.Phase1;

        // En fase 2+ puede hacer lluvia de rocas
        if (canRockRain && Random.value > 0.6f)
        {
            StartCoroutine(RockRainAttack());
        }
        else if (canThrowRock)
        {
            StartCoroutine(RockThrowAttack());
        }
        else
        {
            // Si no puede atacar a distancia, acercarse
            ChasePlayer();
        }
    }

    private void TrySpecialAttacks(float distanceToPlayer)
    {
        // Onda sísmica solo en fase 3 y cuando el jugador está relativamente cerca
        if (currentPhase == BossPhase.Phase3 && distanceToPlayer <= shockwaveRadius)
        {
            bool canShockwave = Time.time >= lastShockwaveTime + shockwaveCooldown;
            
            if (canShockwave && Random.value > 0.85f)
            {
                StartCoroutine(ShockwaveAttack());
            }
        }
    }

    private bool CanMeleeAttack()
    {
        return Time.time >= lastPunchTime + punchCooldown || 
               Time.time >= lastSlamTime + slamCooldown;
    }

    private void ChasePlayer()
    {
        currentState = BossState.Walking;
        if (agent && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
        PlayAnimation(AnimWalk);
    }

    private void MoveAwayFromPlayer()
    {
        currentState = BossState.Walking;
        Vector3 awayDirection = (transform.position - player.position).normalized;
        Vector3 targetPosition = transform.position + awayDirection * 5f;
        
        if (agent && agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
            }
        }
        PlayAnimation(AnimWalk);
    }

    private void LookAtPlayer()
    {
        if (!player) return;
        
        Vector3 lookDir = player.position - transform.position;
        lookDir.y = 0;
        
        if (lookDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 3f);
        }
    }

    #endregion

    #region Ataques Cuerpo a Cuerpo

    private IEnumerator PunchAttack()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastPunchTime = Time.time;

        if (debugMode) Debug.Log("[GolemBossAI] Ejecutando Puñetazo (Attack01)");

        PlayAnimation(AnimAttack01);
        yield return new WaitForSeconds(0.6f); // Momento del impacto

        // Aplicar daño si el jugador está en rango
        if (player && Vector3.Distance(transform.position, player.position) <= meleeRange * 1.2f)
        {
            DamagePlayer(punchDamage);
        }

        yield return new WaitForSeconds(0.6f); // Recuperación
        isAttacking = false;
    }

    private IEnumerator SlamAttack()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastSlamTime = Time.time;

        if (debugMode) Debug.Log("[GolemBossAI] Ejecutando Golpe de Suelo (Attack02)");

        PlayAnimation(AnimAttack02);
        yield return new WaitForSeconds(0.8f); // Momento del impacto

        // Daño en área
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, slamRadius, _overlapBuffer, playerLayer);
        for (int i = 0; i < hitCount; i++)
        {
            var hit = _overlapBuffer[i];
            var playerHealth = hit.GetComponent<PlayerHealthSystem>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(slamDamage);
                if (debugMode) Debug.Log($"[GolemBossAI] Slam golpeó al jugador por {slamDamage}");
            }
        }

        // Efecto visual del impacto
        if (shockwaveVFX)
        {
            Instantiate(shockwaveVFX, transform.position + Vector3.up * 0.1f, Quaternion.identity);
        }

        yield return new WaitForSeconds(0.8f); // Recuperación más lenta por el golpe pesado
        isAttacking = false;
    }

    #endregion

    #region Ataques a Distancia

    private IEnumerator RockThrowAttack()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastRockThrowTime = Time.time;

        if (debugMode) Debug.Log("[GolemBossAI] Ejecutando Lanzar Roca");

        // Fase 1: Arrancar roca (usar Attack01 para la animación de preparación)
        PlayAnimation(AnimAttack01);
        
        // VFX de arrancar roca
        if (rockPickupVFX && rockSpawnPoint)
        {
            Instantiate(rockPickupVFX, rockSpawnPoint.position, Quaternion.identity);
        }

        yield return new WaitForSeconds(0.5f); // Preparación

        // Fase 2: Lanzar la roca
        if (rockProjectilePrefab && rockSpawnPoint && player)
        {
            Vector3 targetPos = player.position + Vector3.up * 1f; // Apuntar al centro del jugador
            Vector3 direction = (targetPos - rockSpawnPoint.position).normalized;
            
            GameObject rock = Instantiate(rockProjectilePrefab, rockSpawnPoint.position, Quaternion.LookRotation(direction));
            
            // Configurar el proyectil
            var proj = rock.GetComponent<EnemyProjectile>();
            if (proj)
            {
                proj.Initialize(direction * rockSpeed, rockDamage);
            }
            else
            {
                // Si no tiene EnemyProjectile, mover manualmente con Rigidbody
                var rb = rock.GetComponent<Rigidbody>();
                if (rb)
                {
                    rb.linearVelocity = direction * rockSpeed;
                }
            }
            
            if (debugMode) Debug.Log($"[GolemBossAI] Roca lanzada hacia {player.name}");
        }

        yield return new WaitForSeconds(0.7f); // Recuperación
        isAttacking = false;
    }

    private IEnumerator RockRainAttack()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastRockRainTime = Time.time;

        if (debugMode) Debug.Log("[GolemBossAI] Ejecutando Lluvia de Rocas");

        // Animación de invocación (levantar brazos) - usar Attack02
        PlayAnimation(AnimAttack02);
        yield return new WaitForSeconds(1f);

        // Hacer caer rocas en área alrededor del jugador
        if (player && fallingRockPrefab)
        {
            for (int i = 0; i < rockRainCount; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * rockRainRadius;
                Vector3 spawnPos = player.position + new Vector3(randomOffset.x, 15f, randomOffset.y);
                Vector3 targetPos = player.position + new Vector3(randomOffset.x, 0.5f, randomOffset.y);
                
                GameObject fallingRock = Instantiate(fallingRockPrefab, spawnPos, Quaternion.identity);
                
                // Configurar la roca que cae
                StartCoroutine(FallingRockBehavior(fallingRock, targetPos));
                
                yield return new WaitForSeconds(0.2f); // Pequeño delay entre rocas
            }
        }

        yield return new WaitForSeconds(1f);
        isAttacking = false;
    }

    private IEnumerator FallingRockBehavior(GameObject rock, Vector3 targetPos)
    {
        if (rock == null) yield break;

        float fallSpeed = 20f;
        float startTime = Time.time;
        float maxDuration = 3f;

        while (rock != null && Time.time - startTime < maxDuration)
        {
            rock.transform.position = Vector3.MoveTowards(rock.transform.position, targetPos, fallSpeed * Time.deltaTime);
            
            // Verificar si llegó al suelo
            if (Vector3.Distance(rock.transform.position, targetPos) < 0.5f)
            {
                // Impacto
                int hitCount = Physics.OverlapSphereNonAlloc(targetPos, 2f, _overlapBuffer, playerLayer);
                for (int i = 0; i < hitCount; i++)
                {
                    var hit = _overlapBuffer[i];
                    var playerHealth = hit.GetComponent<PlayerHealthSystem>();
                    if (playerHealth != null)
                    {
                        playerHealth.TakeDamage(rockRainDamage);
                    }
                }
                
                // Destruir roca con efecto
                Destroy(rock);
                yield break;
            }
            
            yield return null;
        }

        // Si pasó el tiempo máximo, destruir
        if (rock != null)
        {
            Destroy(rock);
        }
    }

    #endregion

    #region Ataque Especial

    private IEnumerator ShockwaveAttack()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastShockwaveTime = Time.time;

        if (debugMode) Debug.Log("[GolemBossAI] Ejecutando Onda Sísmica");

        // Cargar el ataque
        PlayAnimation(AnimAttack02);
        yield return new WaitForSeconds(1.2f);

        // Crear onda sísmica
        if (shockwaveVFX)
        {
            Instantiate(shockwaveVFX, transform.position, Quaternion.identity);
        }

        // Daño y knockback en área
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, shockwaveRadius, _overlapBuffer, playerLayer);
        for (int i = 0; i < hitCount; i++)
        {
            var hit = _overlapBuffer[i];
            var playerHealth = hit.GetComponent<PlayerHealthSystem>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(shockwaveDamage);
                
                // Aplicar knockback
                var rb = hit.GetComponent<Rigidbody>();
                if (rb)
                {
                    Vector3 knockbackDir = (hit.transform.position - transform.position).normalized;
                    knockbackDir.y = 0.3f; // Pequeño impulso hacia arriba
                    rb.AddForce(knockbackDir * shockwaveKnockback, ForceMode.Impulse);
                }
                
                if (debugMode) Debug.Log($"[GolemBossAI] Onda sísmica golpeó por {shockwaveDamage} + knockback");
            }
        }

        yield return new WaitForSeconds(1.5f); // Recuperación larga
        isAttacking = false;
    }

    #endregion

    #region Daño y Muerte

    private void OnDamageTaken(float damage)
    {
        if (isDead || isAttacking) return;

        if (debugMode) Debug.Log($"[GolemBossAI] Recibió {damage} de daño");

        // Pequeña probabilidad de interrupción por daño (el Golem es resistente)
        if (Random.value > 0.8f)
        {
            StartCoroutine(TakeDamageReaction());
        }
    }

    private IEnumerator TakeDamageReaction()
    {
        var previousState = currentState;
        currentState = BossState.TakingDamage;
        
        PlayAnimation(AnimGetHit);
        
        if (agent && agent.isOnNavMesh) agent.isStopped = true;

        yield return new WaitForSeconds(0.5f);

        currentState = previousState;
        if (agent && agent.isOnNavMesh) agent.isStopped = false;
    }

    private void OnDeath()
    {
        if (isDead) return;
        
        isDead = true;
        currentState = BossState.Dead;
        
        Debug.Log("[GolemBossAI] ¡Golem derrotado!");

        if (agent && agent.isOnNavMesh) agent.isStopped = true;
        
        StopAllCoroutines();
        PlayAnimation(AnimDie);
        
        // Desactivar colisiones
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }
    }

    /// <summary>
    /// Llamar cuando el jugador muere para que el Golem celebre.
    /// </summary>
    public void OnPlayerDefeated()
    {
        if (isDead) return;
        
        StopAllCoroutines();
        isAttacking = false;
        
        if (agent && agent.isOnNavMesh) agent.isStopped = true;
        
        PlayAnimation(AnimVictory);
        Debug.Log("[GolemBossAI] ¡Victoria!");
    }

    #endregion

    #region Utilidades

    private void DamagePlayer(float damage)
    {
        if (!player) return;
        
        var playerHealth = player.GetComponent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            if (debugMode) Debug.Log($"[GolemBossAI] Infligió {damage} de daño al jugador");
            return;
        }
        
        var damageable = player.GetComponent<IDamageable>();
        if (damageable != null && damageable.IsAlive)
        {
            damageable.TakeDamage(damage);
            if (debugMode) Debug.Log($"[GolemBossAI] Infligió {damage} de daño (IDamageable)");
        }
    }

    private void PlayAnimation(int animHash)
    {
        if (animator && animator.isActiveAndEnabled)
        {
            animator.CrossFade(animHash, 0.1f);
        }
    }

    private int AnimatorLayerContainingState(int stateHash)
    {
        if (animator == null) return -1;
        
        for (int i = 0; i < animator.layerCount; i++)
        {
            if (animator.HasState(i, stateHash))
                return i;
        }
        return -1;
    }

    #endregion

    #region Gizmos

    void OnDrawGizmosSelected()
    {
        // Rango de detección
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        // Rango de melee
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);
        
        // Rango mínimo de ataque a distancia
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, rangedMinRange);
        
        // Rango máximo de ataque a distancia
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, rangedMaxRange);
        
        // Radio del slam
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, slamRadius);
        
        // Radio de la onda sísmica
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, shockwaveRadius);
    }

    #endregion
}

