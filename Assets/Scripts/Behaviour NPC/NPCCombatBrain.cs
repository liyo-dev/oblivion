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
        // Difficulty configurada desde NPCBehaviourManager (via Settings)

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
        }

        NPCBehaviourManager _ctx;
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
        // Animator validation
        bool _printedAnimatorValidation;

        public void Initialize(NPCBehaviourManager ctx)
        {
            _ctx = ctx;
            _agent = ctx ? ctx.Agent : null;
            _animator = ctx ? ctx.Animator : null;
            _rawAnimator = ctx ? ctx.GetComponent<Animator>() : GetComponent<Animator>();
        }

        public void BeginCombat(Settings settings)
        {
            _settings = settings;
            _ctx?.EnsurePlayerReference();
            _player = _ctx ? _ctx.Player : null;

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
            _nextBurstCount = Mathf.Clamp(UnityEngine.Random.Range(
                Mathf.Max(1, _settings.burstAttacksMin),
                Mathf.Max(Mathf.Max(1, _settings.burstAttacksMin), _settings.burstAttacksMax + 1)), 1, 3);

            // Ventanas de quieto
            _holdTimer = 0f;
            _holdIntervalTimer = UnityEngine.Random.Range(
                Mathf.Max(0.5f, _settings.holdIntervalMin),
                Mathf.Max(Mathf.Max(0.5f, _settings.holdIntervalMin), _settings.holdIntervalMax));

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
        }

        IEnumerator CombatLoop()
        {
            float repathTimer = 0f;

            Debug.Log($"[NPCCombatBrain] ===== CombatLoop INICIADO ===== _ctx: {_ctx != null}, _player: {_player != null}, _agent: {_agent != null}, _animator: {_animator != null}");
            int iterationCount = 0;

            while (_ctx != null && _ctx.isActiveAndEnabled && _player != null)
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
                iterationCount++;
                if (iterationCount <= 5 || iterationCount % 100 == 0)
                {
                    Debug.Log($"[NPCCombatBrain] CombatLoop iteración #{iterationCount} - Estado: {_currentState}");
                }

                _ctx.EnsurePlayerReference();
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

                repathTimer -= Time.deltaTime;
                
                // Decidir acción basada en distancia y estado
                bool inAttackRange = distanceToPlayer >= _settings.minDistance && distanceToPlayer <= _settings.maxDistance;
                bool tooClose = distanceToPlayer < _settings.minDistance;
                bool tooFar = distanceToPlayer > _settings.maxDistance;

                if (iterationCount <= 5 || iterationCount % 100 == 0)
                {
                    Debug.Log($"[NPCCombatBrain] Distancia: {distanceToPlayer:F2}, Estado: {_currentState}, inRange: {inAttackRange}, tooClose: {tooClose}, tooFar: {tooFar}");
                }

                // Timers auxiliares
                if (_circleFlipTimer > 0f) _circleFlipTimer -= Time.deltaTime;
                if (_dodgeCdTimer > 0f) _dodgeCdTimer -= Time.deltaTime;
                if (_attackLockTimer > 0f) _attackLockTimer -= Time.deltaTime;
                if (_postAttackHoldTimer > 0f) _postAttackHoldTimer -= Time.deltaTime;

                if (_pendingDodge && _dodgeCdTimer <= 0f)
                {
                    _pendingDodge = false;
                    Vector3 dodgeTarget = ComputeDodgePosition();
                    if (_ctx.EnsureAgentOnNavMesh(_settings.sightRadius))
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
                    if (_ctx.EnsureAgentOnNavMesh(_settings.sightRadius))
                    {
                        NavMeshAgentUtility.SetDestination(_agent, burstTarget, 0.25f);
                        _burstCdTimer = Mathf.Max(0.5f, _settings.burstRepositionCooldown);
#if UNITY_EDITOR
                        _lastRepositionTarget = burstTarget; _hasRepositionGizmo = true;
#endif
                    }
                }

                // Comportamiento según estado y distancia
                if (tooClose)
                {
                    // Demasiado cerca: retroceder sin atacar
                    Vector3 retreatTarget = ComputeRetreatPosition(distanceToPlayer);
                    
                    if (_postAttackHoldTimer <= 0f && !_isWindup && repathTimer <= 0f && _ctx.EnsureAgentOnNavMesh(_settings.sightRadius))
                    {
                        NavMeshAgentUtility.SetDestination(_agent, retreatTarget, 0.5f);
                        repathTimer = _settings.repathInterval;
                        
                        if (iterationCount <= 5)
                            Debug.Log($"[NPCCombatBrain] 🏃 RETROCEDIENDO desde distancia {distanceToPlayer:F2}");
                    }

                    if (_postAttackHoldTimer > 0f || _isWindup)
                    {
                        NavMeshAgentUtility.SafeSetStopped(_agent, true);
                        _animator?.ResetMovement();
                        _animator?.PlayBattleIdle();
                        FacePlayer();
                    }
                    else
                    {
                        float speed = NavMeshAgentUtility.ComputeSpeedFactor(_agent);
                        _animator?.SetMovementSpeed(speed, 0.08f);
                        FaceMovement(); // evitar andar de espaldas al retroceder
                    }
                }
                else if (tooFar)
                {
                    // Demasiado lejos: acercarse
                    Vector3 approachTarget = ComputeApproachPosition(distanceToPlayer);
                    
                    if (_postAttackHoldTimer <= 0f && !_isWindup && repathTimer <= 0f && _ctx.EnsureAgentOnNavMesh(_settings.sightRadius))
                    {
                        NavMeshAgentUtility.SetDestination(_agent, approachTarget, 0.5f);
                        repathTimer = _settings.repathInterval;
                        
                        if (iterationCount <= 5)
                            Debug.Log($"[NPCCombatBrain] 🏃 ACERCÁNDOSE desde distancia {distanceToPlayer:F2}");
                    }

                    if (_postAttackHoldTimer > 0f || _isWindup)
                    {
                        NavMeshAgentUtility.SafeSetStopped(_agent, true);
                        _animator?.ResetMovement();
                        _animator?.PlayBattleIdle();
                        FacePlayer();
                    }
                    else
                    {
                        float speed = NavMeshAgentUtility.ComputeSpeedFactor(_agent);
                        _animator?.SetMovementSpeed(speed, 0.08f);
                        FaceMovement();
                    }
                }
                else if (inAttackRange)
                {
                    // En rango de ataque: decidir entre atacar, circular o mantener posición
                    bool hasAttackReady = HasAttackAvailable();
                    bool clearLos = !_settings.requireLineOfSight || HasLineOfSight();

                    if (hasAttackReady && clearLos && _attackLockTimer <= 0f && !_isWindup && ShouldAttackNow(distanceToPlayer))
                    {
                        // Detenerse y atacar
                        NavMeshAgentUtility.SafeSetStopped(_agent, true);
                        _animator?.ResetMovement();
                        _animator?.PlayBattleIdle();
                        FacePlayer();
                        
                        TryExecuteAttack();
                    }
                    else
                    {
                        // Micro-pausa ocasional para variar el ritmo
                        if (_microPauseTimer > 0f)
                        {
                            _microPauseTimer -= Time.deltaTime;
                            NavMeshAgentUtility.SafeSetStopped(_agent, true);
                            _animator?.PlayBattleIdle();
                            FacePlayer();
                        }
                        else
                        {
                            // Ventanas de quieto para no moverse todo el rato
                            if (_holdTimer > 0f || _postAttackHoldTimer > 0f || _isWindup)
                            {
                                if (_holdTimer > 0f) _holdTimer -= Time.deltaTime;
                                NavMeshAgentUtility.SafeSetStopped(_agent, true);
                                _animator?.PlayBattleIdle();
                                FacePlayer();
                            }
                            else
                            {
                                // Circular alrededor del jugador para variar posición
                                Vector3 circleTarget = ComputeCirclePosition(distanceToPlayer);
                                if (repathTimer <= 0f && _ctx.EnsureAgentOnNavMesh(_settings.sightRadius))
                                {
                                    NavMeshAgentUtility.SetDestination(_agent, circleTarget, 0.5f);
                                    repathTimer = _settings.repathInterval * 0.5f; // Actualizar más frecuentemente al circular
                                }
                                float speed = NavMeshAgentUtility.ComputeSpeedFactor(_agent);
                                _animator?.SetMovementSpeed(speed * 0.7f, 0.08f);
                                FaceMovement();

                                // Programar la siguiente micro-pausa
                                _microPauseIntervalTimer -= Time.deltaTime;
                                if (_microPauseIntervalTimer <= 0f)
                                {
                                    float dmin = Mathf.Max(0f, _settings.microPauseDurationMin);
                                    float dmax = Mathf.Max(dmin, _settings.microPauseDurationMax);
                                    _microPauseTimer = UnityEngine.Random.Range(dmin, dmax);
                                    float imin = Mathf.Max(0.2f, _settings.microPauseIntervalMin);
                                    float imax = Mathf.Max(imin, _settings.microPauseIntervalMax);
                                    _microPauseIntervalTimer = UnityEngine.Random.Range(imin, imax);
                                }

                                // Programar ventana de quieto de vez en cuando
                                _holdIntervalTimer -= Time.deltaTime;
                                if (_holdIntervalTimer <= 0f)
                                {
                                    float hmin = Mathf.Max(0.2f, _settings.holdDurationMin);
                                    float hmax = Mathf.Max(hmin, _settings.holdDurationMax);
                                    _holdTimer = UnityEngine.Random.Range(hmin, hmax);
                                    float i2min = Mathf.Max(0.6f, _settings.holdIntervalMin);
                                    float i2max = Mathf.Max(i2min, _settings.holdIntervalMax);
                                    _holdIntervalTimer = UnityEngine.Random.Range(i2min, i2max);
                                }

                                // Invertir sentido de vez en cuando para parecer humano
                                if (_circleFlipTimer <= 0f)
                                {
                                    _circleClockwise = !_circleClockwise;
                                    float min = Mathf.Max(0.4f, _settings.strafeFlipMin);
                                    float max = Mathf.Max(min, _settings.strafeFlipMax);
                                    _circleFlipTimer = UnityEngine.Random.Range(min, max);
                                }
                            }
                        }
                    }
                }

                yield return null;
            }

            Debug.Log($"[NPCCombatBrain] ===== CombatLoop TERMINADO ===== (iteraciones: {iterationCount})");
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
            var health = _ctx?.GetComponent<Damageable>();
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
            Vector3 offset = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * radius;

            return _player.position + offset;
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

        void TryExecuteAttack()
        {
            // Priorizar el ataque especial si está disponible en estado agresivo
            if (_currentState == CombatState.Aggressive && _specialAttackCooldown <= 0f && !string.IsNullOrEmpty(_settings.specialAttack.animationState))
            {
                TryExecuteWithWindup(_settings.specialAttack, () => { _specialAttackCooldown = _settings.specialAttack.cooldown; });
                return;
            }

            // Elegir entre left y right si están disponibles
            bool leftReady = _leftAttackCooldown <= 0f && !string.IsNullOrEmpty(_settings.leftAttack.animationState);
            bool rightReady = _rightAttackCooldown <= 0f && !string.IsNullOrEmpty(_settings.rightAttack.animationState);
            bool specialReady = _specialAttackCooldown <= 0f && !string.IsNullOrEmpty(_settings.specialAttack.animationState);

            // Crear lista de ataques disponibles
            var availableAttacks = new System.Collections.Generic.List<(AttackSlot slot, System.Action resetCooldown)>();
            
            if (leftReady)
                availableAttacks.Add((_settings.leftAttack, () => _leftAttackCooldown = _settings.leftAttack.cooldown));
            
            if (rightReady)
                availableAttacks.Add((_settings.rightAttack, () => _rightAttackCooldown = _settings.rightAttack.cooldown));
            
            if (specialReady)
                availableAttacks.Add((_settings.specialAttack, () => _specialAttackCooldown = _settings.specialAttack.cooldown));

            // Si no hay ataques disponibles, no hacer nada
            if (availableAttacks.Count == 0)
                return;

            // Elegir un ataque al azar
            int randomIndex = UnityEngine.Random.Range(0, availableAttacks.Count);
            var (selectedSlot, resetCooldown) = availableAttacks[randomIndex];
            TryExecuteWithWindup(selectedSlot, resetCooldown);
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
            while (t < delay)
            {
                if (_player == null) { cancelled = true; break; }
                if (_settings.requireLineOfSight && !HasLineOfSight()) { cancelled = true; break; }
                float d = Vector3.Distance(transform.position, _player.position);
                if (d < _settings.minDistance - 0.1f) { cancelled = true; break; }
                // Detenerse y mirar al jugador durante el wind-up
                NavMeshAgentUtility.SafeSetStopped(_agent, true);
                _animator?.PlayBattleIdle();
                FacePlayer();
                t += Time.deltaTime;
                yield return null;
            }
            if (cancelled)
            {
                _isWindup = false;
                _attackLockTimer = Mathf.Max(_attackLockTimer, 0.15f);
                yield break;
            }
            ExecuteAttack(slot);
            onExecuted?.Invoke();
            _isWindup = false;
            _attackLockTimer = Mathf.Max(_attackLockTimer, 0.35f);
            _postAttackHoldTimer = Mathf.Max(0f, _settings.attackHoldSeconds);
        }

        void ExecuteAttack(AttackSlot slot)
        {
            if (_animator == null || string.IsNullOrEmpty(slot.animationState))
                return;

            // Reproducir animación en la capa del upperBody
            // Asegurar que la capa esté visible
            _rawAnimator?.SetLayerWeight(_settings.upperBodyLayer, 1f);
            _animator.PlayOneShot(slot.animationState, _settings.upperBodyLayer);
            Debug.Log($"[NPCCombatBrain] ⚔️ Ejecutando {slot.animationState} (slot {slot.slotIndex}) en layer {_settings.upperBodyLayer}");

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
                _nextBurstCount = Mathf.Clamp(UnityEngine.Random.Range(
                    Mathf.Max(1, _settings.burstAttacksMin),
                    Mathf.Max(Mathf.Max(1, _settings.burstAttacksMin), _settings.burstAttacksMax + 1)), 1, 3);
                _burstPending = true;
            }
        }

        IEnumerator FireAfterDelay(int slotIndex, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            // Pequeña validación: si se canceló combate, no dispares
            if (_ctx == null || _player == null)
                yield break;
            _ctx.OnAttackTriggered(slotIndex);
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

            Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * Mathf.Max(0.1f, _settings.turnSpeed));
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
            Quaternion target = Quaternion.LookRotation(v.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * Mathf.Max(0.1f, _settings.turnSpeed));
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
