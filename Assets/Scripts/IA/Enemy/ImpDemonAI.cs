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

    [Header("DEBUG")]
    [SerializeField] private bool debugLogAnimator = false;

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

    // Buffer estático para evitar allocations en OverlapSphereNonAlloc
    private static Collider[] _overlapBuffer = new Collider[16];

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

    // Mapa para mostrar nombres legibles en logs cuando falte un estado
    private static readonly System.Collections.Generic.Dictionary<int, string> AnimNameMap;

    // Cache que mapea el hash lógico (usado en el script) al clip/state real y su capa
    private struct AnimInfo { public int layer; public int clipHash; }
    private System.Collections.Generic.Dictionary<int, AnimInfo> _animLookup;

    static ImpDemonAI()
    {
        AnimNameMap = new System.Collections.Generic.Dictionary<int, string>
        {
            { AnimIdle, "Idle" },
            { AnimFlyForward, "Fly Forward" },
            { AnimSlashAttack, "Slash Attack" },
            { AnimStabAttack, "Stab Attack" },
            { AnimProjectileAttack, "Projectile Attack" },
            { AnimCastSpell, "Cast Spell" },
            { AnimUnderground, "Underground" },
            { AnimTakeDamage, "Take Damage" },
            { AnimDie, "Die" },
            { AnimSpawn, "Spawn" }
        };
    }

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

        // Construir cache de animaciones (intento de detectar estados con sufijos como 'Idle 0')
        BuildAnimatorLookup();

        // Si se pide depuración, volcar información del Animator para ayudar a mapear estados
        if (debugLogAnimator)
        {
            LogAnimatorSetup();
        }
    }

    // Construye un lookup que mapea cada animHash usado por el script al clipHash y la capa donde existe
    private void BuildAnimatorLookup()
    {
        _animLookup = new System.Collections.Generic.Dictionary<int, AnimInfo>();
        if (animator == null || animator.runtimeAnimatorController == null) return;

        int layers = animator.layerCount;
        var clips = animator.runtimeAnimatorController.animationClips;

        foreach (var kv in AnimNameMap)
        {
            int animHash = kv.Key;
            string baseName = kv.Value;
            AnimInfo info = new AnimInfo { layer = -1, clipHash = animHash };

            // 1) Si el estado existe por su hash directo, usarlo
            for (int l = 0; l < layers; l++)
            {
                if (animator.HasState(l, animHash))
                {
                    info.layer = l;
                    info.clipHash = animHash;
                    break;
                }
            }

            if (info.layer == -1 && clips != null)
            {
                // 2) Buscar un clip cuyo nombre contenga el baseName (p.e. 'Idle' -> 'Idle 0')
                foreach (var clip in clips)
                {
                    if (clip == null) continue;
                    if (clip.name.IndexOf(baseName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int clipHash = Animator.StringToHash(clip.name);
                        // comprobar en qué capa existe ese estado (si existe)
                        for (int l = 0; l < layers; l++)
                        {
                            if (animator.HasState(l, clipHash))
                            {
                                info.layer = l;
                                info.clipHash = clipHash;
                                break;
                            }
                        }
                        if (info.layer >= 0) break;
                    }
                }
            }

            _animLookup[animHash] = info;
        }
    }

    [ContextMenu("Log Animator Info")]
    private void LogAnimatorSetup()
    {
        if (animator == null)
        {
            Debug.LogWarning("[ImpDemonAI] No hay Animator asignado para inspeccionar.");
            return;
        }

        var controller = animator.runtimeAnimatorController;
        string ctrlName = controller != null ? controller.name : "<null>";
        Debug.Log($"[ImpDemonAI] Animator Controller: {ctrlName}");
        Debug.Log($"[ImpDemonAI] Layer count: {animator.layerCount}");

        if (controller != null)
        {
            var clips = controller.animationClips;
            Debug.Log($"[ImpDemonAI] Animation Clips ({(clips != null ? clips.Length : 0)}):");
            if (clips != null)
            {
                foreach (var c in clips)
                {
                    if (c == null) continue;
                    Debug.Log($" - {c.name}");
                }
            }
        }

        // Mapear cada animación usada en AnimNameMap a la capa detectada (si hay)
        Debug.Log("[ImpDemonAI] Mapeo de animaciones usadas:");
        foreach (var kv in AnimNameMap)
        {
            int hash = kv.Key;
            string animLabel = kv.Value;
            int layer = AnimatorLayerContainingState(hash);
            if (layer >= 0)
                Debug.Log($" - '{animLabel}' -> encontrada en capa {layer}");
            else
                Debug.Log($" - '{animLabel}' -> NO encontrada (buscar clips que contengan '{animLabel}'...)");

            if (controller != null)
            {
                var clips = controller.animationClips;
                if (clips != null)
                {
                    foreach (var c in clips)
                    {
                        if (c == null) continue;
                        if (c.name.IndexOf(animLabel, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Debug.Log($"    Clip coincidente: {c.name}");
                        }
                    }
                }
            }
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
            var proj = projectile.GetComponent<EnemyProjectile>();
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
            
            // Daño en área (usar versión non-alloc para reducir GC)
            int hitCount = Physics.OverlapSphereNonAlloc(player.position, 5f, _overlapBuffer, playerLayer);
            for (int i = 0; i < hitCount; i++)
            {
                var hit = _overlapBuffer[i];
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
        var playerDamageable = player.GetComponent<IDamageable>();
        if (playerDamageable != null && playerDamageable.IsAlive)
        {
            playerDamageable.TakeDamage(damage);
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

        // Intentar reproducir usando el lookup construido en Awake
        if (_animLookup != null && _animLookup.TryGetValue(animHash, out var info) && info.layer >= 0)
        {
            try
            {
                animator.Play(info.clipHash, info.layer, 0f);
                return;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ImpDemonAI] Error al reproducir animación mapeada hash={animHash} -> clipHash={info.clipHash} en capa={info.layer}: {ex.Message}");
            }
        }

        // Comprueba qué capa contiene el estado antes de reproducir (fallback)
        int layerIndex = AnimatorLayerContainingState(animHash);
        if (layerIndex >= 0)
        {
            try
            {
                // Específicamente reproducir en la capa donde existe el estado
                animator.Play(animHash, layerIndex, 0f);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ImpDemonAI] Error al reproducir animación hash={animHash} en capa={layerIndex}: {ex.Message}");
            }
            return;
        }

        // Si no existe el estado, avisar y reproducir Idle si está disponible
        string animName = AnimNameMap.TryGetValue(animHash, out var n) ? n : animHash.ToString();
        Debug.LogWarning($"[ImpDemonAI] Animator.GotoState: State could not be found -> '{animName}'. Reproduciendo 'Idle' como fallback.");
        int idleLayer = AnimatorLayerContainingState(AnimIdle);
        if (idleLayer >= 0)
        {
            animator.Play(AnimIdle, idleLayer, 0f);
        }
        else
        {
            // Información adicional para facilitar depuración
            string ctrlName = animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "<null>";
            Debug.LogWarning($"[ImpDemonAI] Estado '{animName}' no encontrado en Animator Controller '{ctrlName}'. Capas disponibles: {animator.layerCount}. Comprueba los nombres de los estados y las máquinas de estado anidadas (usa ruta completa si es necesario).");
        }
    }

    // Devuelve la capa que contiene el estado proporcionado o -1 si no existe
    private int AnimatorLayerContainingState(int animHash)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return -1;
        int layers = animator.layerCount;
        for (int i = 0; i < layers; i++)
        {
            if (animator.HasState(i, animHash)) return i;
        }

        // Si no se encuentra por hash exacto, intentar buscar por nombre base usando los animationClips
        string baseName = AnimNameMap.TryGetValue(animHash, out var n) ? n : null;
        if (string.IsNullOrEmpty(baseName)) return -1;

        var clips = animator.runtimeAnimatorController.animationClips;
        if (clips != null && clips.Length > 0)
        {
            foreach (var clip in clips)
            {
                if (clip == null) continue;
                if (clip.name.IndexOf(baseName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    int clipHash = Animator.StringToHash(clip.name);
                    for (int i = 0; i < layers; i++)
                    {
                        if (animator.HasState(i, clipHash)) return i;
                    }
                }
            }
        }

        return -1;
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
