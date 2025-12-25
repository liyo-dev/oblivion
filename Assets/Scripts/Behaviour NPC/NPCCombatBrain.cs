using System;
using System.Collections;
using Game.NPC.Common;
using UnityEngine;
using UnityEngine.AI;

namespace Game.NPC
{
    /// <summary>
    /// Controla la IA de combate del NPC durante una batalla, separada del gestor principal.
    /// Maneja el movimiento para mantener la distancia y lanza las animaciones de ataque usando PlayOneShot.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(NPCSimpleAnimator))]
    public sealed class NPCCombatBrain : MonoBehaviour
    {
        // Difficulty configurada desde NPCBehaviourManagerV2 (via Settings)

        [Serializable]
        public struct AttackSlot
        {
            public string animationState;
            public float cooldown;
            public int slotIndex; // 0=left, 1=right, 2=special
        }

        [Serializable]
        public struct Settings
        {
            public float sightRadius;
            public float minDistance;
            public float maxDistance;
            public float repathInterval;
            public float retreatDistance;
            public float turnSpeed;
            public int upperBodyLayer;
            public string battleIdleState;
            
            // 3 slots de ataque con cooldowns independientes
            public AttackSlot leftAttack;
            public AttackSlot rightAttack;
            public AttackSlot specialAttack;
            
            // Configuración de comportamiento táctico
            public float aggressiveDistance;     // Por debajo de esta distancia, se vuelve más agresivo
            public float retreatHealthPercent;   // % de salud para activar modo defensivo
            public float circleDistance;         // Distancia para "circular" alrededor del jugador
            public float circleSpeed;            // Velocidad de circulación

            // Si true, el proyectil se spawnea mediante Animation Event en el clip
            public bool spawnProjectileViaAnimationEvent;
            // Retardo entre inicio de animación y disparo (cuando no se usan Animation Events)
            public float fireDelaySeconds;

            // Línea de visión y toma de decisiones adicionales
            public bool requireLineOfSight;      // Requiere LOS para atacar
            public LayerMask losMask;            // Capas que bloquean visión
            public float windupMin;              // Retraso mínimo antes de atacar
            public float windupMax;              // Retraso máximo antes de atacar
            public float strafeFlipMin;          // Tiempo mínimo para invertir sentido del círculo
            public float strafeFlipMax;          // Tiempo máximo para invertir sentido del círculo
            public float dodgeDistance;          // Distancia de esquiva lateral
            public float dodgeCooldown;          // CD de esquiva

            // Micro-pausas para ritmo humano
            public float microPauseDurationMin;
            public float microPauseDurationMax;
            public float microPauseIntervalMin;
            public float microPauseIntervalMax;

            // Burst & reposition tras X ataques
            public float burstRepositionDistance;
            public float burstRepositionCooldown;
            public int burstAttacksMin;
            public int burstAttacksMax;

            // Ventanas de quieto (mantener posición)
            public float holdDurationMin;
            public float holdDurationMax;
            public float holdIntervalMin;
            public float holdIntervalMax;

            // Mantener quieto alrededor del ataque (segundos). Incluye post-disparo breve.
            public float attackHoldSeconds;

            // Dificultad y sesgos
            public float attackFrequencyMultiplier; // escala de cooldown
            public float aggressionBias;             // sesgo de agresividad
            public float dodgeChance;                // probabilidad de esquiva
            
            // ✅ ESCUDO DEFENSIVO
            public bool useShield;                   // Si el NPC puede usar escudo
            public float shieldMinDuration;          // Duración mínima del escudo
            public float shieldMaxDuration;          // Duración máxima del escudo
            public float shieldCooldown;             // Cooldown entre usos del escudo
            
            // ✅ HUIDA TÁCTICA Y COBERTURA
            public bool useTacticalRetreat;          // Puede buscar cobertura cuando está en desventaja
            public float retreatHealthThreshold;     // % de salud para activar huida (ej: 0.3 = 30%)
            public float retreatCooldown;            // Cooldown entre intentos de huida (segundos)
            public float coverSearchRadius;          // Radio de búsqueda de cobertura (metros)
            public LayerMask coverLayerMask;         // Capas que se consideran cobertura (Default, Environment, etc.)
            public float minCoverDistance;           // Distancia mínima de la cobertura al NPC
            public float maxCoverDistance;           // Distancia máxima de la cobertura al NPC
            public float coverStayDuration;          // Tiempo que permanece en cobertura (segundos)
            public bool preferShieldOverCover;       // Si true, prioriza escudo sobre buscar cobertura
        }

        NPCBehaviourManagerV2 _manager;
        NPCStateContext _ctx;
        NavMeshAgent _agent;
        NPCSimpleAnimator _animator;
        Animator _rawAnimator;
        Transform _player;

        Coroutine _combatRoutine;
        Settings _settings;
        
        // Cooldowns individuales para cada slot
        float _leftAttackCooldown;
        float _rightAttackCooldown;
        float _specialAttackCooldown;
        
        // Control de estado de movimiento para evitar spam de PlayBattleIdle
        bool _wasMovingLastFrame;
        
        // Estados tácticos
        enum CombatState { Aggressive, Neutral, Defensive }
        CombatState _currentState = CombatState.Neutral;
        float _circleAngle;
        bool _circleClockwise;
        float _circleFlipTimer;
        float _dodgeCdTimer;
        bool _pendingDodge;

        // Micro-pausas y burst reposition
        float _microPauseTimer;
        float _microPauseIntervalTimer;
        float _burstCdTimer;
        bool _burstPending;
        int _attacksSinceBurst;
        int _nextBurstCount;
    #if UNITY_EDITOR
        Vector3 _lastRepositionTarget;
        bool _hasRepositionGizmo;
    #endif
        float _holdTimer;
        float _holdIntervalTimer;
        float _attackLockTimer;
        bool _isWindup;
        float _postAttackHoldTimer;
        
        // Smooth rotation
        float _currentTurnVelocity;
        const float _turnSmoothTime = 0.15f;
        
        // Smooth circular movement
        Vector3 _lastCircleTarget;
        Vector3 _circleVelocity;
        
        // Animator validation
        bool _printedAnimatorValidation;
        
        // ✅ ESCUDO DEFENSIVO
        NPCShieldController _shieldController;
        float _shieldCooldownTimer;
        bool _isDefending;
        
        // ✅ HUIDA TÁCTICA Y COBERTURA
        bool _isRetreating;                    // Flag de estado de huida
        float _retreatCooldownTimer;           // Timer de cooldown de huida
#pragma warning disable CS0414 // Campos de sistema de cobertura - en desarrollo
        Vector3? _coverPosition;               // Posición de cobertura encontrada
        float _coverStayTimer;                 // Tiempo restante en cobertura
        Transform _currentCoverObject;         // Objeto usado como cobertura
#pragma warning restore CS0414
        bool _isBehindCover;                   // Flag: está actualmente detrás de cobertura

