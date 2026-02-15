using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// IA del Boss Golem - VERSIÓN SIMPLIFICADA
/// 
/// Comportamiento:
/// - Camina hacia el jugador
/// - Lanza rocas cuando está en rango
/// 
/// El daño al jugador lo maneja el prefab de la roca (EnemyProjectile).
/// El Golem NO controla el daño, solo instancia y lanza el proyectil.
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
    [SerializeField] private float attackRange = 15f;
    [SerializeField] private float minAttackRange = 5f;
    [Tooltip("Frecuencia de actualización de la IA (segundos). Valores más altos mejoran el rendimiento.")]
    [SerializeField] private float aiUpdateFrequency = 0.1f;

    [Header("Ataque - Lanzar Roca")]
    [Tooltip("Prefab de la roca (debe tener EnemyProjectile configurado con su propio daño)")]
    [SerializeField] private GameObject rockPrefab;
    [Tooltip("Transform de la mano donde aparece la roca (Fase 1)")]
    [SerializeField] private Transform rockHandPoint;
    [Tooltip("Transform de la segunda mano para ataques de Fase 2")]
    [SerializeField] private Transform rockHandPointLeft;
    [Tooltip("Velocidad de la roca al lanzarse")]
    [SerializeField] private float rockSpeed = 18f;
    [Tooltip("VFX de polvo cuando coge la roca")]
    [SerializeField] private GameObject rockPickupVFX;
    
    [Header("Fase 2 - Lluvia de Rocas")]
    [Tooltip("Porcentaje de vida para activar Fase 2 (0.5 = 50%)")]
    [SerializeField] private float phase2HealthThreshold = 0.5f;
    [Tooltip("Número de rocas en la lluvia")]
    [SerializeField] private int rockRainCount = 8;
    [Tooltip("Radio de dispersión de la lluvia")]
    [SerializeField] private float rockRainRadius = 6f;
    [Tooltip("Altura desde donde caen las rocas")]
    [SerializeField] private float rockRainHeight = 15f;
    [Tooltip("Velocidad de caída de las rocas")]
    [SerializeField] private float rockRainSpeed = 12f;
    [Tooltip("Tiempo entre cada roca de la lluvia")]
    [SerializeField] private float rockRainInterval = 0.15f;
    [Tooltip("VFX de advertencia en el suelo antes de que caiga la roca")]
    [SerializeField] private GameObject rockWarningVFX;
    
    [Header("Fase 3 - Salto y Onda Expansiva")]
    [Tooltip("Porcentaje de vida para activar Fase 3 (0.25 = 25%)")]
    [SerializeField] private float phase3HealthThreshold = 0.25f;
    [Tooltip("Altura máxima del salto")]
    [SerializeField] private float jumpHeight = 8f;
    [Tooltip("Duración del salto (subida + bajada)")]
    [SerializeField] private float jumpDuration = 1.2f;
    [Tooltip("Radio de la onda expansiva")]
    [SerializeField] private float shockwaveRadius = 10f;
    [Tooltip("Daño de la onda expansiva")]
    [SerializeField] private float shockwaveDamage = 30f;
    [Tooltip("Fuerza de empuje de la onda")]
    [SerializeField] private float shockwaveKnockback = 15f;
    [Tooltip("VFX de la onda expansiva")]
    [SerializeField] private GameObject shockwaveVFX;
    [Tooltip("VFX de polvo al aterrizar")]
    [SerializeField] private GameObject landingDustVFX;
    [Tooltip("Tiempo que tiembla la cámara al aterrizar")]
    [SerializeField] private float cameraShakeDuration = 0.5f;
    [Tooltip("Intensidad del temblor de cámara")]
    [SerializeField] private float cameraShakeIntensity = 0.3f;
    
    [Header("Cooldowns")]
    [SerializeField] private float attackCooldown = 3f;
    [SerializeField] private float rockRainCooldown = 8f;
    [SerializeField] private float jumpAttackCooldown = 12f;
    
    [Header("Embestida con Puñetazo (Transición entre fases)")]
    [Tooltip("Velocidad de carrera durante la embestida")]
    [SerializeField] private float chargeSpeed = 12f;
    [Tooltip("Multiplicador de velocidad de animación durante la embestida (1.0 = normal, 2.0 = doble velocidad)")]
    [SerializeField] private float chargeAnimationSpeedMultiplier = 1.8f;
    [Tooltip("Distancia mínima para iniciar embestida")]
    [SerializeField] private float chargeMinDistance = 5f;
    [Tooltip("Daño del puñetazo")]
    [SerializeField] private float punchDamage = 25f;
    [Tooltip("Radio del puñetazo")]
    [SerializeField] private float punchRadius = 3f;
    [Tooltip("Knockback del puñetazo")]
    [SerializeField] private float punchKnockback = 12f;
    [Tooltip("VFX del impacto del puñetazo")]
    [SerializeField] private GameObject punchImpactVFX;
    [Tooltip("Cooldown de la embestida")]
    [SerializeField] private float chargeCooldown = 10f;
    [Tooltip("Probabilidad de embestida al cambiar de fase (0-1)")]
    [SerializeField] private float chargeOnPhaseChangeChance = 0.7f;

    [Header("Movimiento")]
    [SerializeField] private float walkSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 3f;
    [Tooltip("Offset de rotación Y si el modelo está orientado incorrectamente (0, 90, 180, -90)")]
    [SerializeField] private float modelRotationOffset = 0f;
    
    [Header("💥 Daño por Contacto/Colisión")]
    [Tooltip("¿Activar daño por contacto cuando el jugador choca con el Golem?")]
    [SerializeField] private bool enableContactDamage = true;
    [Tooltip("Daño que hace el Golem al chocar con el jugador")]
    [SerializeField] private float contactDamage = 15f;
    [Tooltip("Cooldown entre daños por contacto (segundos)")]
    [SerializeField] private float contactDamageCooldown = 1.5f;
    [Tooltip("Multiplicador de daño durante embestida (x2 = doble daño)")]
    [SerializeField] private float chargeDamageMultiplier = 1.5f;
    [Tooltip("Radio de detección de colisión (debe coincidir con el collider del Golem)")]
    [SerializeField] private float contactRadius = 2f;

    [Header("Combat Control")]
    public bool canStartCombat
    {
        get => _canStartCombat;
        set
        {
            if (_canStartCombat != value)
            {
                _canStartCombat = value;
                if (_canStartCombat && !_registeredInCombat)
                {
                    ActiveCombatRegistry.RegisterNPC(gameObject);
                    _registeredInCombat = true;
                    Debug.Log($"[GolemBossAI] 🔴🔴🔴 GOLEM REGISTRADO EN COMBATE INMEDIATAMENTE - ActiveCombatRegistry.Count = {ActiveCombatRegistry.Count}");
                }
            }
        }
    }
    private bool _canStartCombat = false;
    
    [SerializeField] private bool autoStartOnDetection = true;

    [Header("DEBUG")]
    [SerializeField] private bool debugMode = false;

    // Estado interno
    private enum BossState { Idle, Walking, Attacking, Jumping, Charging, Dead }
    private BossState _currentState = BossState.Idle;
    
    private float _lastAttackTime = -999f;
    private float _lastRockRainTime = -999f;
    private float _lastJumpAttackTime = -999f;
    private float _lastChargeTime = -999f;
    private float _lastContactDamageTime = -999f; // Timer para daño por contacto
    private bool _isAttacking;
    private bool _isJumping;
    private bool _isCharging;
    private bool _hasSpawned;
    private bool _isDead;
    private bool _registeredInCombat;
    private Coroutine _aiRoutine;
    
    // Sistema de fases
    private int _currentPhase = 1;
    private bool _useLeftHand = false; // Alternar manos en Fase 2
    private bool _pendingChargeAttack = false; // Para embestida al cambiar de fase
    
    // Posición original para el salto
    private Vector3 _jumpStartPos;
    private Vector3 _jumpTargetPos;
    
    // ✅ OPTIMIZACIÓN FASE 1: Buffers reutilizables para Physics queries (evita allocations)
    private Collider[] _punchHitBuffer = new Collider[16];
    private Collider[] _shockwaveHitBuffer = new Collider[32];

    // Animaciones
    private const string ANIM_IDLE = "Idle";
    private const string ANIM_WALK = "Walk";
    private const string ANIM_ATTACK01 = "Attack01";
    private const string ANIM_ATTACK02 = "Attack02";
    private const string ANIM_JUMP = "Jump"; // O usar Attack02 si no hay animación de salto
    private const string ANIM_GETHIT = "GetHit";
    private const string ANIM_DIE = "Die";

    #region Unity Lifecycle

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
            agent.angularSpeed = 0;
            agent.updateRotation = false;
            agent.isStopped = true;
        }

        _hasSpawned = true;
        _currentState = BossState.Idle;
        PlayAnim(ANIM_IDLE);
        
        // Iniciar la rutina de IA
        _aiRoutine = StartCoroutine(AIBehaviorRoutine());
        
        Log("✅ Golem inicializado");
    }

    void OnDestroy()
    {
        if (damageable)
        {
            damageable.OnDamaged -= OnDamageTaken;
            damageable.OnDied -= OnDeath;
        }
        
        if (_registeredInCombat)
        {
            ActiveCombatRegistry.UnregisterNPC(gameObject);
        }
        
        if (_aiRoutine != null)
        {
            StopCoroutine(_aiRoutine);
        }
    }

    void Update()
    {
        if (!_hasSpawned || _isDead) return;
        
        // Buscar jugador si no lo tenemos (esto puede quedar en Update por si el jugador cambia)
        if (!player && PlayerService.Player != null)
        {
            player = PlayerService.Player.transform;
        }
        if (!player) return;

        float distance = Vector3.Distance(transform.position, player.position);

        // Auto-activar combate si está cerca (esto debe ser responsivo)
        if (!canStartCombat && autoStartOnDetection && distance <= detectionRange)
        {
            canStartCombat = true;
            Log("⚔️ Combate iniciado");
        }
        
        if (!canStartCombat) return;
        
        // El daño por contacto también debe ser responsivo
        if (distance < contactRadius * 3)
        {
            CheckContactDamage(distance);
        }
        
        // La rotación SIEMPRE en Update para que sea fluida
        // IMPORTANTE: Durante la embestida (_isCharging) SÍ debe rotar hacia la dirección de movimiento
        bool isMovingState = _currentState == BossState.Walking || _currentState == BossState.Charging;
        bool hasVelocity = agent != null && agent.velocity.sqrMagnitude > 0.1f;
        
        // Rotar durante movimiento (Walking o Charging) o cuando no está atacando (excepto embestida)
        if (_isCharging && hasVelocity)
        {
            // Durante embestida: SIEMPRE rotar hacia la dirección de movimiento
            RotateTowardsMovementDirection();
        }
        else if (!_isAttacking)
        {
            if (isMovingState && hasVelocity)
            {
                // Durante walking: rotar hacia la dirección de movimiento
                RotateTowardsMovementDirection();
            }
            else
            {
                // En otros estados o sin velocidad: rotar hacia el jugador
                RotateTowardsPlayer();
            }
        }
    }

    #endregion

    #region Comportamiento Principal

    private IEnumerator AIBehaviorRoutine()
    {
        while (true)
        {
            if (canStartCombat && !_isDead && player)
            {
                UpdateBehavior(Vector3.Distance(transform.position, player.position));
            }
            yield return new WaitForSeconds(aiUpdateFrequency);
        }
    }

    private void UpdateBehavior(float distance)
    {
        if (_isAttacking) return;

        // Verificar cambio de fase
        CheckPhaseTransition();

        // Fuera de rango de detección
        if (distance > detectionRange)
        {
            SetIdle();
            return;
        }

        // En rango de ataque
        if (distance <= attackRange && distance >= minAttackRange)
        {
            if (TryAttack())
            {
                // Ataque iniciado
            }
            else
            {
                SetIdle();
            }
        }
        // Demasiado cerca
        else if (distance < minAttackRange)
        {
            if (TryAttack())
            {
                // Ataque iniciado
            }
            else
            {
                SetIdle();
            }
        }
        // Fuera de rango - perseguir
        else
        {
            ChasePlayer();
        }
    }
    
    /// <summary>
    /// Verifica si el Golem debe cambiar de fase basado en su vida actual
    /// </summary>
    private void CheckPhaseTransition()
    {
        if (!damageable) return;
        
        float healthPercent = damageable.Current / damageable.Max;
        int previousPhase = _currentPhase;
        
        // Fase 3: Salto con onda expansiva
        if (_currentPhase < 3 && healthPercent <= phase3HealthThreshold)
        {
            _currentPhase = 3;
            Log($"🔥🔥🔥 FASE 3 ACTIVADA - Vida: {healthPercent:P0}");
            Debug.Log($"[GolemBossAI] 🔥🔥🔥 FASE 3 ACTIVADA - El Golem ahora usará SALTO CON ONDA EXPANSIVA!");
        }
        // Fase 2: Lluvia de rocas
        else if (_currentPhase < 2 && healthPercent <= phase2HealthThreshold)
        {
            _currentPhase = 2;
            Log($"⚡⚡⚡ FASE 2 ACTIVADA - Vida: {healthPercent:P0}");
            Debug.Log($"[GolemBossAI] ⚡⚡⚡ FASE 2 ACTIVADA - El Golem ahora usará lluvia de rocas!");
        }
        
        // Si cambió de fase, posible embestida de transición
        if (_currentPhase != previousPhase && !_isAttacking && !_isCharging)
        {
            if (Random.value <= chargeOnPhaseChangeChance)
            {
                _pendingChargeAttack = true;
                Log($"🏃 ¡EMBESTIDA DE TRANSICIÓN ACTIVADA!");
                Debug.Log($"[GolemBossAI] 🏃 ¡EMBESTIDA DE TRANSICIÓN! El Golem cargará hacia el jugador!");
            }
        }
    }
    
    /// <summary>
    /// Intenta ejecutar un ataque según la fase actual
    /// </summary>
    private bool TryAttack()
    {
        // PRIORIDAD MÁXIMA: Embestida pendiente por cambio de fase
        if (_pendingChargeAttack && CanCharge())
        {
            _pendingChargeAttack = false;
            StartCoroutine(ChargeAttack());
            return true;
        }
        
        // Embestida disponible en cualquier fase (cooldown independiente)
        bool canDoCharge = CanCharge() && GetDistanceToPlayer() >= chargeMinDistance;
        
        if (_currentPhase == 1)
        {
            // Fase 1: Lanzar rocas + embestida ocasional
            if (canDoCharge && Random.value < 0.2f) // 20% de probabilidad
            {
                StartCoroutine(ChargeAttack());
                return true;
            }
            if (CanAttack())
            {
                StartCoroutine(RockThrowAttack());
                return true;
            }
        }
        else if (_currentPhase == 2)
        {
            // Fase 2: Lluvia + lanzar rocas + embestida ocasional
            bool canDoRockRain = Time.time >= _lastRockRainTime + rockRainCooldown;
            bool canDoThrow = CanAttack();
            
            if (canDoCharge && Random.value < 0.25f) // 25% de probabilidad
            {
                StartCoroutine(ChargeAttack());
                return true;
            }
            if (canDoRockRain)
            {
                StartCoroutine(RockRainAttack());
                return true;
            }
            else if (canDoThrow)
            {
                StartCoroutine(RockThrowAttackPhase2());
                return true;
            }
        }
        else // Fase 3
        {
            // Fase 3: Salto + lluvia + lanzar + embestida
            bool canDoJump = Time.time >= _lastJumpAttackTime + jumpAttackCooldown;
            bool canDoRockRain = Time.time >= _lastRockRainTime + rockRainCooldown;
            bool canDoThrow = CanAttack();
            
            // Prioridad: Embestida (30%) > Salto > Lluvia > Lanzar
            if (canDoCharge && Random.value < 0.3f)
            {
                StartCoroutine(ChargeAttack());
                return true;
            }
            if (canDoJump)
            {
                StartCoroutine(JumpAttack());
                return true;
            }
            else if (canDoRockRain)
            {
                StartCoroutine(RockRainAttack());
                return true;
            }
            else if (canDoThrow)
            {
                StartCoroutine(RockThrowAttackPhase2());
                return true;
            }
        }
        
        return false;
    }

    private bool CanAttack() => Time.time >= _lastAttackTime + attackCooldown;
    private bool CanCharge() => Time.time >= _lastChargeTime + chargeCooldown;
    
    private float GetDistanceToPlayer()
    {
        if (!player) return float.MaxValue;
        return Vector3.Distance(transform.position, player.position);
    }

    #endregion 

    #region Movimiento

    private void ChasePlayer()
    {
        if (!player || !agent || !agent.isOnNavMesh) return;
        
        if (_currentState != BossState.Walking)
        {
            _currentState = BossState.Walking;
            PlayAnim(ANIM_WALK);
            agent.isStopped = false;
        }
        
        agent.SetDestination(player.position);
        // La rotación se maneja en Update() para que sea fluida cada frame
    }

    private void SetIdle()
    {
        if (_currentState != BossState.Idle)
        {
            _currentState = BossState.Idle;
            PlayAnim(ANIM_IDLE);
            StopAgent();
        }
    }

    private void StopAgent()
    {
        if (agent && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    private void RotateTowardsPlayer()
    {
        if (!player) return;
        
        Vector3 dir = player.position - transform.position;
        dir.y = 0;
        
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            // Aplicar offset si el modelo está orientado incorrectamente
            if (modelRotationOffset != 0f)
            {
                targetRot *= Quaternion.Euler(0f, modelRotationOffset, 0f);
            }
            // Rotación más agresiva: 360 grados/segundo * rotationSpeed
            float maxDegrees = rotationSpeed * 120f * Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, maxDegrees);
        }
    }
    
    /// <summary>
    /// Rota el Golem hacia su dirección de movimiento actual (velocidad del NavMeshAgent)
    /// Usa una velocidad de rotación más alta para que no camine de perfil
    /// </summary>
    private void RotateTowardsMovementDirection()
    {
        if (!agent) return;
        
        Vector3 moveDir = agent.velocity;
        moveDir.y = 0;
        
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
            // Aplicar offset si el modelo está orientado incorrectamente
            if (modelRotationOffset != 0f)
            {
                targetRot *= Quaternion.Euler(0f, modelRotationOffset, 0f);
            }
            // Rotación MUY rápida durante el movimiento: debe girar casi instantáneamente
            float maxDegrees = rotationSpeed * 200f * Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, maxDegrees);
        }
    }

    #endregion

    #region Ataque - Lanzar Roca

    /// <summary>
    /// Lanza una roca al jugador.
    /// El proyectil (EnemyProjectile) maneja todo el daño.
    /// </summary>
    private IEnumerator RockThrowAttack()
    {
        _isAttacking = true;
        _currentState = BossState.Attacking;
        _lastAttackTime = Time.time;
        
        StopAgent();
        
        Log("🪨 Iniciando lanzar roca");
        Debug.Log($"[GolemBossAI] 🪨 Iniciando lanzar roca");

        // Reproducir animación
        PlayAnim(ANIM_ATTACK01);
        
        // Esperar momento de coger la roca (0.3s)
        yield return new WaitForSeconds(0.3f);
        
        // VFX de polvo en el suelo
        Vector3 handPos = rockHandPoint ? rockHandPoint.position : transform.position + transform.forward * 2f;
        Vector3 groundPos = new Vector3(handPos.x, transform.position.y + 0.2f, handPos.z);
        
        if (rockPickupVFX)
        {
            // TODO: Usar un sistema de pooling para evitar Instantiate/Destroy
            GameObject dustVFX = Instantiate(rockPickupVFX, groundPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            dustVFX.transform.localScale = Vector3.one * 1.5f;
            Destroy(dustVFX, 2f);
        }
        
        // Crear la roca en la mano
        GameObject rock = null;
        if (rockPrefab && rockHandPoint)
        {
            // TODO: Usar un sistema de pooling
            rock = Instantiate(rockPrefab, rockHandPoint.position, Quaternion.identity);
            rock.transform.localScale = rockPrefab.transform.localScale;
            rock.transform.SetParent(rockHandPoint);
            rock.transform.localPosition = Vector3.zero;
            
            // Desactivar física mientras está en la mano
            var proj = rock.GetComponent<EnemyProjectile>();
            if (proj) proj.enabled = false;
            
            var rb = rock.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            
            var col = rock.GetComponent<Collider>();
            if (col) col.enabled = false;
            
            Log($"🪨 Roca creada en mano");
        }
        
        // Esperar hasta el momento de lanzar (0.7s más = 1.0s total)
        yield return new WaitForSeconds(0.7f);
        
        // LANZAR la roca
        if (rock && player)
        {
            // Desadhirir de la mano
            rock.transform.SetParent(null);
            
            // Calcular dirección hacia el jugador
            Vector3 targetPos = player.position + Vector3.up * 1.2f;
            Vector3 spawnPos = rock.transform.position;
            Vector3 direction = (targetPos - spawnPos).normalized;
            
            // Configurar layer del proyectil
            int projectileLayer = LayerMask.NameToLayer("EnemyProjectile");
            if (projectileLayer == -1) projectileLayer = LayerMask.NameToLayer("Projectile");
            if (projectileLayer != -1)
            {
                rock.layer = projectileLayer;
                foreach (Transform child in rock.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = projectileLayer;
                }
            }
            
            // Activar física
            var rb = rock.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.isKinematic = false;
                rb.useGravity = false;
                rb.linearVelocity = direction * rockSpeed;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
            
            // Activar collider
            var col = rock.GetComponent<Collider>();
            if (col) col.enabled = true;
            
            // ✅ IMPORTANTE: Inicializar el proyectil - él maneja el daño
            var proj = rock.GetComponent<EnemyProjectile>();
            if (proj)
            {
                proj.enabled = true;
                // El daño se configura en el prefab (baseDamage del EnemyProjectile)
                // Pasamos -1 para usar el daño configurado en el prefab
                proj.Initialize(direction, -1f);
                Debug.Log($"[GolemBossAI] 🎯 Roca lanzada - EnemyProjectile usará su baseDamage del prefab");
            }
            else
            {
                Debug.LogError("[GolemBossAI] ❌ EnemyProjectile NO encontrado en la roca!");
            }
            
            // Auto-destruir (si no se usa pooling)
            Destroy(rock, 5f);
            
            Log($"🪨 Roca lanzada hacia jugador");
            Debug.Log($"[GolemBossAI] 🪨 Roca lanzada: dir={direction}, speed={rockSpeed}");
        }
        
        // Esperar fin de animación
        yield return new WaitForSeconds(0.8f);
        
        _isAttacking = false;
    }
    
    /// <summary>
    /// Lanza una roca en Fase 2 - alterna entre mano derecha e izquierda
    /// </summary>
    private IEnumerator RockThrowAttackPhase2()
    {
        _isAttacking = true;
        _currentState = BossState.Attacking;
        _lastAttackTime = Time.time;
        
        StopAgent();
        
        // Alternar mano
        _useLeftHand = !_useLeftHand;
        Transform handPoint = _useLeftHand && rockHandPointLeft ? rockHandPointLeft : rockHandPoint;
        string animName = _useLeftHand ? ANIM_ATTACK02 : ANIM_ATTACK01;
        
        Log($"🪨 [Fase 2] Lanzando roca con mano {(_useLeftHand ? "izquierda" : "derecha")}");
        Debug.Log($"[GolemBossAI] 🪨 [Fase 2] Lanzando roca con mano {(_useLeftHand ? "izquierda" : "derecha")}");

        // Reproducir animación
        PlayAnim(animName);
        
        // Esperar momento de coger la roca
        yield return new WaitForSeconds(0.3f);
        
        // VFX de polvo
        Vector3 handPos = handPoint ? handPoint.position : transform.position + transform.forward * 2f;
        Vector3 groundPos = new Vector3(handPos.x, transform.position.y + 0.2f, handPos.z);
        
        if (rockPickupVFX)
        {
            // TODO: Usar un sistema de pooling
            GameObject dustVFX = Instantiate(rockPickupVFX, groundPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            dustVFX.transform.localScale = Vector3.one * 1.5f;
            Destroy(dustVFX, 2f);
        }
        
        // Crear la roca
        GameObject rock = null;
        if (rockPrefab && handPoint)
        {
            // TODO: Usar un sistema de pooling
            rock = Instantiate(rockPrefab, handPoint.position, Quaternion.identity);
            rock.transform.localScale = rockPrefab.transform.localScale;
            rock.transform.SetParent(handPoint);
            rock.transform.localPosition = Vector3.zero;
            
            var proj = rock.GetComponent<EnemyProjectile>();
            if (proj) proj.enabled = false;
            
            var rb = rock.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            
            var col = rock.GetComponent<Collider>();
            if (col) col.enabled = false;
        }
        
        yield return new WaitForSeconds(0.7f);
        
        // LANZAR
        if (rock && player)
        {
            rock.transform.SetParent(null);
            
            Vector3 targetPos = player.position + Vector3.up * 1.2f;
            Vector3 spawnPos = rock.transform.position;
            Vector3 direction = (targetPos - spawnPos).normalized;
            
            SetupRockProjectile(rock, direction, rockSpeed);
            
            Destroy(rock, 5f);
        }
        
        yield return new WaitForSeconds(0.8f);
        
        _isAttacking = false;
    }
    
    /// <summary>
    /// Ataque especial de Fase 2: Lluvia de rocas sobre el jugador
    /// </summary>
    private IEnumerator RockRainAttack()
    {
        _isAttacking = true;
        _currentState = BossState.Attacking;
        _lastRockRainTime = Time.time;
        _lastAttackTime = Time.time; // También actualizar cooldown normal
        
        StopAgent();
        
        Log("🌧️ [Fase 2] ¡LLUVIA DE ROCAS!");
        Debug.Log($"[GolemBossAI] 🌧️ [Fase 2] ¡LLUVIA DE ROCAS! - {rockRainCount} rocas");
        
        // Animación de invocar (usar Attack02 para la mano izquierda alzada)
        PlayAnim(ANIM_ATTACK02);
        
        // Esperar a que levante la mano
        yield return new WaitForSeconds(0.5f);
        
        if (!player)
        {
            _isAttacking = false;
            yield break;
        }
        
        // Centro de la lluvia = posición del jugador
        Vector3 rainCenter = player.position;
        
        // Generar las rocas con delay entre cada una
        for (int i = 0; i < rockRainCount; i++)
        {
            if (_isDead) yield break;
            
            // Posición aleatoria en círculo alrededor del jugador
            Vector2 randomCircle = Random.insideUnitCircle * rockRainRadius;
            Vector3 targetPos = rainCenter + new Vector3(randomCircle.x, 0f, randomCircle.y);
            Vector3 spawnPos = targetPos + Vector3.up * rockRainHeight;
            
            // VFX de advertencia en el suelo
            if (rockWarningVFX)
            {
                // TODO: Usar un sistema de pooling
                GameObject warning = Instantiate(rockWarningVFX, targetPos + Vector3.up * 0.1f, Quaternion.Euler(90f, 0f, 0f));
                Destroy(warning, 1.5f);
            }
            
            // Pequeño delay antes de que caiga la roca
            yield return new WaitForSeconds(rockRainInterval);
            
            // Crear y lanzar la roca
            SpawnFallingRock(spawnPos, targetPos);
        }
        
        // Esperar a que terminen de caer
        yield return new WaitForSeconds(1.5f);
        
        _isAttacking = false;
        
        Log("🌧️ Lluvia de rocas finalizada");
    }
    
    /// <summary>
    /// Crea una roca que cae desde arriba hacia un punto objetivo
    /// </summary>
    private void SpawnFallingRock(Vector3 spawnPos, Vector3 targetPos)
    {
        if (!rockPrefab) return;
        
        // TODO: Usar un sistema de pooling
        GameObject rock = Instantiate(rockPrefab, spawnPos, Quaternion.identity);
        rock.transform.localScale = rockPrefab.transform.localScale * 0.8f; // Rocas un poco más pequeñas
        
        // Dirección hacia abajo (con ligera variación)
        Vector3 direction = (targetPos - spawnPos).normalized;
        
        SetupRockProjectile(rock, direction, rockRainSpeed);
        
        Destroy(rock, 5f);
    }
    
    /// <summary>
    /// Configura un proyectil de roca con física y daño
    /// </summary>
    private void SetupRockProjectile(GameObject rock, Vector3 direction, float speed)
    {
        // Layer del proyectil
        int projectileLayer = LayerMask.NameToLayer("EnemyProjectile");
        if (projectileLayer == -1) projectileLayer = LayerMask.NameToLayer("Projectile");
        if (projectileLayer != -1)
        {
            rock.layer = projectileLayer;
            foreach (Transform child in rock.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = projectileLayer;
            }
        }
        
        // Física
        var rb = rock.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.linearVelocity = direction * speed;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
        
        // Collider
        var col = rock.GetComponent<Collider>();
        if (col) col.enabled = true;
        
        // Proyectil
        var proj = rock.GetComponent<EnemyProjectile>();
        if (proj)
        {
            proj.enabled = true;
            proj.Initialize(direction, -1f); // Usar daño del prefab
        }
    }
    
    #endregion
    
    #region Embestida con Puñetazo
    
    /// <summary>
    /// Embestida: El Golem corre rápidamente hacia el jugador y le da un puñetazo
    /// </summary>
    private IEnumerator ChargeAttack()
    {
        _isAttacking = true;
        _isCharging = true;
        _currentState = BossState.Charging;
        _lastChargeTime = Time.time;
        _lastAttackTime = Time.time;
        
        Log("🏃💨 ¡EMBESTIDA INICIADA!");
        Debug.Log($"[GolemBossAI] 🏃💨 ¡EMBESTIDA! El Golem corre hacia el jugador!");
        
        if (!player)
        {
            _isAttacking = false;
            _isCharging = false;
            yield break;
        }
        
        // Guardar posición objetivo inicial del jugador
        Vector3 targetPos = player.position;
        
        // Configurar NavMeshAgent para velocidad de carga
        if (agent && agent.isOnNavMesh)
        {
            agent.speed = chargeSpeed;
            agent.isStopped = false;
            agent.stoppingDistance = 1.5f; // Pararse cerca del jugador
        }
        
        // Reproducir animación de correr más rápido para mayor impacto
        PlayAnim(ANIM_WALK);
        
        // ✅ Acelerar la animación para que sea más impactante
        if (animator)
        {
            animator.speed = chargeAnimationSpeedMultiplier;
            Debug.Log($"[GolemBossAI] 🏃💨 Velocidad de animación aumentada a {chargeAnimationSpeedMultiplier}x para embestida!");
        }
        
        // Perseguir al jugador hasta estar cerca
        float chargeTimeout = 5f; // Máximo tiempo de persecución
        float elapsed = 0f;
        
        // ✅ NUEVO: Variables para saltitos durante embestida
        float hopInterval = 0.35f; // Intervalo entre saltos
        float lastHopTime = 0f;
        float hopHeight = 0.3f; // Altura del mini-salto
        float hopDuration = 0.15f; // Duración de cada salto
        bool isHopping = false;
        float hopStartTime = 0f;
        float originalY = transform.position.y;
        
        while (elapsed < chargeTimeout)
        {
            if (_isDead) yield break;
            
            elapsed += Time.deltaTime;
            
            // Actualizar destino hacia el jugador
            if (player && agent && agent.isOnNavMesh)
            {
                targetPos = player.position;
                agent.SetDestination(targetPos);
                
                // Debug: Verificar que la rotación se está aplicando correctamente
                if (debugMode)
                {
                    Debug.DrawRay(transform.position + Vector3.up * 2f, transform.forward * 5f, Color.green, 0.1f);
                    Debug.DrawRay(transform.position + Vector3.up * 2f, agent.velocity.normalized * 5f, Color.blue, 0.1f);
                }
            }
            
            // ✅ NUEVO: Aplicar saltitos para dar sensación de peso
            if (agent != null && agent.velocity.sqrMagnitude > 0.5f)
            {
                // Iniciar un nuevo salto si ha pasado el intervalo
                if (!isHopping && elapsed - lastHopTime >= hopInterval)
                {
                    isHopping = true;
                    hopStartTime = elapsed;
                    lastHopTime = elapsed;
                    originalY = transform.position.y;
                    
                    // Mini camera shake con cada paso
                    Sendero.Core.Feedback.FeedbackService.CameraShake(0.15f, 0.1f);
                }
                
                // Aplicar el salto parabólico
                if (isHopping)
                {
                    float hopProgress = (elapsed - hopStartTime) / hopDuration;
                    if (hopProgress >= 1f)
                    {
                        // Fin del salto
                        isHopping = false;
                        Vector3 pos = transform.position;
                        pos.y = originalY;
                        transform.position = pos;
                    }
                    else
                    {
                        // Curva parabólica para el salto
                        float height = Mathf.Sin(hopProgress * Mathf.PI) * hopHeight;
                        Vector3 pos = transform.position;
                        pos.y = originalY + height;
                        transform.position = pos;
                    }
                }
            }
            
            // Verificar si llegamos al jugador
            float distToPlayer = Vector3.Distance(transform.position, targetPos);
            if (distToPlayer <= 2.5f)
            {
                break; // Llegamos, hora de golpear
            }
            
            yield return null;
        }
        
        // ✅ Asegurar que volvemos a la altura original
        if (agent != null && agent.isOnNavMesh)
        {
            Vector3 finalPos = transform.position;
            finalPos.y = originalY;
            transform.position = finalPos;
        }
        
        // Parar movimiento
        StopAgent();
        
        // Restaurar velocidad normal
        if (agent)
        {
            agent.speed = walkSpeed;
            agent.stoppingDistance = 0f;
        }
        
        // ✅ Restaurar velocidad normal de la animación
        if (animator)
        {
            animator.speed = 1f;
            Debug.Log($"[GolemBossAI] 🏃 Velocidad de animación restaurada a normal (1.0x)");
        }
        
        // ¡PUÑETAZO!
        yield return StartCoroutine(ExecutePunch());
        
        _isAttacking = false;
        _isCharging = false;
        
        Log("🏃 Embestida completada");
    }
    
    /// <summary>
    /// Ejecuta el puñetazo al final de la embestida
    /// </summary>
    private IEnumerator ExecutePunch()
    {
        Log("👊 ¡PUÑETAZO!");
        Debug.Log($"[GolemBossAI] 👊 ¡PUÑETAZO!");
        
        // Animación de ataque con la otra mano
        PlayAnim(ANIM_ATTACK02);
        
        // Esperar al momento del impacto
        yield return new WaitForSeconds(0.4f);
        
        // Aplicar daño en área
        ApplyPunchDamage();
        
        // VFX de impacto
        if (punchImpactVFX)
        {
            // TODO: Usar un sistema de pooling
            Vector3 impactPos = transform.position + transform.forward * 2f;
            GameObject vfx = Instantiate(punchImpactVFX, impactPos, Quaternion.identity);
            Destroy(vfx, 2f);
        }
        
        // Esperar fin de animación
        yield return new WaitForSeconds(0.6f);
    }
    
    /// <summary>
    /// Aplica daño del puñetazo a objetivos cercanos
    /// </summary>
    private void ApplyPunchDamage()
    {
        Vector3 punchCenter = transform.position + transform.forward * 1.5f + Vector3.up;
        
        Debug.Log($"[GolemBossAI] 👊 Verificando daño de puñetazo en posición {punchCenter}, radio {punchRadius}");
        
        int hitCount = Physics.OverlapSphereNonAlloc(punchCenter, punchRadius, _punchHitBuffer); // ✅ OPTIMIZACIÓN: NonAlloc
        
        Debug.Log($"[GolemBossAI] 👊 Detectados {hitCount} colliders en el área");
        
        for (int i = 0; i < hitCount; i++)
        {
            var hit = _punchHitBuffer[i];
            Debug.Log($"[GolemBossAI] 👊 Evaluando collider: {hit.name} (Tag: {hit.tag})");
            
            // No dañarse a sí mismo
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                Debug.Log($"[GolemBossAI] 👊 Ignorando a sí mismo: {hit.name}");
                continue;
            }
            
            // Verificar si es el jugador o un aliado
            bool isPlayer = hit.CompareTag("Player");
            var partyMember = hit.GetComponent<Game.NPC.NPCPartyMember>();
            bool isAlly = partyMember != null;
            
            Debug.Log($"[GolemBossAI] 👊 {hit.name}: isPlayer={isPlayer}, isAlly={isAlly}");
            
            if (isPlayer || isAlly)
            {
                // ✅ VERIFICAR SI ESTÁ DEFENDIENDO
                var shieldController = hit.GetComponent<PlayerShieldController>();
                if (shieldController != null && shieldController.IsDefending)
                {
                    Debug.Log($"[GolemBossAI] 🛡️ {hit.name} está DEFENDIENDO - ¡Puñetazo BLOQUEADO!");
                    continue; // No aplicar daño
                }
                
                // Calcular dirección del knockback
                Vector3 knockbackDir = (hit.transform.position - transform.position).normalized;
                knockbackDir.y = 0.2f;
                knockbackDir.Normalize();
                
                // ✅ FIX: Aplicar daño con ambos sistemas (PlayerHealthSystem y Damageable)
                bool damageApplied = false;
                
                // Intentar PlayerHealthSystem primero (para jugador)
                var playerHealth = hit.GetComponent<PlayerHealthSystem>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(punchDamage);
                    damageApplied = true;
                    Log($"👊 Puñetazo golpeó a {hit.name} por {punchDamage}");
                    Debug.Log($"[GolemBossAI] 👊 ✅ DAÑO APLICADO a {hit.name} via PlayerHealthSystem por {punchDamage}!");
                }
                else
                {
                    // Fallback: Damageable (para NPCs aliados)
                    var hitDamageable = hit.GetComponent<Damageable>();
                    if (hitDamageable != null)
                    {
                        hitDamageable.TakeDamage(punchDamage);
                        damageApplied = true;
                        Log($"👊 Puñetazo golpeó a {hit.name} por {punchDamage}");
                        Debug.Log($"[GolemBossAI] 👊 ✅ DAÑO APLICADO a {hit.name} via Damageable por {punchDamage}!");
                    }
                }
                
                if (!damageApplied)
                {
                    Debug.LogWarning($"[GolemBossAI] 👊 ⚠️ {hit.name} no tiene PlayerHealthSystem ni Damageable!");
                }
                
                // Aplicar knockback
                var rb = hit.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    rb.AddForce(knockbackDir * punchKnockback, ForceMode.Impulse);
                    Debug.Log($"[GolemBossAI] 👊 Knockback aplicado a {hit.name}");
                }
            }
        }
        
        // Debug visual opcional
        Debug.DrawRay(punchCenter, Vector3.up * 2f, Color.red, 2f);
    }
    
    #endregion
    
    #region Fase 3 - Salto y Onda Expansiva
    
    /// <summary>
    /// Ataque especial de Fase 3: Salta hacia el jugador y genera una onda expansiva al aterrizar
    /// </summary>
    private IEnumerator JumpAttack()
    {
        _isAttacking = true;
        _isJumping = true;
        _currentState = BossState.Jumping;
        _lastJumpAttackTime = Time.time;
        _lastAttackTime = Time.time;
        
        StopAgent();
        
        Log("🦘 [Fase 3] ¡SALTO HACIA EL JUGADOR!");
        Debug.Log($"[GolemBossAI] 🦘 [Fase 3] ¡SALTO HACIA EL JUGADOR!");
        
        // Guardar posición inicial
        _jumpStartPos = transform.position;
        
        // Calcular posición objetivo (donde está el jugador ahora)
        if (!player)
        {
            _isAttacking = false;
            _isJumping = false;
            yield break;
        }
        
        _jumpTargetPos = player.position;
        
        // Animación de preparación del salto
        PlayAnim(ANIM_JUMP);
        
        // Pequeña pausa antes del salto (preparación)
        yield return new WaitForSeconds(0.3f);
        
        // Desactivar NavMeshAgent durante el salto
        if (agent)
        {
            agent.enabled = false;
        }
        
        // Ejecutar el salto (curva parabólica)
        float elapsed = 0f;
        float halfDuration = jumpDuration / 2f;
        
        // Fase de subida
        while (elapsed < halfDuration)
        {
            if (_isDead) yield break;
            
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            
            // Interpolación horizontal
            Vector3 horizontalPos = Vector3.Lerp(_jumpStartPos, _jumpTargetPos, t * 0.5f);
            
            // Curva de altura (subida)
            float heightT = Mathf.Sin(t * Mathf.PI * 0.5f); // 0 a 1 suavemente
            float currentHeight = _jumpStartPos.y + (jumpHeight * heightT);
            
            transform.position = new Vector3(horizontalPos.x, currentHeight, horizontalPos.z);
            
            // La rotación ya se maneja en Update
            
            yield return null;
        }
        
        // Fase de bajada
        elapsed = 0f;
        Vector3 peakPos = transform.position;
        
        while (elapsed < halfDuration)
        {
            if (_isDead) yield break;
            
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            
            // Interpolación horizontal (segunda mitad del viaje) - CORREGIDO
            Vector3 horizontalPos = Vector3.Lerp(peakPos, _jumpTargetPos, t);
            
            // Curva de altura (bajada acelerada)
            float heightT = 1f - (t * t); // Caída acelerada
            float currentHeight = _jumpTargetPos.y + (jumpHeight * heightT);
            
            // Aplicar posición correctamente
            transform.position = new Vector3(horizontalPos.x, currentHeight, horizontalPos.z);
            
            yield return null;
        }
        
        // Aterrizaje - asegurar posición final exacta
        transform.position = _jumpTargetPos;
        
        Debug.Log($"[GolemBossAI] 🦘 ¡ATERRIZAJE! Posición: {transform.position}");
        
        // Reactivar NavMeshAgent
        if (agent)
        {
            agent.enabled = true;
            agent.Warp(transform.position);
            // ✅ Asegurar que updateRotation siga desactivado para control manual de rotación
            agent.updateRotation = false;
        }
        
        // ¡IMPACTO! - Generar onda expansiva
        CreateShockwave();
        
        // Esperar recuperación
        yield return new WaitForSeconds(1.0f);
        
        _isAttacking = false;
        _isJumping = false;
        
        Log("🦘 Salto completado");
    }
    
    /// <summary>
    /// Crea la onda expansiva al aterrizar
    /// </summary>
    private void CreateShockwave()
    {
        Vector3 impactPos = transform.position;
        
        Log($"💥 [Fase 3] ¡ONDA EXPANSIVA! Radio: {shockwaveRadius}, Daño: {shockwaveDamage}");
        Debug.Log($"[GolemBossAI] 💥 [Fase 3] ¡ONDA EXPANSIVA! en {impactPos}, Radio: {shockwaveRadius}m, Daño: {shockwaveDamage}");
        
        // VFX de onda expansiva
        if (shockwaveVFX)
        {
            // TODO: Usar un sistema de pooling
            GameObject vfx = Instantiate(shockwaveVFX, impactPos, Quaternion.identity);
            vfx.transform.localScale = Vector3.one * (shockwaveRadius / 5f); // Escalar según radio
            Destroy(vfx, 3f);
            Debug.Log($"[GolemBossAI] 💥 VFX de onda expansiva creado");
        }
        else
        {
            Debug.LogWarning($"[GolemBossAI] 💥 ⚠️ No hay VFX de onda expansiva configurado!");
        }
        
        // VFX de polvo al aterrizar
        if (landingDustVFX)
        {
            // TODO: Usar un sistema de pooling
            GameObject dust = Instantiate(landingDustVFX, impactPos, Quaternion.identity);
            dust.transform.localScale = Vector3.one * 2f;
            Destroy(dust, 3f);
            Debug.Log($"[GolemBossAI] 💥 VFX de polvo creado");
        }
        
        // Temblor de cámara
        TriggerCameraShake();
        
        // Detectar y dañar objetivos en el área
        ApplyShockwaveDamage(impactPos);
    }
    
    /// <summary>
    /// Aplica daño y knockback a todos los objetivos en el radio de la onda
    /// </summary>
    private void ApplyShockwaveDamage(Vector3 center)
    {
        Debug.Log($"[GolemBossAI] 💥 Verificando daño de onda expansiva en {center}, radio {shockwaveRadius}");
        
        // Buscar todos los colliders en el radio
        int hitCount = Physics.OverlapSphereNonAlloc(center, shockwaveRadius, _shockwaveHitBuffer); // ✅ OPTIMIZACIÓN: NonAlloc
        
        Debug.Log($"[GolemBossAI] 💥 Detectados {hitCount} colliders en el área de onda");
        
        for (int i = 0; i < hitCount; i++)
        {
            var hit = _shockwaveHitBuffer[i];
            Debug.Log($"[GolemBossAI] 💥 Evaluando collider: {hit.name} (Tag: {hit.tag})");
            
            // No dañarse a sí mismo
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                Debug.Log($"[GolemBossAI] 💥 Ignorando a sí mismo: {hit.name}");
                continue;
            }
            
            // Verificar si es el jugador o un aliado
            bool isPlayer = hit.CompareTag("Player");
            var partyMember = hit.GetComponent<Game.NPC.NPCPartyMember>();
            bool isAlly = partyMember != null;
            
            Debug.Log($"[GolemBossAI] 💥 {hit.name}: isPlayer={isPlayer}, isAlly={isAlly}");
            
            if (isPlayer || isAlly)
            {
                // ✅ VERIFICAR SI ESTÁ DEFENDIENDO
                var shieldController = hit.GetComponent<PlayerShieldController>();
                if (shieldController != null && shieldController.IsDefending)
                {
                    Debug.Log($"[GolemBossAI] 🛡️ {hit.name} está DEFENDIENDO - ¡Onda expansiva BLOQUEADA!");
                    // Reproducir efecto de bloqueo si existe
                    continue; // No aplicar daño
                }
                
                // Calcular dirección del knockback (desde el centro hacia el objetivo)
                Vector3 knockbackDir = (hit.transform.position - center).normalized;
                knockbackDir.y = 0.3f; // Añadir componente vertical para que salte un poco
                knockbackDir.Normalize();
                
                // ✅ FIX: Aplicar daño con ambos sistemas (PlayerHealthSystem y Damageable)
                bool damageApplied = false;
                
                // Intentar PlayerHealthSystem primero (para jugador)
                var playerHealth = hit.GetComponent<PlayerHealthSystem>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(shockwaveDamage);
                    damageApplied = true;
                    Log($"💥 Onda dañó a {hit.name} por {shockwaveDamage}");
                    Debug.Log($"[GolemBossAI] 💥 ✅ DAÑO APLICADO a {hit.name} via PlayerHealthSystem por {shockwaveDamage}!");
                }
                else
                {
                    // Fallback: Damageable (para NPCs aliados)
                    var hitDamageable = hit.GetComponent<Damageable>();
                    if (hitDamageable != null)
                    {
                        hitDamageable.TakeDamage(shockwaveDamage);
                        damageApplied = true;
                        Log($"💥 Onda dañó a {hit.name} por {shockwaveDamage}");
                        Debug.Log($"[GolemBossAI] 💥 ✅ DAÑO APLICADO a {hit.name} via Damageable por {shockwaveDamage}!");
                    }
                }
                
                if (!damageApplied)
                {
                    Debug.LogWarning($"[GolemBossAI] 💥 ⚠️ {hit.name} no tiene PlayerHealthSystem ni Damageable!");
                }
                
                // Aplicar knockback via Rigidbody si existe
                var rb = hit.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    rb.AddForce(knockbackDir * shockwaveKnockback, ForceMode.Impulse);
                    Debug.Log($"[GolemBossAI] 💥 Knockback aplicado a {hit.name}");
                }
                
                // Alternativa: usar CharacterController para empujar
                var cc = hit.GetComponent<CharacterController>();
                if (cc != null)
                {
                    // El knockback se aplicará en el siguiente frame via el sistema de movimiento
                    // Por ahora solo hacemos daño, el knockback es opcional
                    Debug.Log($"[GolemBossAI] 💥 {hit.name} tiene CharacterController (knockback manual necesario)");
                }
            }
        }
        
        // Debug visual opcional
        Debug.DrawRay(center, Vector3.up * 5f, Color.yellow, 3f);
    }
    
    /// <summary>
    /// Activa el temblor de cámara (si hay un sistema disponible)
    /// </summary>
    private void TriggerCameraShake()
    {
        // Intentar usar Cinemachine Impulse si está disponible
        // Nota: Requiere que haya un CinemachineImpulseListener en la cámara virtual
        try
        {
            // Buscar cualquier componente de shake en la cámara principal
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                // Intentar encontrar un componente genérico de shake
                var shakeComponents = mainCam.GetComponents<MonoBehaviour>();
                foreach (var comp in shakeComponents)
                {
                    // Buscar método Shake por reflection (para compatibilidad)
                    var shakeMethod = comp.GetType().GetMethod("Shake");
                    if (shakeMethod != null)
                    {
                        shakeMethod.Invoke(comp, new object[] { cameraShakeDuration, cameraShakeIntensity });
                        return;
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Log($"⚠️ No se pudo activar camera shake: {ex.Message}");
        }
    }
    
    #endregion
    
    #region Daño por Contacto
    
    /// <summary>
    /// Verifica si el jugador está lo suficientemente cerca para recibir daño por contacto
    /// </summary>
    private void CheckContactDamage(float distance)
    {
        if (!enableContactDamage) return;
        if (player == null) return;
        
        // Verificar cooldown
        if (Time.time - _lastContactDamageTime < contactDamageCooldown) return;
        
        // Verificar si el jugador está dentro del radio de contacto
        if (distance <= contactRadius)
        {
            ApplyContactDamage();
        }
    }
    
    /// <summary>
    /// Aplica daño al jugador por contacto/colisión con el Golem
    /// </summary>
    private void ApplyContactDamage()
    {
        if (player == null) return;
        
        // ✅ VERIFICAR SI ESTÁ DEFENDIENDO
        var shieldController = player.GetComponent<PlayerShieldController>();
        if (shieldController != null && shieldController.IsDefending)
        {
            Debug.Log($"[GolemBossAI] 🛡️ Jugador está DEFENDIENDO - ¡Daño por contacto BLOQUEADO!");
            return; // No aplicar daño
        }
        
        // Calcular daño (aumentado durante embestida)
        float damage = contactDamage;
        if (_isCharging)
        {
            damage *= chargeDamageMultiplier;
            Debug.Log($"[GolemBossAI] 💥 ¡COLISIÓN DURANTE EMBESTIDA! Daño aumentado a {damage}");
        }
        
        // ✅ FIX: Intentar ambos sistemas de daño (PlayerHealthSystem y Damageable)
        var playerHealth = player.GetComponent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            _lastContactDamageTime = Time.time;
            
            Log($"💥 Daño por contacto: {damage} (Embestida: {_isCharging})");
            Debug.Log($"[GolemBossAI] 💥 ✅ DAÑO POR CONTACTO aplicado al jugador via PlayerHealthSystem: {damage} (distancia: {Vector3.Distance(transform.position, player.position):F2}m)");
            return;
        }
        
        // Fallback: intentar Damageable
        var playerDamageable = player.GetComponent<Damageable>();
        if (playerDamageable != null)
        {
            playerDamageable.TakeDamage(damage);
            _lastContactDamageTime = Time.time;
            
            Log($"💥 Daño por contacto: {damage} (Embestida: {_isCharging})");
            Debug.Log($"[GolemBossAI] 💥 ✅ DAÑO POR CONTACTO aplicado al jugador via Damageable: {damage} (distancia: {Vector3.Distance(transform.position, player.position):F2}m)");
            return;
        }
        
        Debug.LogWarning($"[GolemBossAI] 💥 ⚠️ El jugador no tiene PlayerHealthSystem ni Damageable!");
    }
    
    #endregion

    #region Daño y Muerte

    private void OnDamageTaken(float amount)
    {
        if (_isDead) return;
        
        Log($"💥 Recibió {amount} de daño");
        
        // No interrumpir ataques con animación de daño
        if (!_isAttacking)
        {
            StartCoroutine(TakeDamageAnim());
        }
    }

    private IEnumerator TakeDamageAnim()
    {
        PlayAnim(ANIM_GETHIT);
        yield return new WaitForSeconds(0.5f);
        
        if (!_isDead && !_isAttacking)
        {
            PlayAnim(ANIM_IDLE);
        }
    }

    private void OnDeath()
    {
        if (_isDead) return;
        
        _isDead = true;
        _currentState = BossState.Dead;
        
        Log("💀 Golem derrotado");
        
        StopAllCoroutines();
        StopAgent();
        
        PlayAnim(ANIM_DIE);
        
        if (_registeredInCombat)
        {
            ActiveCombatRegistry.UnregisterNPC(gameObject);
            _registeredInCombat = false;
        }
    }

    #endregion

    #region Animaciones

    private void PlayAnim(string animName)
    {
        if (!animator) return;
        
        try
        {
            animator.Play(animName, 0, 0f);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[GolemBossAI] Error al reproducir animación '{animName}': {ex.Message}");
        }
    }

    #endregion

    #region Debug

    private void Log(string msg)
    {
        if (debugMode)
        {
            Debug.Log($"[GolemBossAI] {msg}");
        }
    }

    void OnDrawGizmosSelected()
    {
        // Dibujar forward del Golem (verde = hacia donde mira)
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position + Vector3.up * 2f, transform.forward * 5f);
        
        // Dibujar dirección hacia el jugador (rojo)
        if (player != null)
        {
            Gizmos.color = Color.red;
            Vector3 toPlayer = (player.position - transform.position).normalized;
            Gizmos.DrawRay(transform.position + Vector3.up * 2.2f, toPlayer * 5f);
        }
        
        // Dibujar velocidad del NavMeshAgent (azul)
        if (agent != null && agent.velocity.sqrMagnitude > 0.01f)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position + Vector3.up * 1.8f, agent.velocity.normalized * 5f);
        }
        
        // Dibujar rangos
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    #endregion
}
