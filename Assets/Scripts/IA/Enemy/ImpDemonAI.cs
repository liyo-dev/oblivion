using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Sendero.Core.Feedback;

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
    [Tooltip("Propuesta identidad de fase (30 ago 2026): aviso en el suelo antes de que salga un "
             + "proyectil normal (Fase 2+), mismo patron visual que las sombras de RainAttack — antes "
             + "el proyectil solo tenia un breve giro hacia el jugador, sin telegrafia real. "
             + "Si se deja vacio, reutiliza rainShadowPrefab (misma sombra que ya usa la lluvia).")]
    [SerializeField] private GameObject rangedTelegraphPrefab;
    [SerializeField] private float rangedTelegraphDuration = 0.4f;

    [Header("Cooldowns")]
    [SerializeField] private float slashCooldown = 2f;
    [SerializeField] private float stabCooldown = 3f;
    [SerializeField] private float projectileCooldown = 4f;
    [SerializeField] private float spellCooldown = 8f;
    [SerializeField] private float undergroundCooldown = 15f;

    [Header("Fases")]
    [SerializeField] private float phase2HealthPercent = 0.66f;
    [SerializeField] private float phase3HealthPercent = 0.33f;
    [Tooltip("Propuesta identidad de fase (30 ago 2026): VFX opcional que se activa al entrar en "
             + "Fase 3 y se queda encima del demonio mientras dure el combate — el 'enrage' antes solo "
             + "se notaba en la camara (shake/flash) y en el multiplicador de velocidad, nunca en el "
             + "propio demonio. Se deja vacio por defecto: sin asset asignado en el Inspector no pasa "
             + "nada (mismo guard que el resto de VFX opcionales de este script).")]
    [SerializeField] private GameObject enrageAuraVFXPrefab;
    private GameObject _enrageAuraInstance;

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

    [Header("Cadencia de Ataque")]
    [Tooltip("Tiempo minimo de 'respiro' tras CUALQUIER ataque antes de poder iniciar el siguiente. "
             + "Antes las funciones Decide*/Try* se evaluaban cada frame (tirada de moneda por frame), "
             + "asi que un ataque podia encadenar con otro de tipo distinto sin ninguna pausa perceptible. "
             + "Este valor fuerza un hueco minimo entre ataques para que el combate tenga un pulso legible.")]
    [SerializeField] private float attackRecoveryBeat = 0.45f;
    private float _lastAttackEndTime = -999f;

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

        // FIX (petición Raúl, 1 sep 2026): congelar IA hostil mientras hay un diálogo abierto en
        // cualquier parte del mundo (ver mismo fix en Spider1AI.Update). No corta un ataque que ya
        // esté a mitad de ejecución (eso vive en corrutinas propias de UpdateBehavior), solo evita
        // encadenar movimiento/decisiones nuevas mientras el jugador está bloqueado por el diálogo.
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
        {
            if (agent && agent.isOnNavMesh && !agent.isStopped) agent.isStopped = true;
            return;
        }

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
                SpawnEnrageAura();
                // Propuesta mejora fases (27 ago 2026): en la revancha, la entrada a la fase final
                // es un combo de cierre scripted (teletransporte + golpe + lluvia) en vez del mismo
                // cast generico que el resto de transiciones — ver SecondEncounterEnrageSequence().
                if (isSecondEncounter)
                    StartCoroutine(SecondEncounterEnrageSequence());
                else
                    StartCoroutine(PhaseTransitionEffect());
                break;
        }
    }

    // Propuesta identidad de fase (30 ago 2026): la Fase 3 ('enrage') se notaba solo en el shake
    // de camara, el flash y el multiplicador de velocidad — nada distinto en el propio demonio.
    // Guard con _enrageAuraInstance: OnPhaseChanged() solo llama aqui al ENTRAR en Fase 3 (la vida
    // no sube, no debería reentrar), pero el guard evita duplicar el VFX si algún día lo hiciera.
    private void SpawnEnrageAura()
    {
        if (_enrageAuraInstance || !enrageAuraVFXPrefab) return;
        _enrageAuraInstance = Instantiate(enrageAuraVFXPrefab, transform.position, transform.rotation, transform);
    }

    private IEnumerator PhaseTransitionEffect()
    {
        currentState = BossState.CastingSpell;
        isAttacking = true;
        if (agent && agent.isOnNavMesh) agent.isStopped = true;

        // Propuesta mejora fases (27 ago 2026): antes esto solo subia la velocidad y reproducia
        // el cast generico — el jugador no "sentia" el cambio de fase salvo mirando la barra de
        // vida. Ahora es un momento real: invulnerabilidad breve (no se puede interrumpir el
        // rugido a media transicion) + camera shake + flash de pantalla, mas fuerte si es la
        // entrada a Fase 3 (lectura de "enrage").
        bool isEnrage = currentPhase == BossPhase.Phase3;
        float shakeIntensity = isEnrage ? 0.9f : 0.5f;
        float shakeDuration = isEnrage ? 0.5f : 0.35f;
        Color flashColor = isEnrage ? new Color(0.6f, 0f, 0f, 0.35f) : new Color(1f, 1f, 1f, 0.2f);

        if (damageable) damageable.GrantInvulnerability(1.6f);
        FeedbackService.CameraShake(shakeIntensity, shakeDuration);
        FeedbackService.ScreenFlash(flashColor, 0.25f);

        PlayAnimation(AnimCastSpell);

        if (spellEffectPrefab && projectileSpawnPoint)
            Instantiate(spellEffectPrefab, projectileSpawnPoint.position, Quaternion.identity);

        yield return StartCoroutine(WaitFacingPlayer(1.5f));

        EndAttack();
        if (agent && agent.isOnNavMesh) agent.isStopped = false;
    }

    // Propuesta mejora fases (27 ago 2026): combo de cierre de la revancha (2a aparicion, entrada
    // a Fase 3). En vez de solo escalar numeros (velocidad, cooldowns) y dejar el resto a tiradas
    // independientes de TrySecondEncounterAttacks/TrySpecialAttacks, esta es una secuencia
    // scripted una sola vez: teletransporte a espaldas del jugador + golpe inmediato
    // (UndergroundAttack, ya con esa variante en 2a aparicion) -> respiro corto -> lluvia de
    // ataques (RainAttack). Reutiliza corrutinas ya existentes, solo fija el orden la primera vez
    // que se entra en la fase final de la revancha.
    private IEnumerator SecondEncounterEnrageSequence()
    {
        isAttacking = true;
        currentState = BossState.CastingSpell;
        if (agent && agent.isOnNavMesh) agent.isStopped = true;

        FeedbackService.CameraShake(0.9f, 0.5f);
        FeedbackService.ScreenFlash(new Color(0.6f, 0f, 0f, 0.35f), 0.25f);
        if (damageable) damageable.GrantInvulnerability(2.5f);

        PlayAnimation(AnimCastSpell);
        if (spellEffectPrefab && projectileSpawnPoint)
            Instantiate(spellEffectPrefab, projectileSpawnPoint.position, Quaternion.identity);

        yield return StartCoroutine(WaitFacingPlayer(1f));

        EndAttack();

        yield return StartCoroutine(UndergroundAttack());

        // UndergroundAttack() ya hizo su propio EndAttack() al terminar — isAttacking=false aqui
        // dejaria un hueco de 0.4s en el que UpdateBehavior() podria colarse y decidir un ataque
        // normal (melee, con el player ya pegado tras el teletransporte) a mitad del combo
        // scripted. Se vuelve a marcar isAttacking=true para el respiro; RainAttack() ya lo hace
        // igualmente al empezar, esto solo cierra el hueco intermedio.
        isAttacking = true;
        yield return new WaitForSeconds(0.4f);

        yield return StartCoroutine(RainAttack());
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
            if (CanStartNewAttack())
                DecideMeleeAttack();
            else
                PlayAnimation(AnimIdle);
        }
        else if (distanceToPlayer <= projectileRange && currentPhase != BossPhase.Phase1)
        {
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
            if (CanStartNewAttack())
                DecideRangedAttack();
            else
                PlayAnimation(AnimIdle);
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

        if (currentPhase == BossPhase.Phase3 && CanStartNewAttack())
            TrySpecialAttacks();

        if (isSecondEncounter && CanStartNewAttack())
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
        if (isAttacking) return;

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
        EndAttack();
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
        EndAttack();
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
        EndAttack();
    }

    // En 2ª aparición (Fase 2+): triple proyectil en abanico con 2-3 rondas seguidas
    private IEnumerator ProjectileAttack()
    {
        isAttacking = true;
        currentState = BossState.Attacking;
        lastProjectileTime = Time.time;

        PlayAnimation(AnimProjectileAttack);

        // Propuesta identidad de fase (30 ago 2026): este ataque solo se usa en Fase 2+ (ver
        // DecideRangedAttack/UpdateBehavior, currentPhase != Phase1) — antes el unico aviso era el
        // giro hacia el jugador, sin telegrafia real. Ahora reutiliza el mismo patron visual que
        // RainAttack (sombra que crece antes del impacto), aplicado a un unico punto de aviso.
        GameObject telegraphPrefab = rangedTelegraphPrefab ? rangedTelegraphPrefab : rainShadowPrefab;
        if (telegraphPrefab && player)
            yield return StartCoroutine(SpawnRangedTelegraph(player.position, rangedTelegraphDuration));
        else
            yield return StartCoroutine(WaitFacingPlayer(0.5f));

        if (projectilePrefab && projectileSpawnPoint && player)
        {
            bool triple = isSecondEncounter && currentPhase != BossPhase.Phase1;
            int count = triple ? 3 : 1;
            float spreadAngle = 20f;
            int volleys = triple ? Random.Range(2, 4) : 1;

            for (int v = 0; v < volleys; v++)
            {
                if (v > 0) yield return StartCoroutine(WaitFacingPlayer(0.65f));

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
        EndAttack();
    }

    private IEnumerator CastSpellAttack()
    {
        isAttacking = true;
        currentState = BossState.CastingSpell;
        lastSpellTime = Time.time;

        PlayAnimation(AnimCastSpell);
        yield return StartCoroutine(WaitFacingPlayer(1f));

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
        EndAttack();
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
        EndAttack();
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

        if (!player) { EndAttack(); yield break; }

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
        EndAttack();
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
        EndAttack();
    }

    // Propuesta identidad de fase (30 ago 2026): aviso de un unico proyectil normal (Fase 2+),
    // mismo patron visual que SpawnRainWave (sombra que crece antes del impacto) pero para un solo
    // punto. Usa VfxPoolService (regla no negociable de VFX de un solo uso) en vez de
    // Instantiate/Destroy manual — el propio Play() gestiona el despawn al pasar rangedTelegraphDuration.
    private IEnumerator SpawnRangedTelegraph(Vector3 targetPosition, float duration)
    {
        GameObject telegraphPrefab = rangedTelegraphPrefab ? rangedTelegraphPrefab : rainShadowPrefab;
        Quaternion rot = Quaternion.Euler(90f, 0f, 0f);
        Transform telegraph = VfxPoolService.Instance.Play(telegraphPrefab, targetPosition, rot, duration);

        if (!telegraph)
        {
            yield return StartCoroutine(WaitFacingPlayer(duration));
            yield break;
        }

        Vector3 endScale = telegraph.localScale;
        telegraph.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            LookAtPlayer();
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            // El objeto puede haber sido despawneado por el pool si duration es muy corto y este
            // bucle tarda un frame de mas en salir — comprobar antes de tocar el transform.
            if (!telegraph) yield break;
            telegraph.localScale = endScale * t;
            yield return null;
        }
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

    // Cadencia de ataque (ver "Cadencia de Ataque" en el Inspector): true si ha pasado el
    // hueco minimo de respiro desde que termino el ultimo ataque. Envuelve las llamadas a
    // Decide*/Try* en UpdateBehavior() para que no se encadenen ataques sin pausa perceptible.
    private bool CanStartNewAttack() => Time.time >= _lastAttackEndTime + attackRecoveryBeat;

    // Sustituye a "isAttacking = false;" suelto: ademas de bajar el flag, marca el instante en
    // que termino el ataque para que CanStartNewAttack() pueda exigir el respiro minimo.
    private void EndAttack()
    {
        isAttacking = false;
        _lastAttackEndTime = Time.time;
    }

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

    // FIX INC-115: gira hacia el player durante los ataques a distancia (proyectil/hechizo).
    // Antes UpdateBehavior() (unico sitio que llamaba a LookAtPlayer) se saltaba entero mientras
    // isAttacking=true, asi que durante toda la corrutina de ProjectileAttack/CastSpellAttack
    // (windup + rondas + cooldown, varios segundos en la 2a aparicion con rafagas triples) el
    // demonio se quedaba congelado mirando hacia donde estuviera antes de empezar a lanzar.
    private IEnumerator WaitFacingPlayer(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            LookAtPlayer();
            elapsed += Time.deltaTime;
            yield return null;
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

        if (_enrageAuraInstance)
        {
            Destroy(_enrageAuraInstance);
            _enrageAuraInstance = null;
        }

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