        public void Initialize(NPCBehaviourManagerV2 manager)
        {
            _manager = manager;
            _ctx = manager != null ? manager.Context : null;
            _agent = _ctx != null ? _ctx.Agent : null;
            _animator = _ctx != null ? _ctx.Animator : null;
            _rawAnimator = _ctx != null ? _ctx.UnityAnimator : GetComponent<Animator>();
        }

        public void BeginCombat(Settings settings)
        {
            _settings = settings;
            
            // Ensure player reference
            if (_ctx != null && _ctx.Player == null)
            {
                if (PlayerService.TryGetComponent<Transform>(out var player))
                {
                    _ctx.Player = player;
                }
            }
            _player = _ctx != null ? _ctx.Player : null;

            Debug.Log($"[NPCCombatBrain] BeginCombat llamado - isActiveAndEnabled: {isActiveAndEnabled}, _player: {_player != null}, _ctx: {_ctx != null}");

            // Intentar detectar automáticamente la capa UpperBody por nombre si el índice no coincide
            if (_rawAnimator != null)
            {
                int current = Mathf.Clamp(_settings.upperBodyLayer, 0, _rawAnimator.layerCount > 0 ? _rawAnimator.layerCount - 1 : 0);
                int found = -1;
                for (int i = 0; i < _rawAnimator.layerCount; i++)
                {
                    string lname = _rawAnimator.GetLayerName(i);
                    if (!string.IsNullOrEmpty(lname) && lname.ToLowerInvariant().Contains("upper"))
                    {
                        found = i; break;
                    }
                }
                if (found >= 0 && found != current)
                {
                    Debug.Log($"[NPCCombatBrain] Ajustando upperBodyLayer {current} -> {found} por nombre de capa '{_rawAnimator.GetLayerName(found)}'");
                    _settings.upperBodyLayer = found;
                }
            }

            // Inicializar cooldowns en 0 para que pueda atacar inmediatamente
            // Escalar cooldowns por dificultad (más frecuencia => menor cooldown efectivo)
            float cdScale = Mathf.Clamp(_settings.attackFrequencyMultiplier, 0.2f, 3f);
            _leftAttackCooldown = 0f;
            _rightAttackCooldown = 0f;
            _specialAttackCooldown = 0f;
            
            // ✅ Inicializar escudo defensivo
            _shieldCooldownTimer = 0f;
            _isDefending = false;
            if (_settings.useShield)
            {
                _shieldController = GetComponent<NPCShieldController>();
                if (_shieldController == null)
                {
                    Debug.LogWarning($"[NPCCombatBrain] ⚠️ useShield=true pero no hay NPCShieldController en {gameObject.name}");
                }
                else
                {
                    Debug.Log($"[NPCCombatBrain] ✅ Shield controller encontrado");
                }
            }
            
            // ✅ Inicializar huida táctica y cobertura
            _isRetreating = false;
            _retreatCooldownTimer = 0f;
            _coverPosition = null;
            _coverStayTimer = 0f;
            _currentCoverObject = null;
            _isBehindCover = false;
            
            // Estado inicial neutral
            _currentState = CombatState.Neutral;
            _circleAngle = UnityEngine.Random.Range(0f, 360f);
            _circleClockwise = UnityEngine.Random.value > 0.5f;

            // Inicializar micro-pausas y burst reposition
            _microPauseTimer = 0f;
            _microPauseIntervalTimer = UnityEngine.Random.Range(
                Mathf.Max(0.2f, _settings.microPauseIntervalMin),
                Mathf.Max(Mathf.Max(0.2f, _settings.microPauseIntervalMin), _settings.microPauseIntervalMax));
            _burstCdTimer = 0f;
            _burstPending = false;
            _attacksSinceBurst = 0;
            // Generar número aleatorio de ataques antes de reposicionar (1-3 ataques)
            _nextBurstCount = UnityEngine.Random.Range(
                Mathf.Max(1, _settings.burstAttacksMin),
                Mathf.Max(1, _settings.burstAttacksMax) + 1);

            // Ventanas de quieto
            _holdTimer = 0f;
            _holdIntervalTimer = UnityEngine.Random.Range(
                Mathf.Max(0.5f, _settings.holdIntervalMin),
                Mathf.Max(Mathf.Max(0.5f, _settings.holdIntervalMin), _settings.holdIntervalMax));

            // Configurar NavMeshAgent para movimiento suave
            if (_agent != null)
            {
                _agent.acceleration = 8f; // Aceleración gradual
                _agent.angularSpeed = 180f; // Rotación moderada (no instantánea)
                _agent.autoBraking = true; // Frenado automático suave
                _agent.stoppingDistance = 0.1f;
            }
            
            // Inicializar valores de suavizado
            _lastCircleTarget = transform.position;
            _circleVelocity = Vector3.zero;
            _currentTurnVelocity = 0f;

            StopCombat();
            if (!isActiveAndEnabled)
            {
                Debug.LogWarning($"[NPCCombatBrain] ⚠️ No se puede iniciar combate: componente no activo (enabled: {enabled}, gameObject.activeInHierarchy: {gameObject.activeInHierarchy})");
                return;
            }
            
            if (_player == null)
            {
                Debug.LogWarning("[NPCCombatBrain] ⚠️ No se puede iniciar combate: _player es null");
                return;
            }
            
            if (_ctx == null)
            {
                Debug.LogWarning("[NPCCombatBrain] ⚠️ No se puede iniciar combate: _ctx es null");
                return;
            }
            
            // ✅ ACTIVAR MODO BATALLA - Esto es CRÍTICO para que funcione la locomoción
            if (_animator != null)
            {
                _animator.SetBattleMode(true);
                Debug.Log("[NPCCombatBrain] ✅ Modo batalla activado en Animator");
            }

            Debug.Log("[NPCCombatBrain] ✅ Iniciando CombatLoop()");
            _combatRoutine = StartCoroutine(CombatLoop());
        }

        public void StopCombat()
        {
            if (_combatRoutine != null)
            {
                StopCoroutine(_combatRoutine);
                _combatRoutine = null;
            }

            if (_agent)
                NavMeshAgentUtility.SafeSetStopped(_agent, true);

            _animator?.ResetMovement();
            
            // Desactivar modo batalla
            if (_animator != null)
            {
                _animator.SetBattleMode(false);
                Debug.Log("[NPCCombatBrain] Modo batalla desactivado");
            }
        }

