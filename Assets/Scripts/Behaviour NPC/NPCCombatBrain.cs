using System;
using System.Collections;
using System.Collections.Generic;
using Game.NPC.Common;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Game.NPC
{
    /// <summary>
    /// Cerebro de Combate Táctico con FSM (Evaluate -> Reposition -> Attack -> Defense).
    /// Soporta Cobertura, Escudos y Dificultad Dinámica.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(NPCSimpleAnimator))]
    public sealed class NPCCombatBrain : MonoBehaviour
    {
        #region ⚙️ Configuration
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
            [Header("Distances & Movement")]
            public float minSafeDistance;       // Si player está más cerca, huye
            public float optimalDistance;       // Distancia ideal de combate
            public float maxDistance;           // Si player está más lejos, se acerca
            public float runSpeed;              // Velocidad al recolocarse
            public float walkSpeed;             // Velocidad al acercarse
            
            [Header("Attacks")]
            public int upperBodyLayer;
            public AttackSlot leftAttack;
            public AttackSlot rightAttack;
            public AttackSlot specialAttack;
            public float attackFrequencyMultiplier; // 1 = normal, 2 = doble de rápido
            public float globalCooldown;            // Pausa entre ataques
            public bool spawnProjectileViaAnimEvent;
            public float fireDelaySeconds;          // Si no usa anim events
            
            [Header("Mana")]
            public float maxMana;
            public float manaRegenPerSecond;
            public float manaRegenDelayAfterSpend;
            public float manaCostLeft;
            public float manaCostRight;
            public float manaCostSpecial;
            [Range(0f, 1f)] public float lowManaRetreatThreshold;

            [Header("Defense & Tactics")]
            [Range(0f, 1f)] public float difficultyLevel; // 0=Torpe, 1=Experto
            public bool useShield;
            public float shieldDuration;
            public float shieldCooldown;
            public LayerMask coverLayerMask;    // Capa de objetos que sirven de cobertura (Arboles, Cajas)
            public float coverSearchRadius;     // Qué tan lejos busca cobertura
            public float dodgeDistance;         // Distancia de salto lateral
            
            [Header("Line of Sight & Searching")]
            public LayerMask obstacleLayerMask; // Capas que bloquean la visión (Default, etc.)
            public float searchDuration;        // Tiempo que busca al jugador antes de rendirse
            public float searchMovementRadius;  // Radio de movimiento durante búsqueda
            public bool returnToOriginAfterSearch; // Si vuelve al origen después de buscar
            
            [Header("Search Behavior")]
            [Tooltip("Si está activado, el NPC se mueve activamente buscando al jugador. Si no, se queda quieto mostrando interrogación")]
            public bool activelySearchForPlayer; // ¿Busca activamente o se rinde?
            [Tooltip("Si está desactivado 'activelySearchForPlayer', cuánto tiempo espera antes de abandonar")]
            public float passiveSearchDuration; // Tiempo esperando antes de abandonar (si no busca activamente)
            
            [Header("Tactical Deception")]
            [Tooltip("Probabilidad de fingir quedarse sin magia para atraer al player (0 = nunca, 1 = siempre). Basado en dificultad")]
            [Range(0f, 1f)] public float deceptionChance; // Chance de usar estrategia de engaño
            [Tooltip("Mínimo de ataques que debe conservar cuando finge (ej: 1 = guarda al menos 1 ataque)")]
            [Range(1, 3)] public int minAttacksToKeepForAmbush; // Ataques que reserva para emboscada
        }
        #endregion

        #region 🔌 Dependencies & State
        public Settings settings; // Visible en inspector

        NPCBehaviourManagerV2 _manager;
        NPCStateContext _ctx;
        NavMeshAgent _agent;
        NPCSimpleAnimator _animator;
        Animator _rawAnimator;
        Transform _player;
        Transform _combatTarget; // Objetivo actual de ataque (jugador o miembro del equipo)
        NPCShieldController _shieldController;
        NPCAlertIconController _alertIconController; // Sistema de iconos visuales (usa prefabs)
        // FSM State
        public enum CombatState { EVALUATE, REPOSITION, ATTACK, DEFENSE, SEARCHING, HIDING_TO_RECHARGE }
        [SerializeField, ReadOnly] private CombatState _currentState; // Visible debug

        // Cooldowns
        float _leftCd, _rightCd, _specialCd, _shieldCd, _globalCd;
        
        // Mana
        [SerializeField, ReadOnly] private float _currentMana;
        [SerializeField, ReadOnly] private float _maxMana;
        private float _lastManaSpendTime = -999f;
        public event Action<float> OnManaChanged;
        public float CurrentMana => _currentMana;
        public float MaxMana => _maxMana;
        
        // Control de transiciones de estado para evitar nerviosismo
        private float _lastStateChangeTime;
        private const float MIN_STATE_DURATION = 1.5f; // Mínimo 1.5s en cada estado antes de cambiar
        private CombatState _previousState;
        
        // 🎭 ESTRATEGIA DE ENGAÑO
        private bool _isUsingDeceptionStrategy; // ¿Está fingiendo quedarse sin magia?
        private int _attacksReservedForAmbush; // Número de ataques que guarda para emboscada
        
        // Line of Sight & Searching
        bool _hasLineOfSight;
        float _lastSeenTime;
        Vector3 _lastKnownPlayerPosition;
        Vector3 _combatStartPosition; // Posición original para volver
        
        // ✅ OPTIMIZACIÓN: Throttling para CheckLineOfSight (reducir raycasts)
        private float _losCheckTimer;
        private const float LOS_CHECK_INTERVAL = 0.1f; // Verificar cada 0.1s en lugar de cada frame
        
        // 🔥 Memoria de combate reciente para evitar interrogación innecesaria
        private const float RECENT_COMBAT_THRESHOLD = 5f; // Si estuvo en combate hace menos de 5s, NO mostrar interrogación
        private float _lastCombatTime; // Timestamp del último momento en combate activo
        private bool _wasInRecentCombat => (Time.time - _lastCombatTime) < RECENT_COMBAT_THRESHOLD;
        
        // ✅ OPTIMIZACIÓN FASE 2: Buffers reutilizables para Physics queries (evita allocations)
        private Collider[] _projectileBuffer = new Collider[16];
        private Collider[] _coverBuffer = new Collider[32];
        private Collider[] _obstacleBuffer = new Collider[32];
        private RaycastHit[] _raycastBuffer = new RaycastHit[32];
        
        // ✅ OPTIMIZACIÓN: NavMeshPath reutilizable (evita allocations en MoveTo)
        private NavMeshPath _reusablePath;
        
        Coroutine _fsmRoutine;
        bool _isActive;
        #endregion

        // Referencia al config para acceder a prefabs de iconos
        private Modules.NPCCombatConfig _config;

        // Inicialización
        public void Initialize(NPCBehaviourManagerV2 manager)
        {
            _manager = manager;
            _ctx = manager.Context;
            _agent = _ctx.Agent;
            _animator = _ctx.Animator;
            _rawAnimator = _ctx.UnityAnimator;
            _shieldController = GetComponent<NPCShieldController>();
            
            // Buscar componente de iconos visuales si existe
            _alertIconController = _manager.GetComponent<NPCAlertIconController>();
            if (_alertIconController == null)
            {
                Debug.LogWarning($"[CombatBrain:{_manager.name}] ⚠️ NPCAlertIconController no encontrado - Los iconos visuales no se mostrarán");
            }

            // Configurar NavMesh para movimiento fluido
            _agent.updateRotation = false; // Controlamos la rotación manualmente para encarar al player
            _agent.updatePosition = true;
            _agent.acceleration = 12f;
            
            // ✅ OPTIMIZACIÓN: Inicializar NavMeshPath reutilizable
            _reusablePath = new NavMeshPath();
        }

        public void BeginCombat(Settings newSettings, Modules.NPCCombatConfig config = null)
        {
            settings = newSettings;
            _config = config; // Guardar referencia al config
            
            if (_fsmRoutine != null) StopCoroutine(_fsmRoutine);
            
            // Buscar player si no existe
            if (_ctx.Player == null)
                 _ctx.Player = PlayerService.PlayerTransform;
            _player = _ctx.Player;
            _combatTarget = _player;

            // Guardar posición inicial para poder volver
            _combatStartPosition = transform.position;
            _lastKnownPlayerPosition = _player.position;
            _lastSeenTime = Time.time;
            _hasLineOfSight = true;
            
            _maxMana = Mathf.Max(1f, settings.maxMana);
            _currentMana = _maxMana;
            _lastManaSpendTime = -999f;
            NotifyManaChanged();

            // ✅ Mostrar icono de admiración - ¡Te vi!
            if (_alertIconController != null && _config != null && _config.exclamationIconPrefab != null)
            {
                _alertIconController.ShowExclamation(_config.exclamationIconPrefab, _config.alertIconDuration);
            }

            _animator.SetBattleMode(true);
            _isActive = true;
            
            _fsmRoutine = StartCoroutine(FSM_Loop());
        }

        public void StopCombat()
        {
            _isActive = false;
            if (_fsmRoutine != null) StopCoroutine(_fsmRoutine);
            if (_agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
            _animator.SetBattleMode(false);
        }
        
        public float GetCurrentMana() => _currentMana;
        public float GetMaxMana() => _maxMana;
        public float GetManaPercent() => _maxMana > 0f ? _currentMana / _maxMana : 0f;
        
        /// <summary>
        /// Llamado cuando el NPC recibe daño. Si está buscando o huyendo, activa alerta inmediata.
        /// </summary>
        public void OnTakeDamage(Vector3 damageSourcePosition)
        {
            if (!_isActive) return;
            
            // Actualizar última posición conocida del player
            _lastKnownPlayerPosition = damageSourcePosition;
            _lastSeenTime = Time.time;
            
            // Calcular si fue atacado por la espalda
            Vector3 directionToDamage = (damageSourcePosition - transform.position).normalized;
            directionToDamage.y = 0;
            
            Vector3 forward = transform.forward;
            forward.y = 0;
            float angle = Vector3.Angle(forward, directionToDamage);
            bool attackedFromBehind = angle > 90f; // Más de 90 grados = espalda
            
            // Si está en estados vulnerables, reaccionar inmediatamente
            if (_currentState == CombatState.SEARCHING || 
                _currentState == CombatState.HIDING_TO_RECHARGE ||
                _currentState == CombatState.REPOSITION)
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] ⚠️ ¡ATACADO{(attackedFromBehind ? " POR LA ESPALDA" : "")}! Estado: {_currentState}");
                
                // GIRAR hacia la fuente del daño
                if (directionToDamage.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(directionToDamage);
                }
                
                // Reproducir animación de alerta SenseSomethingStart_NoWeapon
                if (_animator != null)
                {
                    _animator.PlaySenseSomething();
                    Debug.Log($"[CombatBrain:{gameObject.name}] 🎬 Reproduciendo animación SenseSomethingStart_NoWeapon");
                }
                
                // Mostrar icono de admiración
                if (_alertIconController != null && _config != null && _config.exclamationIconPrefab != null)
                {
                    _alertIconController.ShowExclamation(_config.exclamationIconPrefab, _config.alertIconDuration);
                }
                
                // Decidir reacción según situación
                int attacksAvailable = CountAttacksReady();
                
                if (attacksAvailable > 0)
                {
                    // Tiene ataques → Contraatacar
                    Debug.Log($"[CombatBrain:{gameObject.name}] ⚡ Contratatacando inmediatamente");
                    StopAllCoroutines();
                    _currentState = CombatState.EVALUATE;
                    _fsmRoutine = StartCoroutine(FSM_Loop());
                }
                else if (settings.useShield && _shieldController != null && _shieldCd <= 0)
                {
                    // No tiene ataques pero tiene escudo → Defender
                    Debug.Log($"[CombatBrain:{gameObject.name}] 🛡️ Activando escudo defensivo");
                    StopAllCoroutines();
                    _currentState = CombatState.DEFENSE;
                    _fsmRoutine = StartCoroutine(FSM_Loop());
                }
                else
                {
                    // No tiene ataques ni escudo → Seguir huyendo/buscando
                    Debug.Log($"[CombatBrain:{gameObject.name}] 🏃 Continúa huyendo - sin recursos para contraatacar");
                }
            }
        }

        private void Update()
        {
            if (!_isActive) return;

            // Reducir Cooldowns
            float dt = Time.deltaTime * settings.attackFrequencyMultiplier;
            if (_leftCd > 0) _leftCd -= dt;
            if (_rightCd > 0) _rightCd -= dt;
            if (_specialCd > 0) _specialCd -= dt;
            if (_shieldCd > 0) _shieldCd -= Time.deltaTime; // Escudo no se ve afectado por multiplicador
            if (_globalCd > 0) _globalCd -= dt;
            
            RegenerateMana();

            // ✅ OPTIMIZACIÓN: Verificar Line of Sight cada 0.1s en lugar de cada frame (10x menos raycasts)
            if (_player != null)
            {
                _losCheckTimer += Time.deltaTime;
                if (_losCheckTimer >= LOS_CHECK_INTERVAL)
                {
                    _losCheckTimer = 0f;
                    _hasLineOfSight = CheckLineOfSight();
                    
                    if (_hasLineOfSight)
                    {
                        _lastSeenTime = Time.time;
                        _lastCombatTime = Time.time; // 🔥 Actualizar tiempo de combate activo
                        _lastKnownPlayerPosition = _player.position;
                    }
                }
            }

            // ✅ Rotación gestionada por NPCSimpleAnimator
            // En ATTACK/DEFENSE: mirar al player (o última posición conocida)
            // En REPOSITION: mirar hacia donde se mueve (gestionado automáticamente por SyncWithNavMeshAgent)
            // En EVALUATE: mirar al player
            // En SEARCHING: animación maneja la rotación
            if (_player != null && _currentState != CombatState.REPOSITION && _currentState != CombatState.SEARCHING && _agent.enabled && _agent.isOnNavMesh && _agent.isStopped)
            {
                // Solo rotar hacia el player cuando está parado (no en movimiento)
                Vector3 targetPos = _hasLineOfSight ? _player.position : _lastKnownPlayerPosition;
                _animator.FaceTarget(targetPos);
            }
        }

        // =================================================================================
        // 🧠 MÁQUINA DE ESTADOS FINITOS (FSM)
        // =================================================================================
        
        /// <summary>
        /// Cambia de estado con control de tiempo mínimo para evitar nerviosismo
        /// </summary>
        private bool TryChangeState(CombatState newState)
        {
            // Permitir cambios inmediatos si es una situación crítica
            bool isCritical = newState == CombatState.SEARCHING || 
                             (_currentState == CombatState.SEARCHING && newState == CombatState.EVALUATE);
            
            if (!isCritical)
            {
                // Verificar que haya pasado el tiempo mínimo desde el último cambio
                float timeSinceLastChange = Time.time - _lastStateChangeTime;
                if (timeSinceLastChange < MIN_STATE_DURATION && _currentState != CombatState.EVALUATE)
                {
                    // No cambiar de estado todavía
                    return false;
                }
            }
            
            // Cambiar estado
            _previousState = _currentState;
            _currentState = newState;
            _lastStateChangeTime = Time.time;
            
            Debug.Log($"[CombatBrain:{gameObject.name}] 🔄 Cambio de estado: {_previousState} → {newState}");
            return true;
        }
        
        IEnumerator FSM_Loop()
        {
            while (_isActive && _player != null)
            {
                switch (_currentState)
                {
                    case CombatState.EVALUATE:
                        yield return State_Evaluate();
                        break;
                    
                    case CombatState.REPOSITION:
                        yield return State_Reposition();
                        break;
                    
                    case CombatState.ATTACK:
                        yield return State_Attack();
                        break;
                    
                    case CombatState.DEFENSE:
                        yield return State_Defense();
                        break;
                    
                    case CombatState.SEARCHING:
                        yield return State_Searching();
                        break;
                    
                    case CombatState.HIDING_TO_RECHARGE:
                        yield return State_HidingToRecharge();
                        break;
                }
                yield return null;
            }
        }

        // 1. EVALUAR: El cerebro que decide qué hacer
        IEnumerator State_Evaluate()
        {
            // ========================================================================
            // PREMISA: El objetivo es MATAR al player
            // ESTRATEGIA: Atacar hasta gastar hechizos, PERO puede fingir quedarse
            //             sin magia para atraer al player a una EMBOSCADA
            // ========================================================================
            
            // 🧠 PAUSA DE PENSAMIENTO: Pequeña pausa para parecer que "piensa"
            // Esto evita que el NPC parezca un robot que reacciona instantáneamente
            float thinkTime = UnityEngine.Random.Range(0.2f, 0.5f) * (1.1f - settings.difficultyLevel);
            yield return new WaitForSeconds(thinkTime);
            
            // ✅ A. PRIORIDAD MÁXIMA: Si no veo al jugador → BUSCAR
            if (!_hasLineOfSight)
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] ❌ Sin línea de visión - Iniciando búsqueda");
                _currentState = CombatState.SEARCHING;
                yield break;
            }

            // Elegir el objetivo más cercano: jugador o miembro del equipo
            ReevaluateTarget();

            float dist = Vector3.Distance(transform.position, _player.position);

            // ✅ B. Si está demasiado cerca (zona de peligro) → HUIR
            if (dist < settings.minSafeDistance)
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] ⚠️ Player demasiado cerca ({dist:F1}m < {settings.minSafeDistance}m) - Reposicionando");
                _currentState = CombatState.REPOSITION;
                yield break;
            }
            
            // ✅ C. Reacción inteligente ante proyectiles entrantes (sin depender de azar)
            if (HasIncomingProjectileThreat())
            {
                bool canShieldNow = settings.useShield && _shieldController != null && _shieldCd <= 0f;
                _currentState = canShieldNow ? CombatState.DEFENSE : CombatState.REPOSITION;
                Debug.Log($"[CombatBrain:{gameObject.name}] ⚡ Amenaza entrante detectada - {(canShieldNow ? "defensa con escudo" : "esquiva/reposición")}");
                yield break;
            }
            
            // ✅ B2. NUEVO: Verificar si hay línea de fuego ANTES de decidir atacar
            // Si no hay línea de fuego clara, moverse a mejor posición
            if (!HasClearLineOfFire())
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] ⚠️ Sin línea de fuego clara - Buscando mejor posición");
                _currentState = CombatState.REPOSITION;
                yield break;
            }

            // ✅ C. LÓGICA PRINCIPAL: ¿Tengo ataques disponibles?
            int attacksReady = CountAttacksReady();
            float manaPercent = GetManaPercent();
            
            if (manaPercent <= settings.lowManaRetreatThreshold && attacksReady <= 1)
            {
                bool canShieldNow = settings.useShield && _shieldController != null && _shieldCd <= 0f;
                _currentState = canShieldNow ? CombatState.DEFENSE : CombatState.HIDING_TO_RECHARGE;
                Debug.Log($"[CombatBrain:{gameObject.name}] 🔋 Maná bajo ({_currentMana:F1}/{_maxMana:F1}) - priorizando {(canShieldNow ? "defensa" : "cobertura/recarga")}");
                yield break;
            }
            
            // 🎭 DECISIÓN ESTRATÉGICA: ¿Debería fingir quedarse sin magia?
            // Solo considera engaño si:
            // 1. Tiene suficientes ataques para reservar algunos
            // 2. La dificultad es lo suficientemente alta
            // 3. No está ya usando estrategia de engaño
            if (!_isUsingDeceptionStrategy && attacksReady > settings.minAttacksToKeepForAmbush)
            {
                // Probabilidad basada en dificultad (NPCs más difíciles son más astutos)
                float actualDeceptionChance = settings.deceptionChance * settings.difficultyLevel;
                
                if (UnityEngine.Random.value < actualDeceptionChance)
                {
                    // 🎭 ENGAÑO ACTIVADO: Fingir que se quedó sin magia
                    _isUsingDeceptionStrategy = true;
                    _attacksReservedForAmbush = Mathf.Min(attacksReady, settings.minAttacksToKeepForAmbush);
                    
                    Debug.Log($"[CombatBrain:{gameObject.name}] 🎭 ESTRATEGIA DE ENGAÑO ACTIVADA - Fingiendo quedarse sin magia (reservando {_attacksReservedForAmbush} ataques para emboscada)");
                    
                    // Ir a esconderse fingiendo necesitar recarga
                    _currentState = CombatState.HIDING_TO_RECHARGE;
                    yield break;
                }
            }
            
            // ✅ D. ESTRATEGIA OFENSIVA NORMAL
            if (attacksReady > 0)
            {
                // ======== ESTRATEGIA OFENSIVA: ATACAR ========
                // Tengo hechizos → Gastarlos atacando al player
                
                if (dist <= settings.maxDistance && _globalCd <= 0)
                {
                    // En rango y sin cooldown global → ATACAR
                    Debug.Log($"[CombatBrain:{gameObject.name}] ⚔️ Atacando - {attacksReady} ataques disponibles{(_isUsingDeceptionStrategy ? " (EMBOSCADA EN CURSO)" : "")}");
                    _currentState = CombatState.ATTACK;
                    yield break;
                }
                else if (dist > settings.maxDistance)
                {
                    // Muy lejos → Acercarse primero
                    Debug.Log($"[CombatBrain:{gameObject.name}] 🚶 Acercándose al player ({dist:F1}m > {settings.maxDistance}m)");
                    MoveTo(_player.position, settings.walkSpeed);
                    yield return new WaitForSeconds(0.5f);
                }
                else if (_globalCd > 0)
                {
                    // En cooldown global → Esperar un momento
                    yield return new WaitForSeconds(0.3f);
                }
            }
            else
            {
                // ======== SIN ATAQUES: DEFENDER o RECARGAR ========
                bool canShieldNow = settings.useShield && _shieldController != null && _shieldCd <= 0f;
                if (canShieldNow)
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] 🛡️ Sin ataques disponibles - priorizando escudo antes de recargar");
                    _currentState = CombatState.DEFENSE;
                }
                else
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] 🔋 Sin ataques disponibles - Necesito esconderme para recargar (REAL)");
                    _currentState = CombatState.HIDING_TO_RECHARGE;
                }
                
                _isUsingDeceptionStrategy = false; // Ya no está fingiendo
                _attacksReservedForAmbush = 0;
                yield break;
            }

            yield return null;
        }
        
        /// <summary>
        /// Cuenta cuántos ataques están listos para usar
        /// </summary>
        private int CountAttacksReady()
        {
            int count = 0;
            if (CanUseAttackSlot(settings.leftAttack, _leftCd)) count++;
            if (CanUseAttackSlot(settings.rightAttack, _rightCd)) count++;
            if (CanUseAttackSlot(settings.specialAttack, _specialCd)) count++;
            return count;
        }
        
        private bool CanUseAttackSlot(AttackSlot slot, float currentCooldown)
        {
            if (_ctx?.Config?.combatConfig == null)
                return false;
            if (!_ctx.Config.combatConfig.HasSpell(slot.slotIndex))
                return false;
            if (currentCooldown > 0f)
                return false;
            return HasManaForSlot(slot.slotIndex);
        }
        
        private bool TrySelectAttackSlot(out AttackSlot chosenAttack)
        {
            chosenAttack = default;
            
            bool canSpecial = CanUseAttackSlot(settings.specialAttack, _specialCd);
            bool canRight = CanUseAttackSlot(settings.rightAttack, _rightCd);
            bool canLeft = CanUseAttackSlot(settings.leftAttack, _leftCd);
            
            if (canSpecial && UnityEngine.Random.value > 0.4f) // 60% chance de special
            {
                chosenAttack = settings.specialAttack;
                return true;
            }
            
            if (canRight)
            {
                chosenAttack = settings.rightAttack;
                return true;
            }
            
            if (canLeft)
            {
                chosenAttack = settings.leftAttack;
                return true;
            }
            
            // Fallback: si special estaba disponible pero no entró por probabilidad
            if (canSpecial)
            {
                chosenAttack = settings.specialAttack;
                return true;
            }
            
            return false;
        }
        
        private float GetManaCostForSlot(int slotIndex)
        {
            return slotIndex switch
            {
                0 => Mathf.Max(0f, settings.manaCostLeft),
                1 => Mathf.Max(0f, settings.manaCostRight),
                2 => Mathf.Max(0f, settings.manaCostSpecial),
                _ => 0f
            };
        }
        
        private bool HasManaForSlot(int slotIndex)
        {
            float manaCost = GetManaCostForSlot(slotIndex);
            return _currentMana + 0.001f >= manaCost;
        }
        
        private bool TrySpendManaForSlot(int slotIndex)
        {
            float manaCost = GetManaCostForSlot(slotIndex);
            if (manaCost <= 0f) return true;
            if (_currentMana < manaCost) return false;
            
            _currentMana = Mathf.Max(0f, _currentMana - manaCost);
            _lastManaSpendTime = Time.time;
            NotifyManaChanged();
            return true;
        }
        
        private void SetCooldownForSlot(int slotIndex, float cooldown)
        {
            float clamped = Mathf.Max(0f, cooldown);
            switch (slotIndex)
            {
                case 0:
                    _leftCd = clamped;
                    break;
                case 1:
                    _rightCd = clamped;
                    break;
                case 2:
                    _specialCd = clamped;
                    break;
            }
        }
        
        private void RegenerateMana()
        {
            if (_maxMana <= 0f || _currentMana >= _maxMana)
                return;
            if (Time.time - _lastManaSpendTime < Mathf.Max(0f, settings.manaRegenDelayAfterSpend))
                return;
            
            float before = _currentMana;
            _currentMana = Mathf.Min(_maxMana, _currentMana + Mathf.Max(0f, settings.manaRegenPerSecond) * Time.deltaTime);
            if (_currentMana > before + 0.01f)
            {
                NotifyManaChanged();
            }
        }
        
        private void NotifyManaChanged()
        {
            OnManaChanged?.Invoke(GetManaPercent());
        }
        
        private float EstimateTimeToRecoverAnyAttack()
        {
            if (_ctx?.Config?.combatConfig == null)
                return 0f;
            if (CountAttacksReady() > 0)
                return 0f;
            
            float minManaNeeded = float.PositiveInfinity;
            if (_ctx.Config.combatConfig.HasSpell(0))
            {
                minManaNeeded = Mathf.Min(minManaNeeded, Mathf.Max(0f, settings.manaCostLeft) - _currentMana);
            }
            if (_ctx.Config.combatConfig.HasSpell(1))
            {
                minManaNeeded = Mathf.Min(minManaNeeded, Mathf.Max(0f, settings.manaCostRight) - _currentMana);
            }
            if (_ctx.Config.combatConfig.HasSpell(2))
            {
                minManaNeeded = Mathf.Min(minManaNeeded, Mathf.Max(0f, settings.manaCostSpecial) - _currentMana);
            }
            
            if (float.IsPositiveInfinity(minManaNeeded))
                return 0f;
            
            float manaToRecover = Mathf.Max(0f, minManaNeeded);
            float regen = Mathf.Max(0.01f, settings.manaRegenPerSecond);
            return Mathf.Max(0f, settings.manaRegenDelayAfterSpend) + (manaToRecover / regen);
        }

        // 2. REPOSICIONARSE: Moverse a un lugar seguro o mejor posición de ataque
        IEnumerator State_Reposition()
        {
            float dist = Vector3.Distance(transform.position, _player.position);
            
            // Determinar qué tipo de reposicionamiento necesitamos
            bool needToRetreat = dist < settings.minSafeDistance;
            bool needBetterFiringPosition = !HasClearLineOfFire();

            if (needToRetreat)
            {
                // ✅ HUIR: Player demasiado cerca
                Vector3 targetPos = FindRetreatPosition();
                
                if (targetPos != transform.position)
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] 🏃 Huyendo a posición segura: {targetPos}");
                    MoveTo(targetPos, settings.runSpeed);
                    
                    // Esperar hasta llegar o 3 segundos máx
                    float timer = 0;
                    while (_agent.enabled && _agent.isOnNavMesh && _agent.remainingDistance > 1.5f && timer < 3f)
                    {
                        timer += Time.deltaTime;
                        
                        // Si durante la huida recuperamos visión y línea de fuego, considerar parar
                        if (_hasLineOfSight && HasClearLineOfFire() && 
                            Vector3.Distance(transform.position, _player.position) >= settings.minSafeDistance)
                        {
                            Debug.Log($"[CombatBrain:{gameObject.name}] ✅ Posición segura alcanzada durante huida");
                            break;
                        }
                        
                        yield return null;
                    }
                    
                    StopMove();
                }
            }
            else if (needBetterFiringPosition)
            {
                // ✅ BUSCAR MEJOR POSICIÓN DE TIRO: Hay obstáculo bloqueando
                Vector3 betterPos = FindBetterFiringPosition();
                
                if (betterPos != transform.position)
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] 🎯 Moviéndose a mejor posición de tiro: {betterPos}");
                    MoveTo(betterPos, settings.walkSpeed);
                    
                    float timer = 0;
                    while (_agent.enabled && _agent.isOnNavMesh && _agent.remainingDistance > 0.5f && timer < 4f)
                    {
                        timer += Time.deltaTime;
                        
                        // Si durante el movimiento obtenemos línea de fuego, parar
                        if (HasClearLineOfFire())
                        {
                            Debug.Log($"[CombatBrain:{gameObject.name}] ✅ Línea de fuego obtenida durante movimiento");
                            break;
                        }
                        
                        yield return null;
                    }
                    
                    StopMove();
                }
                else
                {
                    // No encontró mejor posición - esperar un momento
                    Debug.Log($"[CombatBrain:{gameObject.name}] ⚠️ No se encontró mejor posición de tiro - esperando");
                    yield return new WaitForSeconds(0.5f);
                }
            }
            
            // ✅ Al terminar, verificar estado
            if (!_hasLineOfSight)
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] ❌ Perdió visión del jugador tras reposicionarse - BUSCANDO");
                
                if (_alertIconController != null && _config != null && _config.questionIconPrefab != null)
                {
                    _alertIconController.ShowQuestion(_config.questionIconPrefab, _config.alertIconDuration);
                }
                
                if (_animator != null)
                {
                    _animator.PlaySearching();
                }
                
                _currentState = CombatState.SEARCHING;
                yield break;
            }
            
            // Volver a evaluar
            _currentState = CombatState.EVALUATE;
        }
        
        /// <summary>
        /// Encuentra una posición de retroceso segura, preferiblemente detrás de cobertura
        /// </summary>
        private Vector3 FindRetreatPosition()
        {
            // Verificar que el agente está activo
            if (!_agent.enabled || !_agent.isOnNavMesh)
            {
                return transform.position;
            }
            
            // Intentar encontrar cobertura primero
            if (FindCoverBehindObstacle(out Vector3 coverPos))
            {
                return coverPos;
            }
            
            // Si no hay cobertura, huir en dirección opuesta al jugador
            Vector3 dirAway = (transform.position - _player.position).normalized;
            Vector3 targetPos = transform.position + dirAway * 5f;
            
            // Verificar que la posición está en NavMesh
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
            {
                // Verificar que hay un camino válido
                NavMeshPath path = new NavMeshPath();
                if (_agent.enabled && _agent.isOnNavMesh && _agent.CalculatePath(navHit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    return navHit.position;
                }
            }
            
            // Fallback: intentar posiciones laterales
            Vector3 right = Vector3.Cross(Vector3.up, dirAway).normalized;
            Vector3[] alternatives = {
                transform.position + (dirAway + right).normalized * 4f,
                transform.position + (dirAway - right).normalized * 4f,
                transform.position + right * 3f,
                transform.position - right * 3f
            };
            
            foreach (var altPos in alternatives)
            {
                if (NavMesh.SamplePosition(altPos, out navHit, 2f, NavMesh.AllAreas))
                {
                    NavMeshPath path = new NavMeshPath();
                    if (_agent.enabled && _agent.isOnNavMesh && _agent.CalculatePath(navHit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                    {
                        return navHit.position;
                    }
                }
            }
            
            // No encontró ninguna posición válida
            return transform.position;
        }
        
        /// <summary>
        /// Encuentra una posición desde donde tenga línea de fuego clara al jugador
        /// </summary>
        private Vector3 FindBetterFiringPosition()
        {
            Vector3 dirToPlayer = (_player.position - transform.position).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dirToPlayer).normalized;
            
            // Probar posiciones laterales y diagonales
            float[] distances = { 2f, 3f, 4f, 5f };
            Vector3[] directions = {
                right,
                -right,
                (dirToPlayer + right).normalized,
                (dirToPlayer - right).normalized,
                (-dirToPlayer + right * 0.5f).normalized,
                (-dirToPlayer - right * 0.5f).normalized
            };
            
            float bestScore = float.MinValue;
            Vector3 bestPosition = transform.position;
            
            foreach (float dist in distances)
            {
                foreach (var dir in directions)
                {
                    Vector3 testPos = transform.position + dir * dist;
                    
                    // Verificar NavMesh
                    if (!NavMesh.SamplePosition(testPos, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
                        continue;
                    
                    Vector3 candidatePos = navHit.position;
                    
                    // Verificar camino válido
                    NavMeshPath path = new NavMeshPath();
                    if (!_agent.enabled || !_agent.isOnNavMesh || !_agent.CalculatePath(candidatePos, path) || path.status != NavMeshPathStatus.PathComplete)
                        continue;
                    
                    // Simular línea de fuego desde esa posición
                    Vector3 fireOrigin = candidatePos + Vector3.up * 1.5f + (candidatePos - transform.position).normalized * 0.3f;
                    Vector3 fireTarget = _player.position + Vector3.up * 1f;
                    Vector3 fireDir = (fireTarget - fireOrigin).normalized;
                    float fireDist = Vector3.Distance(fireOrigin, fireTarget);
                    
                    int defaultLayer = LayerMask.NameToLayer("Default");
                    LayerMask obstacleMask = 1 << defaultLayer;
                    
                    // Verificar si tiene línea de fuego desde ahí
                    bool hasClearShot = !Physics.Raycast(fireOrigin, fireDir, fireDist * 0.9f, obstacleMask, QueryTriggerInteraction.Ignore);
                    
                    if (!hasClearShot)
                        continue;
                    
                    // Calcular puntuación
                    float distToPlayer = Vector3.Distance(candidatePos, _player.position);
                    float score = 10f;
                    
                    // Preferir distancia óptima
                    if (distToPlayer >= settings.minSafeDistance && distToPlayer <= settings.maxDistance)
                    {
                        score += 5f;
                    }
                    
                    // Preferir posiciones más cercanas a la actual (menos movimiento)
                    float distFromCurrent = Vector3.Distance(candidatePos, transform.position);
                    score -= distFromCurrent * 0.5f;
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPosition = candidatePos;
                    }
                }
            }
            
            return bestPosition;
        }

        // 3. ATACAR: Seleccionar hechizo y disparar
        IEnumerator State_Attack()
        {
            // ✅ Verificar si está siendo levitado - no puede atacar
            var levitationTarget = GetComponent<LevitationTarget>();
            if (levitationTarget != null && levitationTarget.IsBeingLevitated)
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] ❌ Ataque cancelado - NPC está siendo levitado");
                yield return new WaitForSeconds(0.5f);
                yield break;
            }
            
            // ✅ Verificar que tengamos línea de visión antes de atacar
            if (!_hasLineOfSight)
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] ❌ Ataque cancelado - Sin línea de visión");
                _currentState = CombatState.SEARCHING;
                yield break;
            }
            
            // ✅ NUEVO: Verificar que tenemos línea de fuego clara (sin obstáculos en el camino)
            if (!HasClearLineOfFire())
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] ⚠️ Ataque cancelado - Obstáculo bloqueando línea de fuego, reposicionando...");
                _currentState = CombatState.REPOSITION;
                yield break;
            }
            
            StopMove(); // Quieto para disparar
            _animator.FaceTarget(_combatTarget.position);

            // Seleccionar ataque disponible (Prioridad: Special > Right > Left)
            AttackSlot chosenAttack = new AttackSlot();
            bool found = TrySelectAttackSlot(out chosenAttack);

            if (found)
            {
                // Windup (preparación)
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.2f, 0.5f));
                
                // ✅ Verificar visión de nuevo antes de ejecutar el ataque
                if (!_hasLineOfSight)
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] ❌ Ataque cancelado durante windup - Perdida línea de visión");
                    _currentState = CombatState.SEARCHING;
                    yield break;
                }
                
                // ✅ NUEVO: Verificar línea de fuego de nuevo antes de disparar
                if (!HasClearLineOfFire())
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] ⚠️ Ataque cancelado durante windup - Obstáculo apareció en línea de fuego");
                    _currentState = CombatState.REPOSITION;
                    yield break;
                }
                
                // Consumir maná al confirmar ejecución del hechizo
                if (!TrySpendManaForSlot(chosenAttack.slotIndex))
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] ❌ Ataque cancelado: maná insuficiente en ejecución");
                    _currentState = CombatState.HIDING_TO_RECHARGE;
                    yield break;
                }
                
                SetCooldownForSlot(chosenAttack.slotIndex, chosenAttack.cooldown);
                
                // Ejecutar Animación
                _rawAnimator.Play(chosenAttack.animationState, settings.upperBodyLayer);
                
                // Disparar Proyectil (Si no es por evento de animación)
                if (!settings.spawnProjectileViaAnimEvent)
                {
                    yield return new WaitForSeconds(settings.fireDelaySeconds);
                    
                    // ✅ Verificar visión ANTES de disparar
                    if (!_hasLineOfSight)
                    {
                        Debug.Log($"[CombatBrain:{gameObject.name}] ❌ Disparo cancelado - Jugador se escondió durante animación");
                        _currentState = CombatState.SEARCHING;
                        yield break;
                    }
                    
                    // ✅ NUEVO: Verificación final de línea de fuego
                    if (!HasClearLineOfFire())
                    {
                        Debug.Log($"[CombatBrain:{gameObject.name}] ⚠️ Disparo cancelado - Obstáculo en línea de fuego");
                        _currentState = CombatState.REPOSITION;
                        yield break;
                    }
                    
                    SpawnProjectile(chosenAttack.slotIndex);
                }

                // Pausa post-ataque (Global Cooldown)
                _globalCd = settings.globalCooldown;
                yield return new WaitForSeconds(0.5f);
                
                // ✅ Verificar visión DESPUÉS del ataque
                if (!_hasLineOfSight)
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] ❌ Jugador se escondió después del ataque - Iniciando búsqueda");
                    _currentState = CombatState.SEARCHING;
                    yield break;
                }

                // DECISIÓN TÁCTICA POST-ATAQUE
                // Si tengo maná (otros ataques listos) -> Seguir atacando
                if (HasAnyAttackReady())
                {
                    // 30% chance de moverse para flanquear, 70% de seguir disparando
                    if (UnityEngine.Random.value < 0.3f)
                    {
                        Vector3 flankPos = GetFlankPosition();
                        MoveTo(flankPos, settings.runSpeed);
                        yield return new WaitForSeconds(1f);
                    }
                    else
                    {
                        _currentState = CombatState.ATTACK; // Repetir ataque
                        yield break;
                    }
                }
            }
            else
            {
                // No hay hechizos disponibles por maná/cooldown
                _currentState = CombatState.EVALUATE;
                yield break;
            }

            _currentState = CombatState.EVALUATE;
        }

        // 4. DEFENSA: La lógica inteligente basada en dificultad
        IEnumerator State_Defense()
        {
            bool canShieldNow = settings.useShield && _shieldController != null && _shieldCd <= 0f;
            if (canShieldNow)
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] 🛡️ Activando ESCUDO defensivo por {settings.shieldDuration:F1}s");
                
                _shieldController.StartDefending(settings.shieldDuration);
                _shieldCd = settings.shieldCooldown + settings.shieldDuration;
                
                // Mientras defiende puede moverse: retrocede si está muy cerca o se desplaza lateralmente.
                float defendTime = settings.shieldDuration;
                float elapsed = 0f;
                float repathTimer = 0f;
                Vector3 strafeDirection = Vector3.zero;
                while (elapsed < defendTime)
                {
                    if (_player != null)
                    {
                        repathTimer -= Time.deltaTime;
                        if (repathTimer <= 0f)
                        {
                            Vector3 toPlayer = _player.position - transform.position;
                            toPlayer.y = 0f;
                            float dist = toPlayer.magnitude;
                            if (dist > 0.01f)
                            {
                                Vector3 targetPos;
                                if (dist < settings.minSafeDistance * 0.8f)
                                {
                                    Vector3 retreatDir = -toPlayer.normalized;
                                    targetPos = transform.position + retreatDir * 2f;
                                    MoveTo(targetPos, settings.walkSpeed * 0.55f);
                                }
                                else
                                {
                                    if (strafeDirection == Vector3.zero || UnityEngine.Random.value < 0.2f)
                                    {
                                        Vector3 right = Vector3.Cross(Vector3.up, toPlayer.normalized);
                                        strafeDirection = UnityEngine.Random.value < 0.5f ? right : -right;
                                    }

                                    targetPos = transform.position + strafeDirection * 1.5f;
                                    MoveTo(targetPos, settings.walkSpeed * 0.65f);
                                }
                            }

                            repathTimer = 0.35f;
                        }
                    }
                    
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                
                StopMove();
                Debug.Log($"[CombatBrain:{gameObject.name}] ✅ Escudo completado - volviendo a evaluar");
                _currentState = CombatState.EVALUATE;
                yield break;
            }
            
            // Sin escudo disponible, decidir cobertura/esquiva según dificultad
            bool makeSmartDecision = UnityEngine.Random.value < settings.difficultyLevel;
            if (makeSmartDecision)
            {
                if (_shieldCd > 0)
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] ⏳ Escudo en cooldown ({_shieldCd:F1}s) - buscando cobertura");
                }
                
                Vector3 coverPos;
                if (TryGetCoverPosition(out coverPos))
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] 🌳 Corriendo hacia cobertura para recargar");
                    MoveTo(coverPos, settings.runSpeed);
                    
                    float timeout = 3f;
                    while (_agent.enabled && _agent.isOnNavMesh && _agent.remainingDistance > 0.5f && timeout > 0 && _agent.pathStatus == NavMeshPathStatus.PathComplete)
                    {
                        timeout -= Time.deltaTime;
                        yield return null;
                    }
                    
                    Debug.Log($"[CombatBrain:{gameObject.name}] ⏳ Esperando tras cobertura, recargando cooldowns...");
                    yield return new WaitForSeconds(2.0f);
                }
                else
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] 🤸 No hay cobertura - esquiva táctica");
                    yield return DoDodge();
                }
            }
            else
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] 😵 Defensa torpe (baja dificultad)");
                if (UnityEngine.Random.value > 0.5f)
                    yield return DoDodge();
                else 
                    yield return new WaitForSeconds(1.0f);
            }

            _currentState = CombatState.EVALUATE;
        }
        
        // 5. ESCONDERSE PARA RECARGAR: El NPC busca un lugar seguro para recuperar sus hechizos
        IEnumerator State_HidingToRecharge()
        {
            bool isAmbush = _isUsingDeceptionStrategy; // ¿Es una emboscada o recarga real?
            
            if (isAmbush)
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] 🎭 ESCONDERSE PARA EMBOSCADA - Fingiendo recarga (tiene {_attacksReservedForAmbush} ataques guardados)");
            }
            else
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] 🏃 ESCONDERSE PARA RECARGAR - Buscando cobertura (recarga real)");
            }
            
            // A. Buscar posición de cobertura
            Vector3 coverPosition;
            bool foundCover = FindCoverBehindObstacle(out coverPosition);
            
            if (!foundCover)
            {
                // Si no encuentra cobertura, huir en dirección opuesta
                Vector3 dirAway = (transform.position - _player.position).normalized;
                coverPosition = transform.position + dirAway * 8f;
                
                // Intentar posición válida en NavMesh
                if (!NavMesh.SamplePosition(coverPosition, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
                {
                    // Si no hay NavMesh válido, quedarse donde está y defender
                    Debug.LogWarning($"[CombatBrain:{gameObject.name}] ⚠️ No se encontró cobertura ni posición de huida válida");
                    _currentState = CombatState.DEFENSE;
                    yield break;
                }
                coverPosition = navHit.position;
            }
            
            // B. Moverse hacia la cobertura
            Debug.Log($"[CombatBrain:{gameObject.name}] 🏃 Corriendo hacia cobertura: {coverPosition}");
            MoveTo(coverPosition, settings.runSpeed);
            
            float moveStartTime = Time.time;
            float maxMoveTime = 5f;
            bool arrivedAtCover = false;
            
            // C. Durante el movimiento hacia cobertura
            while (!arrivedAtCover && (Time.time - moveStartTime) < maxMoveTime)
            {
                // Si es atacado durante la huida → GIRAR y usar escudo si está disponible
                // Esto se maneja en OnTakeDamage(), pero aquí podemos verificar si usamos escudo preventivo
                
                // Si el player le ataca y puede defenderse, usar escudo
                if (_player != null && IsPlayerAttacking())
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] ⚔️ ¡ATACADO DURANTE LA HUIDA!");
                    
                    // Usar escudo si está disponible
                    if (settings.useShield && _shieldController != null && _shieldCd <= 0)
                    {
                        Debug.Log($"[CombatBrain:{gameObject.name}] 🛡️ Activando escudo durante huida");
                        _shieldController.StartDefending(settings.shieldDuration);
                        _shieldCd = settings.shieldCooldown + settings.shieldDuration;
                        
                        // Continuar huyendo con el escudo activo
                        yield return new WaitForSeconds(Mathf.Min(settings.shieldDuration, 2f));
                    }
                }
                
                // Verificar si llegó a la posición
                if (_agent.enabled && _agent.isOnNavMesh && _agent.remainingDistance <= 1.5f && !_agent.pathPending)
                {
                    arrivedAtCover = true;
                }
                
                yield return null;
            }
            
            StopMove();
            
            // D. Al llegar a cobertura - Decidir comportamiento según situación
            bool wasBeingAttacked = !_hasLineOfSight || IsPlayerAttacking();
            
            if (isAmbush)
            {
                // 🎭 EMBOSCADA: Siempre mostrar interrogación para engañar
                Debug.Log($"[CombatBrain:{gameObject.name}] 🎭 Llegó a cobertura (EMBOSCADA) - Fingiendo búsqueda");
                
                if (_alertIconController != null && _config != null && _config.questionIconPrefab != null)
                {
                    _alertIconController.ShowQuestion(_config.questionIconPrefab, _config.alertIconDuration);
                }
                
                if (_animator != null)
                {
                    _animator.PlaySearching();
                }
            }
            else if (!_hasLineOfSight)
            {
                // 🔍 PERDIÓ VISIÓN REAL: Mostrar interrogación porque realmente no sabe dónde está el player
                Debug.Log($"[CombatBrain:{gameObject.name}] ❓ Llegó a cobertura sin visión del player - Búsqueda real");
                
                if (_alertIconController != null && _config != null && _config.questionIconPrefab != null)
                {
                    _alertIconController.ShowQuestion(_config.questionIconPrefab, _config.alertIconDuration);
                }
                
                if (_animator != null)
                {
                    _animator.PlaySearching();
                }
            }
            else
            {
                // 👁️ AÚN VE AL PLAYER o SABE QUE ESTÁ CERCA: NO mostrar interrogación
                // Comportamiento: Mirar alrededor defensivamente sin bajar la guardia
                Debug.Log($"[CombatBrain:{gameObject.name}] 🛡️ Llegó a cobertura pero sabe que player está cerca - Alerta defensiva");
                
                // NO mostrar interrogación
                // NO reproducir animación de búsqueda completa
                // En su lugar, mantenerse alerta
                
                // Pequeña pausa de "mirar alrededor"
                yield return new WaitForSeconds(0.5f);
            }
            
            // E. Esperar mientras se recargan los hechizos (o finge recargar si es emboscada)
            if (isAmbush)
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] 🎭 Fingiendo recarga... esperando que el player se acerque");
            }
            else
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] ⏳ Recargando hechizos...");
            }
            
            float rechargeStartTime = Time.time;
            float maxRechargeTime = Mathf.Max(
                Mathf.Max(settings.leftAttack.cooldown, settings.rightAttack.cooldown, settings.specialAttack.cooldown),
                EstimateTimeToRecoverAnyAttack() + 1.5f
            );
            
            // 🎭 LÓGICA DE EMBOSCADA: Monitorear distancia del player
            float ambushTriggerDistance = settings.optimalDistance * 1.2f; // Activar emboscada cuando esté cerca
            
            // 🛡️ Comportamiento defensivo si sabe que player está cerca
            bool playerKnownNearby = _hasLineOfSight || wasBeingAttacked;
            float defensiveCheckTimer = 0f;
            
            // Esperar hasta que recargue o detecte oportunidad de emboscada
            while ((Time.time - rechargeStartTime) < maxRechargeTime)
            {
                int currentAttacks = CountAttacksReady();
                
                // 🛡️ Si sabe que player está cerca, mirar alrededor periódicamente
                if (playerKnownNearby && !isAmbush)
                {
                    defensiveCheckTimer += Time.deltaTime;
                    
                    // Cada 2 segundos, verificar alrededores
                    if (defensiveCheckTimer >= 2f)
                    {
                        defensiveCheckTimer = 0f;
                        
                        // Verificar si ve al player ahora
                        if (_hasLineOfSight)
                        {
                            Debug.Log($"[CombatBrain:{gameObject.name}] 👁️ ¡Player detectado cerca durante recarga! - Preparando respuesta");
                            
                            // Si tiene suficientes ataques, contraatacar
                            if (currentAttacks >= 1)
                            {
                                Debug.Log($"[CombatBrain:{gameObject.name}] ⚡ Interrumpiendo recarga para contraatacar");
                                
                                // Mostrar alerta
                                if (_alertIconController != null && _config != null && _config.exclamationIconPrefab != null)
                                {
                                    _alertIconController.ShowExclamation(_config.exclamationIconPrefab, _config.alertIconDuration);
                                }
                                
                                if (_animator != null)
                                {
                                    _animator.PlaySenseSomething();
                                }
                                
                                yield return new WaitForSeconds(0.5f);
                                
                                _currentState = CombatState.EVALUATE;
                                yield break;
                            }
                            else if (settings.useShield && _shieldController != null && _shieldCd <= 0)
                            {
                                // Sin ataques pero con escudo → Defender
                                Debug.Log($"[CombatBrain:{gameObject.name}] 🛡️ Player muy cerca - Activando escudo preventivo");
                                _shieldController.StartDefending(settings.shieldDuration);
                                _shieldCd = settings.shieldCooldown + settings.shieldDuration;
                                yield return new WaitForSeconds(Mathf.Min(settings.shieldDuration, 1.5f));
                            }
                        }
                    }
                }
                
                // 🎭 Si es emboscada y el player se acerca → ¡SORPRESA!
                if (isAmbush && _player != null)
                {
                    float distToPlayer = Vector3.Distance(transform.position, _player.position);
                    
                    // ¿El player está buscándome y se acerca?
                    if (distToPlayer <= ambushTriggerDistance)
                    {
                        Debug.Log($"[CombatBrain:{gameObject.name}] 🎯 ¡EMBOSCADA ACTIVADA! Player a {distToPlayer:F1}m - ¡ATAQUE SORPRESA!");
                        
                        // Mostrar admiración + alerta
                        if (_alertIconController != null && _config != null && _config.exclamationIconPrefab != null)
                        {
                            _alertIconController.ShowExclamation(_config.exclamationIconPrefab, _config.alertIconDuration);
                        }
                        
                        // Animación de alerta
                        if (_animator != null)
                        {
                            _animator.PlaySenseSomething();
                        }
                        
                        // Pequeña pausa dramática
                        yield return new WaitForSeconds(0.5f);
                        
                        // Resetear estrategia de engaño
                        _isUsingDeceptionStrategy = false;
                        _attacksReservedForAmbush = 0;
                        
                        // ¡ATACAR POR LA ESPALDA!
                        _currentState = CombatState.EVALUATE;
                        yield break;
                    }
                }
                
                // Si es recarga real, verificar si recargó suficientes ataques
                int desiredReadyAttacks = _ctx?.Config?.combatConfig != null
                    ? Mathf.Clamp(_ctx.Config.combatConfig.GetSpellCount(), 1, 2)
                    : 1;
                if (!isAmbush && currentAttacks >= desiredReadyAttacks)
                {
                    break; // Recarga completada
                }
                
                // Si el player le ataca mientras recarga → Defender o contraatacar
                if (_player != null && IsPlayerAttacking())
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] ⚔️ ¡ATACADO MIENTRAS RECARGA!");
                    
                    // Si es emboscada, revelar la trampa inmediatamente
                    if (isAmbush)
                    {
                        Debug.Log($"[CombatBrain:{gameObject.name}] 🎭 ¡Emboscada descubierta! - Contratatacando");
                        _isUsingDeceptionStrategy = false;
                        _attacksReservedForAmbush = 0;
                        _currentState = CombatState.EVALUATE;
                        yield break;
                    }
                    
                    // Decidir entre defender o contraatacar
                    int attacksNow = CountAttacksReady();
                    
                    if (attacksNow > 0)
                    {
                        // Tiene al menos un ataque → Contraatacar
                        Debug.Log($"[CombatBrain:{gameObject.name}] ⚡ Contratatacando con {attacksNow} ataques disponibles");
                        _currentState = CombatState.EVALUATE;
                        yield break;
                    }
                    else if (settings.useShield && _shieldController != null && _shieldCd <= 0)
                    {
                        // No tiene ataques pero tiene escudo → Defender
                        Debug.Log($"[CombatBrain:{gameObject.name}] 🛡️ Defendiendo con escudo");
                        _shieldController.StartDefending(settings.shieldDuration);
                        _shieldCd = settings.shieldCooldown + settings.shieldDuration;
                        yield return new WaitForSeconds(settings.shieldDuration);
                    }
                }
                
                yield return new WaitForSeconds(0.3f);
            }
            
            // F. Hechizos recargados o emboscada fallida - Momento de salir de cobertura
            if (isAmbush)
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] 🎭 Emboscada no activada (player no se acercó) - Cancelando estrategia");
                _isUsingDeceptionStrategy = false;
                _attacksReservedForAmbush = 0;
            }
            else
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] ✅ Hechizos recargados ({CountAttacksReady()} disponibles) - Saliendo de cobertura");
            }
            
            // 🎯 MOMENTO CRÍTICO: Verificar situación al salir de cobertura
            Vector3 expectedPlayerPosition = _lastKnownPlayerPosition;
            
            // Verificar si tiene línea de visión AHORA
            if (_hasLineOfSight)
            {
                // 👁️ VE AL PLAYER: Verificar si está donde se esperaba
                float distanceFromExpected = Vector3.Distance(_player.position, expectedPlayerPosition);
                
                if (distanceFromExpected < 5f)
                {
                    // ✅ Player está DONDE SE ESPERABA (posición A)
                    Debug.Log($"[CombatBrain:{gameObject.name}] 👀 ¡Player visible en posición esperada! - Atacar directamente");
                    
                    // Mostrar admiración
                    if (_alertIconController != null && _config != null && _config.exclamationIconPrefab != null)
                    {
                        _alertIconController.ShowExclamation(_config.exclamationIconPrefab, _config.alertIconDuration);
                    }
                    
                    // Reproducir animación SenseSomethingStart_NoWeapon
                    if (_animator != null)
                    {
                        _animator.PlaySenseSomething();
                    }
                    
                    yield return new WaitForSeconds(0.8f);
                    
                    _currentState = CombatState.EVALUATE;
                }
                else
                {
                    // ⚠️ Player se MOVIÓ de donde estaba (posición diferente)
                    Debug.Log($"[CombatBrain:{gameObject.name}] ⚠️ ¡Player se movió! Era posición A, ahora está en B ({distanceFromExpected:F1}m lejos)");
                    
                    // Mostrar admiración (sorpresa)
                    // ⚠️ DESACTIVADO: Iconos de exclamación durante combate
                    /*
                    if (_alertIconController != null && _config != null && _config.exclamationIconPrefab != null)
                    {
                        _alertIconController.ShowExclamation(_config.exclamationIconPrefab, _config.alertIconDuration);
                    }
                    */
                    
                    // Reproducir animación SenseSomethingStart_NoWeapon
                    if (_animator != null)
                    {
                        _animator.PlaySenseSomething();
                    }
                    
                    yield return new WaitForSeconds(0.8f);
                    
                    _currentState = CombatState.EVALUATE;
                }
            }
            else
            {
                // 🎯 NO VE AL PLAYER - ¡AQUÍ SALE LA INTERROGACIÓN!
                // Escenario: Salió del árbol, player NO está en posición A
                Debug.Log($"[CombatBrain:{gameObject.name}] ❓ ¡Player NO está donde se esperaba! - Mostrando interrogación y entrando en búsqueda");
                
                // 🎯 MOSTRAR INTERROGACIÓN (perdida real)
                if (_alertIconController != null && _config != null && _config.questionIconPrefab != null)
                {
                    _alertIconController.ShowQuestion(_config.questionIconPrefab, _config.alertIconDuration);
                }
                
                // Reproducir animación de búsqueda
                if (_animator != null)
                {
                    _animator.PlaySearching();
                }
                
                yield return new WaitForSeconds(1.0f);
                
                // Entrar en modo búsqueda activa
                _currentState = CombatState.SEARCHING;
            }
        }
        
        /// <summary>
        /// Verifica si el player está atacando al NPC en este momento
        /// (Se puede expandir con más lógica si es necesario)
        /// </summary>
        private bool IsPlayerAttacking()
        {
            return HasIncomingProjectileThreat(10f);
        }
        
        private bool HasIncomingProjectileThreat(float scanRadius = 8f)
        {
            LayerMask projectileMask = LayerMask.GetMask("PlayerProjectile", "Projectile", "ProjectilePlayer", "MagicProjectile");
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, _projectileBuffer, projectileMask);
            
            // Fallback: en algunos prefabs los layers no están normalizados.
            // Hacemos un segundo escaneo abierto y filtramos por tipo.
            if (hitCount <= 0)
            {
                hitCount = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, _projectileBuffer);
                if (hitCount <= 0)
                    return false;
            }
            
            Vector3 npcCenter = transform.position + Vector3.up * 1.1f;
            for (int i = 0; i < hitCount; i++)
            {
                var col = _projectileBuffer[i];
                if (col == null) continue;
                
                GameObject projectileGo = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
                if (projectileGo == null) continue;
                if (projectileGo == gameObject || projectileGo.transform.IsChildOf(transform)) continue;
                if (!IsLikelyPlayerProjectile(projectileGo)) continue;
                
                Vector3 toNpc = npcCenter - projectileGo.transform.position;
                if (toNpc.sqrMagnitude < 0.05f) return true;
                
                Vector3 velocity = Vector3.zero;
                var rb = projectileGo.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    velocity = rb.linearVelocity;
                }
                if (velocity.sqrMagnitude < 0.01f)
                {
                    velocity = projectileGo.transform.forward * 10f;
                }
                
                float approachDot = Vector3.Dot(velocity.normalized, toNpc.normalized);
                if (approachDot > 0.65f)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        private bool IsLikelyPlayerProjectile(GameObject projectileGo)
        {
            if (projectileGo == null)
                return false;
            
            // Ignorar proyectiles de enemigos.
            if (projectileGo.GetComponentInParent<EnemyProjectile>() != null)
                return false;
            
            string layerName = LayerMask.LayerToName(projectileGo.layer);
            if (layerName == "Projectile" || layerName == "PlayerProjectile" || layerName == "ProjectilePlayer" || layerName == "MagicProjectile")
                return true;
            
            if (projectileGo.GetComponentInParent<MagicProjectile>() != null)
                return true;
            
            return false;
        }

        // =================================================================================
        // 🔧 HELPER FUNCTIONS
        // =================================================================================

        private bool TryGetCoverPosition(out Vector3 position)
        {
            position = Vector3.zero;
            
            // Usar el mismo método mejorado de búsqueda de cobertura
            if (FindCoverBehindObstacle(out position))
            {
                return true;
            }
            
            // Fallback: buscar en el radio configurado con coverLayerMask
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, settings.coverSearchRadius, _coverBuffer, settings.coverLayerMask); // ✅ OPTIMIZACIÓN FASE 2: NonAlloc
            
            if (hitCount == 0)
            {
                return false;
            }
            
            float bestScore = float.MinValue;
            bool found = false;
            int defaultLayer = LayerMask.NameToLayer("Default");
            LayerMask defaultMask = 1 << defaultLayer;

            for (int i = 0; i < hitCount; i++)
            {
                var hit = _coverBuffer[i];
                if (hit.isTrigger) continue;
                
                // Calcular punto opuesto al player detrás del objeto
                Vector3 dirFromPlayer = (hit.transform.position - _player.position).normalized;
                dirFromPlayer.y = 0;
                
                // Probar varias distancias
                for (float dist = 1.5f; dist <= 4f; dist += 0.5f)
                {
                    Vector3 coverSpot = hit.transform.position + (dirFromPlayer * dist);

                    // Verificar si es accesible en NavMesh
                    if (!NavMesh.SamplePosition(coverSpot, out NavMeshHit navHit, 2.0f, NavMesh.AllAreas))
                    {
                        continue;
                    }
                    
                    // Verificar espacio libre
                    if (!HasClearSpace(navHit.position, 0.5f, 2f, defaultMask))
                    {
                        continue;
                    }
                    
                    // Verificar camino accesible
                    NavMeshPath path = new NavMeshPath();
                    if (!_agent.enabled || !_agent.isOnNavMesh || !_agent.CalculatePath(navHit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                    {
                        continue;
                    }
                    
                    // Calcular puntuación
                    float d = Vector3.Distance(transform.position, navHit.position);
                    float score = 20f - d;
                    
                    // Bonus si puede disparar desde ahí
                    if (CanFireFromPosition(navHit.position, hit, defaultMask))
                    {
                        score += 10f;
                    }
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        position = navHit.position;
                        found = true;
                    }
                }
            }
            return found;
        }

        private IEnumerator DoDodge()
        {
            // Esquiva lateral simple
            Vector3 side = UnityEngine.Random.value > 0.5f ? transform.right : -transform.right;
            Vector3 dest = transform.position + side * settings.dodgeDistance;
            
            MoveTo(dest, settings.runSpeed * 1.5f);
            yield return new WaitForSeconds(0.5f);
            StopMove();
        }

        private Vector3 GetFlankPosition()
        {
            // Moverse 45 grados a un lado
            Vector3 dir = (_player.position - transform.position).normalized;
            Vector3 flankDir = Quaternion.Euler(0, 45, 0) * dir;
            return transform.position + flankDir * 4f;
        }

        /// <summary>
        /// Selecciona el objetivo de ataque más cercano entre el jugador y los miembros del equipo visibles.
        /// Se llama cada vez que el cerebro entra en estado EVALUATE.
        /// </summary>
        private void ReevaluateTarget()
        {
            if (_player == null) return;

            _combatTarget = _player;
            float bestDist = Vector3.Distance(transform.position, _player.position);

            var party = PlayerParty.Instance;
            if (party == null || party.MemberCount == 0) return;

            var hiddenNpc = ActiveCharacterSwapper.Instance?.HiddenNpc;

            foreach (var member in party.Members)
            {
                if (member == null)                     continue;
                if (member == hiddenNpc)                continue; // Controlado por el jugador, no tiene cuerpo propio

                float dist = Vector3.Distance(transform.position, member.transform.position);
                if (dist < bestDist)
                {
                    bestDist      = dist;
                    _combatTarget = member.transform;
                }
            }
        }

        private void SpawnProjectile(int slotIndex)
        {
            // Aquí iría tu lógica de instanciar prefab
            // Usa _ctx.Config.combatConfig.GetSpellPrefab(slotIndex) como tenías antes
            Debug.Log($"[NPC] Disparando hechizo slot {slotIndex}");
            
            if (_ctx.Config?.combatConfig != null)
            {
                var prefab = _ctx.Config.combatConfig.GetSpellPrefab(slotIndex);
                if (prefab)
                {
                     Vector3 spawnPos   = transform.position + Vector3.up * 1.5f + transform.forward;
                     Vector3 targetPos  = _combatTarget.position + Vector3.up * 1f;
                     var spell = Instantiate(prefab, spawnPos, Quaternion.LookRotation(targetPos - spawnPos));
                     if (spell.TryGetComponent<EnemyProjectile>(out var proj))
                         proj.Initialize((targetPos - spawnPos).normalized, _ctx.Config.combatConfig.GetSpellDamage(slotIndex));
                }
            }
        }

        private bool HasAnyAttackReady()
        {
            return CountAttacksReady() > 0;
        }

        private void MoveTo(Vector3 pos, float speed)
        {
            if (!_agent.enabled || !_agent.isOnNavMesh)
            {
                Debug.LogWarning($"[CombatBrain:{gameObject.name}] ⚠️ Agent no está activo o en NavMesh - no puede moverse");
                return;
            }
            
            // Verificar que el destino está en NavMesh
            if (!NavMesh.SamplePosition(pos, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[CombatBrain:{gameObject.name}] ⚠️ Destino {pos} no está en NavMesh");
                return;
            }
            
            // ✅ OPTIMIZACIÓN: Usar path reutilizable en lugar de crear uno nuevo (reduce GC)
            if (!_agent.CalculatePath(navHit.position, _reusablePath))
            {
                Debug.LogWarning($"[CombatBrain:{gameObject.name}] ⚠️ No se puede calcular camino a {navHit.position}");
                return;
            }
            
            if (_reusablePath.status != NavMeshPathStatus.PathComplete)
            {
                Debug.LogWarning($"[CombatBrain:{gameObject.name}] ⚠️ Camino incompleto a {navHit.position} - status: {_reusablePath.status}");
                
                // Si el camino es parcial, intentar ir al punto más cercano alcanzable
                if (_reusablePath.status == NavMeshPathStatus.PathPartial && _reusablePath.corners.Length > 1)
                {
                    Vector3 lastReachablePoint = _reusablePath.corners[_reusablePath.corners.Length - 1];
                    Debug.Log($"[CombatBrain:{gameObject.name}] 📍 Usando punto parcial más cercano: {lastReachablePoint}");
                    
                    if (_agent.enabled && _agent.isOnNavMesh)
                    {
                        _agent.isStopped = false;
                        _agent.speed = speed;
                        _agent.SetDestination(lastReachablePoint);
                    }
                    _animator.SetMovementSpeed(speed, 0.1f);
                    return;
                }
                return;
            }
            
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.speed = speed;
                _agent.SetDestination(navHit.position);
            }
            _animator.SetMovementSpeed(speed, 0.1f);
        }

        private void StopMove()
        {
            if (_agent.isOnNavMesh) _agent.isStopped = true;
            _animator.SetMovementSpeed(0, 0.1f);
        }
        
        /// <summary>
        /// Verifica si hay línea de visión directa al jugador (sin obstáculos).
        /// Usa raycast para detectar objetos en la capa de obstáculos.
        /// </summary>
        private bool CheckLineOfSight()
        {
            if (_player == null) return false;
            
            Vector3 origin = transform.position + Vector3.up * 1.5f; // Altura de los ojos
            Vector3 targetPos = _player.position + Vector3.up * 1.0f; // Centro del jugador
            Vector3 direction = targetPos - origin;
            float distance = direction.magnitude;

            int hitCount = Physics.RaycastNonAlloc(origin, direction.normalized, _raycastBuffer, distance, ~0, QueryTriggerInteraction.Ignore);
            if (hitCount > 0)
            {
                System.Array.Sort(_raycastBuffer, 0, hitCount, RaycastHitDistanceComparer.Instance);
                for (int i = 0; i < hitCount; i++)
                {
                    var hit = _raycastBuffer[i];
                    if (hit.collider == null) continue;
                    if (ShouldIgnoreVisionHit(hit.collider)) continue;

                    if (hit.collider.CompareTag("Player"))
                    {
                        Debug.DrawRay(origin, direction, Color.green);
                        return true;
                    }

                    Debug.DrawRay(origin, direction.normalized * hit.distance, Color.red);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[CombatBrain:{gameObject.name}] 🚫 Visión bloqueada por: {hit.collider.gameObject.name} (Tag: {hit.collider.tag}, Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)})");
#endif
                    return false;
                }
            }
            
            // No golpeó nada - línea de visión clara (caso raro)
            Debug.DrawRay(origin, direction, Color.yellow);
            return true;
        }
        
        /// <summary>
        /// Verifica si hay una línea de fuego clara para disparar un proyectil.
        /// Similar a CheckLineOfSight pero más estricto: verifica desde la posición de disparo
        /// y con un margen de seguridad para evitar que el proyectil impacte con obstáculos cercanos.
        /// </summary>
        private bool HasClearLineOfFire()
        {
            if (_player == null) return false;
            
            // Posición de origen del proyectil (frente al NPC)
            Vector3 spawnPos = transform.position + Vector3.up * 1.5f + transform.forward * 0.5f;
            Vector3 targetPos = _player.position + Vector3.up * 1.0f;
            Vector3 direction = (targetPos - spawnPos).normalized;
            float distance = Vector3.Distance(spawnPos, targetPos);
            
            int hitCount = Physics.RaycastNonAlloc(spawnPos, direction, _raycastBuffer, distance, ~0, QueryTriggerInteraction.Ignore);
            if (hitCount > 0)
            {
                System.Array.Sort(_raycastBuffer, 0, hitCount, RaycastHitDistanceComparer.Instance);
                for (int i = 0; i < hitCount; i++)
                {
                    var hit = _raycastBuffer[i];
                    if (hit.collider == null) continue;
                    if (ShouldIgnoreVisionHit(hit.collider)) continue;
                    if (hit.collider.CompareTag("Player")) break;

                    float distToObstacle = hit.distance;
                    if (distToObstacle < 2f)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log($"[CombatBrain:{gameObject.name}] 🚫 Línea de fuego bloqueada por {hit.collider.gameObject.name} a {distToObstacle:F1}m - MUY CERCA");
#endif
                        Debug.DrawLine(spawnPos, hit.point, Color.red, 0.5f);
                        return false;
                    }

                    if (distToObstacle < distance * 0.5f)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log($"[CombatBrain:{gameObject.name}] ⚠️ Línea de fuego parcialmente bloqueada por {hit.collider.gameObject.name} a {distToObstacle:F1}m");
#endif
                        Debug.DrawLine(spawnPos, hit.point, Color.yellow, 0.5f);
                        return false;
                    }
                    break;
                }
            }
            
            // Verificación adicional: raycast esférico para detectar obstáculos cercanos al camino del proyectil
            float projectileRadius = 0.3f; // Radio aproximado del proyectil
            if (Physics.SphereCast(spawnPos, projectileRadius, direction, out RaycastHit sphereHit, distance * 0.9f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (sphereHit.collider != null && ShouldIgnoreVisionHit(sphereHit.collider))
                {
                    Debug.DrawLine(spawnPos, targetPos, Color.green, 0.5f);
                    return true;
                }
                
                // Verificar si no es el jugador
                if (!sphereHit.collider.CompareTag("Player"))
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] ⚠️ Proyectil podría rozar con {sphereHit.collider.gameObject.name}");
                    
                    // Si está muy cerca, no disparar
                    if (sphereHit.distance < 1.5f)
                    {
                        Debug.DrawLine(spawnPos, sphereHit.point, Color.magenta, 0.5f);
                        return false;
                    }
                }
            }
            
            // Línea de fuego clara
            Debug.DrawLine(spawnPos, targetPos, Color.green, 0.5f);
            return true;
        }
        
        private bool ShouldIgnoreVisionHit(Collider col)
        {
            if (col == null) return true;
            GameObject go = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (go == null) return true;
            
            if (go == gameObject || go.transform.IsChildOf(transform))
                return true;
            
            // Proyectiles y VFX transitorios no deben bloquear visión ni línea de fuego.
            if (go.GetComponentInParent<MagicProjectile>() != null) return true;
            if (go.GetComponentInParent<EnemyProjectile>() != null) return true;
            if (go.GetComponentInParent<NPCShieldController.NPCShieldMarker>() != null) return true;
            
            string layerName = LayerMask.LayerToName(go.layer);
            if (layerName == "Projectile" || layerName == "ProjectileEnemy" || layerName == "EnemyProjectile" || layerName == "PlayerProjectile" || layerName == "ProjectilePlayer")
                return true;
            
            return false;
        }
        
        /// <summary>
        /// Verifica si el jugador está dentro del campo de visión del NPC.
        /// Usa el fieldOfView configurado en NPCCombatConfig.
        /// </summary>
        private bool IsPlayerInFieldOfView()
        {
            if (_player == null) return false;
            
            // Obtener fieldOfView del config (usar valor por defecto si no está disponible)
            float fov = 160f; // Valor por defecto
            if (_manager != null && _manager.Configuration != null && _manager.Configuration.combatConfig != null)
            {
                fov = _manager.Configuration.combatConfig.fieldOfView;
            }
            
            // Calcular dirección al jugador
            Vector3 dirToPlayer = (_player.position - transform.position).normalized;
            dirToPlayer.y = 0; // Ignorar diferencia de altura
            
            Vector3 forward = transform.forward;
            forward.y = 0;
            forward.Normalize();
            
            // Calcular ángulo entre la dirección del NPC y la dirección al jugador
            float angle = Vector3.Angle(forward, dirToPlayer);
            
            // El jugador está en el campo de visión si el ángulo es menor que la mitad del FOV
            bool inFOV = angle <= (fov / 2f);
            
            return inFOV;
        }
        
        // =================================================================================
        // 🔍 ESTADO DE BÚSQUEDA
        // =================================================================================
        
        /// <summary>
        /// Estado SEARCHING: El NPC ha perdido de vista al jugador y lo busca activamente.
        /// Muestra icono de interrogación y animación en CADA parada.
        /// </summary>
        IEnumerator State_Searching()
        {
            Debug.Log($"[CombatBrain:{gameObject.name}] 🔍 INICIANDO BÚSQUEDA - Última posición conocida: {_lastKnownPlayerPosition}");
            
            StopMove();
            
            // 🔥 CORRECCIÓN: Solo mostrar interrogación si NO estábamos en combate reciente
            // Si el NPC simplemente se giró y perdió visión momentáneamente, NO mostrar interrogación
            bool showQuestionMark = !_wasInRecentCombat;
            
            if (showQuestionMark)
            {
                // ✅ Mostrar icono de interrogación INICIAL
                if (_alertIconController != null && _config != null && _config.questionIconPrefab != null)
                {
                    _alertIconController.ShowQuestion(_config.questionIconPrefab, _config.alertIconDuration);
                }
                
                // ✅ Reproducir animación de búsqueda INICIAL
                if (_animator != null)
                {
                    _animator.PlaySearching();
                }
                
                // ⏳ ESPERAR A QUE LA ANIMACIÓN DE BÚSQUEDA SE VEA (duración aprox de la animación)
                yield return new WaitForSeconds(1.5f);
            }
            else
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] 🎯 Combate reciente detectado ({Time.time - _lastSeenTime:F1}s) - Búsqueda sin interrogación");
            }
            
            float searchStartTime = Time.time;
            float searchTimeout = settings.activelySearchForPlayer ? settings.searchDuration : settings.passiveSearchDuration;
            
            Debug.Log($"[CombatBrain:{gameObject.name}] 🔍 Modo: {(settings.activelySearchForPlayer ? "BÚSQUEDA ACTIVA" : "BÚSQUEDA PASIVA")} - Duración: {searchTimeout}s");
            
            int searchAttempts = 0;
            const int maxSearchAttempts = 5; // Número de veces que buscará en diferentes lugares
            
            // ✅ BUCLE DE BÚSQUEDA
            while (Time.time - searchStartTime < searchTimeout && searchAttempts < maxSearchAttempts)
            {
                // Verificar si encontramos al jugador
                if (_hasLineOfSight)
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] ✅ ¡JUGADOR ENCONTRADO! - Mostrando alerta");
                    
                    // ✅ Ocultar interrogación y mostrar admiración
                    if (_alertIconController != null && _config != null && _config.exclamationIconPrefab != null)
                    {
                        _alertIconController.ShowExclamation(_config.exclamationIconPrefab, _config.alertIconDuration);
                    }
                    
                    // ✅ Reproducir animación SenseSomethingStart_NoWeapon
                    if (_animator != null)
                    {
                        _animator.PlaySenseSomething();
                        Debug.Log($"[CombatBrain:{gameObject.name}] 🎬 Reproduciendo animación SenseSomethingStart_NoWeapon");
                    }
                    
                    // Esperar breve para que se vea el feedback
                    yield return new WaitForSeconds(0.5f);
                    
                    StopMove();
                    
                    // ✅ DECISIÓN INMEDIATA: Si tiene ataques, atacar directamente sin pasar por EVALUATE
                    int attacksAvailable = CountAttacksReady();
                    float distToPlayer = Vector3.Distance(transform.position, _player.position);
                    
                    if (attacksAvailable > 0 && distToPlayer <= settings.maxDistance && HasClearLineOfFire())
                    {
                        Debug.Log($"[CombatBrain:{gameObject.name}] ⚡ ¡ATAQUE INMEDIATO! - {attacksAvailable} ataques listos, distancia: {distToPlayer:F1}m");
                        _currentState = CombatState.ATTACK;
                    }
                    else if (attacksAvailable > 0 && distToPlayer > settings.maxDistance)
                    {
                        Debug.Log($"[CombatBrain:{gameObject.name}] 🚶 Jugador muy lejos ({distToPlayer:F1}m) - Acercándose para atacar");
                        _currentState = CombatState.EVALUATE; // EVALUATE se encargará de acercarse
                    }
                    else
                    {
                        Debug.Log($"[CombatBrain:{gameObject.name}] 🎯 Evaluando situación - Ataques: {attacksAvailable}, Dist: {distToPlayer:F1}m");
                        _currentState = CombatState.EVALUATE;
                    }
                    yield break;
                }
                
                // ✅ BÚSQUEDA ACTIVA: Moverse a diferentes puntos
                if (settings.activelySearchForPlayer)
                {
                    searchAttempts++;
                    
                    // Calcular punto de búsqueda (más cerca en las primeras búsquedas)
                    float searchRadius = settings.searchMovementRadius * (0.5f + searchAttempts * 0.3f);
                    Vector3 searchPoint = _lastKnownPlayerPosition + 
                                         new Vector3(
                                             UnityEngine.Random.Range(-searchRadius, searchRadius),
                                             0,
                                             UnityEngine.Random.Range(-searchRadius, searchRadius)
                                         );
                    
                    // Verificar que el punto esté en NavMesh
                    if (NavMesh.SamplePosition(searchPoint, out NavMeshHit navHit, searchRadius, NavMesh.AllAreas))
                    {
                        Debug.Log($"[CombatBrain:{gameObject.name}] 👣 Movimiento de búsqueda #{searchAttempts} hacia: {navHit.position}");
                        MoveTo(navHit.position, settings.walkSpeed);
                        
                        // ✅ DURANTE EL MOVIMIENTO: Verificar constantemente
                        float moveTimeout = 5f;
                        float moveTimer = 0f;
                        while (_agent.enabled && _agent.isOnNavMesh && (_agent.pathPending || (_agent.remainingDistance > 1f && moveTimer < moveTimeout)))
                        {
                            // Si encontramos al jugador durante el movimiento
                            if (_hasLineOfSight)
                            {
                                Debug.Log($"[CombatBrain:{gameObject.name}] ✅ ¡Jugador encontrado durante movimiento!");
                                
                                // Mostrar admiración
                                if (_alertIconController != null && _config != null && _config.exclamationIconPrefab != null)
                                {
                                    _alertIconController.ShowExclamation(_config.exclamationIconPrefab, _config.alertIconDuration);
                                }
                                
                                // Reproducir animación SenseSomethingStart_NoWeapon
                                if (_animator != null)
                                {
                                    _animator.PlaySenseSomething();
                                    Debug.Log($"[CombatBrain:{gameObject.name}] 🎬 Reproduciendo SenseSomethingStart_NoWeapon");
                                }
                                
                                yield return new WaitForSeconds(0.5f);
                                
                                StopMove();
                                
                                // ✅ ATAQUE INMEDIATO si es posible
                                int attacks = CountAttacksReady();
                                float dist = Vector3.Distance(transform.position, _player.position);
                                
                                if (attacks > 0 && dist <= settings.maxDistance && HasClearLineOfFire())
                                {
                                    Debug.Log($"[CombatBrain:{gameObject.name}] ⚡ ¡ATAQUE INMEDIATO desde búsqueda!");
                                    _currentState = CombatState.ATTACK;
                                }
                                else
                                {
                                    _currentState = CombatState.EVALUATE;
                                }
                                yield break;
                            }
                            
                            moveTimer += Time.deltaTime;
                            yield return null;
                        }
                        
                        StopMove();
                        
                        // ✅ AL DETENERSE: Mostrar interrogación de nuevo y animación
                        // 🔥 CORRECCIÓN: Solo si NO es combate reciente
                        Debug.Log($"[CombatBrain:{gameObject.name}] ❓ Parada de búsqueda #{searchAttempts} - No encontrado");
                        
                        if (showQuestionMark)
                        {
                            if (_alertIconController != null && _config != null && _config.questionIconPrefab != null)
                            {
                                _alertIconController.ShowQuestion(_config.questionIconPrefab, _config.alertIconDuration);
                            }
                            
                            // Reproducir animación de búsqueda
                            if (_animator != null)
                            {
                                _animator.PlaySearching();
                            }
                            
                            // Pausa para la animación y el icono (tiempo realista de "mirar alrededor")
                            yield return new WaitForSeconds(2.0f);
                        }
                        else
                        {
                            // En combate reciente: búsqueda más rápida sin animaciones largas
                            yield return new WaitForSeconds(0.5f);
                        }
                        
                        // Verificar de nuevo si lo encontró mientras miraba alrededor
                        if (_hasLineOfSight)
                        {
                            Debug.Log($"[CombatBrain:{gameObject.name}] ✅ ¡Jugador encontrado mientras miraba alrededor!");
                            
                            // ⚠️ DESACTIVADO: Iconos de exclamación durante combate
                            /*
                            if (_alertIconController != null && _config != null && _config.exclamationIconPrefab != null)
                            {
                                _alertIconController.ShowExclamation(_config.exclamationIconPrefab, _config.alertIconDuration);
                            }
                            */
                            
                            // Reproducir animación SenseSomethingStart_NoWeapon
                            if (_animator != null)
                            {
                                _animator.PlaySenseSomething();
                                Debug.Log($"[CombatBrain:{gameObject.name}] 🎬 Reproduciendo SenseSomethingStart_NoWeapon");
                            }
                            
                            yield return new WaitForSeconds(0.5f);
                            
                            StopMove();
                            
                            // ✅ ATAQUE INMEDIATO si es posible
                            int attacksReady = CountAttacksReady();
                            float distPlayer = Vector3.Distance(transform.position, _player.position);
                            
                            if (attacksReady > 0 && distPlayer <= settings.maxDistance && HasClearLineOfFire())
                            {
                                Debug.Log($"[CombatBrain:{gameObject.name}] ⚡ ¡ATAQUE INMEDIATO desde búsqueda!");
                                _currentState = CombatState.ATTACK;
                            }
                            else
                            {
                                _currentState = CombatState.EVALUATE;
                            }
                            yield break;
                        }
                    }
                }
                else
                {
                    // BÚSQUEDA PASIVA: Solo espera y observa
                    yield return new WaitForSeconds(1.0f);
                }
            }
            
            // ✅ BÚSQUEDA AGOTADA - No encontró al jugador después de todos los intentos
            Debug.Log($"[CombatBrain:{gameObject.name}] 😞 Búsqueda agotada - {searchAttempts} intentos completados sin éxito");
            
            // Ocultar icono de interrogación
            if (_alertIconController != null)
            {
                _alertIconController.HideAlertIcon();
            }
            
            // ✅ DECISIÓN POST-BÚSQUEDA: ¿Volver al origen o abandonar?
            if (settings.returnToOriginAfterSearch)
            {
                // OPCIÓN A: Volver a la posición inicial
                Debug.Log($"[CombatBrain:{gameObject.name}] 🏠 Volviendo al origen tras búsqueda fallida: {_combatStartPosition}");
                MoveTo(_combatStartPosition, settings.walkSpeed);
                
                // Esperar a llegar al origen
                while (_agent.enabled && _agent.isOnNavMesh && (_agent.pathPending || _agent.remainingDistance > 1.5f))
                {
                    // Si encuentra al jugador durante el regreso, retomar combate inmediatamente
                    if (_hasLineOfSight)
                    {
                        Debug.Log($"[CombatBrain:{gameObject.name}] ✅ ¡Jugador encontrado en el camino de regreso!");
                        
                        // ⚠️ DESACTIVADO: Iconos de exclamación durante combate
                        /*
                        if (_alertIconController != null && _config != null && _config.exclamationIconPrefab != null)
                        {
                            _alertIconController.ShowExclamation(_config.exclamationIconPrefab, _config.alertIconDuration);
                        }
                        */
                        
                        // Reproducir animación SenseSomethingStart_NoWeapon
                        if (_animator != null)
                        {
                            _animator.PlaySenseSomething();
                            Debug.Log($"[CombatBrain:{gameObject.name}] 🎬 Reproduciendo SenseSomethingStart_NoWeapon");
                        }
                        
                        yield return new WaitForSeconds(0.5f);
                        
                        StopMove();
                        
                        // ✅ ATAQUE INMEDIATO si es posible
                        int attacksNow = CountAttacksReady();
                        float distNow = Vector3.Distance(transform.position, _player.position);
                        
                        if (attacksNow > 0 && distNow <= settings.maxDistance && HasClearLineOfFire())
                        {
                            Debug.Log($"[CombatBrain:{gameObject.name}] ⚡ ¡ATAQUE INMEDIATO al detectar jugador!");
                            _currentState = CombatState.ATTACK;
                        }
                        else
                        {
                            _currentState = CombatState.EVALUATE;
                        }
                        yield break;
                    }
                    
                    yield return null;
                }
                
                StopMove();
                Debug.Log($"[CombatBrain:{gameObject.name}] ✅ Regresó al origen - Saliendo del modo combate");
            }
            else
            {
                // OPCIÓN B: Abandonar directamente sin volver
                Debug.Log($"[CombatBrain:{gameObject.name}] 🚫 No vuelve al origen (returnToOriginAfterSearch = false) - Abandonando combate");
            }
            
            // ✅ ABANDONAR MODO COMBATE
            Debug.Log($"[CombatBrain:{gameObject.name}] 🏳️ Abandonando modo combate - Jugador no encontrado tras búsqueda exhaustiva");
            StopCombat();
            
            // Notificar al manager que salimos de combate
            if (_manager != null && _manager.Context != null)
            {
                _manager.Context.IsInCombat = false;
            }
        }
        
        // =================================================================================
        // 🛡️ BÚSQUEDA DE COBERTURA
        // =================================================================================
        
        /// <summary>
        /// Busca objetos en el layer Default (obstáculos) para esconderse detrás de ellos.
        /// Retorna true si encontró una posición de cobertura válida.
        /// MEJORADO: Verifica que haya espacio real, que no atraviese obstáculos, y que pueda disparar.
        /// </summary>
        private bool FindCoverBehindObstacle(out Vector3 coverPosition)
        {
            coverPosition = transform.position;
            
            // Verificar que el agente está activo antes de buscar cobertura
            if (!_agent.enabled || !_agent.isOnNavMesh)
            {
                return false;
            }
            
            // Buscar todos los colliders en un radio (layer Default)
            int defaultLayer = LayerMask.NameToLayer("Default");
            LayerMask defaultMask = 1 << defaultLayer;
            
            int obstacleCount = Physics.OverlapSphereNonAlloc(
                transform.position, 
                15f, // Radio de búsqueda de obstáculos
                _obstacleBuffer,
                defaultMask
            ); // ✅ OPTIMIZACIÓN FASE 2: NonAlloc
            
            if (obstacleCount == 0)
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] ⚠️ No se encontraron obstáculos Default cercanos");
                return false;
            }
            
            Debug.Log($"[CombatBrain:{gameObject.name}] 🔍 Encontrados {obstacleCount} obstáculos Default para cobertura");
            
            // Buscar el mejor obstáculo para esconderse
            float bestScore = float.MinValue;
            Vector3 bestPosition = transform.position;
            bool foundValidCover = false;
            
            // Tamaño del NPC para verificaciones de espacio
            float npcRadius = 0.5f; // Radio aproximado del NPC
            float npcHeight = 2f;   // Altura del NPC
            float minClearanceFromObstacle = 1.5f; // Distancia mínima detrás del obstáculo
            float maxClearanceFromObstacle = 4f;   // Distancia máxima detrás del obstáculo
            
            for (int i = 0; i < obstacleCount; i++) // ✅ OPTIMIZACIÓN FASE 2: for loop para NonAlloc
            {
                var obstacle = _obstacleBuffer[i];
                
                // Ignorar triggers
                if (obstacle.isTrigger) continue;
                
                // Ignorar obstáculos muy grandes (probablemente son terreno o estructuras completas)
                Bounds bounds = obstacle.bounds;
                if (bounds.size.x > 15f || bounds.size.z > 15f)
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] ⏭️ Ignorando {obstacle.gameObject.name} - demasiado grande ({bounds.size})");
                    continue;
                }
                
                // Obtener el centro del obstáculo
                Vector3 obstacleCenter = bounds.center;
                
                // Calcular dirección del jugador al obstáculo
                Vector3 dirPlayerToObstacle = (obstacleCenter - _player.position).normalized;
                dirPlayerToObstacle.y = 0; // Mantener en el plano horizontal
                
                // Probar varias distancias detrás del obstáculo
                for (float distance = minClearanceFromObstacle; distance <= maxClearanceFromObstacle; distance += 0.5f)
                {
                    // Posición de cobertura: detrás del obstáculo, alejándose del jugador
                    Vector3 potentialCoverPos = obstacleCenter + dirPlayerToObstacle * distance;
                    
                    // 1. Verificar que esté en NavMesh
                    if (!NavMesh.SamplePosition(potentialCoverPos, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
                    {
                        continue;
                    }
                    
                    Vector3 coverPos = navHit.position;
                    
                    // 2. CRÍTICO: Verificar que hay espacio libre en la posición de cobertura
                    // Esto evita que el NPC se meta dentro de casas/estructuras
                    if (!HasClearSpace(coverPos, npcRadius, npcHeight, defaultMask))
                    {
                        Debug.Log($"[CombatBrain:{gameObject.name}] ❌ Posición {coverPos} no tiene espacio libre - rechazada");
                        continue;
                    }
                    
                    // 3. Verificar que el camino desde la posición actual hasta la cobertura es válido
                    NavMeshPath path = new NavMeshPath();
                    if (!_agent.enabled || !_agent.isOnNavMesh || !_agent.CalculatePath(coverPos, path) || path.status != NavMeshPathStatus.PathComplete)
                    {
                        Debug.Log($"[CombatBrain:{gameObject.name}] ❌ Camino a {coverPos} no es accesible - rechazada");
                        continue;
                    }
                    
                    // 4. Verificar que el obstáculo realmente bloquea la visión del jugador
                    Vector3 dirToPlayer = (_player.position - coverPos).normalized;
                    float distToPlayer = Vector3.Distance(coverPos, _player.position);
                    
                    // Raycast desde la posición de cobertura hacia el jugador
                    Vector3 rayOrigin = coverPos + Vector3.up * 1f; // A altura de torso
                    if (!Physics.Raycast(rayOrigin, dirToPlayer, distToPlayer * 0.8f, defaultMask))
                    {
                        // NO hay obstáculo entre la cobertura y el jugador - no es una buena cobertura
                        Debug.Log($"[CombatBrain:{gameObject.name}] ⚠️ Posición {coverPos} no tiene cobertura real contra el jugador");
                        continue;
                    }
                    
                    // 5. NUEVO: Verificar que hay al menos un ángulo desde donde PUEDE disparar
                    // (Para evitar que se quede atrapado sin poder atacar)
                    bool canFireFromCover = CanFireFromPosition(coverPos, obstacle, defaultMask);
                    
                    // 6. Calcular puntuación
                    float distanceToNpc = Vector3.Distance(transform.position, coverPos);
                    float score = 20f - distanceToNpc; // Mayor puntuación = más cerca
                    
                    // Bonus si puede disparar desde la cobertura
                    if (canFireFromCover)
                    {
                        score += 10f;
                    }
                    
                    // Bonus por estar a una distancia óptima del jugador
                    float distFromPlayer = Vector3.Distance(coverPos, _player.position);
                    if (distFromPlayer >= settings.minSafeDistance && distFromPlayer <= settings.maxDistance)
                    {
                        score += 5f;
                    }
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPosition = coverPos;
                        foundValidCover = true;
                        
                        Debug.Log($"[CombatBrain:{gameObject.name}] 🛡️ Cobertura válida: {obstacle.gameObject.name} pos:{coverPos} (score: {score:F1}, canFire:{canFireFromCover})");
                    }
                    
                    // Si encontramos una buena posición a esta distancia, no probar más lejos
                    if (foundValidCover && canFireFromCover)
                        break;
                }
            }
            
            if (foundValidCover)
            {
                coverPosition = bestPosition;
                Debug.Log($"[CombatBrain:{gameObject.name}] ✅ Mejor cobertura seleccionada en: {coverPosition}");
                return true;
            }
            
            Debug.Log($"[CombatBrain:{gameObject.name}] ❌ No se encontró cobertura válida detrás de obstáculos");
            return false;
        }
        
        /// <summary>
        /// Verifica si hay espacio libre suficiente para que el NPC esté en una posición.
        /// Esto evita que se meta dentro de estructuras cerradas.
        /// </summary>
        private bool HasClearSpace(Vector3 position, float radius, float height, LayerMask obstacleMask)
        {
            // Verificar con un overlap de cápsula si hay espacio libre
            Vector3 bottom = position + Vector3.up * radius;
            Vector3 top = position + Vector3.up * (height - radius);
            
            // Si hay colisión, no hay espacio libre
            Collider[] colliders = Physics.OverlapCapsule(bottom, top, radius * 0.9f, obstacleMask);
            
            if (colliders.Length > 0)
            {
                // Hay algo en el camino - verificar si es algo que debería bloquear
                foreach (var col in colliders)
                {
                    // Ignorar triggers
                    if (col.isTrigger) continue;
                    
                    // Si hay un collider sólido, no hay espacio
                    return false;
                }
            }
            
            // Verificación adicional: raycast hacia arriba para detectar techos
            if (Physics.Raycast(position + Vector3.up * 0.1f, Vector3.up, height + 1f, obstacleMask))
            {
                // Hay un techo encima - probablemente es interior de una estructura
                Debug.Log($"[CombatBrain:{gameObject.name}] ⚠️ Posición {position} tiene techo - probablemente interior");
                return false;
            }
            
            // Verificación adicional: varios raycasts horizontales para detectar paredes cercanas
            int wallsDetected = 0;
            float checkRadius = 2f;
            Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, 
                                     (Vector3.forward + Vector3.right).normalized,
                                     (Vector3.forward + Vector3.left).normalized,
                                     (Vector3.back + Vector3.right).normalized,
                                     (Vector3.back + Vector3.left).normalized };
            
            foreach (var dir in directions)
            {
                if (Physics.Raycast(position + Vector3.up, dir, checkRadius, obstacleMask))
                {
                    wallsDetected++;
                }
            }
            
            // Si hay paredes en más de 5 de 8 direcciones, probablemente está encerrado
            if (wallsDetected >= 5)
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] ⚠️ Posición {position} rodeada por {wallsDetected}/8 paredes - espacio cerrado");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Verifica si el NPC puede disparar al jugador desde una posición de cobertura.
        /// Busca ángulos laterales desde donde tenga línea de tiro clara.
        /// </summary>
        private bool CanFireFromPosition(Vector3 coverPosition, Collider coverObstacle, LayerMask obstacleMask)
        {
            // Verificar si moviéndose ligeramente a los lados puede tener línea de tiro
            Vector3 dirToPlayer = (_player.position - coverPosition).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dirToPlayer).normalized;
            
            float[] offsets = { -2f, -1f, 1f, 2f }; // Probar posiciones a los lados
            
            foreach (float offset in offsets)
            {
                Vector3 peekPosition = coverPosition + right * offset;
                
                // Verificar que la posición de "asomarse" está en NavMesh
                if (!NavMesh.SamplePosition(peekPosition, out NavMeshHit navHit, 1f, NavMesh.AllAreas))
                    continue;
                
                Vector3 firePosition = navHit.position + Vector3.up * 1.5f; // Altura de disparo
                Vector3 targetPosition = _player.position + Vector3.up * 1f; // Centro del jugador
                
                Vector3 fireDir = (targetPosition - firePosition).normalized;
                float fireDist = Vector3.Distance(firePosition, targetPosition);
                
                // Verificar si hay línea de tiro clara (excluyendo el obstáculo de cobertura)
                RaycastHit hit;
                if (Physics.Raycast(firePosition, fireDir, out hit, fireDist, obstacleMask))
                {
                    // Hay algo en el camino - verificar si es el mismo obstáculo de cobertura
                    if (hit.collider == coverObstacle)
                    {
                        // El proyectil impactaría con el obstáculo de cobertura - no válido
                        continue;
                    }
                }
                else
                {
                    // Línea de tiro clara desde esta posición
                    return true;
                }
            }
            
            return false;
        }
        
        // Debug Visual
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, settings.minSafeDistance);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, settings.maxDistance);

            // Visualizar radio de búsqueda
            if (_currentState == CombatState.SEARCHING)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_lastKnownPlayerPosition, settings.searchMovementRadius);
            }
        }
    }

    // Comparador singleton para ordenar RaycastHit[] sin allocar lambdas
    internal sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
}
