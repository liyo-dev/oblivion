using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Damageable))]
public class ImpDemonAI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;
    [SerializeField] private Damageable damageable;
    [SerializeField] private NavMeshAgent agent;

    [Header("Configuración General")]
    [SerializeField] private float detectionRange = 20f;
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private float projectileRange = 10f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Ataques")]
    [SerializeField] private float slashDamage = 15f;
    [SerializeField] private float stabDamage = 20f;
    [SerializeField] private float projectileDamage = 10f;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private GameObject spellEffectPrefab;

    [Header("Cooldowns")]
    [SerializeField] private float slashCooldown = 2f;
    [SerializeField] private float stabCooldown = 3f;
    [SerializeField] private float projectileCooldown = 4f;
    [SerializeField] private float spellCooldown = 8f;
    [SerializeField] private float undergroundCooldown = 15f;

    [Header("Fases")]
    [SerializeField] private float phase2HealthPercent = 0.66f; // 66%
    [SerializeField] private float phase3HealthPercent = 0.33f; // 33%

    // Estado interno
    private enum BossPhase { Phase1, Phase2, Phase3 }
    private enum BossState { Idle, Chasing, Attacking, CastingSpell, Underground, TakingDamage, Dead }
    
    private BossPhase currentPhase = BossPhase.Phase1;
    private BossState currentState = BossState.Idle;
    private float lastSlashTime = -999f;
    private float lastStabTime = -999f;
    private float lastProjectileTime = -999f;
    private float lastSpellTime = -999f;
    private float lastUndergroundTime = -999f;
    private bool isAttacking = false;
    private bool hasSpawned = false;
    private bool isDead = false;

    // Hash de animaciones para optimización
    private static readonly int AnimIdle = Animator.StringToHash("Idle");
    private static readonly int AnimFlyForward = Animator.StringToHash("Fly Forward");
    private static readonly int AnimSlashAttack = Animator.StringToHash("Slash Attack");
    private static readonly int AnimStabAttack = Animator.StringToHash("Stab Attack");
    private static readonly int AnimProjectileAttack = Animator.StringToHash("Projectile Attack");
    private static readonly int AnimCastSpell = Animator.StringToHash("Cast Spell");
    private static readonly int AnimUnderground = Animator.StringToHash("Underground");
    private static readonly int AnimTakeDamage = Animator.StringToHash("Take Damage");
    private static readonly int AnimDie = Animator.StringToHash("Die");
    private static readonly int AnimSpawn = Animator.StringToHash("Spawn");

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!damageable) damageable = GetComponent<Damageable>();
        if (!agent) agent = GetComponent<NavMeshAgent>();
        
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

        // Iniciar con animación de spawn
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

        UpdatePhase();
        UpdateBehavior();
    }

    private IEnumerator SpawnSequence()
    {
        currentState = BossState.Idle;
        
        // Solo detener el agent si está activo y en el NavMesh
        if (agent && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
        
        PlayAnimation(AnimSpawn);
        yield return new WaitForSeconds(2f); // Duración aproximada de la animación de spawn
        
        hasSpawned = true;
        
        // Solo reactivar el agent si está en el NavMesh
        if (agent && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }

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
        Debug.Log($"[ImpDemonAI] Cambiando a {currentPhase}");
        
        switch (currentPhase)
        {
            case BossPhase.Phase2:
                // Fase 2: más agresivo, permite proyectiles
                if (agent) agent.speed *= 1.2f;
                StartCoroutine(PhaseTransitionEffect());
                break;
            
            case BossPhase.Phase3:
                // Fase 3: modo berserk
                if (agent) agent.speed *= 1.3f;
                StartCoroutine(PhaseTransitionEffect());
                break;
        }
    }

    private IEnumerator PhaseTransitionEffect()
    {
        currentState = BossState.CastingSpell;
        isAttacking = true;
        if (agent && agent.isOnNavMesh) agent.isStopped = true;

        PlayAnimation(AnimCastSpell);
        
        // Efecto visual de transición
        if (spellEffectPrefab && projectileSpawnPoint)
        {
            Instantiate(spellEffectPrefab, projectileSpawnPoint.position, Quaternion.identity);
        }

        yield return new WaitForSeconds(1.5f);

        isAttacking = false;
        if (agent && agent.isOnNavMesh) agent.isStopped = false;
    }

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

        // Decidir acción según fase y distancia
        if (distanceToPlayer <= attackRange)
        {
            // Rango de ataque cuerpo a cuerpo
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
            DecideMeleeAttack();
        }
        else if (distanceToPlayer <= projectileRange && currentPhase != BossPhase.Phase1)
        {
            // Rango de proyectiles (solo fase 2 y 3)
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
            DecideRangedAttack();
        }
        else
        {
            // Perseguir al jugador
            currentState = BossState.Chasing;
            if (agent && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(player.position);
            }
            PlayAnimation(AnimFlyForward);
        }

        // Ataques especiales de fase 3
        if (currentPhase == BossPhase.Phase3)
        {
            TrySpecialAttacks();
        }
    }

    private void DecideMeleeAttack()
    {
        bool canSlash = Time.time >= lastSlashTime + slashCooldown;
        bool canStab = Time.time >= lastStabTime + stabCooldown;

        if (canSlash && canStab)
        {
            // Elegir aleatoriamente
            if (Random.value > 0.5f)
                StartCoroutine(SlashAttack());
            else
                StartCoroutine(StabAttack());
        }
        else if (canSlash)
        {
            StartCoroutine(SlashAttack());
        }
        else if (canStab)
        {
            StartCoroutine(StabAttack());
        }
        else
        {
            PlayAnimation(AnimIdle);
        }
    }

    private void DecideRangedAttack()
    {
        bool canProjectile = Time.time >= lastProjectileTime + projectileCooldown;
        bool canSpell = Time.time >= lastSpellTime + spellCooldown && currentPhase == BossPhase.Phase3;

        if (canSpell && Random.value > 0.7f)
        {
            StartCoroutine(CastSpellAttack());
        }
        else if (canProjectile)
        {
            StartCoroutine(ProjectileAttack());
        }
        else
        {
            PlayAnimation(AnimIdle);
        }
    }

    private void TrySpecialAttacks()
    {
        bool canUnderground = Time.time >= lastUndergroundTime + undergroundCooldown;
        
        if (canUnderground && Random.value > 0.9f)
        {
            StartCoroutine(UndergroundAttack());
        }
    }

    // ========== ATAQUES ==========

    private IEnumerator SlashAttack()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastSlashTime = Time.time;

        PlayAnimation(AnimSlashAttack);
        yield return new WaitForSeconds(0.5f); // Momento del impacto

        // Aplicar daño si el jugador está en rango
        if (Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            DamagePlayer(slashDamage);
        }

        yield return new WaitForSeconds(0.5f); // Recuperación
        isAttacking = false;
    }

    private IEnumerator StabAttack()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastStabTime = Time.time;

        PlayAnimation(AnimStabAttack);
        yield return new WaitForSeconds(0.6f); // Momento del impacto

        // Aplicar daño si el jugador está en rango
        if (Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            DamagePlayer(stabDamage);
        }

        yield return new WaitForSeconds(0.4f); // Recuperación
        isAttacking = false;
    }

    private IEnumerator ProjectileAttack()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastProjectileTime = Time.time;

        PlayAnimation(AnimProjectileAttack);
        yield return new WaitForSeconds(0.5f); // Momento de lanzamiento

        // Lanzar proyectil
        if (projectilePrefab && projectileSpawnPoint)
        {
            Vector3 direction = (player.position - projectileSpawnPoint.position).normalized;
            GameObject projectile = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.LookRotation(direction));
            
            // Configurar el proyectil (asumiendo que tiene un componente para daño)
            var proj = projectile.GetComponent<DemonProjectile>();
            if (proj) proj.Initialize(direction, projectileDamage);
        }

        yield return new WaitForSeconds(0.5f);
        isAttacking = false;
    }

    private IEnumerator CastSpellAttack()
    {
        isAttacking = true;
        currentState = BossState.CastingSpell;
        lastSpellTime = Time.time;

        PlayAnimation(AnimCastSpell);
        yield return new WaitForSeconds(1f); // Casteo del hechizo

        // Crear efecto de hechizo
        if (spellEffectPrefab && player)
        {
            // Invocar efecto en la posición del jugador
            Instantiate(spellEffectPrefab, player.position, Quaternion.identity);
            
            // Daño en área
            Collider[] hits = Physics.OverlapSphere(player.position, 5f, playerLayer);
            foreach (var hit in hits)
            {
                var dmg = hit.GetComponent<IDamageable>();
                if (dmg != null && dmg.IsAlive)
                {
                    dmg.TakeDamage(projectileDamage * 1.5f);
                }
            }
        }

        yield return new WaitForSeconds(0.5f);
        isAttacking = false;
    }

    private IEnumerator UndergroundAttack()
    {
        isAttacking = true;
        currentState = BossState.Underground;
        lastUndergroundTime = Time.time;

        PlayAnimation(AnimUnderground);
        if (agent && agent.isOnNavMesh) agent.isStopped = true;

        yield return new WaitForSeconds(1f); // Tiempo bajo tierra

        // Teletransportar cerca del jugador
        Vector3 newPosition = player.position + (Random.insideUnitSphere * 3f);
        newPosition.y = transform.position.y; // Mantener altura
        
        if (NavMesh.SamplePosition(newPosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
        }

        // Reaparecer con ataque
        PlayAnimation(AnimSpawn);
        yield return new WaitForSeconds(0.5f);

        // Ataque sorpresa
        if (Vector3.Distance(transform.position, player.position) <= attackRange * 1.5f)
        {
            DamagePlayer(stabDamage * 1.5f);
        }

        yield return new WaitForSeconds(0.5f);
        if (agent && agent.isOnNavMesh) agent.isStopped = false;
        isAttacking = false;
    }

    // ========== UTILIDADES ==========

    private void DamagePlayer(float damage)
    {
        if (!player) return;
        
        // Intentar primero con PlayerHealthSystem (sistema específico del jugador)
        var playerHealth = player.GetComponent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            Debug.Log($"[ImpDemonAI] Infligió {damage} de daño al jugador (PlayerHealthSystem)");
            return;
        }
        
        // Si no tiene PlayerHealthSystem, intentar con IDamageable
        var damageable = player.GetComponent<IDamageable>();
        if (damageable != null && damageable.IsAlive)
        {
            damageable.TakeDamage(damage);
            Debug.Log($"[ImpDemonAI] Infligió {damage} de daño al jugador (IDamageable)");
            return;
        }
        
        Debug.LogWarning("[ImpDemonAI] El jugador no tiene un sistema de salud compatible!");
    }

    private void LookAtPlayer()
    {
        if (!player) return;
        
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0; // Solo rotación horizontal
        
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }

    private void PlayAnimation(int animHash)
    {
        if (!animator) return;
        animator.Play(animHash);
    }

    private void OnDamageTaken(float amount)
    {
        if (isAttacking || isDead) return;
        
        StartCoroutine(TakeDamageSequence());
    }

    private IEnumerator TakeDamageSequence()
    {
        currentState = BossState.TakingDamage;
        bool wasAttacking = isAttacking;
        
        PlayAnimation(AnimTakeDamage);
        yield return new WaitForSeconds(0.3f);
        
        if (!wasAttacking)
        {
            currentState = BossState.Idle;
        }
    }

    private void OnDeath()
    {
        if (isDead) return;
        
        isDead = true;
        currentState = BossState.Dead;
        
        if (agent && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        StopAllCoroutines();
        PlayAnimation(AnimDie);
        
        // Desactivar colisiones
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        Debug.Log("[ImpDemonAI] Boss derrotado!");
    }

    // ========== DEBUG ==========

    void OnDrawGizmosSelected()
    {
        // Rango de detección
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Rango de ataque cuerpo a cuerpo
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Rango de proyectiles
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, projectileRange);
    }
}