        IEnumerator CombatLoop()
        {
            float repathTimer = 0f;

            Debug.Log($"[NPCCombatBrain] ===== CombatLoop INICIADO ===== _ctx: {_ctx != null}, _player: {_player != null}, _agent: {_agent != null}, _animator: {_animator != null}");

            while (_ctx != null && _manager != null && _manager.isActiveAndEnabled && _player != null)
            {
                if (!_printedAnimatorValidation && _rawAnimator != null)
                {
                    _printedAnimatorValidation = true;
                    int leftHash = Animator.StringToHash(_settings.leftAttack.animationState ?? "");
                    int rightHash = Animator.StringToHash(_settings.rightAttack.animationState ?? "");
                    int specialHash = Animator.StringToHash(_settings.specialAttack.animationState ?? "");
                    bool hasLeft = !string.IsNullOrEmpty(_settings.leftAttack.animationState) && _rawAnimator.HasState(_settings.upperBodyLayer, leftHash);
                    bool hasRight = !string.IsNullOrEmpty(_settings.rightAttack.animationState) && _rawAnimator.HasState(_settings.upperBodyLayer, rightHash);
                    bool hasSpecial = !string.IsNullOrEmpty(_settings.specialAttack.animationState) && _rawAnimator.HasState(_settings.upperBodyLayer, specialHash);
                    Debug.Log($"[NPCCombatBrain] Animator check (layer {_settings.upperBodyLayer}): left={hasLeft}, right={hasRight}, special={hasSpecial}");
                    if (!hasLeft || !hasRight || !hasSpecial)
                    {
                        bool baseLeft = !string.IsNullOrEmpty(_settings.leftAttack.animationState) && _rawAnimator.HasState(0, leftHash);
                        bool baseRight = !string.IsNullOrEmpty(_settings.rightAttack.animationState) && _rawAnimator.HasState(0, rightHash);
                        bool baseSpecial = !string.IsNullOrEmpty(_settings.specialAttack.animationState) && _rawAnimator.HasState(0, specialHash);
                        Debug.Log($"[NPCCombatBrain] Base layer check: left={baseLeft}, right={baseRight}, special={baseSpecial}");
                    }
                }

                // Ensure player reference
                if (_ctx != null && _ctx.Player == null)
                {
                    if (PlayerService.TryGetComponent<Transform>(out var player))
                    {
                        _ctx.Player = player;
                    }
                }
                _player = _ctx.Player;
                if (_player == null)
                {
                    Debug.LogWarning("[NPCCombatBrain] Player perdido durante combate");
                    break;
                }

                float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
                // Nota: el giro se decide por rama. Al atacar/pausar miramos al jugador; al movernos miramos la dirección de avance.
                
                // Actualizar estado táctico basado en salud (si está disponible)
                UpdateCombatState(distanceToPlayer);

                // Reducir cooldowns
                float cdStep = Time.deltaTime * Mathf.Clamp(_settings.attackFrequencyMultiplier, 0.2f, 3f);
                _leftAttackCooldown -= cdStep;
                _rightAttackCooldown -= cdStep;
                _specialAttackCooldown -= cdStep;
                
                // ✅ Reducir cooldown del escudo
                if (_shieldCooldownTimer > 0f)
                {
                    _shieldCooldownTimer -= Time.deltaTime;
                }
                
                // ✅ Reducir cooldown de huida táctica
                UpdateRetreatCooldown();

                repathTimer -= Time.deltaTime;
                
                // Decidir acción basada en distancia y estado
                bool inAttackRange = distanceToPlayer >= _settings.minDistance && distanceToPlayer <= _settings.maxDistance;
                bool tooClose = distanceToPlayer < _settings.minDistance;
                bool tooFar = distanceToPlayer > _settings.maxDistance;


                // Timers auxiliares
                if (_circleFlipTimer > 0f) _circleFlipTimer -= Time.deltaTime;
                if (_dodgeCdTimer > 0f) _dodgeCdTimer -= Time.deltaTime;
                if (_attackLockTimer > 0f) _attackLockTimer -= Time.deltaTime;
                if (_postAttackHoldTimer > 0f) _postAttackHoldTimer -= Time.deltaTime;

                if (_pendingDodge && _dodgeCdTimer <= 0f)
                {
                    _pendingDodge = false;
                    Vector3 dodgeTarget = ComputeDodgePosition();
                    if (EnsureAgentOnNavMesh(_settings.sightRadius))
                    {
                        NavMeshAgentUtility.SetDestination(_agent, dodgeTarget, 0.25f);
                        _dodgeCdTimer = Mathf.Max(0.25f, _settings.dodgeCooldown);
                    }
                }

                // Burst & reposition programado
                if (_burstPending && _burstCdTimer <= 0f)
                {
                    _burstPending = false;
                    Vector3 burstTarget = ComputeBurstRepositionPosition();
                    if (EnsureAgentOnNavMesh(_settings.sightRadius))
                    {
                        NavMeshAgentUtility.SetDestination(_agent, burstTarget, 0.25f);
                        _burstCdTimer = Mathf.Max(0.5f, _settings.burstRepositionCooldown);
#if UNITY_EDITOR
                        _lastRepositionTarget = burstTarget; _hasRepositionGizmo = true;
#endif
                    }
                }

                // =====================================================
                // ESTRATEGIA SIMPLE: PARADO para atacar, MOVERSE para reposicionar
                // =====================================================
                
                bool hasAttackReady = HasAttackAvailable();
                bool clearLos = !_settings.requireLineOfSight || HasLineOfSight();
                
                // ✅ PRIORIDAD 1: Si puede atacar → PARADO y atacar
                if (hasAttackReady && clearLos && _attackLockTimer <= 0f && !_isWindup && inAttackRange)
                {
                    // PARADO - Atacar
                    StopAndIdle();
                    FacePlayer();
                    TryExecuteAttack();
                    Debug.Log($"[NPCCombatBrain] ⚔️ PARADO - Atacando");
                }
                // ✅ PRIORIDAD 2: Si está en windup o post-ataque → PARADO
                else if (_isWindup || _postAttackHoldTimer > 0f)
                {
                    // PARADO - Esperando
                    StopAndIdle();
                    FacePlayer();
                    Debug.Log($"[NPCCombatBrain] ⏸️ PARADO - Esperando (windup={_isWindup}, postAttack={_postAttackHoldTimer:F2})");
                }
                // ✅ PRIORIDAD 3: Necesita reposicionarse → MOVERSE
                else
                {
                    // MOVIMIENTO - Buscar nueva posición
                    Vector3 targetPos;
                    
                    if (tooClose)
                    {
                        // Retroceder
                        targetPos = ComputeRetreatPosition(distanceToPlayer);
                        Debug.Log($"[NPCCombatBrain] 🏃 MOVIENDO - Retrocediendo");
                    }
                    else if (tooFar)
                    {
                        // Acercarse
                        targetPos = ComputeApproachPosition(distanceToPlayer);
                        Debug.Log($"[NPCCombatBrain] 🏃 MOVIENDO - Acercándose");
                    }
                    else
                    {
                        // Circular
                        targetPos = ComputeCirclePosition(distanceToPlayer);
                        Debug.Log($"[NPCCombatBrain] 🏃 MOVIENDO - Circulando");
                    }
                    
                    // Actualizar destino NavMesh
                    if (repathTimer <= 0f && EnsureAgentOnNavMesh(_settings.sightRadius))
                    {
                        NavMeshAgentUtility.SetDestination(_agent, targetPos, 0.5f);
                        repathTimer = _settings.repathInterval;
                    }
                    
                    // ACTIVAR LOCOMOCIÓN
                    float speed = NavMeshAgentUtility.ComputeSpeedFactor(_agent);
                    StartMoving(speed);
                    FaceMovement();
                }

                // Invertir sentido circular de vez en cuando
                if (_circleFlipTimer <= 0f)
                {
                    _circleClockwise = !_circleClockwise;
                    float min = Mathf.Max(0.4f, _settings.strafeFlipMin);
                    float max = Mathf.Max(min, _settings.strafeFlipMax);
                    _circleFlipTimer = UnityEngine.Random.Range(min, max);
                }

                yield return null;
            }

            Debug.Log($"[NPCCombatBrain] ===== CombatLoop TERMINADO =====");
            StopCombat();
        }

