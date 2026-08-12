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
    [SerializeField] private float phase2HealthPercent = 0.66f;
    [SerializeField] private float phase3HealthPercent = 0.33f;

    [Header("Segunda Aparición")]
    [Tooltip("Actívalo en el prefab/instancia del segundo encuentro. No afecta al primero.")]
    public bool isSecondEncounter = false;
    [Tooltip("Multiplicador de cooldowns (0.65 = 35% más rápido).")]
    [SerializeField] private float secondCooldownMultiplier = 0.65f;
    [Tooltip("La fase berserk se activa antes (50% en lugar del 33%).")]
    [SerializeField] private float phase3HealthPercentSecond = 0.50f;

    [Header("  · Dash (2ª aparición)")]
    [SerializeField] private float dashDamage = 25f;
    [SerializeField] private float dashSpeed = 22f;
    [SerializeField] private float dashDuration = 0.35f;
    [SerializeField] private float dashCooldown = 7f;

    [Header("  · Lluvia de ataques (2ª aparición)")]
    [Tooltip("Prefab con un quad/decal semitransparente que hace de sombra de aviso.")]
    [SerializeField] private GameObject rainShadowPrefab;
    [Tooltip("Efecto de explosión/impacto que aparece tras el aviso.")]
    [SerializeField] private GameObject rainImpactPrefab;
    [SerializeField] private int rainCount = 8;
    [SerializeField] private float rainRadius = 7f;
    [Tooltip("Tiempo que las sombras permanecen en el suelo antes del impacto.")]
    [SerializeField] private float rainWarningDuration = 1.5f;
    [SerializeField] private float rainImpactRadius = 1.8f;
    [SerializeField] private float rainDamage = 20f;
    [SerializeField] private float rainCooldown = 22f;

    [Header("DEBUG")]
    [SerializeField] private bool debugLogAnimator = false;

    [Header("Combat Control")]
    [Tooltip("Permite iniciar el combate. Se activa externamente después de la presentación.")]
    public bool canStartCombat = false;

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
    private float lastDashTime = -999f;
    private float lastRainTime = -999f;
    private bool isAttacking = false;
    private bool hasSpawned = false;
    private bool isDead = false;
    private bool _registeredInCombat = false;

    // Cooldowns efectivos calculados en Awake
    private float _effSlashCooldown;
    private float _effStabCooldown;
    private float _effProjectileCooldown;
    private float _effSpellCooldown;
    private float _effUndergroundCooldown;

    private static readonly Collider[] _overlapBuffer = new Collider[16];
    private float _targetRefreshTimer;

    // Hashes de animaciones
    private static readonly int AnimIdle            = Animator.StringToHash("Idle");
    private static readonly int AnimFlyForward      = Animator.StringToHash("Fly Forward");
    private static readonly int AnimSlashAttack     = Animator.StringToHash("Slash Attack");
    private static readonly int AnimStabAttack      = Animator.StringToHash("Stab Attack");
    private static readonly int AnimProjectileAttack = Animator.StringToHash("Projectile Attack");
    private static readonly int AnimCastSpell       = Animator.StringToHash("Cast Spell");
    private static readonly int AnimUnderground     = Animator.StringToHash("Underground");
    private static readonly int AnimTakeDamage      = Animator.StringToHash("Take Damage");
    private static readonly int AnimDie             = Animator.StringToHash("Die");
    private static readonly int AnimSpawn           = Animator.StringToHash("Spawn");

    private static readonly System.Collections.Generic.Dictionary<int, string> AnimNameMap;

    private struct AnimInfo { public int layer; public int clipHash; }
    private System.Collections.Generic.Dictionary<int, AnimInfo> _animLookup;

    static ImpDemonAI()
    {
        AnimNameMap = new System.Collections.Generic.Dictionary<int, string>
        {
            { AnimIdle,             "Idle" },
            { AnimFlyForward,       "Fly Forward" },
            { AnimSlashAttack,      "Slash Attack" },
            { AnimStabAttack,       "Stab Attack" },
            { AnimProjectileAttack, "Projectile Attack" },
            { AnimCastSpell,        "Cast Spell" },
            { AnimUnderground,      "Underground" },
            { AnimTakeDamage,       "Take Damage" },
            { AnimDie,              "Die" },
            { AnimSpawn,            "Spawn" }
        };
    }

    void Awake()
    {
        if (!animator)  animator  = GetComponent<Animator>();
        if (!damageable) damageable = GetComponent<Damageable>();
        if (!agent)     agent     = GetComponent<NavMeshAgent>();

        if (!player && PlayerService.Player != null)
            player = PlayerService.Player.transform;

        float m = isSecondEncounter ? secondCooldownMultiplier : 1f;
        _effSlashCooldown       = slashCooldown       * m;
        _effStabCooldown        = stabCooldown        * m;
        _effProjectileCooldown  = projectileCooldown  * m;
        _effSpellCooldown       = spellCooldown       * m;
        _effUndergroundCooldown = undergroundCooldown * m;

        BuildAnimatorLookup();

        if (debugLogAnimator)
            LogAnimatorSetup();
    }

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
                foreach (var clip in clips)
                {
                    if (clip == null) continue;
                    if (clip.name.IndexOf(baseName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int clipHash = Animator.StringToHash(clip.name);
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

        Debug.Log("[ImpDemonAI] Mapeo de animaciones usadas:");
        foreach (var kv in AnimNameMap)
        {
            int hash = kv.Key;
            string animLabel = kv.Value;
            int layer = AnimatorLayerContainingState(hash);
            if (layer >= 0)
                Debug.Log($" - '{animLabel}' -> encontrada en capa {layer}");
            else
                Debug.Log($" - '{animLabel}' -> NO encontrada");

            if (controller != null)
            {
                var clips = controller.animationClips;
                if (clips != null)
                {
                    foreach (var c in clips)
                    {
                        if (c == null) continue;
                        if (c.name.IndexOf(animLabel, System.StringComparison.OrdinalIgnoreCase) >= 0)
                            Debug.Log($"    Clip coincidente: {c.name}");
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
            damageable.OnDied    += OnDeath;
        }

        StartCoroutine(SpawnSequence());
    }

    void OnDestroy()
    {
        if (damageable)
        {
            damageable.OnDamaged -= OnDamageTaken;
            damageable.OnDied    -= OnDeath;
        }

        UnregisterFromCombatRegistry();
    }

    void Update()
    {
        SyncCombatRegistryState();
        if (!hasSpawned || isDead || !canStartCombat) return;

        _targetRefreshTimer += Time.deltaTime;
        if (_targetRefreshTimer >= 0.5f)
        {
            _targetRefreshTimer = 0f;
            var playerTransform = CombatTargetProvider.GetNearestTarget(transform.position);
            if (playerTransform != null) player = playerTransform;
        }

        if (!player) return;

        UpdatePhase();
        UpdateBehavior();
    }

    private void SyncCombatRegistryState()
    {
        if (isDead)
        {
            UnregisterFromCombatRegistry();
            return;
        }

        if (canStartCombat && !_registeredInCombat)
        {
            ActiveCombatRegistry.RegisterNPC(gameObject);
            _registeredInCombat = true;
        }
        else if (!canStartCombat && _registeredInCombat)
        {
            UnregisterFromCombatRegistry();
        }
    }

    private void UnregisterFromCombatRegistry()
    {
        if (!_registeredInCombat) return;
        ActiveCombatRegistry.UnregisterNPC(gameObject);
        _registeredInCombat = false;
    }

    private IEnumerator SpawnSequence()
    {
        currentState = BossState.Idle;

        if (agent && agent.isOnNavMesh)
            agent.isStopped = true;

        PlayAnimation(AnimSpawn);
        yield return new WaitForSeconds(2f);

        hasSpawned = true;

        if (agent && agent.isOnNavMesh)
            agent.isStopped = false;
    }

    private void UpdatePhase()
    {
        if (!damageable) return;

        float healthPercent = damageable.Current / damageable.Max;
        float p3Threshold = isSecondEncounter ? phase3HealthPercentSecond : phase3HealthPercent;

        BossPhase newPhase;
        if (healthPercent <= p3Threshold)
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[ImpDemonAI] Cambiando a {currentPhase}");
#endif
        switch (currentPhase)
        {
            case BossPhase.Phase2:
                if (agent) agent.speed *= 1.2f;
                StartCoroutine(PhaseTransitionEffect());
                break;

            case BossPhase.Phase3:
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

        if (spellEffectPrefab && projectileSpawnPoint)
            Instantiate(spellEffectPrefab, projectileSpawnPoint.position, Quaternion.identity);

        yield return new WaitForSeconds(1.5f);

        isAttacking = false;
        if (agent && agent.isOnNavMesh) agent.isStopped = false;
    }

    private void UpdateBehavior()
    {
        if (isAttacking || currentState == BossState.TakingDamage) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer > detectionRange)
        {
            currentState = BossState.Idle;
            PlayAnimation(AnimIdle);
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        LookAtPlayer();

        if (distanceToPlayer <= attackRange)
        {
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
            DecideMeleeAttack();
        }
        else if (distanceToPlayer <= projectileRange && currentPhase != BossPhase.Phase1)
        {
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
            DecideRangedAttack();
        }
        else
        {
            currentState = BossState.Chasing;
            if (agent && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(player.position);

                if (agent.velocity.sqrMagnitude > 0.1f)
                    LookAtDirection(agent.velocity.normalized);
                else
                    LookAtPlayer();
            }
            PlayAnimation(AnimFlyForward);
        }

        if (currentPhase == BossPhase.Phase3)
            TrySpecialAttacks();

        if (isSecondEncounter)
            TrySecondEncounterAttacks();
    }

    private void DecideMeleeAttack()
    {
        bool canSlash = Time.time >= lastSlashTime + _effSlashCooldown;
        bool canStab  = Time.time >= lastStabTime  + _effStabCooldown;

        // En segunda aparición, combo melee cuando ambos están disponibles (60% de probabilidad)
        if (isSecondEncounter && canSlash && canStab && Random.value > 0.4f)
        {
            StartCoroutine(MeleeCombo());
            return;
        }

        if (canSlash && canStab)
        {
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
        bool canProjectile = Time.time >= lastProjectileTime + _effProjectileCooldown;
        bool canSpell      = Time.time >= lastSpellTime      + _effSpellCooldown && currentPhase == BossPhase.Phase3;

        if (canSpell && Random.value > 0.7f)
            StartCoroutine(CastSpellAttack());
        else if (canProjectile)
            StartCoroutine(ProjectileAttack());
        else
            PlayAnimation(AnimIdle);
    }

    private void TrySpecialAttacks()
    {
        bool canUnderground = Time.time >= lastUndergroundTime + _effUndergroundCooldown;

        if (canUnderground && Random.value > 0.9f)
            StartCoroutine(UndergroundAttack());
    }

    // Segunda aparición: dash y lluvia de ataques
    private void TrySecondEncounterAttacks()
    {
        if (isAttacking) return;

        bool canRain = Time.time >= lastRainTime + rainCooldown && currentPhase != BossPhase.Phase1;
        bool canDash = Time.time >= lastDashTime + dashCooldown;

        // La lluvia tiene prioridad si está disponible (28% de probabilidad por frame cuando el cooldown lo permite)
        if (canRain && Random.value > 0.72f)
        {
            StartCoroutine(RainAttack());
            return;
        }

        if (canDash && Random.value > 0.75f)
            StartCoroutine(DashAttack());
    }

    // ========== ATAQUES ==========

    private IEnumerator SlashAttack()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastSlashTime = Time.time;

        PlayAnimation(AnimSlashAttack);
        yield return new WaitForSeconds(0.5f);

        if (player && Vector3.Distance(transform.position, player.position) <= attackRange)
            DamagePlayer(slashDamage);

        yield return new WaitForSeconds(0.5f);
        isAttacking = false;
    }

    private IEnumerator StabAttack()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastStabTime = Time.time;

        PlayAnimation(AnimStabAttack);
        yield return new WaitForSeconds(0.6f);

        if (player && Vector3.Distance(transform.position, player.position) <= attackRange)
            DamagePlayer(stabDamage);

        yield return new WaitForSeconds(0.4f);
        isAttacking = false;
    }

    // Slash + Stab encadenados sin pausa completa entre ellos (solo 2ª aparición)
    private IEnumerator MeleeCombo()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastSlashTime = Time.time;
        lastStabTime  = Time.time;

        PlayAnimation(AnimSlashAttack);
        yield return new WaitForSeconds(0.5f);
        if (player && Vector3.Distance(transform.position, player.position) <= attackRange)
            DamagePlayer(slashDamage);

        yield return new WaitForSeconds(0.15f);

        PlayAnimation(AnimStabAttack);
        yield return new WaitForSeconds(0.5f);
        if (player && Vector3.Distance(transform.position, player.position) <= attackRange)
            DamagePlayer(stabDamage);

        yield return new WaitForSeconds(0.35f);
        isAttacking = false;
    }

    // En 2ª aparición (Fase 2+): triple proyectil en abanico con 2-3 rondas seguidas
    private IEnumerator ProjectileAttack()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastProjectileTime = Time.time;

        PlayAnimation(AnimProjectileAttack);
        yield return new WaitForSeconds(0.5f);

        if (projectilePrefab && projectileSpawnPoint && player)
        {
            bool triple = isSecondEncounter && currentPhase != BossPhase.Phase1;
            int count = triple ? 3 : 1;
            float spreadAngle = 20f;
            int volleys = triple ? Random.Range(2, 4) : 1;

            for (int v = 0; v < volleys; v++)
            {
                if (v > 0) yield return new WaitForSeconds(0.65f);

                Vector3 aimPos = player.position + Vector3.up * 1f;
                for (int i = 0; i < count; i++)
                {
                    float angle = count == 1 ? 0f : Mathf.Lerp(-spreadAngle, spreadAngle, i / (float)(count - 1));
                    Vector3 baseDir = (aimPos - projectileSpawnPoint.position).normalized;
                    Vector3 direction = Quaternion.Euler(0f, angle, 0f) * baseDir;

                    GameObject projectile = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.LookRotation(direction));
                    var proj = projectile.GetComponent<EnemyProjectile>()
                               ?? projectile.GetComponentInChildren<EnemyProjectile>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (!proj) Debug.LogError($"[ImpDemonAI] El prefab '{projectilePrefab.name}' no tiene componente EnemyProjectile en la raíz ni en hijos.");
#endif
                    if (proj) proj.Initialize(direction, projectileDamage);
                }
            }
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
        yield return new WaitForSeconds(1f);

        if (spellEffectPrefab && player)
        {
            Instantiate(spellEffectPrefab, player.position, Quaternion.identity);

            int hitCount = Physics.OverlapSphereNonAlloc(player.position, 5f, _overlapBuffer, playerLayer);
            for (int i = 0; i < hitCount; i++)
            {
                var dmg = _overlapBuffer[i].GetComponent<IDamageable>();
                if (dmg != null && dmg.IsAlive)
                    dmg.TakeDamage(projectileDamage * 1.5f);
            }
        }

        yield return new WaitForSeconds(0.5f);
        isAttacking = false;
    }

    // En 2ª aparición: teleporta detrás del jugador en lugar de posición aleatoria
    private IEnumerator UndergroundAttack()
    {
        isAttacking = true;
        currentState = BossState.Underground;
        lastUndergroundTime = Time.time;

        PlayAnimation(AnimUnderground);
        if (agent && agent.isOnNavMesh) agent.isStopped = true;

        yield return new WaitForSeconds(1f);

        if (player)
        {
            Vector3 newPosition;
            if (isSecondEncounter)
                newPosition = player.position - player.forward * 2f;
            else
                newPosition = player.position + (Random.insideUnitSphere * 3f);

            newPosition.y = transform.position.y;

            if (NavMesh.SamplePosition(newPosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                transform.position = hit.position;
        }

        PlayAnimation(AnimSpawn);
        yield return new WaitForSeconds(0.5f);

        if (player && Vector3.Distance(transform.position, player.position) <= attackRange * 1.5f)
            DamagePlayer(stabDamage * 1.5f);

        yield return new WaitForSeconds(0.5f);
        if (agent && agent.isOnNavMesh) agent.isStopped = false;
        isAttacking = false;
    }

    // Dash: embestida rápida hacia el jugador con breve telegrafía (solo 2ª aparición)
    private IEnumerator DashAttack()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastDashTime = Time.time;

        if (agent && agent.isOnNavMesh) agent.isStopped = true;

        // Telegrafía breve
        PlayAnimation(AnimSlashAttack);
        yield return new WaitForSeconds(0.25f);

        if (!player) { isAttacking = false; yield break; }

        float originalSpeed = agent ? agent.speed : dashSpeed;
        if (agent)
        {
            agent.speed = dashSpeed;
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }

        float elapsed = 0f;
        bool hasDealtDamage = false;

        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;

            if (!hasDealtDamage && player && Vector3.Distance(transform.position, player.position) <= attackRange)
            {
                DamagePlayer(dashDamage);
                hasDealtDamage = true;
            }

            yield return null;
        }

        if (agent)
        {
            agent.speed = originalSpeed;
            if (agent.isOnNavMesh) agent.isStopped = false;
        }

        yield return new WaitForSeconds(0.3f);
        isAttacking = false;
    }

    // Lluvia de ataques: dos olas de sombras escalonadas (solo 2ª aparición)
    private IEnumerator RainAttack()
    {
        isAttacking = true;
        currentState = BossState.CastingSpell;
        lastRainTime = Time.time;

        if (agent && agent.isOnNavMesh) agent.isStopped = true;

        PlayAnimation(AnimCastSpell);
        yield return new WaitForSeconds(0.5f);

        // Ola 1: amplia, sigue al jugador mientras avisa
        Vector3 center = player ? player.position : transform.position;
        yield return StartCoroutine(SpawnRainWave(center, rainCount, rainRadius, rainWarningDuration, trackPlayer: true));

        yield return new WaitForSeconds(0.35f);

        // Ola 2: más concentrada en donde el jugador se refugió, sin seguimiento
        center = player ? player.position : transform.position;
        int wave2Count = rainCount / 2 + 2;
        yield return StartCoroutine(SpawnRainWave(center, wave2Count, rainRadius * 0.55f, rainWarningDuration * 0.6f, trackPlayer: false));

        yield return new WaitForSeconds(0.5f);
        if (agent && agent.isOnNavMesh) agent.isStopped = false;
        isAttacking = false;
    }

    // Genera una oleada de sombras que crecen, luego impactan
    private IEnumerator SpawnRainWave(Vector3 center, int count, float radius, float warningDuration, bool trackPlayer)
    {
        Vector3[] positions = new Vector3[count];
        Transform[] shadowTrans = new Transform[count];
        GameObject[] shadowGOs = new GameObject[count];

        for (int i = 0; i < count; i++)
        {
            Vector2 rand2D = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(rand2D.x, 0f, rand2D.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
                positions[i] = navHit.position;
            else
                positions[i] = candidate;

            if (rainShadowPrefab)
            {
                // Rotación forzada para que el quad quede plano en el suelo
                Quaternion rot = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
                shadowGOs[i] = Instantiate(rainShadowPrefab, positions[i], rot);
                shadowTrans[i] = shadowGOs[i].transform;
                shadowTrans[i].localScale = Vector3.zero;
            }
        }

        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / warningDuration);

            for (int i = 0; i < count; i++)
            {
                if (shadowTrans[i])
                    shadowTrans[i].localScale = Vector3.one * t;
            }

            if (trackPlayer && player)
            {
                Vector3 newCenter = player.position;
                for (int i = 0; i < count; i++)
                {
                    Vector3 offset = positions[i] - center;
                    positions[i] = newCenter + offset;
                    if (shadowTrans[i])
                        shadowTrans[i].position = positions[i];
                }
                center = newCenter;
            }

            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            if (shadowGOs[i]) Destroy(shadowGOs[i]);

            if (rainImpactPrefab)
                Instantiate(rainImpactPrefab, positions[i], Quaternion.identity);

            DamagePlayerInRadius(positions[i], rainImpactRadius, rainDamage);

            yield return new WaitForSeconds(0.08f);
        }
    }

    private void DamagePlayerInRadius(Vector3 center, float radius, float damage)
    {
        if (!player) return;
        if (Vector3.Distance(center, player.position) <= radius)
            DamagePlayer(damage);
    }

    // ========== UTILIDADES ==========

    private void DamagePlayer(float damage)
    {
        if (!player) return;

        var playerHealth = player.GetComponent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            return;
        }

        var playerDamageable = player.GetComponent<IDamageable>();
        if (playerDamageable != null && playerDamageable.IsAlive)
            playerDamageable.TakeDamage(damage);
    }

    private void LookAtPlayer()
    {
        if (!player) return;

        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }

    private void LookAtDirection(Vector3 direction)
    {
        direction.y = 0;
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 7.5f);
        }
    }

    // FIX A11 (auditoría 2026-08-07): último (hash, capa) reproducido, para no reiniciar la misma
    // animación en la misma capa en cada llamada. Antes PlayAnimation() llamaba a animator.Play()
    // sin ningún guard — como varios estados llaman a PlayAnimation(AnimIdle) (u otras) en cada
    // frame que se re-evalúan (ver p. ej. el bucle de vuelo), la animación se reiniciaba al frame 0
    // constantemente: animación visualmente congelada en la primera pose, más el coste de
    // Animator.Play() cada frame. Mismo guard que ya usa Spider1AI.PlayAnimation, portado aquí.
    private int _lastPlayedAnimHash = -1;
    private int _lastPlayedLayer = -1;

    private void PlayAnimation(int animHash)
    {
        if (!animator) return;

        if (_animLookup != null && _animLookup.TryGetValue(animHash, out var info) && info.layer >= 0)
        {
            if (_lastPlayedAnimHash == animHash && _lastPlayedLayer == info.layer) return;
            try
            {
                animator.Play(info.clipHash, info.layer, 0f);
                _lastPlayedAnimHash = animHash;
                _lastPlayedLayer = info.layer;
                return;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ImpDemonAI] Error al reproducir animación mapeada hash={animHash}: {ex.Message}");
            }
        }

        int layerIndex = AnimatorLayerContainingState(animHash);
        if (layerIndex >= 0)
        {
            if (_lastPlayedAnimHash == animHash && _lastPlayedLayer == layerIndex) return;
            try
            {
                animator.Play(animHash, layerIndex, 0f);
                _lastPlayedAnimHash = animHash;
                _lastPlayedLayer = layerIndex;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ImpDemonAI] Error al reproducir animación hash={animHash} en capa={layerIndex}: {ex.Message}");
            }
            return;
        }

        string animName = AnimNameMap.TryGetValue(animHash, out var n) ? n : animHash.ToString();
        Debug.LogWarning($"[ImpDemonAI] Estado '{animName}' no encontrado. Reproduciendo Idle como fallback.");
        int idleLayer = AnimatorLayerContainingState(AnimIdle);
        if (idleLayer >= 0)
        {
            animator.Play(AnimIdle, idleLayer, 0f);
            _lastPlayedAnimHash = AnimIdle;
            _lastPlayedLayer = idleLayer;
        }
    }

    private int AnimatorLayerContainingState(int animHash)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return -1;
        int layers = animator.layerCount;
        for (int i = 0; i < layers; i++)
        {
            if (animator.HasState(i, animHash)) return i;
        }

        string baseName = AnimNameMap.TryGetValue(animHash, out var n) ? n : null;
        if (string.IsNullOrEmpty(baseName)) return -1;

        var clips = animator.runtimeAnimatorController.animationClips;
        if (clips != null)
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
            currentState = BossState.Idle;
    }

    private void OnDeath()
    {
        if (isDead) return;

        isDead = true;
        currentState = BossState.Dead;
        UnregisterFromCombatRegistry();

        if (agent && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        StopAllCoroutines();
        PlayAnimation(AnimDie);

        var colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
            col.enabled = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[ImpDemonAI] Boss derrotado!");
#endif
    }

    // ========== DEBUG ==========

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, projectileRange);

        if (isSecondEncounter)
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.5f); // naranja semitransparente
            Gizmos.DrawWireSphere(transform.position, rainRadius);
        }
    }
}
