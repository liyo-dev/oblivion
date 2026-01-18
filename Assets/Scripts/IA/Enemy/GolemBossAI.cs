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
    [SerializeField] private float shockwaveSpeed = 10f;
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

        if (agent)
        {
            agent.speed = walkSpeed;
            agent.isStopped = true;
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
        if (!hasSpawned || isDead || !player) return;

        if (!canStartCombat)
        {
            if (debugMode) Debug.Log("[GolemBossAI] Esperando para iniciar combate (canStartCombat = false)");
            return;
        }

        UpdatePhase();
        UpdateBehavior();
    }

    private IEnumerator SpawnSequence()
    {
        currentState = BossState.Idle;
        PlayAnimation(AnimIdle);
        yield return new WaitForSeconds(1f);
        
        hasSpawned = true;
        
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
                if (agent) agent.speed = chaseSpeed;
                StartCoroutine(PhaseTransitionRoar());
                break;
            
            case BossPhase.Phase3:
                if (agent) agent.speed = chaseSpeed * 1.2f;
                StartCoroutine(PhaseTransitionRoar());
                break;
        }
    }

    private IEnumerator PhaseTransitionRoar()
    {
        isAttacking = true;
        StopMovement();

        PlayAnimation(AnimAttack02);
        
        if (shockwaveVFX)
        {
            var vfx = Instantiate(shockwaveVFX, transform.position, Quaternion.identity);
            Destroy(vfx, 3f);
        }

        yield return new WaitForSeconds(2f);

        isAttacking = false;
        ResumeMovement();
    }

    #endregion

    #region Comportamiento Principal

    private void UpdateBehavior()
    {
        if (isAttacking || currentState == BossState.TakingDamage) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer > detectionRange)
        {
            Idle();
            return;
        }

        LookAtPlayer();

        if (TrySpecialAttacks(distanceToPlayer)) return;

        if (distanceToPlayer <= meleeRange)
        {
            StopMovement();
            DecideMeleeAttack();
        }
        else if (distanceToPlayer >= rangedMinRange && distanceToPlayer <= rangedMaxRange)
        {
            StopMovement();
            DecideRangedAttack();
        }
        else
        {
            ChasePlayer();
        }
    }

    private void DecideMeleeAttack()
    {
        bool canPunch = Time.time >= lastPunchTime + punchCooldown;
        bool canSlam = Time.time >= lastSlamTime + slamCooldown;

        if (canSlam && (currentPhase != BossPhase.Phase1 || Random.value > 0.7f))
        {
            StartCoroutine(SlamAttack());
        }
        else if (canPunch)
        {
            StartCoroutine(PunchAttack());
        }
        else
        {
            Idle();
        }
    }

    private void DecideRangedAttack()
    {
        bool canThrowRock = Time.time >= lastRockThrowTime + rockThrowCooldown;
        bool canRockRain = Time.time >= lastRockRainTime + rockRainCooldown && currentPhase != BossPhase.Phase1;

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
            ChasePlayer();
        }
    }

    private bool TrySpecialAttacks(float distanceToPlayer)
    {
        if (currentPhase == BossPhase.Phase3 && distanceToPlayer <= rangedMaxRange)
        {
            bool canShockwave = Time.time >= lastShockwaveTime + shockwaveCooldown;
            
            if (canShockwave && Random.value > 0.85f)
            {
                StartCoroutine(ShockwaveAttack());
                return true;
            }
        }
        return false;
    }

    private void Idle()
    {
        currentState = BossState.Idle;
        PlayAnimation(AnimIdle);
        StopMovement();
    }

    private void ChasePlayer()
    {
        currentState = BossState.Walking;
        PlayAnimation(AnimWalk);
        ResumeMovement();
        if (agent && agent.isOnNavMesh)
        {
            agent.SetDestination(player.position);
        }
    }

    private void StopMovement()
    {
        if (agent && agent.isOnNavMesh && !agent.isStopped)
        {
            agent.isStopped = true;
        }
    }

    private void ResumeMovement()
    {
        if (agent && agent.isOnNavMesh && agent.isStopped)
        {
            agent.isStopped = false;
        }
    }

    private void LookAtPlayer()
    {
        if (!player) return;
        
        Vector3 lookDir = player.position - transform.position;
        lookDir.y = 0;
        
        if (lookDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }

    #endregion

    #region Ataques

    private IEnumerator PunchAttack()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastPunchTime = Time.time;
        StopMovement();

        if (debugMode) Debug.Log("[GolemBossAI] Ejecutando Puñetazo (Attack01)");

        PlayAnimation(AnimAttack01);
        yield return new WaitForSeconds(0.6f);

        if (player && Vector3.Distance(transform.position, player.position) <= meleeRange * 1.2f)
        {
            DamagePlayer(punchDamage);
        }

        yield return new WaitForSeconds(0.6f);
        isAttacking = false;
    }

    private IEnumerator SlamAttack()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastSlamTime = Time.time;
        StopMovement();

        if (debugMode) Debug.Log("[GolemBossAI] Ejecutando Golpe de Suelo (Attack02)");

        PlayAnimation(AnimAttack02);
        yield return new WaitForSeconds(0.8f);

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, slamRadius, _overlapBuffer, playerLayer);
        for (int i = 0; i < hitCount; i++)
        {
            DamagePlayerInCollider(hitCount > i ? _overlapBuffer[i] : null, slamDamage);
        }

        if (shockwaveVFX)
        {
            var vfx = Instantiate(shockwaveVFX, transform.position + Vector3.up * 0.1f, Quaternion.identity);
            Destroy(vfx, 3f);
        }

        yield return new WaitForSeconds(0.8f);
        isAttacking = false;
    }

    private IEnumerator RockThrowAttack()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastRockThrowTime = Time.time;
        StopMovement();

        if (debugMode) Debug.Log("[GolemBossAI] Ejecutando Lanzar Roca");

        PlayAnimation(AnimAttack01);
        
        if (rockPickupVFX && rockSpawnPoint)
        {
            var vfx = Instantiate(rockPickupVFX, rockSpawnPoint.position, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        yield return new WaitForSeconds(1.0f);

        PlayAnimation(AnimAttack02);
        yield return new WaitForSeconds(0.5f);

        if (rockProjectilePrefab && rockSpawnPoint && player)
        {
            Vector3 targetPos = player.position + Vector3.up * 1f;
            Vector3 direction = (targetPos - rockSpawnPoint.position).normalized;
            
            GameObject rock = Instantiate(rockProjectilePrefab, rockSpawnPoint.position, Quaternion.LookRotation(direction));
            
            var proj = rock.GetComponent<EnemyProjectile>();
            if (proj)
            {
                proj.Initialize(direction * rockSpeed, rockDamage);
            }
            else
            {
                var rb = rock.GetComponent<Rigidbody>();
                if (rb) rb.linearVelocity = direction * rockSpeed;
            }
            
            Destroy(rock, 5f);
            if (debugMode) Debug.Log($"[GolemBossAI] Roca lanzada hacia {player.name}");
        }

        yield return new WaitForSeconds(0.7f);
        isAttacking = false;
    }

    private IEnumerator RockRainAttack()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastRockRainTime = Time.time;
        StopMovement();

        if (debugMode) Debug.Log("[GolemBossAI] Ejecutando Lluvia de Rocas");

        PlayAnimation(AnimAttack02);
        yield return new WaitForSeconds(1f);

        if (player && fallingRockPrefab)
        {
            for (int i = 0; i < rockRainCount; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * rockRainRadius;
                Vector3 spawnPos = player.position + new Vector3(randomOffset.x, 15f, randomOffset.y);
                
                GameObject fallingRock = Instantiate(fallingRockPrefab, spawnPos, Quaternion.identity);
                Destroy(fallingRock, 5f);
                
                yield return new WaitForSeconds(0.2f);
            }
        }

        yield return new WaitForSeconds(1f);
        isAttacking = false;
    }

    private IEnumerator ShockwaveAttack()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastShockwaveTime = Time.time;
        StopMovement();

        if (debugMode) Debug.Log("[GolemBossAI] Ejecutando Onda Sísmica");

        PlayAnimation(AnimAttack02);
        yield return new WaitForSeconds(1.2f);

        if (shockwaveVFX && player)
        {
            Vector3 spawnPos = transform.position + transform.forward * 1.5f + Vector3.up * 0.1f;
            GameObject shockwave = Instantiate(shockwaveVFX, spawnPos, transform.rotation);
            StartCoroutine(MoveShockwave(shockwave, player.position));
            Destroy(shockwave, 5f);
        }

        yield return new WaitForSeconds(1.5f);
        isAttacking = false;
    }

    private IEnumerator MoveShockwave(GameObject shockwave, Vector3 playerPosition)
    {
        if (shockwave == null) yield break;

        Vector3 targetPosition = new Vector3(playerPosition.x, shockwave.transform.position.y, playerPosition.z);
        float duration = Vector3.Distance(shockwave.transform.position, targetPosition) / shockwaveSpeed;
        float elapsedTime = 0f;

        while (elapsedTime < duration && shockwave != null)
        {
            shockwave.transform.position = Vector3.Lerp(shockwave.transform.position, targetPosition, elapsedTime / duration);
            
            int hitCount = Physics.OverlapSphereNonAlloc(shockwave.transform.position, 1.5f, _overlapBuffer, playerLayer);
            for (int i = 0; i < hitCount; i++)
            {
                if (DamagePlayerInCollider(hitCount > i ? _overlapBuffer[i] : null, shockwaveDamage))
                {
                    ApplyKnockback(_overlapBuffer[i].transform);
                }
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    #endregion

    #region Daño y Muerte

    private void OnDamageTaken(float damage)
    {
        if (isDead) return;

        if (debugMode) Debug.Log($"[GolemBossAI] Recibió {damage} de daño");

        if (!isAttacking && Random.value > 0.8f)
        {
            StartCoroutine(TakeDamageReaction());
        }
    }

    private IEnumerator TakeDamageReaction()
    {
        isAttacking = true;
        currentState = BossState.TakingDamage;
        StopMovement();
        
        PlayAnimation(AnimGetHit);
        yield return new WaitForSeconds(0.5f);

        currentState = BossState.Idle;
        isAttacking = false;
    }

    private void OnDeath()
    {
        if (isDead) return;
        
        isDead = true;
        currentState = BossState.Dead;
        
        Debug.Log("[GolemBossAI] ¡Golem derrotado!");

        StopAllCoroutines();
        StopMovement();
        PlayAnimation(AnimDie);
        
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }
        Destroy(gameObject, 10f);
    }

    public void OnPlayerDefeated()
    {
        if (isDead) return;
        
        StopAllCoroutines();
        isAttacking = false;
        StopMovement();
        
        PlayAnimation(AnimVictory);
        Debug.Log("[GolemBossAI] ¡Victoria!");
    }

    #endregion

    #region Utilidades

    private void DamagePlayer(float damage)
    {
        if (!player) return;
        
        var damageable = player.GetComponent<IDamageable>();
        if (damageable != null && damageable.IsAlive)
        {
            damageable.TakeDamage(damage);
            if (debugMode) Debug.Log($"[GolemBossAI] Infligió {damage} de daño (IDamageable)");
        }
    }

    private bool DamagePlayerInCollider(Collider col, float damage)
    {
        if (col == null) return false;

        var damageable = col.GetComponent<IDamageable>();
        if (damageable != null && damageable.IsAlive)
        {
            damageable.TakeDamage(damage);
            if (debugMode) Debug.Log($"[GolemBossAI] Golpeó a {col.name} por {damage}");
            return true;
        }
        return false;
    }

    private void ApplyKnockback(Transform target)
    {
        var rb = target.GetComponent<Rigidbody>();
        if (rb)
        {
            Vector3 knockbackDir = (target.position - transform.position).normalized;
            knockbackDir.y = 0.3f;
            rb.AddForce(knockbackDir * shockwaveKnockback, ForceMode.Impulse);
        }
    }

    private void PlayAnimation(int animHash)
    {
        if (animator && animator.isActiveAndEnabled)
        {
            animator.CrossFade(animHash, 0.1f);
        }
    }

    #endregion

    #region Gizmos

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, rangedMinRange);
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, rangedMaxRange);
        
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, slamRadius);
    }

    #endregion
}