        Vector3 ComputeTarget(float currentDistance)
        {
            if (_player == null)
                return transform.position;

            // Si está demasiado lejos, acercarse
            if (currentDistance > _settings.maxDistance)
                return _player.position;

            // Si está demasiado cerca, alejarse
            if (currentDistance < _settings.minDistance)
            {
                Vector3 away = (transform.position - _player.position).normalized;
                float retreat = Mathf.Max(_settings.retreatDistance, 0.5f);
                return transform.position + away * retreat;
            }

            // Si está en rango de ataque, mantener posición
            return transform.position;
        }

        void UpdateCombatState(float distance)
        {
            // Obtener salud si está disponible
            var health = _ctx != null ? _ctx.Transform.GetComponent<Damageable>() : null;
            float healthPercent = health != null ? (health.Current / health.Max) : 1f;

            // Cambiar a defensivo si la salud es baja
            if (healthPercent < _settings.retreatHealthPercent)
            {
                if (_currentState != CombatState.Defensive)
                {
                    _currentState = CombatState.Defensive;
                    Debug.Log("[NPCCombatBrain] 🛡️ Cambiando a estado DEFENSIVO (salud baja)");
                }
                return;
            }

            // Cambiar a agresivo si el jugador está muy cerca
            if (distance < _settings.aggressiveDistance)
            {
                if (_currentState != CombatState.Aggressive)
                {
                    _currentState = CombatState.Aggressive;
                    Debug.Log("[NPCCombatBrain] ⚔️ Cambiando a estado AGRESIVO (jugador cerca)");
                }
                return;
            }

            // Estado neutral por defecto
            if (_currentState != CombatState.Neutral)
            {
                _currentState = CombatState.Neutral;
                Debug.Log("[NPCCombatBrain] ⚖️ Cambiando a estado NEUTRAL");
            }
        }

        Vector3 ComputeRetreatPosition(float currentDistance)
        {
            if (_player == null)
                return transform.position;

            Vector3 away = (transform.position - _player.position).normalized;
            float retreat = Mathf.Max(_settings.retreatDistance, 2f);
            return transform.position + away * retreat;
        }

        Vector3 ComputeApproachPosition(float currentDistance)
        {
            if (_player == null)
                return transform.position;

            // En estado defensivo, no acercarse tanto
            if (_currentState == CombatState.Defensive)
            {
                Vector3 toPlayer = (_player.position - transform.position).normalized;
                float targetDist = (_settings.minDistance + _settings.maxDistance) * 0.75f; // 75% del rango
                return transform.position + toPlayer * (currentDistance - targetDist);
            }

            // En otros estados, acercarse directamente
            return _player.position;
        }

        Vector3 ComputeCirclePosition(float currentDistance)
        {
            if (_player == null)
                return transform.position;

            // Avanzar el ángulo de circulación
            float angleStep = _settings.circleSpeed * Time.deltaTime;
            _circleAngle += _circleClockwise ? angleStep : -angleStep;

            // Mantener el ángulo en rango [0, 360]
            if (_circleAngle > 360f) _circleAngle -= 360f;
            if (_circleAngle < 0f) _circleAngle += 360f;

            // Calcular posición en círculo alrededor del jugador
            float radius = _settings.circleDistance > 0f ? _settings.circleDistance : currentDistance;
            float radians = _circleAngle * Mathf.Deg2Rad;
            Vector3 rawTarget = _player.position + new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * radius;

            // Suavizar con SmoothDamp para evitar cambios bruscos
            Vector3 smoothedTarget = Vector3.SmoothDamp(_lastCircleTarget, rawTarget, ref _circleVelocity, 0.3f);
            _lastCircleTarget = smoothedTarget;

            return smoothedTarget;
        }

        bool HasAttackAvailable()
        {
            return _leftAttackCooldown <= 0f || _rightAttackCooldown <= 0f || _specialAttackCooldown <= 0f;
        }

        bool ShouldAttackNow(float distance)
        {
            // Base probabilities
            float pNeutral = 0.5f;
            float pAgg = Mathf.Lerp(0.6f, 0.9f, _settings.aggressionBias);
            float pDef = Mathf.Lerp(0.15f, 0.35f, _settings.aggressionBias);
            float mult = Mathf.Clamp(_settings.attackFrequencyMultiplier, 0.2f, 3f);
            float p;
            if (_currentState == CombatState.Aggressive) p = pAgg;
            else if (_currentState == CombatState.Defensive) p = pDef;
            else p = pNeutral;
            // Scale by multiplier but cap to 0.95 to avoid spam
            p = Mathf.Clamp01(p * Mathf.Lerp(0.6f, 1.4f, (mult - 0.2f) / (3f - 0.2f)));
            p = Mathf.Min(p, 0.95f);
            return UnityEngine.Random.value < p;
        }

        int _lastUsedAttackSlot = -1;  // Track último ataque usado
        
        void TryExecuteAttack()
        {
            // Elegir entre left, right y special de forma INTELIGENTE
            bool leftReady = _leftAttackCooldown <= 0f && !string.IsNullOrEmpty(_settings.leftAttack.animationState);
            bool rightReady = _rightAttackCooldown <= 0f && !string.IsNullOrEmpty(_settings.rightAttack.animationState);
            bool specialReady = _specialAttackCooldown <= 0f && !string.IsNullOrEmpty(_settings.specialAttack.animationState);

            // ✅ PENALIZAR repetir el mismo ataque
            float leftPenalty = (_lastUsedAttackSlot == 0) ? 0.2f : 1f;
            float rightPenalty = (_lastUsedAttackSlot == 1) ? 0.2f : 1f;
            float specialPenalty = (_lastUsedAttackSlot == 2) ? 0.3f : 1f;

            // Crear lista de ataques disponibles con PESOS variables
            var availableAttacks = new System.Collections.Generic.List<(AttackSlot slot, System.Action resetCooldown, float weight)>();
            
            if (leftReady)
            {
                // ✅ Peso base + penalización si fue el último usado
                float baseWeight = UnityEngine.Random.Range(0.8f, 1.2f);
                float weight = baseWeight * leftPenalty;
                availableAttacks.Add((_settings.leftAttack, () => {
                    // ✅ RESPETAR cooldown del config con variabilidad MÍNIMA (±10%)
                    float variance = UnityEngine.Random.Range(0.9f, 1.1f);
                    _leftAttackCooldown = _settings.leftAttack.cooldown * variance;
                    _lastUsedAttackSlot = 0;
                    Debug.Log($"[NPCCombatBrain] 🔄 LEFT cooldown: {_leftAttackCooldown:F2}s (config: {_settings.leftAttack.cooldown:F2}s)");
                }, weight));
            }
            
            if (rightReady)
            {
                float baseWeight = UnityEngine.Random.Range(0.8f, 1.2f);
                float weight = baseWeight * rightPenalty;
                availableAttacks.Add((_settings.rightAttack, () => {
                    float variance = UnityEngine.Random.Range(0.9f, 1.1f);
                    _rightAttackCooldown = _settings.rightAttack.cooldown * variance;
                    _lastUsedAttackSlot = 1;
                    Debug.Log($"[NPCCombatBrain] 🔄 RIGHT cooldown: {_rightAttackCooldown:F2}s (config: {_settings.rightAttack.cooldown:F2}s)");
                }, weight));
            }
            
            if (specialReady)
            {
                // ✅ El especial tiene peso variable según el estado + penalización
                float baseWeight = _currentState == CombatState.Aggressive ? 
                    UnityEngine.Random.Range(1.2f, 1.8f) :  // Más probable en agresivo
                    UnityEngine.Random.Range(0.5f, 1f);     // Menos probable en neutral/defensivo
                float weight = baseWeight * specialPenalty;
                    
                availableAttacks.Add((_settings.specialAttack, () => {
                    float variance = UnityEngine.Random.Range(0.9f, 1.1f);
                    _specialAttackCooldown = _settings.specialAttack.cooldown * variance;
                    _lastUsedAttackSlot = 2;
                    Debug.Log($"[NPCCombatBrain] 🔄 SPECIAL cooldown: {_specialAttackCooldown:F2}s (config: {_settings.specialAttack.cooldown:F2}s)");
                }, weight));
            }

            // Si no hay ataques disponibles, no hacer nada
            if (availableAttacks.Count == 0)
            {
                Debug.Log($"[NPCCombatBrain] ⏳ Esperando cooldowns... LEFT:{_leftAttackCooldown:F1}s RIGHT:{_rightAttackCooldown:F1}s SPECIAL:{_specialAttackCooldown:F1}s");
                
                // ✅ SISTEMA DE HUIDA TÁCTICA
                // Evaluar si debe huir/buscar cobertura o simplemente defenderse
                bool shouldRetreat = ShouldRetreat();
                
                if (shouldRetreat && _settings.useTacticalRetreat && _retreatCooldownTimer <= 0f)
                {
                    // Prioridad 1: Buscar cobertura si está habilitado y no prefiere escudo
                    if (!_settings.preferShieldOverCover)
                    {
                        if (TryFindAndMoveToCover())
                        {
                            // Movimiento a cobertura iniciado
                            return;
                        }
                    }
                    
                    // Prioridad 2: Usar escudo si está disponible
                    if (_settings.useShield)
                    {
                        TryActivateShield();
                        return;
                    }
                    
                    // Prioridad 3: Si prefiere escudo pero no puede, buscar cobertura
                    if (_settings.preferShieldOverCover)
                    {
                        if (TryFindAndMoveToCover())
                        {
                            return;
                        }
                    }
                }
                else if (_settings.useShield)
                {
                    // Si no debe huir pero tiene escudo, usarlo como defensa pasiva
                    TryActivateShield();
                }
                
                return;
            }

            // Elegir un ataque usando SELECCIÓN PONDERADA aleatoria
            float totalWeight = 0f;
            foreach (var (_, _, weight) in availableAttacks)
                totalWeight += weight;
            
            float randomValue = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;
            
            foreach (var (slot, resetCooldown, weight) in availableAttacks)
            {
                cumulative += weight;
                if (randomValue <= cumulative)
                {
                    TryExecuteWithWindup(slot, resetCooldown);
                    return;
                }
            }
            
            // Fallback: elegir el último si algo falla
            var lastAttack = availableAttacks[availableAttacks.Count - 1];
            TryExecuteWithWindup(lastAttack.slot, lastAttack.resetCooldown);
        }

        void TryExecuteWithWindup(AttackSlot slot, System.Action onExecuted)
        {
            if (_attackLockTimer > 0f || _isWindup)
                return;
            float min = Mathf.Max(0f, _settings.windupMin);
            float max = Mathf.Max(min, _settings.windupMax);
            float delay = (max > 0f) ? UnityEngine.Random.Range(min, max) : 0f;
            if (delay <= 0f)
            {
                ExecuteAttack(slot);
                onExecuted?.Invoke();
                _attackLockTimer = 0.3f;
            }
            else
            {
                _isWindup = true;
                _attackLockTimer = Mathf.Max(_attackLockTimer, delay + 0.05f);
                StartCoroutine(DoWindup(slot, onExecuted, delay));
            }
        }

        IEnumerator DoWindup(AttackSlot slot, System.Action onExecuted, float delay)
        {
            float t = 0f;
            bool cancelled = false;
            bool isFacingPlayer = false;
            
            while (t < delay || !isFacingPlayer)
            {
                if (_player == null) { cancelled = true; break; }
                if (_settings.requireLineOfSight && !HasLineOfSight()) { cancelled = true; break; }
                float d = Vector3.Distance(transform.position, _player.position);
                if (d < _settings.minDistance - 0.1f) { cancelled = true; break; }
                
                // Detenerse y mirar al jugador durante el wind-up
                StopAndIdle();
                FacePlayer();
                
                // ✅ VERIFICAR si está mirando al player (ángulo < 15°)
                Vector3 dirToPlayer = (_player.position - transform.position).normalized;
                dirToPlayer.y = 0f;
                Vector3 forward = transform.forward;
                forward.y = 0f;
                float angle = Vector3.Angle(forward, dirToPlayer);
                isFacingPlayer = angle < 15f;  // Debe estar casi de frente
                
                t += Time.deltaTime;
                yield return null;
            }
            
            if (cancelled)
            {
                _isWindup = false;
                _attackLockTimer = Mathf.Max(_attackLockTimer, 0.15f);
                Debug.Log($"[NPCCombatBrain] ❌ Ataque CANCELADO (no mira al player o sin LoS)");
                yield break;
            }
            
            // ✅ Solo atacar si está mirando al player
            if (!isFacingPlayer)
            {
                _isWindup = false;
                _attackLockTimer = Mathf.Max(_attackLockTimer, 0.2f);
                Debug.Log($"[NPCCombatBrain] ❌ Ataque CANCELADO (no está de frente al player)");
                yield break;
            }
            
            Debug.Log($"[NPCCombatBrain] ✅ ATACANDO - Mirando al player correctamente");
            ExecuteAttack(slot);
            onExecuted?.Invoke();
            _isWindup = false;
            _attackLockTimer = Mathf.Max(_attackLockTimer, 0.35f);
            _postAttackHoldTimer = Mathf.Max(0f, _settings.attackHoldSeconds);
        }

        void ExecuteAttack(AttackSlot slot)
        {
            // ✅ Reproducir animación directamente en el Animator usando CrossFade
            if (_rawAnimator != null && !string.IsNullOrEmpty(slot.animationState))
            {
                int animHash = Animator.StringToHash(slot.animationState);
                int targetLayer = _settings.upperBodyLayer;
                
                // Verificar que la animación existe en el UpperBody layer
                if (!_rawAnimator.HasState(targetLayer, animHash))
                {
                    // Si no está en UpperBody, buscar en base layer
                    if (_rawAnimator.HasState(0, animHash))
                    {
                        targetLayer = 0;
                        Debug.Log($"[NPCCombatBrain] Animación '{slot.animationState}' encontrada en base layer");
                    }
                    else
                    {
                        Debug.LogWarning($"[NPCCombatBrain] ⚠️ Animación '{slot.animationState}' NO EXISTE en el Animator");
                        return;
                    }
                }
                
                // Asegurar que el layer esté visible
                if (targetLayer > 0)
                {
                    _rawAnimator.SetLayerWeight(targetLayer, 1f);
                }
                
                // Reproducir animación con CrossFade corto para transición rápida
                _rawAnimator.CrossFadeInFixedTime(slot.animationState, 0.1f, targetLayer);
                
                string handName = slot.slotIndex == 0 ? "LEFT" : slot.slotIndex == 1 ? "RIGHT" : "SPECIAL";
                Debug.Log($"[NPCCombatBrain] ⚔️ Ejecutando spell cast {handName} (slot {slot.slotIndex}) - Animación: {slot.animationState}");
                
                // ✅ Marcar que estamos en medio de un cast para poder interrumpirlo
                var lifecycleHandler = GetComponent<Modules.NPCCombatLifecycleHandler>();
                if (lifecycleHandler != null)
                {
                    lifecycleHandler.StartCasting(slot.animationState, targetLayer);
                    Debug.Log($"[NPCCombatBrain] 🎭 Casting iniciado - puede ser interrumpido por daño");
                }
                
                // ✅ Iniciar coroutine para monitorear cuando termina la animación
                StartCoroutine(MonitorSpellCastEnd(slot.animationState, targetLayer));
            }
            else
            {
                Debug.LogWarning($"[NPCCombatBrain] ⚠️ No se puede ejecutar ataque: Animator={_rawAnimator != null}, Animation={slot.animationState}");
            }

            // SIEMPRE disparar proyectil (con o sin animación)
            // Si NO usamos Animation Events, disparar proyectil tras un retardo configurable
            if (_ctx != null && !_settings.spawnProjectileViaAnimationEvent)
            {
                float delay = Mathf.Max(0f, _settings.fireDelaySeconds);
                StartCoroutine(FireAfterDelay(slot.slotIndex, delay));
            }

            // Contar ataques para el patrón "burst & reposition"
            _attacksSinceBurst++;
            if (_burstCdTimer <= 0f && _attacksSinceBurst >= _nextBurstCount)
            {
                _attacksSinceBurst = 0;
                
                // ✅ Generar burst con distribución INTELIGENTE:
                // - 40% probabilidad: 1 ataque solo
                // - 35% probabilidad: 2 ataques
                // - 20% probabilidad: 3 ataques
                // - 5% probabilidad: 4 ataques
                float roll = UnityEngine.Random.value;
                if (roll < 0.4f)
                {
                    _nextBurstCount = 1;  // Ataque único (40%)
                }
                else if (roll < 0.75f)
                {
                    _nextBurstCount = 2;  // Ráfaga corta (35%)
                }
                else if (roll < 0.95f)
                {
                    _nextBurstCount = 3;  // Ráfaga media (20%)
                }
                else
                {
                    _nextBurstCount = 4;  // Ráfaga larga (5%)
                }
                
                _burstPending = true;
                
                Debug.Log($"[NPCCombatBrain] ✅ Burst completado - próximo burst: {_nextBurstCount} ataques");
            }
        }
        
        /// <summary>
        /// Monitorea cuando termina la animación de spell cast
        /// </summary>
        IEnumerator MonitorSpellCastEnd(string animationName, int layer)
        {
            // Esperar un frame para que la animación empiece
            yield return null;
            
            // Esperar a que la animación actual sea la del spell cast
            float timeout = 0f;
            while (!_rawAnimator.GetCurrentAnimatorStateInfo(layer).IsName(animationName) && timeout < 0.5f)
            {
                timeout += Time.deltaTime;
                yield return null;
            }
            
            // Esperar a que la animación termine (normalizedTime >= 0.9)
            while (_rawAnimator.GetCurrentAnimatorStateInfo(layer).IsName(animationName))
            {
                var stateInfo = _rawAnimator.GetCurrentAnimatorStateInfo(layer);
                if (stateInfo.normalizedTime >= 0.9f)
                {
                    break;
                }
                yield return null;
            }
            
            // La animación terminó - limpiar estado de casting
            var lifecycleHandler = GetComponent<Modules.NPCCombatLifecycleHandler>();
            if (lifecycleHandler != null)
            {
                lifecycleHandler.EndCasting();
            }
            
            Debug.Log($"[NPCCombatBrain] ✅ Spell cast '{animationName}' completado");
        }

        IEnumerator FireAfterDelay(int slotIndex, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            
            // Validación: si se canceló combate, no dispares
            if (_ctx == null || _player == null || _manager == null)
                yield break;
            
            // Obtener el prefab del hechizo desde el config
            var combatConfig = _ctx.Config?.combatConfig;
            if (combatConfig == null)
            {
                Debug.LogWarning($"[NPCCombatBrain] No hay combatConfig para disparar hechizo");
                yield break;
            }
            
            GameObject spellPrefab = combatConfig.GetSpellPrefab(slotIndex);
            
            if (spellPrefab == null)
            {
                Debug.LogWarning($"[NPCCombatBrain] No hay prefab configurado para slot {slotIndex}");
                yield break;
            }
            
            // Punto de origen del hechizo (frente al NPC, a altura del pecho)
            Vector3 spawnPosition = transform.position + Vector3.up * 1.5f + transform.forward * 0.5f;
            
            // Dirección hacia el jugador
            Vector3 directionToPlayer = (_player.position + Vector3.up * 1.0f) - spawnPosition;
            directionToPlayer.Normalize();
            Quaternion spawnRotation = Quaternion.LookRotation(directionToPlayer);
            
            // Instanciar el proyectil/hechizo
            GameObject spellInstance = UnityEngine.Object.Instantiate(spellPrefab, spawnPosition, spawnRotation);
            
            // ✅ INICIALIZAR EL PROYECTIL
            // Intentar obtener el componente EnemyProjectile
            var enemyProjectile = spellInstance.GetComponent<EnemyProjectile>();
            if (enemyProjectile != null)
            {
                // Obtener daño del config (usar el daño base del NPC)
                float damage = combatConfig.attackDamage;
                
                // Inicializar el proyectil con dirección y daño
                enemyProjectile.Initialize(directionToPlayer, damage);
                
                Debug.Log($"[NPCCombatBrain] 🔮 Hechizo disparado e inicializado: {spellPrefab.name} (slot {slotIndex}, daño: {damage})");
            }
            else
            {
                Debug.LogWarning($"[NPCCombatBrain] ⚠️ El prefab '{spellPrefab.name}' no tiene componente EnemyProjectile. El proyectil no se inicializó.");
            }
        }

        public void RequestDodge()
        {
            if (_dodgeCdTimer > 0f) return;
            if (UnityEngine.Random.value > Mathf.Clamp01(_settings.dodgeChance)) return;
            _pendingDodge = true;
        }

        bool HasLineOfSight()
        {
            if (_player == null) return false;
            Vector3 origin = transform.position + Vector3.up * 1.6f;
            Vector3 dest = _player.position + Vector3.up * 1.0f;
            Vector3 dir = dest - origin;
            float dist = dir.magnitude;
            if (dist <= 0.001f) return true;
            dir /= dist;

            // RaycastAll y omitir los colliders del propio NPC; bloquear si algo distinto al player se interpone
            var hits = Physics.RaycastAll(origin, dir, dist, _settings.losMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (!h.transform) continue;
                if (h.transform == transform || h.transform.IsChildOf(transform)) continue; // ignorar self
                if (h.transform == _player || h.transform.IsChildOf(_player)) return true;  // visión clara
                return false; // otro obstáculo bloquea
            }
            return true; // sin impactos relevantes
        }

        Vector3 ComputeDodgePosition()
        {
            if (_player == null) return transform.position;
            Vector3 toPlayer = (_player.position - transform.position); toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f) toPlayer = transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, toPlayer.normalized);
            float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            float dist = Mathf.Max(0.5f, _settings.dodgeDistance);
            Vector3 target = transform.position + right * side * dist;
            return target;
        }

        Vector3 ComputeBurstRepositionPosition()
        {
            if (_player == null) return transform.position;
            Vector3 toPlayer = (transform.position - _player.position); toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f) toPlayer = transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, toPlayer.normalized);
            // Reposicionar de forma lateral con ligera variación hacia delante/atrás del jugador
            float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            float lateral = Mathf.Max(0.5f, _settings.burstRepositionDistance);
            float forwardJitter = UnityEngine.Random.Range(-0.6f, 0.6f) * lateral * 0.5f;
            Vector3 target = _player.position + right * side * lateral + toPlayer.normalized * forwardJitter;
            return target;
        }

        void FacePlayer()
        {
            if (_player == null)
                return;

            Vector3 direction = _player.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            // ✅ Rotación rápida durante windup/ataque
            SmoothRotateTowards(direction, fast: _isWindup);
        }

        void FaceMovement()
        {
            if (_agent == null) return;
            Vector3 v = _agent.desiredVelocity; v.y = 0f;
            if (v.sqrMagnitude < 0.0001f)
            {
                if (_player != null)
                {
                    // como fallback, mira en sentido opuesto al jugador al retirarse
                    v = (transform.position - _player.position); v.y = 0f;
                }
            }
            if (v.sqrMagnitude < 0.0001f) return;
            
            SmoothRotateTowards(v);
        }
        
        /// <summary>
        /// Rotación suavizada usando SmoothDampAngle para movimiento más natural
        /// </summary>
        void SmoothRotateTowards(Vector3 direction, bool fast = false)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float currentAngle = transform.eulerAngles.y;
            
            // ✅ Rotación más rápida durante windup/ataque
            float smoothTime = fast ? 0.05f : _turnSmoothTime;
            float angle = Mathf.SmoothDampAngle(currentAngle, targetAngle, ref _currentTurnVelocity, smoothTime);
            
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        bool EnsureAgentOnNavMesh(float maxDistance = 5f)
        {
            if (_agent == null)
                return false;

            if (_agent.isOnNavMesh)
                return true;

            // Try to find closest point on NavMesh
            if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out var hit, maxDistance, _agent.areaMask))
            {
                _agent.Warp(hit.position);
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Detiene el agente y reproduce Battle Idle solo si estaba moviéndose
        /// Evita spam de PlayBattleIdle()
        /// </summary>
        void StopAndIdle()
        {
            NavMeshAgentUtility.SafeSetStopped(_agent, true);
            _animator?.ResetMovement();
            
            // Reactivar sync del NavMeshAgent cuando nos detenemos
            if (_animator != null)
            {
                var npcAnimator = _animator as NPCSimpleAnimator;
                if (npcAnimator != null)
                {
                    npcAnimator.syncWithNavAgent = true;
                }
            }
            
            // Solo llamar PlayBattleIdle si acabamos de detenernos
            if (_wasMovingLastFrame)
            {
                _animator?.PlayBattleIdle();
                _wasMovingLastFrame = false;
            }
        }
        
        /// <summary>
        /// Inicia movimiento y marca que está en movimiento
        /// </summary>
        void StartMoving(float speed)
        {
            // Desactivar sync del NavMeshAgent temporalmente para que no sobrescriba
            if (_animator != null)
            {
                var npcAnimator = _animator as NPCSimpleAnimator;
                if (npcAnimator != null)
                {
                    npcAnimator.syncWithNavAgent = false;
                }
            }
            
            _animator?.SetMovementSpeed(speed, 0.08f);
            _wasMovingLastFrame = true;
            
            Debug.Log($"[NPCCombatBrain] StartMoving({speed:F2}) - NavAgent sync desactivado temporalmente");
        }
        
        /// <summary>
        /// ✅ Intenta activar el escudo defensivo cuando no puede atacar
        /// </summary>
        void TryActivateShield()
        {
            // Verificar si puede usar escudo
            if (!_settings.useShield)
                return;
                
            if (_shieldController == null)
                return;
            
            // Ya está defendiendo
            if (_isDefending || _shieldController.IsDefending)
                return;
            
            // Escudo en cooldown
            if (_shieldCooldownTimer > 0f)
            {
                Debug.Log($"[NPCCombatBrain] 🛡️ Escudo en cooldown: {_shieldCooldownTimer:F1}s");
                return;
            }
            
            // Activar escudo
            float duration = UnityEngine.Random.Range(
                Mathf.Max(0.5f, _settings.shieldMinDuration),
                Mathf.Max(Mathf.Max(0.5f, _settings.shieldMinDuration), _settings.shieldMaxDuration)
            );
            
            _shieldController.StartDefending(duration);
            _isDefending = true;
            
            // Aplicar cooldown
            _shieldCooldownTimer = _settings.shieldCooldown;
            
            Debug.Log($"[NPCCombatBrain] 🛡️ ESCUDO ACTIVADO - Duración: {duration:F1}s, Cooldown: {_shieldCooldownTimer:F1}s");
            
            // Programar desactivación automática
            StartCoroutine(DeactivateShieldAfter(duration));
        }
        
        /// <summary>
        /// Desactiva el escudo después de un delay
        /// </summary>
        IEnumerator DeactivateShieldAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            _isDefending = false;
            Debug.Log($"[NPCCombatBrain] 🛡️ Escudo desactivado automáticamente");
        }
        
        // ========== SISTEMA DE HUIDA TÁCTICA Y COBERTURA ==========
        
        /// <summary>
        /// Evalúa si el NPC debe activar huida táctica
        /// </summary>
        bool ShouldRetreat()
        {
            // Verificar salud
            var health = _ctx?.Transform.GetComponent<Damageable>();
            if (health == null)
                return false;
                
            float healthPercent = health.Current / health.Max;
            
            // Huir si salud es baja
            if (healthPercent <= _settings.retreatHealthThreshold)
            {
                Debug.Log($"[NPCCombatBrain] 🏃 Salud baja ({healthPercent:P0}), activando huida táctica");
                return true;
            }
            
            // Huir si todos los ataques están en cooldown Y escudo también en cooldown
            bool allAttacksOnCooldown = !HasAttackAvailable();
            bool shieldOnCooldown = _shieldCooldownTimer > 0f || _isDefending;
            
            if (allAttacksOnCooldown && shieldOnCooldown && _currentState == CombatState.Defensive)
            {
                Debug.Log($"[NPCCombatBrain] 🏃 Sin ataques ni escudo disponibles en modo defensivo, buscando cobertura");
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Intenta encontrar cobertura y moverse hacia ella
        /// </summary>
        bool TryFindAndMoveToCover()
        {
            if (_isRetreating)
            {
                Debug.Log($"[NPCCombatBrain] Ya está en proceso de huida");
                return true;
            }
            
            // Verificar componente de huida táctica
            var retreatComponent = GetComponent<NPCTacticalRetreat>();
            if (retreatComponent == null)
            {
                Debug.LogWarning($"[NPCCombatBrain] ⚠️ useTacticalRetreat=true pero no hay NPCTacticalRetreat en {gameObject.name}");
                return false;
            }
            
            // Intentar iniciar huida
            if (!retreatComponent.StartRetreat(_player))
            {
                Debug.Log($"[NPCCombatBrain] ❌ No se pudo encontrar cobertura");
                return false;
            }
            
            _isRetreating = true;
            _retreatCooldownTimer = _settings.retreatCooldown;
            _isBehindCover = false;
            
            Debug.Log($"[NPCCombatBrain] 🏃 Iniciando huida táctica hacia cobertura");
            
            // Iniciar coroutine para gestionar el estado de cobertura
            StartCoroutine(ManageCoverState(retreatComponent));
            
            return true;
        }
        
        /// <summary>
        /// Gestiona el estado mientras el NPC está en cobertura
        /// </summary>
        IEnumerator ManageCoverState(NPCTacticalRetreat retreatComponent)
        {
            Debug.Log($"[NPCCombatBrain] 🛡️ En cobertura, esperando...");
            
            // Esperar hasta que llegue a la cobertura o se cancele
            while (_isRetreating && retreatComponent.IsRetreating)
            {
                // Actualizar el flag de estar detrás de cobertura
                _isBehindCover = retreatComponent.IsBehindCover;
                
                // Si está detrás de cobertura, puede activar el escudo como defensa adicional
                if (_isBehindCover && _settings.useShield && !_isDefending)
                {
                    TryActivateShield();
                }
                
                yield return null;
            }
            
            // Salir de la cobertura
            _isRetreating = false;
            _isBehindCover = false;
            
            Debug.Log($"[NPCCombatBrain] ✅ Saliendo de cobertura, volviendo a combate activo");
        }
        
        /// <summary>
        /// Reduce el cooldown de huida (se llama cada frame desde CombatLoop)
        /// </summary>
        void UpdateRetreatCooldown()
        {
            if (_retreatCooldownTimer > 0f)
            {
                _retreatCooldownTimer -= Time.deltaTime;
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!_hasRepositionGizmo) return;
            var prev = Gizmos.color;
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.9f); // verde
            Gizmos.DrawSphere(_lastRepositionTarget, 0.1f);
            if (transform != null)
                Gizmos.DrawLine(transform.position, _lastRepositionTarget);
            Gizmos.color = prev;
        }
#endif
    }
}

